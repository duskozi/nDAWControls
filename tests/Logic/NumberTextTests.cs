using nDAWControls.Primitives;
using Xunit;

namespace nDAWControls.Tests.Logic;

public class NumberTextTests
{
    [Theory]
    [InlineData("120", 120)]
    [InlineData("120 bpm", 120)]
    [InlineData("-6 dB", -6)]
    [InlineData("+3", 3)]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    [InlineData(".5", 0.5)]
    [InlineData("-.5", -0.5)]
    [InlineData("-,25", -0.25)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234,567.25", 1234567.25)]
    [InlineData("12.", 12)]
    [InlineData("gain 0.75x", 0.75)]
    public void Parses(string text, double expected)
    {
        Assert.True(NumberText.TryParse(text, out double v));
        Assert.Equal(expected, v, 9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    [InlineData("1.234.567,5,1")]
    public void Rejects(string? text) => Assert.False(NumberText.TryParse(text, out _));
}
