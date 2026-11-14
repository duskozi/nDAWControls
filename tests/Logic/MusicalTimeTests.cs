using System;
using nDAWControls.Primitives;
using Xunit;

namespace nDAWControls.Tests.Logic;

public class MusicalTimeTests
{
    [Theory]
    [InlineData(0, 4, 960, "  1.1.000")]
    [InlineData(4, 4, 960, "  2.1.000")]
    [InlineData(5.25, 4, 960, "  2.2.240")]
    [InlineData(2.5, 3, 480, "  1.3.240")]
    public void FormatsBars(double beats, int perBar, int tpb, string expected) =>
        Assert.Equal(expected, MusicalTime.FormatMusical(beats, perBar, tpb));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-3)]
    public void BadPositionsShowTheStart(double beats) =>
        Assert.Equal("  1.1.000", MusicalTime.FormatMusical(beats, 4, 960));

    [Fact]
    public void RejectsBadTimeSignature()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicalTime.FormatMusical(1, 0, 960));
        Assert.Throws<ArgumentOutOfRangeException>(() => MusicalTime.FormatMusical(1, 4, 0));
    }

    [Fact]
    public void FormatsClock()
    {
        Assert.Equal("01:23.456", MusicalTime.FormatClock(TimeSpan.FromMilliseconds(83_456)));
        Assert.Equal("1:00:00.000", MusicalTime.FormatClock(TimeSpan.FromHours(1)));
    }
}
