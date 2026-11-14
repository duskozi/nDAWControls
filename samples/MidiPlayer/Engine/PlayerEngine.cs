using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace nDAWControls.MidiPlayer.Engine;

/// <summary>A channel message for the UI: which track, raw status/data bytes.</summary>
public readonly record struct VisualEvent(int Track, byte Status, byte Data1, byte Data2);

/// <summary>
/// Real-time MIDI scheduler.
/// <para>
/// DryWetMIDI's <c>Playback</c> sends everything to one output device and loses track identity, so per-track device
/// routing and track mute/solo need our own loop. DryWetMIDI still does the heavy lifting: file parsing, tempo map,
/// time conversion (see <see cref="Song"/>) and device I/O (<see cref="OutputDevice"/>).
/// </para>
/// <para>
/// Threading: one dedicated high-priority thread owns playback state and all device sends. The UI talks to it only
/// through a command queue; the thread talks back through a lock-free visual queue and a few volatile fields.
/// The visual queue is only filled while <see cref="VisualsEnabled"/> is true, i.e. while some panel shows MIDI.
/// </para>
/// </summary>
public sealed class PlayerEngine : IDisposable
{
    public const string NoDevice = "(none)";

    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentQueue<VisualEvent> _visuals = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<string, OutputDevice> _openDevices = new(StringComparer.Ordinal);
    private readonly object _heldLock = new();
    private volatile bool _running = true;

    // ---- engine-thread state ----
    private Song? _song;
    private string[] _deviceOf = Array.Empty<string>();
    private bool[] _audible = Array.Empty<bool>();
    private byte[][] _held = Array.Empty<byte[]>();   // [track][channel * 128 + note] = velocity sent (0 = not held)
    private bool _playing;
    private int _next;
    private long _startUs;
    private int _visualCount;

    // ---- shared with the UI ----
    private long _positionUs;
    private int _isPlaying;
    private volatile bool _visualsEnabled;
    private volatile bool _loop;
    private volatile string? _lastError;

