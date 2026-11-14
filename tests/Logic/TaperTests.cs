using System;
using System.Globalization;
using System.Threading;
using nDAWControls.Primitives;
using Xunit;

namespace nDAWControls.Tests.Logic;

public class TaperTests
{
    public static TheoryData<string, double, double> Tapers => new()
    {
        { "linear", 0, 1 },
        { "linear", -50, 50 },
        { "log", 20, 20000 },
        { "decibel", -70, 6 },
        { "decibel", -60, 0 },
    };

    private static ValueTaper Taper(string name) =>
        (ValueTaper)new ValueTaperConverter().ConvertFrom(null, CultureInfo.InvariantCulture, name)!;

    [Theory]
    [MemberData(nameof(Tapers))]
    public void RoundTrips(string name, double min, double max)
    {
        var taper = Taper(name);
        for (int i = 1; i < 20; i++)
        {
            double n = i / 20.0;
            double v = taper.FromNormalized(n, min, max);
            Assert.InRange(v, min, max);
            Assert.Equal(n, taper.ToNormalized(v, min, max), 6);
        }
    }

    [Theory]
    [MemberData(nameof(Tapers))]
    public void EndsAndOutOfRange(string name, double min, double max)
    {
        var taper = Taper(name);
        Assert.Equal(0, taper.ToNormalized(min, min, max), 9);
        Assert.Equal(1, taper.ToNormalized(max, min, max), 9);
        Assert.Equal(0, taper.ToNormalized(min - 100, min, max), 9);
        Assert.Equal(1, taper.ToNormalized(max + 100, min, max), 9);
        Assert.Equal(min, taper.FromNormalized(double.NaN, min, max), 9);
    }

    [Fact]
    public void DecibelPutsZeroHighOnTheFader() =>
        Assert.InRange(ValueTaper.Decibel.ToNormalized(0, -70, 6), 0.7, 0.9);

    [Fact]
    public void UnknownTaperNameThrows() =>
        Assert.Throws<FormatException>(() => Taper("cubic"));

    [Fact]
    public void DbMathRoundTrips()
    {
        Assert.Equal(double.NegativeInfinity, DbMath.GainToDb(0));
        Assert.Equal(0, DbMath.DbToGain(double.NegativeInfinity));
        Assert.Equal(-6.0206, DbMath.GainToDb(0.5), 3);
        Assert.Equal(0.5, DbMath.DbToGain(DbMath.GainToDb(0.5)), 9);
    }

    [Fact]
    public void Formatters()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("-6.0 dB", ValueFormatters.Decibel(-6));
            Assert.Equal("+3.0 dB", ValueFormatters.Decibel(3));
            Assert.Equal("-∞ dB", ValueFormatters.Decibel(double.NegativeInfinity));
            Assert.Equal("C", ValueFormatters.Pan(0));
            Assert.Equal("40L", ValueFormatters.Pan(-0.4));
            Assert.Equal("100R", ValueFormatters.Pan(1));
            Assert.Equal("1.20 kHz", ValueFormatters.Frequency(1200));
            Assert.Equal("440 Hz", ValueFormatters.Frequency(440));
            Assert.Equal("50%", ValueFormatters.Percent(0.5));
            Assert.Equal("250 ms", ValueFormatters.Time(0.25));
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }
}
