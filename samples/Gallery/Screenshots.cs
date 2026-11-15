using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using nDAWControls.Skinning;

namespace nDAWControls.Gallery;

/// <summary>Renders every catalog entry and both gallery views to PNG files, once per skin.</summary>
internal static class Screenshots
{
    private const string XamlNamespaces =
        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:daw='urn:ndaw-controls'";

    private const double Scale = 2;   // images for high-DPI screens

    public static async Task RunAsync(string outDir)
    {
        int exitCode = 0;
        Directory.CreateDirectory(outDir);
        using var log = new StreamWriter(Path.Combine(outDir, "log.txt")) { AutoFlush = true };
        Console.SetOut(log);
        Console.SetError(log);
        try
        {
            foreach (var skin in Enum.GetValues<DawSkin>())
            {
                SkinManager.Apply(skin);
                string dir = Path.Combine(outDir, Slug(skin.ToString()));
                Directory.CreateDirectory(dir);

                foreach (var snippet in SnippetCatalog.All)
                {
                    try { await CaptureSnippetAsync(snippet, Path.Combine(dir, Slug(snippet.Name) + ".png")); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"{skin} / {snippet.Name}: {ex.Message}");
                        exitCode = 1;
                    }
                }

                await CaptureGalleryAsync(dir);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            exitCode = 1;
        }
        Application.Current.Shutdown(exitCode);
    }

    private static async Task CaptureSnippetAsync(Snippet snippet, string path)
    {
        var root = (FrameworkElement)XamlReader.Parse($"<Grid {XamlNamespaces}>{snippet.Xaml}</Grid>");
        var host = new Border { Padding = new Thickness(24), Child = root, MaxWidth = 1000, SnapsToDevicePixels = true };
        host.SetResourceReference(Border.BackgroundProperty, DawSkinKeys.WindowBackground);
        host.SetResourceReference(TextElement.ForegroundProperty, DawSkinKeys.Text);

        var window = NewWindow(host);
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Show();
        snippet.Setup?.Invoke(root);
        await Task.Delay(1500);   // meters and activity lights run for a while, as in the gallery
        Save(host, path);
        Demo.StopAll();
        window.Close();
    }

    private static async Task CaptureGalleryAsync(string dir)
    {
        var main = new MainWindow { Width = 1480, Height = 900, Left = 0, Top = 0, ShowActivated = false };
        main.Show();
        await Task.Delay(1500);
        Save((FrameworkElement)main.Content, Path.Combine(dir, "gallery-controls.png"));

        main.ShowStudioDemo(play: true);
        await Task.Delay(4000);   // let the demo song play: meters, playhead and keys move
        Save((FrameworkElement)main.Content, Path.Combine(dir, "gallery-studio-demo.png"));
        main.Close();
    }

    private static Window NewWindow(UIElement content) => new()
    {
        Content = content,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        ShowActivated = false,
        Left = 0,
        Top = 0,
    };

    private static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        int w = (int)Math.Ceiling(element.ActualWidth * Scale), h = (int)Math.Ceiling(element.ActualHeight * Scale);
        var bitmap = new RenderTargetBitmap(w, h, 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
        Console.WriteLine($"{path}  {w}x{h}");
    }

    /// <summary>"Fader with scale and meter" → "fader-with-scale-and-meter".</summary>
    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
