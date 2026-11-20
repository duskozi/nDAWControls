using System.Windows;
using nDAWControls.Skinning;

namespace nDAWControls.Gallery;

/// <summary>
/// Gives the standard WPF controls in the gallery (buttons, text boxes, lists, menus, scroll bars) the
/// look of the current skin, using the matching NWpfThemes theme. Active only when the NWpfThemes
/// repository is cloned next to this one (see the project file); otherwise the controls keep the
/// Windows look.
/// </summary>
internal static class StandardControlThemes
{
    public static void Initialize()
    {
#if NWPFTHEMES
        Application.Current.Resources.MergedDictionaries.Insert(0, new NWpfThemes.ThemeResources { Theme = ThemeFor(SkinManager.CurrentSkin ?? DawSkin.Graphite) });
        SkinManager.SkinChanged += (_, _) =>
        {
            // A custom skin (CurrentSkin is null) keeps the last theme.
            if (SkinManager.CurrentSkin is { } skin) NWpfThemes.ThemeManager.ApplyTheme(ThemeFor(skin));
        };
#endif
    }

    /// <summary>The NWpfThemes theme with the same palette as <paramref name="skin"/>.</summary>
    private static string ThemeFor(DawSkin skin) => skin switch
    {
        // NWpfThemes' "High Contrast" follows the Windows colours; this is the fixed nDAWControls palette.
        DawSkin.HighContrast => "High Contrast Black",
        _ => skin.ToString(),   // theme names ignore spaces: AlloyNight finds "Alloy Night"
    };
}
