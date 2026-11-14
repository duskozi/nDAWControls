# Controls

Every example assumes `xmlns:daw="urn:ndaw-controls"` on the root element. All of them are also in the gallery
sample, where you can see them live, change the XAML and copy it.

**Common to the value controls** (`Knob`, `Fader`, `ValueBox`): they derive from WPF's `RangeBase`, so `Minimum`,
`Maximum`, `Value` and `ValueChanged` work as usual. On top of that:

| Property | What it does |
| --- | --- |
| `DefaultValue` | Value after a double-click (or Delete key) |
| `Taper` | `Linear`, `Log` (frequencies) or `Decibel` (volume faders) |
| `Step` | Snap the value to steps, for example `1` for whole numbers |
| `Label` | Caption shown under the control |
| `ValueFormatter` | A `Func<double, string>` for the value text, see `ValueFormatters` (`Decibel`, `Pan`, `Percent`, `Frequency`, `Time`, `Tempo`, `Integer`) |
| `ValueStringFormat` | Or a format string, for example `{}{0:0.0} dB` |
| `MinimumText` | Text at the minimum, for example `-∞` |
| `DragSensitivity`, `FineFactor` | Drag speed, and how much slower it gets with Shift |

Mouse: drag up and down, use the wheel, Shift for fine steps, double-click for the default. Keyboard: arrows,
Page Up/Down, Home/End.

