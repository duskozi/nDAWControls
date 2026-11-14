using System.Collections.Generic;
using nDAWControls.Models;
using Xunit;

namespace nDAWControls.Tests.Wpf;

public class ModelTests
{
    [Fact]
    public void StepPatternRaisesOnlyRealChanges()
    {
        var p = new StepPattern(4, 16);
        var events = new List<StepChangedEventArgs>();
        p.StepChanged += (_, e) => events.Add(e);

        p.SetVelocity(1, 3, 100);
        p.SetVelocity(1, 3, 100);   // same value: no event
        p.SetVelocity(2, 5, 500);   // clamped to 127

        Assert.Equal(2, events.Count);
        Assert.Equal((1, 3, 100), (events[0].Row, events[0].Step, events[0].Velocity));
        Assert.Equal(127, p.GetVelocity(2, 5));
        Assert.True(p.IsOn(1, 3));
        Assert.False(p.IsOn(0, 0));
    }

    [Fact]
    public void MidiNoteClampsItsValues()
    {
        var n = new MidiNote(200, -1, 0, 0);
        Assert.Equal(127, n.Pitch);
        Assert.Equal(0, n.Start);
        Assert.Equal(MidiNote.MinLength, n.Length);
        Assert.Equal(1, n.Velocity);
    }

    [Theory]
    [InlineData(60, 4, "C4")]
    [InlineData(60, 3, "C3")]
    [InlineData(61, 4, "C#4")]
    [InlineData(21, 4, "A0")]
    public void NoteNames(int note, int middleC, string name) => Assert.Equal(name, MidiNoteName.Get(note, middleC));

    [Fact]
    public void BlackKeys()
    {
        Assert.True(MidiNoteName.IsBlackKey(61));
        Assert.False(MidiNoteName.IsBlackKey(60));
    }

    [Fact]
    public void PedalControllers()
    {
        Assert.Equal(64, PianoPedals.ControllerOf(PianoPedal.Sustain));
        Assert.Equal(66, PianoPedals.ControllerOf(PianoPedal.Sostenuto));
        Assert.Equal(67, PianoPedals.ControllerOf(PianoPedal.Soft));
        Assert.False(new PedalEventArgs(PianoPedals.PedalChangedEvent, new object(), PianoPedal.Sustain, 127).IsUserChange);
    }

    [Fact]
    public void ValueBoxUsesTheSameParser()
    {
        Assert.True(ValueBox.TryParse(".5", out double v));
        Assert.Equal(0.5, v);
    }
}
