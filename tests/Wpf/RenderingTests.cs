using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using nDAWControls.Primitives;
using Xunit;

namespace nDAWControls.Tests.Wpf;

public class RenderingTests
{
    private static readonly Color Grey = Color.FromRgb(0x40, 0x40, 0x40);

    [Fact]
    public void MarginCoversCornerOutlineAndBevel()
    {
        Assert.Equal(1, NineSkins.Margin(FrameSpec.Solid(Grey, Colors.Black, radius: 0, strokeWidth: 0)));
        Assert.Equal(6, NineSkins.Margin(FrameSpec.Solid(Grey, Colors.Black, radius: 4, strokeWidth: 1)));
        Assert.Equal(4, NineSkins.Margin(new FrameSpec(Grey, Grey, Colors.Black, 0, 1, FrameStyle.Raised)));   // bevel rows count
        Assert.Equal(16, NineSkins.Margin(FrameSpec.Solid(Grey, Colors.Black, radius: 40, strokeWidth: 9)));   // clamped to 12 and 3
    }

    [Fact]
    public void TryFitShrinksRadiusThenBevelThenOutline()
    {
        var spec = new FrameSpec(Grey, Grey, Colors.Black, 6, 2, FrameStyle.Raised);
        Assert.True(NineSkins.TryFit(ref spec, 100));
        Assert.Equal(6, spec.Radius);

        spec = new FrameSpec(Grey, Grey, Colors.Black, 6, 2, FrameStyle.Raised);
        Assert.True(NineSkins.TryFit(ref spec, 10));
        Assert.Equal(FrameStyle.Raised, spec.Style);
        Assert.True(2 * NineSkins.Margin(spec) <= 10);

        spec = new FrameSpec(Grey, Grey, Colors.Black, 6, 2, FrameStyle.Raised);
        Assert.True(NineSkins.TryFit(ref spec, 2));
        Assert.Equal((0, FrameStyle.Flat, 0), (spec.Radius, spec.Style, spec.StrokeWidth));

        spec = new FrameSpec(Grey, Grey, Colors.Black, 6, 2, FrameStyle.Raised);
        Assert.False(NineSkins.TryFit(ref spec, 1));
    }

    [Fact]
    public void NativeRendererShipsWithTheLibrary()
    {
        // The library project copies NinePatchLib.dll to every project that references it.
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "NinePatchLib.dll")));
        Assert.NotNull(NineSkins.TryGet(FrameSpec.Solid(Grey, Colors.Black, 3)));
        Assert.True(NineSkins.IsNativeAvailable);
    }

    [Fact]
    public void EqualSpecsShareOneImage()
    {
        var a = NineSkins.TryGet(FrameSpec.Solid(Grey, Colors.Black, 3));
        var b = NineSkins.TryGet(FrameSpec.Solid(Grey, Colors.Black, 3));
        Assert.Same(a, b);
    }

    [Fact]
    public void BrushCachesReturnTheSameFrozenInstance()
    {
        var s = Kit.Solid(Colors.Orange);
        Assert.Same(s, Kit.Solid(Colors.Orange));
        Assert.True(s.IsFrozen);

        var m1 = Kit.Linear(true, (Colors.Red, 0), (Colors.Green, 0.5), (Colors.Blue, 1));
        var m2 = Kit.Linear(true, (Colors.Red, 0), (Colors.Green, 0.5), (Colors.Blue, 1));
        Assert.Same(m1, m2);
        Assert.NotSame(m1, Kit.Linear(false, (Colors.Red, 0), (Colors.Green, 0.5), (Colors.Blue, 1)));

        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), Palette.C("#123456"));
    }

    [Fact]
    public void BrushColorsReadsSkinBrushes()
    {
        Assert.True(BrushColors.TryGet(new SolidColorBrush(Colors.Red), out var top, out var bottom));
        Assert.Equal((Colors.Red, Colors.Red), (top, bottom));

        var vertical = new LinearGradientBrush(Colors.White, Colors.Black, new Point(0, 0), new Point(0, 1));
        Assert.True(BrushColors.TryGet(vertical, out top, out bottom));
        Assert.Equal((Colors.White, Colors.Black), (top, bottom));

        Assert.True(BrushColors.TryGet(null, out top, out _));
        Assert.Equal(Colors.Transparent, top);

        Assert.False(BrushColors.TryGet(new DrawingBrush(), out _, out _));
    }
}