- [Knob](#knob) · [Fader](#fader) · [ValueBox](#valuebox) · [XYPad](#xypad) · [EnvelopeEditor](#envelopeeditor)
- [LevelMeter](#levelmeter) · [ValueScale](#valuescale) · [MidiActivityMonitor](#midiactivitymonitor)
- [LedToggleButton](#ledtogglebutton) · [TransportButton](#transportbutton) · [TransportBar](#transportbar) · [TimeDisplay](#timedisplay)
- [ChannelStrip](#channelstrip) · [TrackHeader](#trackheader) · [DawPanel](#dawpanel)
- [PianoKeyboard](#pianokeyboard) · [PerformanceWheel](#performancewheel) · [PianoPedals](#pianopedals)
- [PianoRoll](#pianoroll) · [ControllerLane](#controllerlane) · [StepSequencer](#stepsequencer)
- [NineFrame](#nineframe)

---

## Knob

A rotary control.

```xml
<daw:Knob Label="Gain" Minimum="0" Maximum="24" Value="6" ValueStringFormat="{}{0:0.0} dB"/>

<daw:Knob Label="Cutoff" Taper="Log" Minimum="20" Maximum="20000" DefaultValue="1200"
          Value="{Binding Cutoff, Mode=TwoWay}" ValueFormatter="{x:Static daw:ValueFormatters.Frequency}"/>

<!-- Bipolar: the arc starts in the middle -->
<daw:Knob Label="Pan" IsBipolar="True" Minimum="-1" Maximum="1" DefaultValue="0"
          ValueFormatter="{x:Static daw:ValueFormatters.Pan}"/>

<!-- Own colour and size -->
<daw:Knob Label="Mix" ArcBrush="#FFB18CFF" Width="36" Height="48"/>
```

```csharp
knob.ValueChanged += (s, e) => synth.SetCutoff(e.NewValue);
```

Also: `MinAngle`, `MaxAngle` (sweep, default -135 to 135), `ArcOrigin` (where the arc starts).

## Fader

A slider with a moving cap, vertical by default.

```xml
<daw:Fader Height="220" Taper="Decibel" Minimum="-60" Maximum="6" DefaultValue="0" MinimumText="-∞"
           Value="{Binding VolumeDb, Mode=TwoWay}" ValueFormatter="{x:Static daw:ValueFormatters.Decibel}"/>

<daw:Fader Orientation="Horizontal" Width="200" Height="24" Label="Glide"
           ValueFormatter="{x:Static daw:ValueFormatters.Percent}"/>
```

`ShowValueText="False"` hides the value under the fader. `IsMoveToPointEnabled="True"` jumps to a click.

A mixer fader with a dB scale and a meter. The cap is 40 high, so padding the scale and the meter by 20 lines
the ticks up with the middle of the cap:

```xml
<StackPanel Orientation="Horizontal">
    <daw:ValueScale Placement="End" Padding="0,20,0,20" Minimum="-60" Maximum="0" Taper="Decibel"
                    MinimumText="-∞" Ticks="0 -3 -6 -12 -20 -30 -60"/>
    <daw:Fader Width="30" Height="240" Taper="Decibel" Minimum="-60" Maximum="0" ShowValueText="False"/>
    <daw:LevelMeter x:Name="meter" Width="8" Margin="4,20,0,20"/>
</StackPanel>
```

## ValueBox

A numeric field. Drag or scroll to change it, double-click to type.

```xml
<daw:ValueBox Label="TRANSPOSE" Minimum="-24" Maximum="24" Step="1" Value="0" Width="90"
              ValueStringFormat="{}{0:+0;-0;0}" Unit="st"/>
<daw:ValueBox Label="TEMPO" Minimum="20" Maximum="300" Value="120" Width="80"
              ValueFormatter="{x:Static daw:ValueFormatters.Tempo}"/>
```

`IsEditing` is true while the user types.

## XYPad

Two values at once, both 0 to 1.

```xml
<daw:XYPad Width="260" Height="180" XLabel="cutoff" YLabel="resonance"
           XValue="{Binding Cutoff, Mode=TwoWay}" YValue="{Binding Resonance, Mode=TwoWay}"
           DefaultX="0.5" DefaultY="0.5" GridDivisions="4"/>
```

Events: `DragStarted`, `DragCompleted` (for one undo step per drag).

## EnvelopeEditor

An ADSR envelope. `Attack`, `Decay` and `Release` are in seconds, `Sustain` is 0 to 1.

```xml
<daw:EnvelopeEditor Width="320" Height="150" Attack="0.02" Decay="0.4" Sustain="0.65" Release="0.9"
                    MaxSegmentTime="5"/>
```

Also: `Curvature`, `TimeScale`, and the events `DragStarted` and `DragCompleted`.

---

## LevelMeter

A peak meter with RMS, peak hold and a clip light (click it to reset).

```xml
<daw:LevelMeter x:Name="meter" Width="10" Height="200"/>
<daw:LevelMeter Orientation="Horizontal" Width="240" Height="10" MinDb="-48" ShowRms="True"/>
```

Feed it from the audio thread. `PushLevels` takes linear values (1 = 0 dBFS), needs no lock and no dispatcher,
and the meter draws once per frame with the newest values:

```csharp
void OnAudioBlock(float[] samples)          // audio thread
{
    float peak = 0, sum = 0;
    foreach (float s in samples) { peak = Math.Max(peak, Math.Abs(s)); sum += s * s; }
    meter.PushLevels(peak, MathF.Sqrt(sum / samples.Length));
}
```

You can also bind `Peak` and `Rms` instead. Other settings: `MinDb`, `MaxDb`, `WarningDb`, `DangerDb`,
`ReleaseRate`, `PeakHoldTime`, `SegmentSize` (LED ladder look), `ShowClipIndicator`. `Reset()` clears it.

## ValueScale

Tick marks and labels for a fader or meter.

```xml
<daw:ValueScale Placement="End" Minimum="-60" Maximum="0" Taper="Decibel" MinimumText="-∞"
                Ticks="0 -3 -6 -12 -20 -30 -60" LabelFormat="{}{0:0}"/>
```

`Placement` is `Start` or `End` (which side the ticks are on), `Orientation` vertical or horizontal.

## MidiActivityMonitor

Lights for 16 MIDI channels or for your tracks: notes on top, controllers below, with an optional note map.

```xml
<daw:MidiActivityMonitor x:Name="activity" Source="Channels" RowHeight="12" LowestNote="24" HighestNote="96"/>

<!-- Tracks instead of channels -->
<daw:MidiActivityMonitor x:Name="tracks" Source="Tracks" LaneCount="4" RowHeight="15"/>

<!-- A row of small LEDs for a toolbar -->
<daw:MidiActivityMonitor Layout="Compact" Width="240" Height="20"/>
```

```csharp
tracks.LaneLabels = new[] { "Kick", "Snare", "Hats", "Synth" };

// From any thread:
activity.NoteOn(channel, note, velocity);
activity.NoteOff(channel, note);
activity.ControlChange(channel, controller, value);
activity.PitchBend(channel, value);
activity.AllNotesOff();          // all lanes
```

`LaneClicked` reports a click on a lane, for example to mute it.

---

## LedToggleButton

A toggle button with a coloured light. `Role` takes its colour from the skin.

```xml
<daw:LedToggleButton Role="Mute" Content="M" IsChecked="{Binding IsMuted}"/>
<daw:LedToggleButton Role="Solo" Content="S" IsChecked="{Binding IsSolo}"/>
<daw:LedToggleButton Role="RecordArm" Content="R"/>
<daw:LedToggleButton Role="Monitor" Content="MON"/>
<daw:LedToggleButton Content="LEGATO" LitBrush="#FF35D08A"/>
```

Roles: `Generic` (accent colour), `Mute`, `Solo`, `RecordArm`, `Monitor`.

## TransportButton

A single transport button with a built-in icon.

```xml
<daw:TransportButton Kind="Play" Command="{Binding PlayCommand}"/>
<daw:TransportButton Kind="Loop" IsChecked="{Binding IsLooping}"/>
```

Kinds: `Play`, `Pause`, `Stop`, `Record`, `Rewind`, `FastForward`, `ToStart`, `ToEnd`, `Loop`, `Metronome`.
`Icon` takes your own geometry.

## TransportBar

A complete transport: buttons, position, tempo and time signature.

```xml
<daw:TransportBar Tempo="{Binding Tempo, Mode=TwoWay}" BeatsPerBar="4" BeatUnit="4"
                  IsPlaying="{Binding IsPlaying}" IsLooping="{Binding IsLooping, Mode=TwoWay}"
                  PlayCommand="{Binding PlayCommand}" StopCommand="{Binding StopCommand}"
                  RecordCommand="{Binding RecordCommand}" ToStartCommand="{Binding ToStartCommand}"/>
```

```csharp
CompositionTarget.Rendering += (s, e) => transport.Position = engine.PositionInBeats;
```

Also: `IsRecording`, `IsMetronomeOn`, `RewindCommand`, `ForwardCommand`, `SeekStep` (beats per rewind or
forward step).

## TimeDisplay

An LCD readout of a position in beats.

```xml
<daw:TimeDisplay Position="{Binding PositionInBeats}" Tempo="120"/>
<daw:TimeDisplay Position="{Binding PositionInBeats}" Tempo="120" Mode="Clock"/>
```

`Mode` is `Musical` (bars:beats:ticks) or `Clock` (minutes:seconds); the small caption follows it and reads BAR or TIME. `TicksPerBeat` and `BeatsPerBar` set the
musical format.

---

## ChannelStrip

A mixer channel: arm, mute, solo, pan, fader and a stereo meter.

```xml
<StackPanel Orientation="Horizontal">
    <daw:ChannelStrip TrackName="Kick" TrackColor="#FFFF6B5A" Volume="{Binding KickDb, Mode=TwoWay}"/>
    <daw:ChannelStrip TrackName="Bass" TrackColor="#FF6FA8FF" Volume="-6" Pan="-0.2" IsMuted="{Binding BassMuted}"/>
    <daw:ChannelStrip TrackName="Master" ShowArmButton="False"
                      TrackColor="{DynamicResource {x:Static daw:DawSkinKeys.Accent}}"/>
</StackPanel>
```

```csharp
strip.PushLevels(peakLeft, rmsLeft, peakRight, rmsRight);   // any thread
```

`Volume` is in dB, `Pan` from -1 to 1. `IsMuted`, `IsSoloed` and `IsArmed` are two-way.

## TrackHeader

The header of a track in an arrangement.

```xml
<daw:TrackHeader TrackNumber="1" TrackName="Piano" Subtitle="Ch 1 · Grand Piano" TrackColor="#FF6FA8FF"
                 OutputDevices="{Binding MidiOutputs}" SelectedOutputDevice="{Binding Output, Mode=TwoWay}"
                 ShowDeviceSelector="True" ShowActivity="True"/>
```

```csharp
header.NoteOn(note, velocity);    // lights the activity LED
header.NoteOff(note);
header.HeaderClicked += (s, e) => SelectTrack(1);
```

`IsAudible="False"` shows the track as muted.

## DawPanel

A titled panel in the skin's colours.

```xml
<daw:DawPanel Header="INSTRUMENT" CanHide="True" IsShown="{Binding ShowInstrument, Mode=TwoWay}">
    <daw:DawPanel.Tools>
        <daw:LedToggleButton Content="BYPASS" Height="18"/>
    </daw:DawPanel.Tools>
    <WrapPanel Margin="8">
        <daw:Knob Label="Attack"/>
        <daw:Knob Label="Release"/>
    </WrapPanel>
</daw:DawPanel>
```

`ShowHeader="False"` gives a plain panel.

---

## PianoKeyboard

Click or drag to play; lower on a key plays harder. With keyboard focus, the computer keyboard plays too
(A W S E D F T G Y H U J K, Z and X change the octave).

```xml
<daw:PianoKeyboard x:Name="keys" Height="100" LowestNote="36" HighestNote="96"/>
<daw:PianoKeyboard Height="90" LowestNote="21" HighestNote="108" KeyStyle="Grand" LabelMode="Fingers"/>
```

```csharp
keys.NoteOn  += (s, e) => midiOut.NoteOn(0, e.Note, e.Velocity);
keys.NoteOff += (s, e) => midiOut.NoteOff(0, e.Note);

// Light keys from playback:
keys.SetHighlighted(60, velocity: 100);
keys.SetHighlighted(64, 80, Brushes.Orange);
keys.ClearHighlights();
```

`KeyStyle` is `ThreeD` (default), `Flat` or `Grand`. The Grand style draws ivory keys and red felt, and can show
the hand and finger on lit keys:

```csharp
keys.SetState(new[]
{
    new KeyHighlight(60, Colors.DodgerBlue, 1.0, Hand: 2, Finger: 1),   // right hand, thumb
    new KeyHighlight(48, Colors.Orange, 0.8, Hand: 1, Finger: 5),       // left hand, little finger
});
```

Also: `ShowOctaveLabels`, `MiddleCOctave`, `IsComputerKeyboardEnabled`, `FixedVelocity`,
`VelocitySensitiveLighting`, `SetRange(low, high)`, `ReleaseAll()`.

## PerformanceWheel

Pitch-bend and modulation wheels.

```xml
<daw:PerformanceWheel x:Name="pitch" Mode="PitchBend" Height="110"/>
<daw:PerformanceWheel x:Name="mod" Mode="Modulation" Height="110"/>
```

```csharp
pitch.ValueChanged += (s, e) => midiOut.PitchBend(0, e.NewValue);       // -8192..8191
mod.ValueChanged   += (s, e) => midiOut.ControlChange(0, 1, e.NewValue); // 0..127
```

Pitch bend springs back to the centre (`SpringReturn`), modulation stays where it is left.

## PianoPedals

Soft, sostenuto and sustain pedals.

```xml
<daw:PianoPedals x:Name="pedals" ShowSoft="True" ShowSostenuto="True"/>
```

```csharp
pedals.PedalChanged += (s, e) =>
{
    if (e.IsUserChange) midiOut.ControlChange(0, e.Controller, e.Value);   // only clicks, not playback
};

// Show pedals from the song: returns true for CC 64, 66 and 67.
pedals.ApplyControlChange(controller, value);
```

`SustainValue`, `SostenutoValue` and `SoftValue` (0 to 127) can also be bound. `IsInteractive="False"` makes the
pedals display-only.

---

## PianoRoll

A note editor with a keyboard gutter and a bar ruler. Only the visible part is drawn, so long songs stay fast.
Put it in a `ScrollViewer` with `CanContentScroll="True"`.

```xml
<ScrollViewer CanContentScroll="True" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
    <daw:PianoRoll x:Name="roll" LengthInBeats="64" BeatsPerBar="4" Snap="0.25" FollowPlayhead="True"/>
</ScrollViewer>
```

```csharp
var notes = new ObservableCollection<MidiNote>
{
    new(60, start: 0, length: 1, velocity: 100),
    new(64, 1, 1),
    new(67, 2, 2),
};
roll.Notes = notes;                              // edits go straight into the collection

roll.AuditionNoteOn  += (s, e) => midiOut.NoteOn(0, e.Note, e.Velocity);
roll.AuditionNoteOff += (s, e) => midiOut.NoteOff(0, e.Note);
roll.NotesEdited     += (s, e) => undo.Snapshot(notes);

CompositionTarget.Rendering += (s, e) => roll.PlayheadPosition = engine.PositionInBeats;
```

Mouse: click to add (drag to place), drag to move, drag the right edge to resize, right-click to delete,
Ctrl+click to add to the selection, Ctrl+drag on empty grid to select an area, Alt to ignore snap. Click the
keyboard to hear a note, click the ruler to move the playhead. Wheel: scroll, Shift for horizontal, Ctrl to zoom
time, Alt to zoom pitch. Keys: Delete, Ctrl+A, Up/Down transpose (Shift for an octave), Left/Right move by the
snap, Ctrl+D duplicates.

Positions and lengths are in beats. `Snap` is in beats too (`0.25` = sixteenths, `0` = off). Also:
`PixelsPerBeat`, `KeyHeight`, `KeyboardWidth`, `DefaultVelocity`, `IsReadOnly`, `MiddleCOctave`.

## ControllerLane

Velocity, controller or pitch-bend values under a piano roll. It follows the roll's scroll, zoom, snap and
playhead.

```xml
<daw:ControllerLane Height="100" Roll="{Binding ElementName=roll}" Mode="Velocity"/>
```

```csharp
lane.Mode = LaneMode.Controller;
lane.ControllerNumber = 1;                                  // modulation
lane.Points = new ObservableCollection<ControllerPoint>
{
    new(0, 0), new(4, 127),
};
lane.LaneEdited += (s, e) => SaveLane();
```

Mouse: drag to draw, right-drag to erase, Shift for a straight line. In `Velocity` mode it edits the roll's notes.

## StepSequencer

A drum grid.

```xml
<daw:StepSequencer x:Name="steps" Height="140" StepsPerGroup="4"/>
```

```csharp
var pattern = new StepPattern(rows: 4, steps: 16);
pattern.SetVelocity(0, 0, 120);      // kick on step 1
pattern.SetVelocity(1, 4, 110);      // snare on step 5
steps.Pattern = pattern;
steps.RowLabels = new[] { "Kick", "Snare", "Hat", "Perc" };

steps.CurrentStep = engine.Step;     // shows the playhead on that step
```

Mouse: click to toggle, drag up and down on a step for velocity. Keyboard: arrows move the cursor, Space or Enter
toggles, + and - change the velocity.

---

## NineFrame

A drop-in replacement for `Border` that draws with the nine-patch renderer and adds a `Bevel` (`Flat`, `Raised`,
`Sunken`, `Glow`). Use it for your own panels and displays so they match the controls.

```xml
<daw:NineFrame Bevel="Sunken" CornerRadius="3" BorderThickness="1" Padding="6,2"
               Background="{DynamicResource {x:Static daw:DawSkinKeys.LcdBackground}}"
               BorderBrush="{DynamicResource {x:Static daw:DawSkinKeys.LcdBorder}}">
    <TextBlock Text="120.0 BPM" Foreground="{DynamicResource {x:Static daw:DawSkinKeys.LcdText}}"
               FontFamily="{DynamicResource {x:Static daw:DawSkinKeys.LcdFontFamily}}"/>
</daw:NineFrame>
```

It uses the renderer for a uniform border and corner radius with solid or two-colour gradient brushes, and falls
back to normal `Border` drawing for anything else.
