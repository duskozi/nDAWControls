using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using nDAWControls;
using nDAWControls.Models;
using nDAWControls.Primitives;
using nDAWControls.Skinning;

namespace nDAWControls.Gallery;

/// <summary>
/// Demo shell. A fake "engine" runs on a worker thread and feeds the meters through the lock-free
/// PushLevels API, exactly how a real audio callback would. The UI thread only advances the playhead.
/// </summary>
public partial class MainWindow : Window
{
    private const int StepsPerBeat = 4;

    private readonly ObservableCollection<MidiNote> _notes = new();
    private readonly ObservableCollection<ControllerPoint> _modPoints = new();
    private readonly ObservableCollection<ControllerPoint> _pitchPoints = new();
    private readonly StepPattern _pattern = new(4, 16);
    private readonly Timer _engine;
    private readonly Random _rng = new(7);

    // Shared with the engine thread.
    private double _enginePosition;
    private int _enginePlaying;
    private readonly double[] _gain = { 1, 1, 1, 1 };
    private readonly double[] _env = new double[4];
    private int _lastEngineStep = -1;
    private int _engineBusy;

    // MIDI activity demo state
    private const int SynthChannel = 0, DrumChannel = 9, SynthTrack = 3;
    private static readonly int[] DrumNotes = { 36, 38, 42, 39 };   // kick, snare, closed hat, clap
    private static readonly int[] DrumTrack = { 0, 1, 2, 1 };       // which demo track each drum row belongs to
    private readonly System.Collections.Generic.List<(int row, int note)> _drumOffs = new(); // engine thread only
    private readonly bool[] _synthOn = new bool[128];
    private double _prevPos = -1;
    private readonly double[] _lastVolume = { double.NaN, double.NaN, double.NaN, double.NaN };
    private double _lastFrame = -1;

