using System;
using System.Collections.Generic;
using System.Linq;
using nDAWControls.Models;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace nDAWControls.MidiPlayer.Engine;

/// <summary>One channel event of the song, with its absolute time and the track it belongs to.</summary>
public readonly record struct PlayEvent(long Us, int Track, ChannelEvent Event);

/// <summary>A tempo segment: from <see cref="StartUs"/> on, one beat lasts <see cref="UsPerBeat"/>.</summary>
public readonly record struct TempoSegment(long StartUs, double StartBeat, double UsPerBeat);

public sealed class SongTrack
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    /// <summary>Most used channel (0-based), or -1 if the track has no channel events.</summary>
    public required int Channel { get; init; }
    public required int NoteCount { get; init; }
    public required int? Program { get; init; }
    /// <summary>Notes in beats, for the piano roll.</summary>
    public required IReadOnlyList<(int Pitch, double Start, double Length, int Velocity)> Notes { get; init; }
    /// <summary>Controller curves for the lane: key = CC number, or -1 for pitch bend.</summary>
    public required IReadOnlyDictionary<int, List<(double Beat, int Value)>> Controllers { get; init; }

    public bool IsDrums => Channel == 9;

    public string Subtitle
    {
        get
        {
            string ch = Channel >= 0 ? $"Ch {Channel + 1}" : "No channel data";
            string inst = IsDrums ? "Drums" : Program is int p ? GeneralMidi.Instrument(p) : "";
            return string.Join(" · ", new[] { ch, inst, $"{NoteCount} notes" }.Where(s => s.Length > 0));
        }
    }
}

/// <summary>
/// A MIDI file prepared for playback and display. DryWetMIDI does the parsing, tempo map and time conversion;
/// the result is a flat, time-sorted event array the scheduler can walk without allocating.
/// </summary>
public sealed class Song
{
    private readonly TempoSegment[] _tempo;

    private Song(string title, IReadOnlyList<SongTrack> tracks, PlayEvent[] events, TempoSegment[] tempo,
                 long lengthUs, int beatsPerBar, int beatUnit)
    {
        Title = title;
        Tracks = tracks;
        Events = events;
        _tempo = tempo;
        LengthUs = lengthUs;
        BeatsPerBar = beatsPerBar;
        BeatUnit = beatUnit;
        LengthBeats = UsToBeats(lengthUs);
    }

    public string Title { get; }
    public IReadOnlyList<SongTrack> Tracks { get; }
    /// <summary>All channel events of all tracks, sorted by time (stable: file order within equal times).</summary>
    public PlayEvent[] Events { get; }
    public long LengthUs { get; }
    public double LengthBeats { get; }
    public int BeatsPerBar { get; }
    public int BeatUnit { get; }

    public static Song Load(string path) => FromMidiFile(MidiFile.Read(path), System.IO.Path.GetFileNameWithoutExtension(path));

