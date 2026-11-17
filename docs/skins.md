# Skins

A skin is a `ResourceDictionary` with a value for every colour key in `DawSkinKeys`. All control templates use
`DynamicResource`, so a new skin shows at once, even while the app runs.

## Built-in skins

| Skin | Look |
| --- | --- |
| `Graphite` | Dark graphite with an orange accent. The default. |
| `Studio` | Dark blue-grey with a blue accent. |
| `LightClassic` | Light grey with a blue accent and light knobs. |
| `RetroHardware` | Warm dark brown with an orange accent and amber displays. |
| `HighContrast` | Black and white with a yellow accent and strong outlines. |
| `Alloy` | Lit brushed silver with dark rubber knobs, blue glass displays and orange for anything switched on. |
| `AlloyNight` | Alloy on dark anodised metal: the same light and depth, blue glass displays, orange accents. |
| `AlloyEmber` | Alloy in champagne metal with amber glass displays and blue for selection. |

## Switching skins

For the whole app:

```csharp
using nDAWControls.Skinning;

SkinManager.Apply(DawSkin.Studio);
SkinManager.SkinChanged += (s, e) => Debug.WriteLine(SkinManager.CurrentSkin);
```

For one part of a window only, for example a dark mixer in a light app:

```xml
<Border daw:SkinManager.Skin="Graphite">
    <StackPanel Orientation="Horizontal">
        <daw:ChannelStrip TrackName="Drums"/>
        <daw:ChannelStrip TrackName="Bass"/>
    </StackPanel>
</Border>
```

Or merge a skin in XAML yourself:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/nDAWControls;component/Skins/RetroHardware.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

## Using skin colours in your own UI

```xml
<Window Background="{DynamicResource {x:Static daw:DawSkinKeys.WindowBackground}}"
        TextElement.Foreground="{DynamicResource {x:Static daw:DawSkinKeys.Text}}"
        TextElement.FontFamily="{DynamicResource {x:Static daw:DawSkinKeys.FontFamily}}">

    <Border Background="{DynamicResource {x:Static daw:DawSkinKeys.PanelBackground}}"
            BorderBrush="{DynamicResource {x:Static daw:DawSkinKeys.PanelBorder}}" BorderThickness="1">
        <TextBlock Text="SYNTH" Foreground="{DynamicResource {x:Static daw:DawSkinKeys.TextDim}}"/>
    </Border>
</Window>
```

## Changing a few colours

Override single keys in your own resources. They win over the skin:

```xml
<Application.Resources>
    <SolidColorBrush x:Key="{x:Static daw:DawSkinKeys.Accent}" Color="#FF00C2A8"/>
    <SolidColorBrush x:Key="{x:Static daw:DawSkinKeys.LedSolo}" Color="#FF4CC2FF"/>
</Application.Resources>
```

## Making your own skin

1. Copy one of the files in [`src/nDAWControls/Skins`](../src/nDAWControls/Skins) into your app, for example as
   `Skins/Ocean.xaml` (build action `Page`).
2. Change the colours. Change `xmlns:skin` to
   `clr-namespace:nDAWControls.Skinning;assembly=nDAWControls`, because the file now lives in your assembly.
3. Apply it:

```csharp
SkinManager.Apply(new Uri("pack://application:,,,/MyApp;component/Skins/Ocean.xaml"));
```

Frames, LEDs and notes drawn by the nine-patch renderer are generated from these colours, so they follow
automatically.

## Keys

| Group | Keys |
| --- | --- |
| Window and text | `WindowBackground`, `PanelBackground`, `PanelRaised`, `PanelBorder`, `Text`, `TextDim`, `Accent`, `AccentForeground`, `Focus` |
| Knobs | `KnobBody`, `KnobBodyBorder`, `KnobTrack`, `KnobIndicator`, `KnobArcThickness` |
| Faders | `FaderGroove`, `FaderFill`, `FaderThumb`, `FaderThumbBorder`, `FaderThumbLine` |
| Meters and scales | `MeterBackground`, `MeterSafe`, `MeterWarning`, `MeterDanger`, `MeterPeakHold`, `MeterClipOn`, `MeterClipOff`, `ScaleTick`, `ScaleText` |
| Buttons and LEDs | `ButtonBackground`, `ButtonHover`, `ButtonPressed`, `ButtonBorder`, `LedMute`, `LedSolo`, `LedArm`, `LedMonitor`, `LedLitForeground`, `TransportPlay`, `TransportRecord` |
| Displays | `LcdBackground`, `LcdBorder`, `LcdText`, `LcdTextDim`, `LcdFontFamily` |
| Keyboard | `KeyWhite`, `KeyBlack`, `KeyBorder`, `KeyPressed`, `KeyHighlight` |
| Piano roll | `GridBackground`, `GridBlackRow`, `GridBarLine`, `GridBeatLine`, `GridSubLine`, `NoteFill`, `NoteSelectedFill`, `NoteBorder`, `NoteText`, `Playhead`, `RulerBackground`, `RulerText`, `Selection` |
| Step sequencer | `StepOff`, `StepOn`, `StepGroupBackground`, `StepPlayhead` |
| XY pad and envelope | `PadBackground`, `PadGrid`, `PadPuck`, `PadCrosshair`, `EnvelopeStroke`, `EnvelopeFill` |
| Wheels and pedals | `WheelSlot`, `WheelBody`, `WheelRib`, `WheelMarker`, `PedalBody`, `PedalLit` |
| MIDI activity, tracks and lanes | `ActivityNote`, `ActivityControl`, `ActivityOff`, `ActivityRowAlt`, `TrackSelected`, `LaneValue`, `LaneFill` |
| Shape and fonts | `CornerRadius`, `FontFamily` |
