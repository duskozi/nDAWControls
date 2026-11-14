using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using nDAWControls.Models;

namespace nDAWControls.Gallery;

/// <summary>
/// One entry of the control catalog. <see cref="Xaml"/> is exactly what the gallery parses and shows live,
/// so what you copy is what you see. <see cref="CSharp"/> shows the code a control needs on top of its XAML
/// (data, events); <see cref="Setup"/> runs the same thing for the live preview.
/// </summary>
public sealed record Snippet(string Name, string Group, string Description, string Xaml, string? CSharp = null,
                             Action<FrameworkElement>? Setup = null)
{
    public override string ToString() => Name;
}

public static class SnippetCatalog
{
    public static IReadOnlyList<Snippet> All { get; } = new List<Snippet>
    {
        // --- Knobs, faders and values ---

        new("Knob", "Knobs, faders and values",
            "A rotary control. Drag up and down (Shift for fine steps), use the mouse wheel or the arrow keys. " +
            "Double-click returns to DefaultValue. Taper changes the curve, ValueFormatter the text.",
            """
            <WrapPanel>
                <daw:Knob Label="Gain" Minimum="0" Maximum="24" Value="6" ValueStringFormat="{}{0:0.0} dB"/>
                <daw:Knob Label="Cutoff" Taper="Log" Minimum="20" Maximum="20000" DefaultValue="1200" Value="1200"
                          ValueFormatter="{x:Static daw:ValueFormatters.Frequency}"/>
                <daw:Knob Label="Pan" IsBipolar="True" Minimum="-1" Maximum="1" DefaultValue="0" Value="-0.3"
                          ValueFormatter="{x:Static daw:ValueFormatters.Pan}"/>
                <daw:Knob Label="Mix" Value="0.8" ArcBrush="#FFB18CFF" ValueFormatter="{x:Static daw:ValueFormatters.Percent}"/>
                <daw:Knob Label="Small" Width="36" Height="48" Value="0.5"/>
            </WrapPanel>
            """,
            """
            knob.ValueChanged += (s, e) => synth.SetCutoff(e.NewValue);
            """),

        new("Fader", "Knobs, faders and values",
            "A vertical or horizontal slider with a moving cap. Taper=\"Decibel\" gives the usual mixer curve.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:Fader Height="220" Taper="Decibel" Minimum="-60" Maximum="6" DefaultValue="0" Value="-6"
                           MinimumText="-∞" ValueFormatter="{x:Static daw:ValueFormatters.Decibel}"/>
                <daw:Fader Orientation="Horizontal" Width="220" Height="24" Margin="24,0,0,0" VerticalAlignment="Top"
                           Label="Glide" Value="0.25" ValueFormatter="{x:Static daw:ValueFormatters.Percent}"/>
            </StackPanel>
            """),

        new("Fader with scale and meter", "Knobs, faders and values",
            "Put a ValueScale and a LevelMeter next to a fader. The fader cap is 40 high, so padding the scale and " +
            "meter by 20 lines the ticks up with the middle of the cap.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:ValueScale Placement="End" Padding="0,20,0,20" Minimum="-60" Maximum="0" Taper="Decibel"
                                MinimumText="-∞" Ticks="0 -3 -6 -12 -20 -30 -60"/>
                <daw:Fader x:Name="fader" Width="30" Height="240" Taper="Decibel" Minimum="-60" Maximum="0" Value="-4"
                           ShowValueText="False" MinimumText="-∞"/>
                <daw:LevelMeter x:Name="meter" Width="8" Margin="4,20,0,20"/>
            </StackPanel>
            """,
            """
            // From the audio thread (lock-free, any thread):
            meter.PushLevels(peak, rms);
            """,
            root => Demo.FeedMeters(root, "meter")),

        new("ValueBox", "Knobs, faders and values",
            "A numeric field. Drag up and down, use the mouse wheel, or double-click to type a value.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:ValueBox Label="TRANSPOSE" Minimum="-24" Maximum="24" Step="1" Value="0" Width="90"
                              ValueStringFormat="{}{0:+0;-0;0}" Unit="st"/>
                <daw:ValueBox Label="VOICES" Minimum="1" Maximum="16" Step="1" Value="8" Width="70" Margin="8,0,0,0"
                              ValueFormatter="{x:Static daw:ValueFormatters.Integer}"/>
                <daw:ValueBox Label="TEMPO" Minimum="20" Maximum="300" Value="120" Width="80" Margin="8,0,0,0"
                              ValueFormatter="{x:Static daw:ValueFormatters.Tempo}"/>
            </StackPanel>
            """),

        new("XY pad", "Knobs, faders and values",
            "Two values at once, for example filter cutoff and resonance. Double-click returns to DefaultX and DefaultY.",
            """
            <daw:XYPad Width="260" Height="180" XLabel="cutoff" YLabel="resonance" XValue="0.6" YValue="0.35"/>
            """,
            """
            // XValue and YValue are 0..1; bind them to your model:
            // <daw:XYPad XValue="{Binding Cutoff, Mode=TwoWay}" YValue="{Binding Resonance, Mode=TwoWay}"/>
            """),

        new("Envelope editor", "Knobs, faders and values",
            "An ADSR envelope. Drag the handles; Attack, Decay and Release are in seconds, Sustain is 0 to 1.",
            """
            <daw:EnvelopeEditor Width="320" Height="150" Attack="0.02" Decay="0.4" Sustain="0.65" Release="0.9"/>
            """),

        // --- Buttons and transport ---

        new("LED buttons", "Buttons and transport",
            "Toggle buttons with a coloured light. Role picks the colour from the skin: Mute, Solo, RecordArm, Monitor or Generic.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:LedToggleButton Role="Mute" Content="M" Width="28" Height="24"/>
                <daw:LedToggleButton Role="Solo" Content="S" Width="28" Height="24" Margin="4,0,0,0" IsChecked="True"/>
                <daw:LedToggleButton Role="RecordArm" Content="R" Width="28" Height="24" Margin="4,0,0,0"/>
                <daw:LedToggleButton Role="Monitor" Content="MON" Height="24" Margin="4,0,0,0"/>
                <daw:LedToggleButton Content="LEGATO" Height="24" Margin="4,0,0,0" IsChecked="True"/>
            </StackPanel>
            """),

        new("Transport buttons", "Buttons and transport",
            "Single transport buttons with built-in icons, when you lay out your own transport.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:TransportButton Kind="ToStart"/>
                <daw:TransportButton Kind="Play" Margin="4,0,0,0"/>
                <daw:TransportButton Kind="Stop" Margin="4,0,0,0"/>
                <daw:TransportButton Kind="Record" Margin="4,0,0,0"/>
                <daw:TransportButton Kind="Loop" Margin="4,0,0,0" IsChecked="True"/>
                <daw:TransportButton Kind="Metronome" Margin="4,0,0,0"/>
            </StackPanel>
            """),

        new("Transport bar", "Buttons and transport",
            "A complete transport: buttons, position, tempo and time signature. Bind IsPlaying, Position and Tempo to your engine.",
            """
            <daw:TransportBar Width="760" Tempo="124" BeatsPerBar="4" IsLooping="True"/>
            """,
            """
            transport.IsPlaying = engine.IsPlaying;
            transport.Position = engine.PositionInBeats;   // update every frame
            """),

        new("Time display", "Buttons and transport",
            "An LCD readout of a position in beats, as bars:beats:ticks or as clock time.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:TimeDisplay Position="13.5" Tempo="120" Caption="BAR"/>
                <daw:TimeDisplay Position="13.5" Tempo="120" Mode="Clock" Caption="TIME" Margin="8,0,0,0"/>
            </StackPanel>
            """),

        // --- Mixer ---

        new("Channel strip", "Mixer",
            "A complete mixer channel: arm, mute, solo, pan, fader and a stereo meter. Feed the meter with PushLevels.",
            """
            <StackPanel Orientation="Horizontal" Height="360">
                <daw:ChannelStrip x:Name="kick" TrackName="Kick" TrackColor="#FFFF6B5A"/>
                <daw:ChannelStrip x:Name="bass" TrackName="Bass" TrackColor="#FF6FA8FF" Volume="-6" Pan="-0.2" Margin="4,0,0,0"/>
                <daw:ChannelStrip x:Name="keys" TrackName="Keys" TrackColor="#FF5AD1A0" Volume="-9" IsArmed="True" Margin="4,0,0,0"/>
            </StackPanel>
            """,
            """
            // From the audio thread (lock-free, any thread):
            kick.PushLevels(peakL, rmsL, peakR, rmsR);
            """,
            root => Demo.FeedMeters(root, "kick", "bass", "keys")),

        new("Level meter", "Mixer",
            "A peak meter with RMS, peak hold and a clip light. Click the clip light to reset it.",
            """
            <StackPanel Orientation="Horizontal" Height="200">
                <daw:LevelMeter x:Name="left" Width="10"/>
                <daw:LevelMeter x:Name="right" Width="10" Margin="3,0,0,0"/>
                <daw:LevelMeter x:Name="wide" Orientation="Horizontal" Width="240" Height="10" Margin="24,0,0,0"
                                VerticalAlignment="Top" ShowRms="True"/>
            </StackPanel>
            """,
            """
            meter.PushLevels(peak, rms);   // linear 0..1 (1 = 0 dBFS), any thread
            """,
            root => Demo.FeedMeters(root, "left", "right", "wide")),

        new("Track header", "Mixer",
            "The header of a track in an arrangement: number, colour, name, subtitle and an activity light.",
            """
            <StackPanel Width="280">
                <daw:TrackHeader TrackNumber="1" TrackName="Piano" Subtitle="Ch 1 · Grand Piano" TrackColor="#FF6FA8FF"/>
                <daw:TrackHeader TrackNumber="2" TrackName="Strings" Subtitle="Ch 2 · String Ensemble" TrackColor="#FFFFC24A"
                                 Margin="0,4,0,0" IsAudible="False"/>
            </StackPanel>
            """),

        new("Panel", "Mixer",
            "A titled panel in the skin's colours, with an optional hide button and tool area in the header.",
            """
            <daw:DawPanel Header="INSTRUMENT" Width="300" Height="160" CanHide="True">
                <WrapPanel Margin="8">
                    <daw:Knob Label="Attack" Value="0.2"/>
                    <daw:Knob Label="Release" Value="0.6"/>
                    <daw:Knob Label="Volume" Value="0.8"/>
                </WrapPanel>
            </daw:DawPanel>
            """),

        // --- Piano ---

        new("Piano keyboard", "Piano",
            "Click or drag to play; lower on a key plays harder. With focus, the computer keyboard plays too (A W S E D…, Z/X = octave). " +
            "KeyStyle is ThreeD, Flat or Grand.",
            """
            <StackPanel>
                <daw:PianoKeyboard x:Name="keys" Height="100" LowestNote="36" HighestNote="96"/>
                <daw:PianoKeyboard Height="90" LowestNote="48" HighestNote="84" KeyStyle="Grand" Margin="0,12,0,0"/>
                <daw:PianoKeyboard Height="60" LowestNote="48" HighestNote="84" KeyStyle="Flat" Margin="0,12,0,0"/>
            </StackPanel>
            """,
            """
            keys.NoteOn  += (s, e) => midiOut.NoteOn(0, e.Note, e.Velocity);
            keys.NoteOff += (s, e) => midiOut.NoteOff(0, e.Note);
            keys.SetHighlighted(60, 100);   // light a key from playback
            """,
            root => { if (root.FindName("keys") is PianoKeyboard k) { k.SetHighlighted(60, 110); k.SetHighlighted(64, 80); k.SetHighlighted(67, 60); } }),

        new("Wheels and pedals", "Piano",
            "Pitch-bend and modulation wheels (pitch bend springs back), and the three piano pedals.",
            """
            <StackPanel Orientation="Horizontal">
                <daw:PerformanceWheel Mode="PitchBend" Height="110"/>
                <daw:PerformanceWheel Mode="Modulation" Height="110" Margin="6,0,0,0"/>
                <daw:PianoPedals Margin="24,0,0,0" VerticalAlignment="Bottom" SustainValue="127"/>
            </StackPanel>
            """,
            """
            wheel.ValueChanged += (s, e) => midiOut.PitchBend(0, e.NewValue);
            pedals.PedalChanged += (s, e) => midiOut.ControlChange(0, e.Controller, e.Value);
            pedals.ApplyControlChange(64, 127);   // show a sustain pedal from the song
            """),

        // --- Editors ---

        new("Piano roll", "Editors",
            "A note editor. Click to add, drag to move, drag an edge to resize, right-click to delete, Ctrl+wheel to zoom. " +
            "Notes is an ObservableCollection<MidiNote>, so your model and the roll stay in sync.",
            """
            <ScrollViewer Width="760" Height="320" CanContentScroll="True"
                          HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
                <daw:PianoRoll x:Name="roll" LengthInBeats="16" Snap="0.25"/>
            </ScrollViewer>
            """,
            """
            var notes = new ObservableCollection<MidiNote>
            {
                new(60, start: 0, length: 1), new(64, 1, 1), new(67, 2, 2),
            };
            roll.Notes = notes;
            roll.AuditionNoteOn += (s, e) => midiOut.NoteOn(0, e.Note, e.Velocity);
            """,
            root => { if (root.FindName("roll") is PianoRoll r) r.Notes = Demo.Notes(); }),

        new("Controller lane", "Editors",
            "Velocity, controller or pitch-bend values under a piano roll. It follows the roll's scroll, zoom and snap.",
            """
            <StackPanel Width="760">
                <ScrollViewer Height="220" CanContentScroll="True" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
                    <daw:PianoRoll x:Name="roll" LengthInBeats="16" Snap="0.25"/>
                </ScrollViewer>
                <daw:ControllerLane Height="100" Mode="Velocity" Roll="{Binding ElementName=roll}"/>
            </StackPanel>
            """,
            """
            lane.Mode = LaneMode.Controller;
            lane.ControllerNumber = 1;                        // modulation
            lane.Points = new ObservableCollection<ControllerPoint>();
            """,
            root => { if (root.FindName("roll") is PianoRoll r) r.Notes = Demo.Notes(); }),

        new("Step sequencer", "Editors",
            "A drum grid. Click to toggle a step, drag up and down on a step to change its velocity.",
            """
            <daw:StepSequencer x:Name="steps" Width="700" Height="140" CurrentStep="4"/>
            """,
            """
            var pattern = new StepPattern(rows: 4, steps: 16);
            pattern.SetVelocity(0, 0, 120);   // kick on step 1
            steps.Pattern = pattern;
            steps.RowLabels = new[] { "Kick", "Snare", "Hat", "Perc" };
            """,
            root =>
            {
                if (root.FindName("steps") is not StepSequencer s) return;
                var p = new StepPattern(4, 16);
                for (int i = 0; i < 16; i += 4) p.SetVelocity(0, i, 120);
                p.SetVelocity(1, 4, 110); p.SetVelocity(1, 12, 110);
                for (int i = 0; i < 16; i += 2) p.SetVelocity(2, i, i % 4 == 0 ? 90 : 60);
                p.SetVelocity(3, 14, 80);
                s.Pattern = p;
                s.RowLabels = new[] { "Kick", "Snare", "Hat", "Perc" };
            }),

        new("MIDI activity", "Editors",
            "Lights for 16 MIDI channels (or your tracks): notes on top, controllers below. Call NoteOn, NoteOff and ControlChange from any thread.",
            """
            <StackPanel Width="420">
                <daw:MidiActivityMonitor x:Name="activity" Source="Channels" RowHeight="12" LowestNote="24" HighestNote="96"/>
                <daw:MidiActivityMonitor x:Name="compact" Layout="Compact" Height="20" Margin="0,12,0,0"/>
            </StackPanel>
            """,
            """
            activity.NoteOn(channel, note, velocity);
            activity.ControlChange(channel, 7, 100);
            """,
            root => Demo.FeedActivity(root, "activity", "compact")),
    };
}