    public static Song FromMidiFile(MidiFile file, string title)
    {
        if (file.TimeDivision is not TicksPerQuarterNoteTimeDivision tpqDivision)
            throw new NotSupportedException("SMPTE time division is not supported.");
        double tpq = tpqDivision.TicksPerQuarterNote;
        TempoMap tempoMap = file.GetTempoMap();

        // Tempo segments (µs ↔ beats) built once, so per-frame position conversion is a binary search.
        var segments = new List<TempoSegment>();
        double usPerBeat = tempoMap.GetTempoAtTime(new MidiTimeSpan(0)).MicrosecondsPerQuarterNote;
        segments.Add(new TempoSegment(0, 0, usPerBeat));
        foreach (var change in tempoMap.GetTempoChanges())
        {
            if (change.Time == 0) { segments[0] = new TempoSegment(0, 0, change.Value.MicrosecondsPerQuarterNote); continue; }
            long us = TimeConverter.ConvertTo<MetricTimeSpan>(change.Time, tempoMap).TotalMicroseconds;
            segments.Add(new TempoSegment(us, change.Time / tpq, change.Value.MicrosecondsPerQuarterNote));
        }

        var ts = tempoMap.GetTimeSignatureAtTime(new MidiTimeSpan(0));
        var tracks = new List<SongTrack>();
        var events = new List<(long us, int track, int seq, ChannelEvent ev)>();
        long lengthUs = 0;
        int seq = 0;
        int index = 0;

        foreach (TrackChunk chunk in file.GetTrackChunks())
        {
            string? name = chunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text?.Trim();
            var channelUse = new int[16];
            int? program = null;
            var controllers = new Dictionary<int, List<(double, int)>>();

            foreach (TimedEvent te in chunk.GetTimedEvents())
            {
                if (te.Event is not ChannelEvent ce) continue;
                long us = TimeConverter.ConvertTo<MetricTimeSpan>(te.Time, tempoMap).TotalMicroseconds;
                events.Add((us, index, seq++, ce));
                lengthUs = Math.Max(lengthUs, us);
                channelUse[ce.Channel]++;
                double beat = te.Time / tpq;
                switch (ce)
                {
                    case ProgramChangeEvent pc when program is null:
                        program = pc.ProgramNumber;
                        break;
                    case ControlChangeEvent cc:
                        Add(controllers, cc.ControlNumber, beat, cc.ControlValue);
                        break;
                    case PitchBendEvent pb:
                        Add(controllers, -1, beat, pb.PitchValue - 8192);
                        break;
                }
            }

            var notes = chunk.GetNotes()
                .Select(n => ((int)n.NoteNumber, n.Time / tpq, Math.Max(n.Length / tpq, MidiNote.MinLength), (int)n.Velocity))
                .ToList();

            int channel = channelUse.Max() == 0 ? -1 : Array.IndexOf(channelUse, channelUse.Max());
            // Skip pure conductor / meta tracks: nothing to play or show.
            if (channel >= 0 || notes.Count > 0)
            {
                tracks.Add(new SongTrack
                {
                    Index = index,
                    Name = string.IsNullOrEmpty(name) ? $"Track {index + 1}" : name!,
                    Channel = channel,
                    NoteCount = notes.Count,
                    Program = program,
                    Notes = notes,
                    Controllers = controllers,
                });
            }
            index++;
        }

        // Re-index tracks to 0..n-1 (conductor tracks removed) and remap events.
        var remap = tracks.Select((t, i) => (t.Index, i)).ToDictionary(x => x.Index, x => x.i);
        tracks = tracks.Select((t, i) => new SongTrack
        {
            Index = i, Name = t.Name, Channel = t.Channel, NoteCount = t.NoteCount, Program = t.Program,
            Notes = t.Notes, Controllers = t.Controllers,
        }).ToList();

        PlayEvent[] sorted = events
            .Where(e => remap.ContainsKey(e.track))
            .OrderBy(e => e.us).ThenBy(e => e.seq)
            .Select(e => new PlayEvent(e.us, remap[e.track], e.ev))
            .ToArray();

        return new Song(title, tracks, sorted, segments.ToArray(), lengthUs, ts.Numerator, ts.Denominator);

        static void Add(Dictionary<int, List<(double, int)>> d, int key, double beat, int value)
        {
            if (!d.TryGetValue(key, out var list)) d[key] = list = new List<(double, int)>();
            list.Add((beat, value));
        }
    }

    private int SegmentAtUs(long us)
    {
        int lo = 0, hi = _tempo.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_tempo[mid].StartUs <= us) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    private int SegmentAtBeat(double beat)
    {
        int lo = 0, hi = _tempo.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_tempo[mid].StartBeat <= beat) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    public double UsToBeats(long us)
    {
        TempoSegment s = _tempo[SegmentAtUs(us)];
        return s.StartBeat + (us - s.StartUs) / s.UsPerBeat;
    }

    public long BeatsToUs(double beats)
    {
        TempoSegment s = _tempo[SegmentAtBeat(beats)];
        return s.StartUs + (long)Math.Round((beats - s.StartBeat) * s.UsPerBeat);
    }

    public double BpmAt(long us) => 60_000_000.0 / _tempo[SegmentAtUs(us)].UsPerBeat;

    /// <summary>Index of the first event at or after <paramref name="us"/>.</summary>
    public int FirstEventAtOrAfter(long us)
    {
        int lo = 0, hi = Events.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Events[mid].Us < us) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
