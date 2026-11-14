# nDAWControls

WPF controls for DAW and MIDI applications: knobs, faders, level meters, channel strips, a transport bar, a piano
keyboard, a piano roll with controller lane, a step sequencer, an XY pad, an envelope editor, pitch and modulation
wheels, piano pedals and more. Everything follows one of five built-in skins, and you can switch skins while the
app runs.

The controls come from [nMIDI Editor](https://github.com/duskozi/nMidiEditor), where they draw the mixer.

## Features

- **20 controls** for parameters, metering, transport, mixing, piano and note editing.
- **Five skins**: Graphite, Studio, Light classic, Retro hardware and High contrast. Switch with one line of code,
  for the whole app or for one panel. Make your own by copying a skin file.
- **Built for real-time audio**: meters and MIDI activity lights take values from any thread without locks, and
  draw once per frame.
- **Mouse, wheel and keyboard**: every interactive control can take focus. Knobs, faders and value boxes work with
  the arrow keys, double-click returns to the default value, Shift drags in fine steps.
- **Fast drawing**: frames are drawn by a native nine-patch renderer and cached, so a hundred equal buttons share
  one bitmap. Without the native renderer the controls draw the same frames as vectors.
- **Any CPU**: the native renderer is used in 64-bit processes; x86 and ARM64 apps use the vector drawing.

## Install

```
dotnet add package nDAWControls
```

You need .NET 8 or .NET 10 and Windows. The package brings [NinePatchWpf](https://www.nuget.org/packages/NinePatchWpf)
and its native renderer (`NinePatchLib.dll`), so there is nothing else to set up. The renderer needs the Microsoft
Visual C++ Redistributable (x64), which most PCs already have.

## Quick start

**1. Add the namespace** to a window or user control:

```xml
<Window xmlns:daw="urn:ndaw-controls" ...>
```

**2. Use the controls:**

```xml
<StackPanel Orientation="Horizontal">
    <daw:Knob Label="Cutoff" Taper="Log" Minimum="20" Maximum="20000" Value="{Binding Cutoff}"
              ValueFormatter="{x:Static daw:ValueFormatters.Frequency}"/>
    <daw:Fader Height="200" Taper="Decibel" Minimum="-60" Maximum="6" Value="{Binding VolumeDb}"/>
    <daw:LevelMeter x:Name="meter" Width="8"/>
    <daw:LedToggleButton Role="Mute" Content="M" IsChecked="{Binding IsMuted}"/>
</StackPanel>
```

**3. Feed the meter** from your audio code, on any thread:

```csharp
meter.PushLevels(peak, rms);   // linear 0..1, where 1 is 0 dBFS
```

**4. Pick a skin** (optional; Graphite is the default):

```csharp
using nDAWControls.Skinning;

SkinManager.Apply(DawSkin.Studio);
```

To make your own windows follow the skin, use the skin colours for them too:

```xml
<Window Background="{DynamicResource {x:Static daw:DawSkinKeys.WindowBackground}}"
        TextElement.Foreground="{DynamicResource {x:Static daw:DawSkinKeys.Text}}">
```

## Controls

| Area | Controls |
| --- | --- |
| Parameters | [`Knob`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#knob), [`Fader`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#fader), [`ValueBox`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#valuebox), [`XYPad`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#xypad), [`EnvelopeEditor`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#envelopeeditor) |
| Metering | [`LevelMeter`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#levelmeter), [`ValueScale`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#valuescale), [`MidiActivityMonitor`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#midiactivitymonitor) |
| Buttons and transport | [`LedToggleButton`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#ledtogglebutton), [`TransportButton`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#transportbutton), [`TransportBar`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#transportbar), [`TimeDisplay`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#timedisplay) |
| Mixer and layout | [`ChannelStrip`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#channelstrip), [`TrackHeader`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#trackheader), [`DawPanel`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#dawpanel) |
| Piano | [`PianoKeyboard`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#pianokeyboard), [`PerformanceWheel`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#performancewheel), [`PianoPedals`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#pianopedals) |
| Note editing | [`PianoRoll`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#pianoroll), [`ControllerLane`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#controllerlane), [`StepSequencer`](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md#stepsequencer) |

Every control with XAML and C# examples: **[docs/controls.md](https://github.com/duskozi/nDAWControls/blob/main/docs/controls.md)**.
Skins, skin colours and making your own skin: **[docs/skins.md](https://github.com/duskozi/nDAWControls/blob/main/docs/skins.md)**.

## Links

- Source, samples and issues: [github.com/duskozi/nDAWControls](https://github.com/duskozi/nDAWControls)
- A ready-to-run gallery of every control is attached to each [release](https://github.com/duskozi/nDAWControls/releases).
- [Changelog](https://github.com/duskozi/nDAWControls/blob/main/CHANGELOG.md)

MIT license.