/// <summary>Demo data and fake signals for the live previews.</summary>
internal static class Demo
{
    private static readonly List<System.Windows.Threading.DispatcherTimer> s_timers = new();

    /// <summary>Stops the timers of the previous preview.</summary>
    public static void StopAll()
    {
        foreach (var t in s_timers) t.Stop();
        s_timers.Clear();
    }

    public static ObservableCollection<MidiNote> Notes()
    {
        var notes = new ObservableCollection<MidiNote>();
        int[] chord = { 60, 64, 67, 72 };
        for (int bar = 0; bar < 4; bar++)
        {
            int root = new[] { 0, -3, 5, 7 }[bar];
            foreach (int n in chord) notes.Add(new MidiNote(n + root - 12, bar * 4, 4, 70));
            for (int i = 0; i < 8; i++) notes.Add(new MidiNote(chord[i % 4] + root + (i >= 4 ? 12 : 0), bar * 4 + i * 0.5, 0.5, 80 + i * 4));
        }
        return notes;
    }

    private static void Every(int ms, Action tick)
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => tick();
        t.Start();
        s_timers.Add(t);
    }

    public static void FeedMeters(FrameworkElement root, params string[] names)
    {
        var rng = new Random(3);
        double phase = 0;
        Every(30, () =>
        {
            phase += 0.08;
            for (int i = 0; i < names.Length; i++)
            {
                double level = 0.35 + 0.3 * Math.Abs(Math.Sin(phase * (1 + i * 0.37))) + rng.NextDouble() * 0.25;
                float peak = (float)Math.Min(1.05, level), rms = (float)(level * 0.6);
                switch (root.FindName(names[i]))
                {
                    case LevelMeter m: m.PushLevels(peak, rms); break;
                    case ChannelStrip s: s.PushLevels(peak, rms, peak * 0.92f, rms * 0.95f); break;
                }
            }
        });
    }

    public static void FeedActivity(FrameworkElement root, params string[] names)
    {
        var rng = new Random(5);
        Every(120, () =>
        {
            foreach (var name in names)
            {
                if (root.FindName(name) is not MidiActivityMonitor m) continue;
                int ch = rng.Next(16), note = 36 + rng.Next(48);
                m.NoteOn(ch, note, 60 + rng.Next(60));
                if (rng.Next(3) == 0) m.ControlChange(rng.Next(16), 7, rng.Next(128));
                int off = rng.Next(16);
                m.AllNotesOff(off);
            }
        });
    }
}
