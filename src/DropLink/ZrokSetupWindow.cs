using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DropLink.Services;

namespace DropLink;

/// <summary>
/// One-time zrok account setup. The token is held only in this window and passed to zrok2;
/// DropLink does not write it to its own settings or source files.
/// </summary>
internal sealed class ZrokSetupWindow : Window
{
    private readonly PasswordBox _tokenBox;
    private string _accountToken = string.Empty;
    public string AccountToken => _accountToken;

    public void ClearToken()
    {
        _tokenBox.Clear();
        _accountToken = string.Empty;
    }

    public ZrokSetupWindow()
    {
        Title = LocalizationService.Get("zrokSetupTitle");
        Width = 570;
        Height = 400;
        MinWidth = 570;
        MaxWidth = 570;
        MinHeight = 400;
        MaxHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = BrushFromHex("#0C100D");
        Foreground = BrushFromHex("#F0F6ED");
        FontFamily = new FontFamily("Segoe UI");

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = LocalizationService.Get("zrokSetupTitle"),
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFromHex("#B5F17A"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        root.Children.Add(new TextBlock
        {
            Text = LocalizationService.Get("zrokSetupBody"),
            FontSize = 12,
            Foreground = BrushFromHex("#C0CDBC"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var accountButton = new Button
        {
            Content = LocalizationService.Get("zrokOpenAccount"),
            Background = BrushFromHex("#202C21"),
            Foreground = BrushFromHex("#F0F6ED"),
            BorderBrush = BrushFromHex("#2B3B2C"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 16)
        };
        accountButton.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://myzrok.io/") { UseShellExecute = true }); }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "DropLink", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        root.Children.Add(accountButton);

        root.Children.Add(new TextBlock
        {
            Text = LocalizationService.Get("zrokTokenLabel"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFromHex("#F0F6ED"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _tokenBox = new PasswordBox
        {
            Background = BrushFromHex("#101710"),
            Foreground = BrushFromHex("#F0F6ED"),
            BorderBrush = BrushFromHex("#2B3B2C"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 9, 10, 9),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 18)
        };
        root.Children.Add(_tokenBox);

        var buttons = new DockPanel { LastChildFill = false };
        var cancel = new Button
        {
            Content = LocalizationService.Get("zrokCancel"),
            Background = BrushFromHex("#202C21"),
            Foreground = BrushFromHex("#F0F6ED"),
            BorderBrush = BrushFromHex("#2B3B2C"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 9, 18, 9),
            MinWidth = 95,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        DockPanel.SetDock(cancel, Dock.Right);
        buttons.Children.Add(cancel);
        var connect = new Button
        {
            Content = LocalizationService.Get("zrokConnect"),
            Background = BrushFromHex("#B5F17A"),
            Foreground = BrushFromHex("#192411"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(18, 9, 18, 9),
            MinWidth = 120,
            FontWeight = FontWeights.SemiBold,
            IsDefault = true
        };
        connect.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_tokenBox.Password))
            {
                MessageBox.Show(LocalizationService.Get("zrokTokenRequired"), "DropLink", MessageBoxButton.OK, MessageBoxImage.Information);
                _tokenBox.Focus();
                return;
            }
            _accountToken = _tokenBox.Password.Trim();
            _tokenBox.Clear();
            DialogResult = true;
        };
        DockPanel.SetDock(connect, Dock.Right);
        buttons.Children.Add(connect);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) => _tokenBox.Focus();
    }

    private static SolidColorBrush BrushFromHex(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
