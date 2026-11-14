using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace nDAWControls.MidiPlayer.Engine;

/// <summary>
/// Builds a 16-bar demo song in memory with DryWetMIDI, so the player works without a file:
/// drums (ch 10), bass, electric piano with sustain pedal, a lead with mod wheel and pitch bends,
/// and a pad with expression swells – i.e. notes plus every kind of controller the monitors show.
/// </summary>
public static class DemoSong
{
    private const short Tpq = 480;
    private const int Bars = 16;

    public static MidiFile Create()
    {
        var conductor = new List<TimedEvent>
        {
            new(new SequenceTrackNameEvent("Conductor"), 0),
            new(new SetTempoEvent(60_000_000 / 112), 0),
            new(new TimeSignatureEvent(4, 4), 0),
        };

        var file = new MidiFile(
            conductor.ToTrackChunk(),
            Drums(), Bass(), Keys(), Lead(), Pad())
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(Tpq),
        };
        return file;
    }

    private static readonly int[][] s_chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };

    private static long Beat(double b) => (long)Math.Round(b * Tpq);

    private static void Note(List<TimedEvent> list, int ch, int note, double start, double length, int vel)
    {
        list.Add(new TimedEvent(new NoteOnEvent((SevenBitNumber)note, (SevenBitNumber)Math.Clamp(vel, 1, 127)) { Channel = (FourBitNumber)ch }, Beat(start)));
        list.Add(new TimedEvent(new NoteOffEvent((SevenBitNumber)note, SevenBitNumber.MinValue) { Channel = (FourBitNumber)ch }, Beat(start + length)));
    }

    private static TimedEvent Cc(int ch, int cc, int value, double at)
        => new(new ControlChangeEvent((SevenBitNumber)cc, (SevenBitNumber)Math.Clamp(value, 0, 127)) { Channel = (FourBitNumber)ch }, Beat(at));

    private static TimedEvent Program(int ch, int program)
        => new(new ProgramChangeEvent((SevenBitNumber)program) { Channel = (FourBitNumber)ch }, 0);

    private static TrackChunk Drums()
    {
        var e = new List<TimedEvent> { new(new SequenceTrackNameEvent("Drums"), 0) };
        for (int bar = 0; bar < Bars; bar++)
        {
            for (int s = 0; s < 16; s++)
            {
                double t = bar * 4 + s * 0.25;
                if (s % 4 == 0 || (bar % 4 == 3 && s == 14)) Note(e, 9, 36, t, 0.2, 118);
                if (s % 8 == 4) Note(e, 9, 38, t, 0.2, 110);
                if (s % 2 == 0) Note(e, 9, 42, t, 0.1, s % 4 == 2 ? 96 : 60);
                if (bar % 4 == 3 && s >= 12) Note(e, 9, 45 + (s - 12) * 2, t, 0.2, 90);
            }
            if (bar % 4 == 0) Note(e, 9, 49, bar * 4, 1.5, 100);
        }
        return e.ToTrackChunk();
    }

    private static TrackChunk Bass()
    {
        var e = new List<TimedEvent> { new(new SequenceTrackNameEvent("Bass"), 0), Program(1, 33) };
        for (int bar = 0; bar < Bars; bar++)
        {
            int root = s_chords[(bar / 2) % 4][0] - 24;
            for (int i = 0; i < 8; i++)
                Note(e, 1, root + (i == 6 ? 7 : i == 7 ? 12 : 0), bar * 4 + i * 0.5, 0.42, i % 2 == 0 ? 105 : 80);
        }
        return e.ToTrackChunk();
    }

    private static TrackChunk Keys()
    {
        var e = new List<TimedEvent> { new(new SequenceTrackNameEvent("Electric Piano"), 0), Program(0, 4) };
        for (int bar = 0; bar < Bars; bar++)
        {
            int[] chord = s_chords[(bar / 2) % 4];
            double t = bar * 4;
            e.Add(Cc(0, 64, 0, t));           // lift pedal…
            e.Add(Cc(0, 64, 127, t + 0.05));  // …and catch the new chord
            foreach (double off in new[] { 0, 1.5, 2.5 })
                foreach (int n in chord) Note(e, 0, n, t + off, 0.8, off == 0 ? 92 : 70);
        }
        e.Add(Cc(0, 64, 0, Bars * 4));
        return e.ToTrackChunk();
    }

    private static TrackChunk Lead()
    {
        var e = new List<TimedEvent> { new(new SequenceTrackNameEvent("Lead"), 0), Program(2, 81) };
        int[] melody = { 76, 74, 72, 74, 76, 76, 79, 76, 74, 72, 71, 72, 74, 72, 69, 67 };
        for (int bar = 4; bar < Bars; bar++)
        {
            for (int i = 0; i < 4; i++)
            {
                int n = melody[((bar - 4) * 4 + i) % melody.Length];
                Note(e, 2, n, bar * 4 + i, i == 3 ? 0.9 : 0.6, 85 + (i % 2) * 20);
            }
            // Mod wheel swell over every second bar.
            for (int k = 0; k <= 16; k++)
                e.Add(Cc(2, 1, bar % 2 == 0 ? k * 6 : 96 - k * 6, bar * 4 + k * 0.25));
            // A pitch bend scoop into the last note of every 4th bar.
            if (bar % 4 == 3)
            {
                for (int k = 0; k <= 8; k++)
                {
                    int bend = 8192 - (int)(2048 * (1 - k / 8.0));
                    e.Add(new TimedEvent(new PitchBendEvent((ushort)bend) { Channel = (FourBitNumber)2 }, Beat(bar * 4 + 3 + k * 0.03)));
                }
            }
        }
        return e.ToTrackChunk();
    }

    private static TrackChunk Pad()
    {
        var e = new List<TimedEvent> { new(new SequenceTrackNameEvent("Pad"), 0), Program(3, 89) };
        for (int bar = 0; bar < Bars; bar += 2)
        {
            int[] chord = s_chords[(bar / 2) % 4];
            foreach (int n in chord) Note(e, 3, n + 12, bar * 4, 7.9, 64);
            for (int k = 0; k <= 32; k++) // expression swell (CC 11)
                e.Add(Cc(3, 11, (int)(40 + 80 * Math.Sin(k / 32.0 * Math.PI)), bar * 4 + k * 0.25));
        }
        return e.ToTrackChunk();
    }
}
