# Changelog

## 1.0.0 (2026-11-14)

First release as nDAWControls, taken out of nMIDI Editor (where it was nMIDI.Controls 2.0) so other apps can use it.

- 20 controls: `Knob`, `Fader`, `ValueBox`, `XYPad`, `EnvelopeEditor`, `LevelMeter`, `ValueScale`,
  `MidiActivityMonitor`, `LedToggleButton`, `TransportButton`, `TransportBar`, `TimeDisplay`, `ChannelStrip`,
  `TrackHeader`, `DawPanel`, `PianoKeyboard`, `PerformanceWheel`, `PianoPedals`, `PianoRoll`, `ControllerLane`,
  `StepSequencer`, plus `NineFrame` for your own panels.
- Five skins: Graphite, and new Studio, Light classic, Retro hardware and High contrast, matching the themes of
  nMIDI Editor.
- Any CPU: the native nine-patch renderer is used in 64-bit processes, other processes draw the same frames as
  vectors.
- Targets .NET 8 and .NET 10.
- Uses the NinePatchWpf 2.1.0 package for the nine-patch frames.
- MIT license.
- Gallery sample with a control catalog: every control live, with its XAML (editable) and C#.
- Documentation of every control with examples, and a guide to skins.

Changes for apps that used nMIDI.Controls:

- Namespace `nDAWControls` and XML namespace `urn:ndaw-controls` (prefix `daw:` in the docs).
- `LedSolo` is yellow and `LedMute` orange in the new skins, as in nMIDI Editor. Graphite keeps its colours.