    public MainWindow()
    {
        InitializeComponent();
        InitCatalog();


        SnapPicker.ItemsSource = new[] { "1/4", "1/8", "1/16", "1/32", "Off" };
        SnapPicker.SelectedIndex = 2;

        SeedNotes();
        Roll.Notes = _notes;

        SeedLanes();
        LanePicker.ItemsSource = new[] { "Velocity", "CC 1  Modulation", "Pitch bend" };
        LanePicker.SelectedIndex = 0;

        SeedPattern();
        Steps.Pattern = _pattern;
        Steps.RowLabels = new[] { "Kick", "Snare", "Hat", "Perc" };

        TrackMonitor.LaneLabels = new[] { "Kick", "Snare", "Hats", "Synth" };
        TrackMonitor.LaneBrushes = new Brush[] { StripKick.TrackColor!, StripSnare.TrackColor!, StripHats.TrackColor!, StripSynth.TrackColor! };
        SynthLed.LaneBrushes = new Brush[] { StripSynth.TrackColor! };

        // Computer-keyboard sustain: hold the left Ctrl key while playing the on-screen keyboard
        // (Space belongs to the transport, and Tab must keep moving focus).
        Keys.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.LeftCtrl) Pedals.SustainValue = 127; };
        Keys.PreviewKeyUp += (_, e) => { if (e.Key == System.Windows.Input.Key.LeftCtrl) Pedals.SustainValue = 0; };

        CompositionTarget.Rendering += OnFrame;
        _engine = new Timer(EngineTick, null, 0, 10);
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            _engine.Dispose();
        };
    }

    #region Seed data

    private void SeedNotes()
    {
        // i–VI–III–VII in A minor, two bars each chord, plus a little arpeggio.
        int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
        for (int c = 0; c < chords.Length; c++)
        {
            foreach (int p in chords[c])
                _notes.Add(new MidiNote(p, c * 8, 7.5, 90));
            for (int i = 0; i < 8; i++)
                _notes.Add(new MidiNote(chords[c][i % 3] + 12, c * 8 + i, 0.5, 70 + (i % 2) * 30));
        }
    }

    private void SeedLanes()
    {
        // A slow mod swell over the first two bars and a pitch dip at bar 3.
        for (double b = 0; b <= 8; b += 0.25)
            _modPoints.Add(new ControllerPoint(b, (int)Math.Round(64 + 50 * Math.Sin(b / 8 * Math.PI * 2))));
        foreach (var (b, v) in new[] { (8.0, 0), (8.5, -2048), (9.0, -4096), (9.5, -2048), (10.0, 0) })
            _pitchPoints.Add(new ControllerPoint(b, v));
    }

    private void SeedPattern()
    {
        for (int s = 0; s < 16; s++)
        {
            if (s % 4 == 0) _pattern.SetVelocity(0, s, 120);
            if (s % 8 == 4) _pattern.SetVelocity(1, s, 110);
            if (s % 2 == 0) _pattern.SetVelocity(2, s, s % 4 == 2 ? 100 : 60);
        }
        _pattern.SetVelocity(3, 3, 80);
        _pattern.SetVelocity(3, 11, 80);
        _pattern.SetVelocity(3, 14, 60);
    }

    #endregion

    #region UI thread: playhead

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = ((RenderingEventArgs)e).RenderingTime.TotalSeconds;
        double dt = _lastFrame < 0 ? 0 : Math.Min(now - _lastFrame, 0.1);
        _lastFrame = now;

        // Mirror strip state for the engine thread (DependencyProperties are UI-thread only).
        ChannelStrip[] strips = { StripKick, StripSnare, StripHats, StripSynth };
        bool anySolo = strips.Any(s => s.IsSoloed);
        for (int i = 0; i < strips.Length; i++)
        {
            ChannelStrip s = strips[i];
            bool audible = !s.IsMuted && (!anySolo || s.IsSoloed);
            Volatile.Write(ref _gain[i], audible ? DbMath.DbToGain(s.Volume) : 0);
        }

        if (!Transport.IsPlaying)
        {
            Volatile.Write(ref _enginePlaying, 0);
            Steps.CurrentStep = -1;
            if (_keysLit)
            {
                for (int n = 0; n < 128; n++) Keys.SetHighlighted(n, 0);   // stopped: no stuck lit keys
                _keysLit = false;
            }
            Roll.PlayheadPosition = Transport.Position;
            UpdateMidiActivity(Transport.Position, false, stackalloc int[128]);
            return;
        }

        double pos = Transport.Position + dt * Transport.Tempo / 60.0;
        double loopEnd = Transport.IsLooping ? 16 : Roll.LengthInBeats;
        if (pos >= loopEnd)
        {
            if (Transport.IsLooping) pos -= loopEnd;
            else { pos = loopEnd; Transport.IsPlaying = false; }
        }
        Transport.Position = pos;
        Roll.PlayheadPosition = pos;
        Steps.CurrentStep = (int)(pos * StepsPerBeat) % _pattern.Steps;

        // Light up keys for notes under the playhead (what a real app does with outgoing MIDI).
        // Brightness follows each note's velocity.
        Span<int> lit = stackalloc int[128];
        foreach (MidiNote note in _notes)
            if (note.Start <= pos && note.End > pos) lit[note.Pitch] = Math.Max(lit[note.Pitch], note.Velocity);
        for (int n = 0; n < 128; n++) Keys.SetHighlighted(n, lit[n]);
        _keysLit = true;
        UpdateMidiActivity(pos, true, lit);

        Volatile.Write(ref _enginePosition, pos);
        Volatile.Write(ref _enginePlaying, 1);
    }

    #endregion

    #region Worker thread: fake audio engine

    private void EngineTick(object? state)
    {
        // Timer callbacks can overlap under load; the engine state is single-threaded by design.
        if (Interlocked.Exchange(ref _engineBusy, 1) == 1) return;
        try { EngineTickCore(); }
        finally { Volatile.Write(ref _engineBusy, 0); }
    }

    private void EngineTickCore()
    {
        bool playing = Volatile.Read(ref _enginePlaying) == 1;
        double pos = Volatile.Read(ref _enginePosition);
        int step = (int)(pos * StepsPerBeat) % _pattern.Steps;

        if (playing && step != _lastEngineStep)
        {
            _lastEngineStep = step;
            ReleaseDrums(); // drum hits last one step
            for (int row = 0; row < 4; row++)
            {
                int vel = _pattern.GetVelocity(row, step); // benign race: byte reads
                if (vel == 0) continue;
                if (row < 3) _env[row] = vel / 127.0;
                // Thread-safe: called from the timer thread, applied by the monitors on the next frame.
                ChannelMonitor.NoteOn(DrumChannel, DrumNotes[row], vel);
                ChannelLeds.NoteOn(DrumChannel, DrumNotes[row], vel);
                TrackMonitor.NoteOn(DrumTrack[row], DrumNotes[row], vel);
                _drumOffs.Add((row, DrumNotes[row]));
            }
        }
        if (!playing)
        {
            if (_lastEngineStep >= 0) ReleaseDrums();
            _lastEngineStep = -1;
        }

        // Decay envelopes (10 ms tick).
        _env[0] *= 0.90;
        _env[1] *= 0.86;
        _env[2] *= 0.70;
        _env[3] = playing ? 0.35 + 0.1 * Math.Sin(pos * Math.PI) : _env[3] * 0.9;

        double master = 0;
        var strips = new[] { StripKick, StripSnare, StripHats, StripSynth };
        for (int i = 0; i < 4; i++)
        {
            double g = Volatile.Read(ref _gain[i]);
            double peak = Math.Min(1.2, _env[i] * g * (0.9 + 0.2 * _rng.NextDouble()));
            double rms = peak * 0.62;
            strips[i].PushLevels((float)peak, (float)rms, (float)(peak * 0.95), (float)(rms * 0.95));
            master += peak * 0.55;
        }
        StripMaster.PushLevels((float)master, (float)(master * 0.6), (float)(master * 0.97), (float)(master * 0.58));
    }

    private void ReleaseDrums()
    {
        foreach (var (row, note) in _drumOffs)
        {
            ChannelMonitor.NoteOff(DrumChannel, note);
            ChannelLeds.NoteOff(DrumChannel, note);
            TrackMonitor.NoteOff(DrumTrack[row], note);
        }
        _drumOffs.Clear();
    }

    #endregion

    #region MIDI activity (UI thread)

    private void SynthNote(int note, int velocity)
    {
        ChannelMonitor.NoteOn(SynthChannel, note, velocity);   // velocity 0 = note off
        ChannelLeds.NoteOn(SynthChannel, note, velocity);
        TrackMonitor.NoteOn(SynthTrack, note, velocity);
        SynthLed.NoteOn(0, note, velocity);
    }

    private void SynthControl(int controller, int value)
    {
        ChannelMonitor.ControlChange(SynthChannel, controller, value);
        ChannelLeds.ControlChange(SynthChannel, controller, value);
        TrackMonitor.ControlChange(SynthTrack, controller, value);
        SynthLed.ControlChange(0, controller, value);
    }

    private void SynthPitchBend(int value)
    {
        ChannelMonitor.PitchBend(SynthChannel, value);
        ChannelLeds.PitchBend(SynthChannel, value);
        TrackMonitor.PitchBend(SynthTrack, value);
        SynthLed.PitchBend(0, value);
    }

    /// <summary>Piano-roll notes, lane automation and fader moves → activity, as a sequencer would send them.</summary>
    private void UpdateMidiActivity(double pos, bool playing, ReadOnlySpan<int> lit)
    {
        for (int n = 0; n < 128; n++)
        {
            bool on = playing && lit[n] > 0;
            if (on == _synthOn[n]) continue;
            _synthOn[n] = on;
            SynthNote(n, on ? lit[n] : 0);
        }

        if (playing && _prevPos >= 0)
        {
            // Lane points crossed since the last frame (handles the loop wrap).
            foreach (ControllerPoint p in _modPoints)
                if (Crossed(_prevPos, pos, p.Position)) SynthControl(1, p.Value);
            foreach (ControllerPoint p in _pitchPoints)
                if (Crossed(_prevPos, pos, p.Position)) SynthPitchBend(p.Value);
        }
        _prevPos = playing ? pos : -1;

        // Fader moves → CC 7 (channel volume) on that track.
        ChannelStrip[] strips = { StripKick, StripSnare, StripHats, StripSynth };
        for (int i = 0; i < strips.Length; i++)
        {
            double v = strips[i].Volume;
            if (v.Equals(_lastVolume[i])) continue;
            bool first = double.IsNaN(_lastVolume[i]);
            _lastVolume[i] = v;
            if (first) continue;
            int cc7 = (int)Math.Round(Math.Clamp(Math.Pow(DbMath.DbToGain(v) / DbMath.DbToGain(6), 0.5) * 127, 0, 127));
            TrackMonitor.ControlChange(i, 7, cc7);
            if (i == SynthTrack) { ChannelMonitor.ControlChange(SynthChannel, 7, cc7); ChannelLeds.ControlChange(SynthChannel, 7, cc7); }
        }
    }

    private static bool Crossed(double prev, double now, double at)
        => now >= prev ? at > prev && at <= now : at > prev || at <= now;

    #endregion

    #region Handlers

    private void OnLaneModeChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (LanePicker.SelectedIndex)
        {
            case 1:
                Lane.Mode = LaneMode.Controller;
                Lane.ControllerNumber = 1;
                Lane.Points = _modPoints;
                break;
            case 2:
                Lane.Mode = LaneMode.PitchBend;
                Lane.Points = _pitchPoints;
                break;
            default:
                Lane.Mode = LaneMode.Velocity;
                Lane.Points = null;
                break;
        }
    }

    private void OnLaneEdited(object sender, RoutedEventArgs e)
        => Status.Text = Lane.Mode == LaneMode.Velocity ? "Velocities edited" : $"{Lane.Points?.Count ?? 0} lane points";

    private void OnWheelChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
    {
        bool isPitch = ReferenceEquals(sender, PitchWheel);
        Status.Text = isPitch ? $"Pitch bend {e.NewValue:+0;-0;0}" : $"Mod wheel {e.NewValue}";
        if (isPitch) SynthPitchBend(e.NewValue); else SynthControl(1, e.NewValue);

        // "Automation recording": while recording, wheel moves are written into the lane at the playhead.
        if (!Transport.IsRecording || !Transport.IsPlaying) return;
        ObservableCollection<ControllerPoint> target = isPitch ? _pitchPoints : _modPoints;
        double step = 0.0625;
        double pos = Math.Round(Transport.Position / step) * step;
        for (int i = target.Count - 1; i >= 0; i--)
            if (Math.Abs(target[i].Position - pos) < step / 2) target.RemoveAt(i);
        int at = 0;
        while (at < target.Count && target[at].Position < pos) at++;
        target.Insert(at, new ControllerPoint(pos, e.NewValue));
    }

    private void OnPedalChanged(object sender, PedalEventArgs e)
    {
        Status.Text = $"{e.Pedal} pedal (CC {e.Controller}) = {e.Value}";
        SynthControl(e.Controller, e.Value);
    }

    private void OnSnapChanged(object sender, SelectionChangedEventArgs e)
    {
        Roll.Snap = SnapPicker.SelectedIndex switch
        {
            0 => 1,
            1 => 0.5,
            2 => 0.25,
            3 => 0.125,
            _ => 0,
        };
    }

    private void OnKeyboardNoteOn(object sender, NoteEventArgs e)
    {
        Status.Text = $"Note on  {MidiNoteName.Get(e.Note)}  vel {e.Velocity}";
        SynthNote(e.Note, e.Velocity);
    }

    private void OnKeyboardNoteOff(object sender, NoteEventArgs e)
    {
        Status.Text = $"Note off {MidiNoteName.Get(e.Note)}";
        SynthNote(e.Note, 0);
    }

    private void OnAuditionOn(object sender, NoteEventArgs e)
    {
        Keys.SetHighlighted(e.Note, true);
        Status.Text = $"Audition {MidiNoteName.Get(e.Note)}";
    }

    private bool _keysLit;

    private void OnAuditionOff(object sender, NoteEventArgs e) => Keys.SetHighlighted(e.Note, false);

    private void OnNotesEdited(object sender, RoutedEventArgs e)
        => Status.Text = $"{_notes.Count} notes · {_notes.Count(n => n.IsSelected)} selected";

    #endregion
}