    public PlayerEngine()
    {
        _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "MIDI scheduler" };
        _thread.Start();
    }

    #region Public API (any thread)

    public long PositionUs => Interlocked.Read(ref _positionUs);

    public bool IsPlaying => Volatile.Read(ref _isPlaying) == 1;

    /// <summary>Set false when no visible panel needs MIDI events – the engine then queues nothing.</summary>
    public bool VisualsEnabled
    {
        get => _visualsEnabled;
        set
        {
            _visualsEnabled = value;
            if (!value) while (_visuals.TryDequeue(out _)) Interlocked.Decrement(ref _visualCount);
        }
    }

    public bool Loop { get => _loop; set => _loop = value; }

    /// <summary>Last device error (e.g. a port in use by another app), or null. Cleared on read.</summary>
    public string? TakeError() => Interlocked.Exchange(ref _lastError, null);

    public bool TryDequeueVisual(out VisualEvent e)
    {
        if (_visuals.TryDequeue(out e)) { Interlocked.Decrement(ref _visualCount); return true; }
        return false;
    }

    public static IReadOnlyList<string> GetOutputDeviceNames()
    {
        var names = new List<string> { NoDevice };
        try
        {
            foreach (OutputDevice d in OutputDevice.GetAll())
            {
                using (d) names.Add(d.Name);
            }
        }
        catch (Exception)
        {
            // No MIDI subsystem available (e.g. running under a non-Windows test host): just offer "(none)".
        }
        return names;
    }

    public void Load(Song song, IReadOnlyList<string> devicePerTrack) => Post(() =>
    {
        StopCore(resetPosition: true);
        _song = song;
        int n = song.Tracks.Count;
        _deviceOf = Enumerable.Range(0, n).Select(i => i < devicePerTrack.Count ? devicePerTrack[i] : NoDevice).ToArray();
        _audible = Enumerable.Repeat(true, n).ToArray();
        lock (_heldLock) _held = Enumerable.Range(0, n).Select(_ => new byte[16 * 128]).ToArray();
        _next = 0;
        PublishPosition(0);
    });

    public void Play() => Post(() =>
    {
        if (_song is null || _playing) return;
        if (PositionUsLocal >= _song.LengthUs) SeekCore(0);
        Chase(PositionUsLocal);
        _startUs = PositionUsLocal;
        _next = _song.FirstEventAtOrAfter(_startUs);
        _clock.Restart();
        _playing = true;
        Volatile.Write(ref _isPlaying, 1);
    });

    public void Pause() => Post(() => StopCore(resetPosition: false));

    public void Stop() => Post(() =>
    {
        StopCore(resetPosition: true);
        Panic();
    });

    public void Seek(long us) => Post(() => SeekCore(us));

    /// <summary>Applies mute/solo for all tracks (solo wins; muted tracks never sound).</summary>
    public void SetMuteSolo(bool[] muted, bool[] soloed) => Post(() =>
    {
        bool anySolo = soloed.Any(s => s);
        for (int t = 0; t < _audible.Length; t++)
        {
            bool audible = !(t < muted.Length && muted[t]) && (!anySolo || (t < soloed.Length && soloed[t]));
            if (_audible[t] && !audible) ReleaseTrack(t); // never leave notes hanging on a muted track
            _audible[t] = audible;
        }
    });

    public void SetDevice(int track, string deviceName) => Post(() =>
    {
        if (track < 0 || track >= _deviceOf.Length || _deviceOf[track] == deviceName) return;
        ReleaseTrack(track);                 // note-offs go to the old device
        _deviceOf[track] = deviceName;
        CloseUnusedDevices();
        if (_playing) Chase(PositionUsLocal, onlyTrack: track); // bring the new device up to date
    });

    /// <summary>Plays a live event (keyboard, wheels, pedals) on a track's device and channel.</summary>
    public void SendLive(int track, ChannelEvent e) => Post(() =>
    {
        if (_song is null || track < 0 || track >= _song.Tracks.Count) return;
        int ch = Math.Max(0, _song.Tracks[track].Channel);
        e.Channel = (FourBitNumber)ch;
        Dispatch(track, e, live: true);
    });

    /// <summary>All sounding notes as (track, channel, note, velocity) – for re-syncing a panel that was just shown.</summary>
    public List<(int Track, int Channel, int Note, int Velocity)> GetHeldSnapshot()
    {
        var list = new List<(int, int, int, int)>();
        lock (_heldLock)
        {
            for (int t = 0; t < _held.Length; t++)
            {
                byte[] h = _held[t];
                for (int i = 0; i < h.Length; i++)
                    if (h[i] > 0) list.Add((t, i / 128, i % 128, h[i]));
            }
        }
        return list;
    }

    public void Dispose()
    {
        _running = false;
        _wake.Set();
        _thread.Join(1000);
        foreach (OutputDevice d in _openDevices.Values) d.Dispose();
        _openDevices.Clear();
        _wake.Dispose();
    }

    private void Post(Action a)
    {
        _commands.Enqueue(a);
        _wake.Set();
    }

    #endregion

    #region Scheduler thread

    private long PositionUsLocal => Interlocked.Read(ref _positionUs);

    private void PublishPosition(long us) => Interlocked.Exchange(ref _positionUs, us);

    private void Run()
    {
        WinTimer.BeginHighResolution();
        try
        {
            while (_running)
            {
                while (_commands.TryDequeue(out Action? cmd))
                {
                    try { cmd(); }
                    catch (Exception ex) { _lastError = ex.Message; }
                }

                if (!_playing || _song is null)
                {
                    _wake.WaitOne(20);
                    continue;
                }

                long now = _startUs + _clock.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
                PlayEvent[] events = _song.Events;
                while (_next < events.Length && events[_next].Us <= now)
                {
                    PlayEvent pe = events[_next++];
                    Dispatch(pe.Track, pe.Event, live: false);
                }
                PublishPosition(Math.Min(now, _song.LengthUs));

                if (_next >= events.Length && now >= _song.LengthUs)
                {
                    if (_loop) { SeekCore(0); continue; }
                    StopCore(resetPosition: false);
                    continue;
                }

                // Wait until the next event: sleep when it is far away, yield when close (1 ms timer resolution).
                long wait = _next < events.Length ? events[_next].Us - now : 5000;
                if (wait > 2000) _wake.WaitOne(1);
                else if (wait > 300) Thread.Yield();
                else Thread.SpinWait(50);
            }
        }
        finally
        {
            // Shutting down: run what's still queued (e.g. a final Stop), then make sure no external synth is
            // left with hanging notes.
            while (_commands.TryDequeue(out Action? cmd))
            {
                try { cmd(); } catch (Exception) { /* closing anyway */ }
            }
            try
            {
                ReleaseAll();
                Panic();
            }
            catch (Exception) { /* device may already be gone */ }
            WinTimer.EndHighResolution();
        }
    }

    private void Dispatch(int track, ChannelEvent e, bool live)
    {
        if (track >= _deviceOf.Length) return;
        bool audible = live || _audible[track];
        int ch = e.Channel;

        switch (e)
        {
            case NoteOnEvent on when on.Velocity > 0:
                if (!audible) return; // muted / not soloed: nothing sounds, nothing lights
                Send(track, e);
                lock (_heldLock) _held[track][ch * 128 + on.NoteNumber] = (byte)on.Velocity;
                Visual(track, 0x90 | ch, on.NoteNumber, on.Velocity);
                return;

            case NoteOnEvent offAsOn:          // velocity 0 = note off
                NoteOff(track, ch, offAsOn.NoteNumber);
                return;

            case NoteOffEvent off:
                NoteOff(track, ch, off.NoteNumber);
                return;
        }

        // Controllers, program, pitch bend, aftertouch: always sent so the device state stays correct
        // even while muted; only shown when audible.
        Send(track, e);
        if (!audible) return;
        switch (e)
        {
            case ControlChangeEvent cc: Visual(track, 0xB0 | ch, cc.ControlNumber, cc.ControlValue); break;
            case PitchBendEvent pb: Visual(track, 0xE0 | ch, pb.PitchValue & 0x7F, pb.PitchValue >> 7); break;
            case ProgramChangeEvent pc: Visual(track, 0xC0 | ch, pc.ProgramNumber, 0); break;
            case ChannelAftertouchEvent at: Visual(track, 0xD0 | ch, at.AftertouchValue, 0); break;
            case NoteAftertouchEvent nat: Visual(track, 0xA0 | ch, nat.NoteNumber, nat.AftertouchValue); break;
        }
    }

    private void NoteOff(int track, int ch, int note)
    {
        int key = ch * 128 + note;
        lock (_heldLock)
        {
            if (_held[track][key] == 0) return; // never sent (muted at note-on): nothing to release
            _held[track][key] = 0;
        }
        Send(track, new NoteOffEvent((SevenBitNumber)note, SevenBitNumber.MinValue) { Channel = (FourBitNumber)ch });
        Visual(track, 0x80 | ch, note, 0);
    }

    private void ReleaseTrack(int track)
    {
        if (track >= _held.Length) return;
        for (int key = 0; key < 16 * 128; key++)
        {
            bool held;
            lock (_heldLock) held = _held[track][key] > 0;
            if (held) NoteOff(track, key / 128, key % 128);
        }
    }

    private void ReleaseAll()
    {
        for (int t = 0; t < _held.Length; t++) ReleaseTrack(t);
    }

    private void StopCore(bool resetPosition)
    {
        _playing = false;
        Volatile.Write(ref _isPlaying, 0);
        _clock.Stop();
        ReleaseAll();
        if (resetPosition) SeekCore(0);
    }

    private void SeekCore(long us)
    {
        if (_song is null) return;
        us = Math.Clamp(us, 0, _song.LengthUs);
        ReleaseAll();
        PublishPosition(us);
        _next = _song.FirstEventAtOrAfter(us);
        if (_playing)
        {
            Chase(us);
            _startUs = us;
            _clock.Restart();
        }
    }

    /// <summary>Sends the last program, controller values and pitch bend before <paramref name="us"/> for every track.</summary>
    private void Chase(long us, int onlyTrack = -1)
    {
        if (_song is null) return;
        int end = _song.FirstEventAtOrAfter(us);
        var last = new Dictionary<(int track, int ch, int kind), ChannelEvent>();
        for (int i = 0; i < end; i++)
        {
            PlayEvent pe = _song.Events[i];
            if (onlyTrack >= 0 && pe.Track != onlyTrack) continue;
            int kind = pe.Event switch
            {
                ProgramChangeEvent => 1000,
                PitchBendEvent => 1001,
                ControlChangeEvent cc when (int)cc.ControlNumber is not (120 or 121 or 123) => cc.ControlNumber,
                _ => -1,
            };
            if (kind >= 0) last[(pe.Track, pe.Event.Channel, kind)] = pe.Event;
        }
        // Program first, then controllers, then pitch bend.
        foreach (var kv in last.OrderBy(k => k.Key.kind == 1000 ? 0 : k.Key.kind == 1001 ? 2 : 1))
            Dispatch(kv.Key.track, kv.Value, live: false);
    }

    private void Panic()
    {
        // CC 123 (all notes off) + CC 64 = 0 on every channel of every open device.
        foreach (OutputDevice d in _openDevices.Values)
        {
            for (int ch = 0; ch < 16; ch++)
            {
                TrySend(d, new ControlChangeEvent((SevenBitNumber)64, SevenBitNumber.MinValue) { Channel = (FourBitNumber)ch });
                TrySend(d, new ControlChangeEvent((SevenBitNumber)123, SevenBitNumber.MinValue) { Channel = (FourBitNumber)ch });
            }
        }
    }

    private void Visual(int track, int status, int d1, int d2)
    {
        if (!_visualsEnabled || Volatile.Read(ref _visualCount) > 20000) return;
        Interlocked.Increment(ref _visualCount);
        _visuals.Enqueue(new VisualEvent(track, (byte)status, (byte)d1, (byte)d2));
    }

    private void Send(int track, MidiEvent e)
    {
        string name = _deviceOf[track];
        if (name == NoDevice) return;
        if (!_openDevices.TryGetValue(name, out OutputDevice? dev))
        {
            try
            {
                dev = OutputDevice.GetByName(name);
                dev.PrepareForEventsSending();
                _openDevices[name] = dev;
            }
            catch (Exception ex)
            {
                _lastError = $"Can't open \"{name}\": {ex.Message}";
                _deviceOf[track] = NoDevice; // don't retry every event
                return;
            }
        }
        TrySend(dev, e);
    }

    private void TrySend(OutputDevice dev, MidiEvent e)
    {
        try { dev.SendEvent(e); }
        catch (Exception ex) { _lastError = $"{dev.Name}: {ex.Message}"; }
    }

    private void CloseUnusedDevices()
    {
        foreach (string name in _openDevices.Keys.ToList())
        {
            if (_deviceOf.Contains(name)) continue;
            _openDevices[name].Dispose();
            _openDevices.Remove(name);
        }
    }

    #endregion
}

/// <summary>1 ms Windows timer resolution while playing, so Sleep/WaitOne(1) really waits ~1 ms.</summary>
internal static class WinTimer
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);

    public static void BeginHighResolution()
    {
        try { _ = timeBeginPeriod(1); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }

    public static void EndHighResolution()
    {
        try { _ = timeEndPeriod(1); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
}
