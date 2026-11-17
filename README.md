# nDAWControls

[![Build](https://github.com/duskozi/nDAWControls/actions/workflows/build.yml/badge.svg)](https://github.com/duskozi/nDAWControls/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/nDAWControls.svg)](https://www.nuget.org/packages/nDAWControls)

WPF controls for DAW and MIDI applications: knobs, faders, level meters, channel strips, a transport bar, a piano
keyboard, a piano roll with controller lane, a step sequencer, an XY pad, an envelope editor, pitch and modulation
wheels, piano pedals and more. Everything follows one of five built-in skins, and you can switch skins while the
app runs.

The controls come from [nMIDI Editor](https://github.com/duskozi/nMidiEditor), where they draw the mixer.

## Features

- **20 controls** for parameters, metering, transport, mixing, piano and note editing.
- **Eight skins**: Graphite, Studio, Light classic, Retro hardware, High contrast, and the hardware-style Alloy, Alloy Night and Alloy Ember. Switch with one line of code,
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

## Samples

Two sample apps are in [`samples/`](https://github.com/duskozi/nDAWControls/tree/main/samples). A ready-to-run build of the gallery is attached to every
[release](https://github.com/duskozi/nDAWControls/releases).

- **Gallery** (`samples/Gallery`)
  - **Controls** lists every control. It shows the control live next to its XAML and the C# it needs. You can edit
    the XAML and press Ctrl+Enter to see the change, and copy either part into your app.
  - **Studio demo** shows the controls working together: a mixer with live meters, a synth panel, a piano roll with
    a controller lane, a step sequencer and a keyboard with wheels and pedals.
  - A skin picker at the top switches all five skins live.
- **MidiPlayer** (`samples/MidiPlayer`): a MIDI file player built from the controls, with a transport bar,
  piano roll and controller lane, MIDI activity lights, and a keyboard with wheels and pedals that plays along.
  Parts can be hidden, and a hidden part costs no CPU.

```
dotnet run --project samples/Gallery
```

## Building

You need the .NET 10 SDK on Windows (or Visual Studio 2026 with the .NET desktop workload).

```
git clone https://github.com/duskozi/nDAWControls.git
cd nDAWControls
dotnet build nDAWControls.sln -c Release
dotnet test tests/nDAWControls.Tests.csproj
```

| Folder | What is in it |
| --- | --- |
| `src/nDAWControls/Controls` | The controls |
| `src/nDAWControls/Skins` | The five skins |
| `src/nDAWControls/Themes` | Default styles and templates of the controls |
| `src/nDAWControls/Skinning` | Skin keys and `SkinManager` |
| `src/nDAWControls/Primitives` | Base classes, tapers, value formatters, `NineFrame` |
| `src/nDAWControls/Rendering` | Nine-patch frames, note bitmaps, brush and text caches |
| `src/nDAWControls/Models` | `MidiNote`, `ControllerPoint`, `StepPattern` and event arguments |
| `samples` | The gallery and the MIDI player |
| `tests` | Tests |



## License

MIT, see [LICENSE](https://github.com/duskozi/nDAWControls/blob/main/LICENSE).
