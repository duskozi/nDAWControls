using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using nDAWControls;
using nDAWControls.MidiPlayer.Engine;
using nDAWControls.Models;
using nDAWControls.Skinning;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Microsoft.Win32;

namespace nDAWControls.MidiPlayer;

/// <summary>
/// MIDI player built from nDAWControls. The engine runs on its own thread; this window polls it once per frame and
/// forwards MIDI events only to the parts of the UI that are currently shown. A part that is hidden gets nothing,
/// and is re-synced from the engine's held-note snapshot when it is shown again.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Color[] s_palette =
    {
        Color.FromRgb(0xFF, 0x6B, 0x5A), Color.FromRgb(0xFF, 0xC2, 0x4A), Color.FromRgb(0x5A, 0xD1, 0xA0), Color.FromRgb(0x6F, 0xA8, 0xFF),
        Color.FromRgb(0xB1, 0x8C, 0xFF), Color.FromRgb(0xFF, 0x8A, 0xC8), Color.FromRgb(0x4C, 0xD6, 0xE8), Color.FromRgb(0xC7, 0xE0, 0x5A),
        Color.FromRgb(0xFF, 0x9A, 0x3C), Color.FromRgb(0x8E, 0x9B, 0xFF), Color.FromRgb(0x5A, 0xE0, 0x6E), Color.FromRgb(0xE8, 0x6A, 0x6A),
    };

    private static readonly (string Label, int Controller, LaneMode Mode)[] s_laneModes =
    {
        ("Velocity", 0, LaneMode.Velocity), ("CC 1 · Modulation", 1, LaneMode.Controller), ("CC 7 · Volume", 7, LaneMode.Controller),
        ("CC 10 · Pan", 10, LaneMode.Controller), ("CC 11 · Expression", 11, LaneMode.Controller),
        ("CC 64 · Sustain", 64, LaneMode.Controller), ("Pitch bend", -1, LaneMode.PitchBend),
    };

    private readonly PlayerEngine _engine = new();
    private readonly IReadOnlyList<string> _devices;
    private readonly List<TrackViewModel> _tracks = new();
    private readonly List<TrackHeader> _headers = new();
    private Song? _song;
    private int _selected = -1;

    // UI-side note state for the keyboard: [track][note] = velocity (only maintained while the keyboard is shown).
    private byte[][] _keyHeld = Array.Empty<byte[]>();
    private readonly bool[] _keyDirty = new bool[128];

    // Which consumers were active last frame (to detect "just shown" and re-sync).
    private bool _wasKeys, _wasChannels, _wasStrip, _wasHeaders, _wasPerf;

    // Guards so values we push into controls aren't echoed back to the engine as user input.
    private bool _settingPlayhead, _fromEngine;

    // After a transport click, give the engine thread a moment before mirroring its state back (avoids flicker).
    private long _transportGraceUntil;

    public MainWindow()
    {
        InitializeComponent();
        _devices = PlayerEngine.GetOutputDeviceNames();

        LanePicker.ItemsSource = s_laneModes.Select(m => m.Label).ToList();
        LanePicker.SelectedIndex = 0;

        Transport.PlayCommand = new Relay(p =>
        {
            _transportGraceUntil = Environment.TickCount64 + 250;
            if (p is true) _engine.Play(); else _engine.Pause();
        });
        Transport.StopCommand = new Relay(_ => { _transportGraceUntil = Environment.TickCount64 + 250; _engine.Stop(); });
        Transport.RewindCommand = new Relay(_ => SeekBars(-1));
        Transport.ForwardCommand = new Relay(_ => SeekBars(+1));
        Transport.ToStartCommand = new Relay(_ => _engine.Seek(0));

        // Ruler clicks / scrubbing in the piano roll seek the engine.
        Roll.PlayheadMoved += (_, _) =>
        {
            if (!_settingPlayhead && _song is not null) _engine.Seek(_song.BeatsToUs(Roll.PlayheadPosition));
        };

        CompositionTarget.Rendering += OnFrame;
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            _engine.Dispose();
        };

        LoadSong(Song.FromMidiFile(DemoSong.Create(), "Demo song"));
    }

    #region Loading

    private void LoadSong(Song song)
    {
        _engine.Stop();
        _song = song;
        SongTitle.Text = song.Title;

        // Default device: the first real output (on Windows usually "Microsoft GS Wavetable Synth").
        string defaultDevice = _devices.Count > 1 ? _devices[1] : PlayerEngine.NoDevice;

        foreach (TrackViewModel old in _tracks) old.PropertyChanged -= OnTrackChanged;
        _tracks.Clear();
        _headers.Clear();
        TrackList.Children.Clear();

        foreach (SongTrack t in song.Tracks)
        {
            var brush = new SolidColorBrush(s_palette[t.Index % s_palette.Length]);
            brush.Freeze();
            var vm = new TrackViewModel(t, brush, defaultDevice);
            vm.PropertyChanged += OnTrackChanged;
            _tracks.Add(vm);
            TrackHeader header = CreateHeader(vm);
            _headers.Add(header);
            TrackList.Children.Add(header);
        }

        _engine.Load(song, _tracks.Select(t => t.Device).ToList());
        _keyHeld = _tracks.Select(_ => new byte[128]).ToArray();
        Keys.ClearHighlights();
        ChannelMonitor.Reset();
        MidiStrip.Reset();

        Transport.BeatsPerBar = song.BeatsPerBar;
        Transport.BeatUnit = song.BeatUnit;
        Roll.BeatsPerBar = song.BeatsPerBar;
        Roll.LengthInBeats = Math.Max(4, Math.Ceiling(song.LengthBeats / song.BeatsPerBar + 1) * song.BeatsPerBar);
        RebuildRoll();

        // Select the first melodic track so wheels, pedals and the lane show something meaningful.
        TrackViewModel? first = _tracks.FirstOrDefault(t => !t.Track.IsDrums && t.Track.NoteCount > 0) ?? _tracks.FirstOrDefault();
        if (first is not null) first.IsSelected = true;
        ApplyMuteSolo();
    }

    private TrackHeader CreateHeader(TrackViewModel vm)
    {
        var h = new TrackHeader
        {
            TrackName = vm.Name,
            TrackNumber = vm.Number,
            Subtitle = vm.Subtitle,
            TrackColor = vm.Color,
            OutputDevices = _devices,
        };
        Bind(h, TrackHeader.IsMutedProperty, vm, nameof(vm.IsMuted), BindingMode.TwoWay);
        Bind(h, TrackHeader.IsSoloedProperty, vm, nameof(vm.IsSoloed), BindingMode.TwoWay);
        Bind(h, TrackHeader.IsShownOnPianoProperty, vm, nameof(vm.IsShownOnPiano), BindingMode.TwoWay);
        Bind(h, TrackHeader.IsSelectedProperty, vm, nameof(vm.IsSelected), BindingMode.TwoWay);
        Bind(h, TrackHeader.IsAudibleProperty, vm, nameof(vm.IsAudible), BindingMode.OneWay);
        Bind(h, TrackHeader.SelectedOutputDeviceProperty, vm, nameof(vm.Device), BindingMode.TwoWay);
        Bind(h, TrackHeader.ShowDeviceSelectorProperty, DevicesChip, nameof(ToggleButton.IsChecked), BindingMode.OneWay);
        Bind(h, TrackHeader.ShowActivityProperty, ActivityChip, nameof(ToggleButton.IsChecked), BindingMode.OneWay);
        return h;

        static void Bind(DependencyObject target, DependencyProperty dp, object source, string path, BindingMode mode)
            => BindingOperations.SetBinding(target, dp, new Binding(path) { Source = source, Mode = mode });
    }

    private void RebuildRoll()
    {
        var notes = new List<MidiNote>();
        foreach (TrackViewModel t in _tracks.Where(t => t.IsShownOnPiano))
            foreach (var (pitch, start, length, velocity) in t.Track.Notes)
                notes.Add(new MidiNote(pitch, start, length, velocity) { Brush = t.Color });
        Roll.Notes = new ObservableCollection<MidiNote>(notes);
    }

    private void UpdateLane()
    {
        var (_, controller, mode) = s_laneModes[Math.Max(0, LanePicker.SelectedIndex)];
        Lane.Mode = mode;
        if (mode == LaneMode.Velocity || _selected < 0) { Lane.Points = null; return; }
        Lane.ControllerNumber = Math.Max(0, controller);
        var points = new ObservableCollection<ControllerPoint>();
        if (_tracks[_selected].Track.Controllers.TryGetValue(controller, out var list))
            foreach (var (beat, value) in list) points.Add(new ControllerPoint(beat, value));
        Lane.Points = points;
    }

    #endregion

    #region Track state → engine

    private void OnTrackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TrackViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(TrackViewModel.IsMuted):
            case nameof(TrackViewModel.IsSoloed):
                ApplyMuteSolo();
                break;
            case nameof(TrackViewModel.Device):
                _engine.SetDevice(vm.Index, vm.Device);
                break;
            case nameof(TrackViewModel.IsShownOnPiano):
                RebuildRoll();
                for (int n = 0; n < 128; n++) _keyDirty[n] = true;
                break;
            case nameof(TrackViewModel.IsSelected) when vm.IsSelected:
                foreach (TrackViewModel other in _tracks.Where(t => t != vm)) other.IsSelected = false;
                _selected = vm.Index;
                UpdateLane();
                ResetPerformanceDisplay();
                break;
        }
    }

    private void ApplyMuteSolo()
    {
        bool[] muted = _tracks.Select(t => t.IsMuted).ToArray();
        bool[] soloed = _tracks.Select(t => t.IsSoloed).ToArray();
        bool anySolo = soloed.Any(s => s);
        foreach (TrackViewModel t in _tracks) t.IsAudible = !t.IsMuted && (!anySolo || t.IsSoloed);
        _engine.SetMuteSolo(muted, soloed);
    }

    private void SeekBars(int bars)
    {
        if (_song is null) return;
        double beats = _song.UsToBeats(_engine.PositionUs) + bars * _song.BeatsPerBar;
        beats = Math.Max(0, Math.Floor(beats / _song.BeatsPerBar + 1e-6) * _song.BeatsPerBar);
        _engine.Seek(_song.BeatsToUs(beats));
    }

    #endregion

    #region Per-frame update

    private void OnFrame(object? sender, EventArgs e)
    {
        if (_song is null) return;
        long pos = _engine.PositionUs;
        double beats = _song.UsToBeats(pos);
        bool playing = _engine.IsPlaying;

        if (TransportPanel.IsShown)
        {
            if (Transport.IsPlaying != playing && Environment.TickCount64 > _transportGraceUntil) Transport.IsPlaying = playing;
            Transport.Position = beats;
            Transport.Tempo = Math.Round(_song.BpmAt(pos), 2);
            _engine.Loop = Transport.IsLooping;
        }
        if (RollPanel.IsShown || LanePanel.IsShown)
        {
            _settingPlayhead = true;
            Roll.PlayheadPosition = beats;
            _settingPlayhead = false;
        }

        // Who wants MIDI events this frame? Hidden parts get nothing.
        bool keys = KeyboardPanel.IsShown;
        bool channels = ChannelsPanel.IsShown;
        bool strip = MidiStrip.IsVisible;
        bool headers = TracksPanel.IsShown && ActivityChip.IsChecked == true;
        bool perf = KeyboardPanel.IsShown && (WheelsChip.IsChecked == true || PedalsChip.IsChecked == true);
        _engine.VisualsEnabled = keys || channels || strip || headers || perf;

        // Parts that were just shown start empty: re-sync them with the notes that are sounding right now.
        if ((keys && !_wasKeys) || (channels && !_wasChannels) || (strip && !_wasStrip) || (headers && !_wasHeaders))
            Resync(keys && !_wasKeys, channels && !_wasChannels, strip && !_wasStrip, headers && !_wasHeaders);
        if (perf && !_wasPerf) ResetPerformanceDisplay();
        (_wasKeys, _wasChannels, _wasStrip, _wasHeaders, _wasPerf) = (keys, channels, strip, headers, perf);

        int budget = 20000; // never spend an unbounded amount of a frame on a burst
        while (budget-- > 0 && _engine.TryDequeueVisual(out VisualEvent v))
        {
            if (channels) ChannelMonitor.ProcessMessage(v.Status, v.Data1, v.Data2);
            if (strip) MidiStrip.ProcessMessage(v.Status, v.Data1, v.Data2);
            if (headers && v.Track < _headers.Count) _headers[v.Track].Activity?.ProcessMessage(v.Status, v.Data1, v.Data2, 0);

            int kind = v.Status & 0xF0;
            if (keys && (kind == 0x90 || kind == 0x80) && v.Track < _keyHeld.Length)
            {
                _keyHeld[v.Track][v.Data1] = kind == 0x90 ? v.Data2 : (byte)0;
                _keyDirty[v.Data1] = true;
            }
            if (perf && v.Track == _selected) ApplyPerformance(kind, v.Data1, v.Data2);
        }

        if (keys) RefreshDirtyKeys();

        if (_engine.TakeError() is { } error) SongTitle.Text = $"{_song.Title} — {error}";
    }

    private void Resync(bool keys, bool channels, bool strip, bool headers)
    {
        var held = _engine.GetHeldSnapshot();
        if (keys)
        {
            foreach (byte[] arr in _keyHeld) Array.Clear(arr);
            foreach (var (t, _, note, vel) in held) if (t < _keyHeld.Length) _keyHeld[t][note] = (byte)vel;
            for (int n = 0; n < 128; n++) _keyDirty[n] = true;
        }
        foreach (var (t, ch, note, vel) in held)
        {
            if (channels) ChannelMonitor.NoteOn(ch, note, vel);
            if (strip) MidiStrip.NoteOn(ch, note, vel);
            if (headers && t < _headers.Count) _headers[t].NoteOn(note, vel);
        }
    }

    /// <summary>A key shows the first visible track (track order) that holds it, in that track's colour.</summary>
    private void RefreshDirtyKeys()
    {
        for (int n = 0; n < 128; n++)
        {
            if (!_keyDirty[n]) continue;
            _keyDirty[n] = false;
            int vel = 0;
            Brush? brush = null;
            for (int t = 0; t < _keyHeld.Length && t < _tracks.Count; t++)
            {
                if (!_tracks[t].IsShownOnPiano || _keyHeld[t][n] == 0) continue;
                vel = _keyHeld[t][n];
                brush = _tracks[t].Color;
                break;
            }
            Keys.SetHighlighted(n, vel, brush);
        }
    }

    private void ApplyPerformance(int kind, int d1, int d2)
    {
        _fromEngine = true;
        try
        {
            if (kind == 0xB0 && PedalsChip.IsChecked == true) Pedals.ApplyControlChange(d1, d2);
            if (kind == 0xB0 && d1 == 1 && WheelsChip.IsChecked == true && !ModWheel.IsMouseCaptured) ModWheel.Value = d2;
            if (kind == 0xE0 && WheelsChip.IsChecked == true && !PitchWheel.IsMouseCaptured) PitchWheel.Value = (d2 << 7 | d1) - 8192;
        }
        finally
        {
            _fromEngine = false;
        }
    }

    private void ResetPerformanceDisplay()
    {
        _fromEngine = true;
        try
        {
            PitchWheel.Value = 0;
            ModWheel.Value = 0;
            Pedals.SustainValue = Pedals.SostenutoValue = Pedals.SoftValue = 0;
        }
        finally
        {
            _fromEngine = false;
        }
    }

    #endregion

    #region Live input → selected track

    private void SendLive(ChannelEvent e)
    {
        if (_selected >= 0) _engine.SendLive(_selected, e);
    }

    private void OnKeyNoteOn(object sender, NoteEventArgs e)
        => SendLive(new NoteOnEvent((SevenBitNumber)e.Note, (SevenBitNumber)Math.Clamp(e.Velocity, 1, 127)));

    private void OnKeyNoteOff(object sender, NoteEventArgs e)
        => SendLive(new NoteOffEvent((SevenBitNumber)e.Note, SevenBitNumber.MinValue));

    private void OnWheelChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
    {
        if (_fromEngine) return;
        if (ReferenceEquals(sender, PitchWheel)) SendLive(new PitchBendEvent((ushort)(e.NewValue + 8192)));
        else SendLive(new ControlChangeEvent((SevenBitNumber)1, (SevenBitNumber)e.NewValue));
    }

    private void OnPedalChanged(object sender, PedalEventArgs e)
    {
        if (!e.IsUserChange) return;   // values set from the engine are not echoed back out
        SendLive(new ControlChangeEvent((SevenBitNumber)e.Controller, (SevenBitNumber)e.Value));
    }

    #endregion

    #region UI handlers

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb) tb.IsChecked = false;
        var dlg = new OpenFileDialog { Filter = "MIDI files (*.mid;*.midi;*.kar)|*.mid;*.midi;*.kar|All files|*.*" };
        if (dlg.ShowDialog(this) == true) TryLoad(dlg.FileName);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) TryLoad(files[0]);
    }

    private void TryLoad(string path)
    {
        try
        {
            LoadSong(Song.Load(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                   or Melanchall.DryWetMidi.Common.MidiException)
        {
            SongTitle.Text = $"Couldn't open {Path.GetFileName(path)}: {ex.Message}";
        }
    }

    private void OnDemo(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb) tb.IsChecked = false;
        LoadSong(Song.FromMidiFile(DemoSong.Create(), "Demo song"));
    }

    private void OnPanic(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb) tb.IsChecked = false;
        _engine.Stop();
    }

    private void OnLaneModeChanged(object sender, SelectionChangedEventArgs e) => UpdateLane();

    private void OnKeyStyle(object sender, RoutedEventArgs e)
    {
        // Fires during InitializeComponent (the chip's style checks it) before Keys exists.
        if (Keys is null) return;
        Keys.KeyStyle = Keys3DChip.IsChecked == true ? PianoKeyStyle.ThreeD : PianoKeyStyle.Flat;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Space = play/pause, unless the on-screen keyboard has focus (it uses the computer keyboard).
        if (e.Key != Key.Space || Keys.IsKeyboardFocused) return;
        if (_engine.IsPlaying) _engine.Pause(); else _engine.Play();
        e.Handled = true;
    }

    #endregion

    private sealed class Relay : ICommand
    {
        private readonly Action<object?> _run;
        public Relay(Action<object?> run) => _run = run;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _run(parameter);
    }
}
