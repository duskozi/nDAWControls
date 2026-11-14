using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using nDAWControls.Skinning;
using Xunit;

namespace nDAWControls.Tests.Wpf;

public class SkinTests
{
    private static string SkinFile(DawSkin skin) => Path.Combine(AppContext.BaseDirectory, "Skins", skin + ".xaml");

    [Fact]
    public void EveryBuiltInSkinHasAFile()
    {
        foreach (var skin in Enum.GetValues<DawSkin>())
            Assert.True(File.Exists(SkinFile(skin)), $"Skins/{skin}.xaml is missing");
    }

    [Fact]
    public void EverySkinDefinesEveryKeyOnce()
    {
        var keys = typeof(DawSkinKeys).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(System.Windows.ComponentResourceKey))
            .Select(p => p.Name)
            .ToList();
        Assert.True(keys.Count > 80);

        foreach (var skin in Enum.GetValues<DawSkin>())
        {
            string xaml = File.ReadAllText(SkinFile(skin));
            foreach (string key in keys)
            {
                int count = Regex.Matches(xaml, $@"DawSkinKeys\.{key}\}}").Count;
                Assert.True(count == 1, $"{skin}: {key} is defined {count} times");
            }
        }
    }

    [Fact]
    public void SkinUriPointsIntoTheLibrary()
    {
        Assert.Equal("pack://application:,,,/nDAWControls;component/Skins/Studio.xaml",
                     SkinManager.GetSkinUri(DawSkin.Studio).ToString());
    }
}
