using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using nDAWControls.Skinning;

namespace nDAWControls.Gallery;

/// <summary>The control catalog and the skin picker.</summary>
public partial class MainWindow
{
    private const string XamlNamespaces =
        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:daw='urn:ndaw-controls'";

    private Snippet? _snippet;

    private void InitCatalog()
    {
        SkinPicker.ItemsSource = Enum.GetValues<DawSkin>();
        SkinPicker.SelectedItem = SkinManager.CurrentSkin ?? DawSkin.Graphite;

        var view = new ListCollectionView(SnippetCatalog.All.ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Snippet.Group)));
        SnippetList.ItemsSource = view;
        SnippetList.SelectedIndex = 0;
    }

    private void OnSkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkinPicker.SelectedItem is DawSkin skin) SkinManager.Apply(skin);
    }

    private void OnViewClick(object sender, RoutedEventArgs e)
    {
        bool catalog = sender == ShowCatalog;
        ShowCatalog.IsChecked = catalog;
        ShowDemo.IsChecked = !catalog;
        CatalogView.Visibility = catalog ? Visibility.Visible : Visibility.Collapsed;
        DemoView.Visibility = catalog ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSnippetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SnippetList.SelectedItem is not Snippet snippet) return;
        _snippet = snippet;
        SnippetTitle.Text = snippet.Name;
        SnippetDescription.Text = snippet.Description;
        XamlCode.Text = snippet.Xaml;
        CSharpCode.Text = snippet.CSharp ?? "// Nothing needed: the XAML is all there is.";
        ShowXaml(snippet.Xaml);
    }

    /// <summary>Parses the XAML the same way your app would, and shows it in the preview.</summary>
    private void ShowXaml(string xaml)
    {
        Demo.StopAll();
        try
        {
            var root = (FrameworkElement)XamlReader.Parse($"<Grid {XamlNamespaces}>{xaml}</Grid>");
            Preview.Content = root;
            XamlError.Text = "";
            _snippet?.Setup?.Invoke(root);
        }
        catch (Exception ex) when (ex is XamlParseException or InvalidOperationException or ArgumentException)
        {
            XamlError.Text = ex.Message.Split('\n')[0];
        }
    }

    private void OnApplyXaml(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton b) b.IsChecked = false;
        ShowXaml(XamlCode.Text);
    }

    private void OnXamlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowXaml(XamlCode.Text);
        }
    }

    private void OnCopyXaml(object sender, RoutedEventArgs e) => Copy(sender, XamlCode.Text);

    private void OnCopyCSharp(object sender, RoutedEventArgs e) => Copy(sender, CSharpCode.Text);

    private static void Copy(object sender, string text)
    {
        if (sender is ToggleButton b) b.IsChecked = false;
        try { Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy: try again */ }
    }
}
