using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DropLink.Models;
using DropLink.Services;
using Microsoft.Win32;

namespace DropLink;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ShareManager _shareManager = new();
    private readonly ZrokTunnelService _tunnelService = new();
    private readonly AppSettings _settings;
    private readonly ShareServer _server;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _toastTimer;
    private PageKind _currentPage = PageKind.Shares;
    private bool _allowClose;
    private bool _shutdownStarted;
    private Uri? _temporaryPublicBaseUri;
    private Button? _tunnelToggleButton;
    private TextBlock? _tunnelStatusText;
    private ComboBox? _languageInput;
    private TextBox? _publicHostInput;
    private TextBox? _publicPortInput;
    private TextBox? _serverPortInput;
    private TextBlock? _dnsResultText;
    private TextBlock? _downloadsValueText;
    private TextBlock? _transfersPageCount;

    public MainWindow()
    {
        _settings = _settingsService.Load();
        _settings.Language = LocalizationService.Languages.Any(language => language.Code == _settings.Language) ? _settings.Language : "it";
        _settings.PublicHost ??= string.Empty;
        if (!string.IsNullOrWhiteSpace(_settings.PublicHost))
        {
            try { _settings.PublicHost = SettingsValidator.NormalizeHostname(_settings.PublicHost); }
            catch { _settings.PublicHost = string.Empty; }
        }
        if (_settings.ServerPort is < 1024 or > 65535 or 80 or 443) _settings.ServerPort = 43821;
        if (_settings.PublicPort != 443) _settings.PublicPort = 443;
        LocalizationService.Language = _settings.Language;
        _server = new ShareServer(_shareManager, _settings.ServerPort);
        InitializeComponent();
        ApplyStaticTexts();
        RenderCurrentPage();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) =>
        {
            UpdateServerStatus();
            if (_downloadsValueText is not null) _downloadsValueText.Text = _server.ActiveDownloads.ToString();
            if (_transfersPageCount is not null) _transfersPageCount.Text = _server.ActiveDownloads.ToString();
            if (_temporaryPublicBaseUri is not null && !_tunnelService.IsRunning)
            {
                _temporaryPublicBaseUri = null;
                _shareManager.UpdateUrls(token => ShareUrlBuilder.Build(token, _settings, _server.Port));
                UpdateHeader();
                if (_currentPage == PageKind.Shares) RenderCurrentPage();
            }
        };
        _statusTimer.Start();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        };
    }

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var value = 1;
            var handle = new WindowInteropHelper(this).Handle;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch { }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _server.StartAsync();
            UpdateServerStatus();
        }
        catch (Exception exception)
        {
            UpdateServerStatus(exception.Message);
            MessageBox.Show(string.Format(L("serverError") + ": {0}", exception.Message), "DropLink", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_shutdownStarted) return;
        _shutdownStarted = true;
        _statusTimer.Stop();
        _toastTimer.Stop();
        try { await _tunnelService.StopAsync(); }
        catch { }
        _temporaryPublicBaseUri = null;
        try { await _server.StopAsync(); }
        catch { }
        _allowClose = true;
        Close();
    }

    private void ApplyStaticTexts()
    {
        BrandTagline.Text = L("tagline");
        WorkspaceLabel.Text = L("workspace");
        SharesNav.Content = L("navShares");
        TransfersNav.Content = L("navTransfers");
        SettingsNav.Content = L("navSettings");
        UpdateHeader();
        UpdateServerStatus();
    }

    private static string L(string key) => LocalizationService.Get(key);

    private void SharesNav_Click(object sender, RoutedEventArgs e) => Navigate(PageKind.Shares);
    private void TransfersNav_Click(object sender, RoutedEventArgs e) => Navigate(PageKind.Transfers);
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => Navigate(PageKind.Settings);

    private void Navigate(PageKind page)
    {
        _currentPage = page;
        ApplyStaticTexts();
        RenderCurrentPage();
    }

    private void UpdateHeader()
    {
        (TopTitle.Text, TopSubtitle.Text) = _currentPage switch
        {
            PageKind.Shares => (L("sharesTitle"), L("sharesSubtitle")),
            PageKind.Transfers => (L("transfersTitle"), L("transfersSubtitle")),
            _ => (L("settingsTitle"), L("settingsSubtitle"))
        };
        SetNavState(SharesNav, _currentPage == PageKind.Shares);
        SetNavState(TransfersNav, _currentPage == PageKind.Transfers);
        SetNavState(SettingsNav, _currentPage == PageKind.Settings);
        TopStatusText.Text = _temporaryPublicBaseUri is not null ? L("temporaryInternetLink") : string.IsNullOrWhiteSpace(_settings.PublicHost) ? L("localLink") : L("publicLink");
    }

    private static void SetNavState(Button button, bool active)
    {
        button.Background = active ? new SolidColorBrush(Color.FromRgb(31, 48, 31)) : Brushes.Transparent;
        button.Foreground = active ? new SolidColorBrush(Color.FromRgb(181, 241, 122)) : new SolidColorBrush(Color.FromRgb(161, 176, 159));
    }

    private void RenderCurrentPage(string? draftHost = null, string? draftPublicPort = null, string? draftServerPort = null)
    {
        _languageInput = null;
        _publicHostInput = null;
        _publicPortInput = null;
        _serverPortInput = null;
        _tunnelToggleButton = null;
        _tunnelStatusText = null;
        _dnsResultText = null;
        _downloadsValueText = null;
        _transfersPageCount = null;

        UIElement content = _currentPage switch
        {
            PageKind.Shares => BuildSharesPage(),
            PageKind.Transfers => BuildTransfersPage(),
            _ => BuildSettingsPage(draftHost, draftPublicPort, draftServerPort)
        };
        PageHost.Content = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(3, 0, 8, 12)
        };
        UpdateHeader();
    }

    private UIElement BuildSharesPage()
    {
        var page = new StackPanel();
        var hero = new Border
        {
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(26),
            Margin = new Thickness(0, 0, 0, 14),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 75, 49)),
            BorderThickness = new Thickness(1),
            Background = new LinearGradientBrush(Color.FromRgb(34, 55, 32), Color.FromRgb(20, 31, 21), 25)
        };
        var heroGrid = new Grid();
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heroText.Children.Add(Text(_temporaryPublicBaseUri is null ? L("heroEyebrow") : L("heroEyebrowTunnel"), 10, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 13));
        heroText.Children.Add(Text(L("heroTitle1"), 32, Brushes.White, true, 0, 0, 0, 0));
        heroText.Children.Add(Text(L("heroTitle2"), 32, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 10));
        heroText.Children.Add(Text(L("heroSubtitle"), 13, new SolidColorBrush(Color.FromRgb(201, 214, 196)), false, 0, 0, 0, 18, 440));
        heroText.Children.Add(Button(L("selectFile"), SelectFile_Click, "PrimaryButton"));
        _tunnelToggleButton = Button(
            _temporaryPublicBaseUri is not null && _tunnelService.IsRunning ? L("internetShareStop") : L("internetShareStart"),
            ToggleInternetSharing_Click,
            _temporaryPublicBaseUri is not null && _tunnelService.IsRunning ? "SecondaryButton" : "PrimaryButton",
            new Thickness(0, 9, 0, 0));
        heroText.Children.Add(_tunnelToggleButton);
        var tunnelHelp = _temporaryPublicBaseUri is not null && _tunnelService.IsRunning
            ? string.Format(L("internetShareActiveBody"), _temporaryPublicBaseUri.GetLeftPart(UriPartial.Authority))
            : L("internetShareHelp");
        _tunnelStatusText = Text(tunnelHelp, 10, new SolidColorBrush(Color.FromRgb(201, 214, 196)), false, 0, 8, 0, 0, 500);
        heroText.Children.Add(_tunnelStatusText);
        Grid.SetColumn(heroText, 0);
        heroGrid.Children.Add(heroText);

        var art = new Grid { Margin = new Thickness(16, 8, 0, 8), MinHeight = 150 };
        var circle1 = new Border { Width = 150, Height = 150, CornerRadius = new CornerRadius(75), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 181, 241, 122)), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var circle2 = new Border { Width = 118, Height = 118, CornerRadius = new CornerRadius(59), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 181, 241, 122)), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var fileCard = new Border { Width = 94, Height = 116, CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromRgb(38, 56, 36)), BorderBrush = new SolidColorBrush(Color.FromRgb(90, 126, 78)), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new RotateTransform(5) };
        var fileDrawing = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        fileDrawing.Children.Add(Text("▤", 36, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 7));
        fileDrawing.Children.Add(new Border { Height = 5, Width = 50, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(133, 168, 119)), Margin = new Thickness(0, 2, 0, 2) });
        fileDrawing.Children.Add(new Border { Height = 5, Width = 39, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(93, 124, 83)), Margin = new Thickness(0, 2, 0, 2) });
        fileCard.Child = fileDrawing;
        art.Children.Add(circle1); art.Children.Add(circle2); art.Children.Add(fileCard);
        Grid.SetColumn(art, 1);
        heroGrid.Children.Add(art);
        hero.Child = heroGrid;
        page.Children.Add(hero);

        if (_temporaryPublicBaseUri is not null && _tunnelService.IsRunning)
            page.Children.Add(WarningPanel(L("warningTitleTunnel"), L("warningBodyTunnel")));
        else
            page.Children.Add(WarningPanel(
                string.IsNullOrWhiteSpace(_settings.PublicHost) ? L("warningTitle") : L("warningTitlePublic"),
                string.IsNullOrWhiteSpace(_settings.PublicHost) ? L("warningBody") : L("warningBodyPublic")));

        var stats = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 22) };
        stats.Children.Add(StatCard("↗", L("statShares"), _shareManager.Shares.Count.ToString()));
        stats.Children.Add(StatCard("▤", L("statTotal"), FormatSize(_shareManager.Shares.Sum(share => share.Size))));
        var downloadCard = StatCard("⇩", L("statDownloads"), _server.ActiveDownloads.ToString());
        _downloadsValueText = FindNamedText(downloadCard, "StatValue");
        stats.Children.Add(downloadCard);
        page.Children.Add(stats);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headings = new StackPanel();
        headings.Children.Add(Text(L("linksTitle"), 18, Brushes.White, true));
        headings.Children.Add(Text(L("linksSubtitle"), 11, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false, 0, 4, 0, 0));
        header.Children.Add(headings);
        var badge = new Border { CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Color.FromRgb(19, 27, 20)), BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 44)), BorderThickness = new Thickness(1), Padding = new Thickness(10, 7, 10, 7), VerticalAlignment = VerticalAlignment.Top };
        badge.Child = Text($"{_shareManager.Shares.Count} {L("fileCount")}", 9, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true);
        Grid.SetColumn(badge, 1); header.Children.Add(badge);
        page.Children.Add(header);

        var shares = _shareManager.Shares;
        if (shares.Count == 0)
        {
            var emptyPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 24, 12, 24) };
            emptyPanel.Children.Add(Text("↗", 34, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 8, align: TextAlignment.Center));
            emptyPanel.Children.Add(Text(L("emptyTitle"), 16, Brushes.White, true, 0, 0, 0, 5, align: TextAlignment.Center));
            emptyPanel.Children.Add(Text(L("emptyBody"), 12, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false, 0, 0, 0, 0, align: TextAlignment.Center));
            page.Children.Add(Panel(emptyPanel, new Thickness(20), new Thickness(0, 0, 0, 10)));
        }
        else
        {
            foreach (var share in shares) page.Children.Add(BuildShareCard(share));
        }

        return page;
    }

    private UIElement BuildShareCard(ShareRecord share)
    {
        var outer = Panel(new StackPanel(), new Thickness(15), new Thickness(0, 0, 0, 10));
        var content = (StackPanel)outer.Child;
        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var fileIcon = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(Color.FromRgb(31, 47, 30)), BorderBrush = new SolidColorBrush(Color.FromRgb(56, 77, 53)), BorderThickness = new Thickness(1) };
        fileIcon.Child = Text("▤", 22, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 0, align: TextAlignment.Center);
        header.Children.Add(fileIcon);
        var detail = new StackPanel { Margin = new Thickness(12, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        detail.Children.Add(Text(share.Name, 13, Brushes.White, true, 0, 0, 0, 3));
        detail.Children.Add(Text($"{FormatSize(share.Size)}  ·  {L("created")} {share.CreatedUtc.ToLocalTime():dd MMM yyyy, HH:mm}", 10, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false));
        Grid.SetColumn(detail, 1); header.Children.Add(detail);
        content.Children.Add(header);

        var linkBox = new TextBox { Text = share.Url, IsReadOnly = true, FontSize = 11, Margin = new Thickness(0, 0, 0, 10), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = share.Url };
        linkBox.Style = (Style)FindResource("InputBox");
        content.Children.Add(linkBox);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Button(L("copy"), (_, _) => CopyLink(share.Url), "SecondaryButton", new Thickness(0, 0, 7, 0)));
        actions.Children.Add(Button(L("open"), (_, _) => OpenLink(share.Url), "SecondaryButton", new Thickness(0, 0, 7, 0)));
        actions.Children.Add(Button(L("revoke"), (_, _) => RevokeLink(share), "SecondaryButton"));
        content.Children.Add(actions);
        return outer;
    }

    private UIElement BuildTransfersPage()
    {
        var page = new StackPanel();
        var card = new Border { Background = new SolidColorBrush(Color.FromRgb(20, 28, 21)), BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 44)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17), Padding = new Thickness(23), Margin = new Thickness(0, 0, 0, 14) };
        var stack = new StackPanel();
        stack.Children.Add(Text(L("currentDownloads"), 12, new SolidColorBrush(Color.FromRgb(161, 176, 159)), true, 0, 0, 0, 10));
        _transfersPageCount = Text(_server.ActiveDownloads.ToString(), 42, Brushes.White, true, 0, 0, 0, 13);
        stack.Children.Add(_transfersPageCount);
        stack.Children.Add(Text(_server.ActiveDownloads == 0 ? L("noDownloads") : L("statDownloads"), 13, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true));
        card.Child = stack;
        page.Children.Add(card);
        page.Children.Add(WarningPanel(L("transfersTitle"), _temporaryPublicBaseUri is not null && _tunnelService.IsRunning ? L("transferNoteTunnel") : L("transferNote")));
        return page;
    }

    private UIElement BuildSettingsPage(string? draftHost, string? draftPublicPort, string? draftServerPort)
    {
        var page = new StackPanel();
        var cardStack = new StackPanel();
        cardStack.Children.Add(Text(L("language"), 13, Brushes.White, true, 0, 0, 0, 5));
        cardStack.Children.Add(Text(L("languageHelp"), 11, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false, 0, 0, 0, 10));
        _languageInput = new ComboBox
        {
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = LocalizationService.Languages,
            DisplayMemberPath = nameof(LanguageOption.Name),
            SelectedValuePath = nameof(LanguageOption.Code),
            SelectedValue = _settings.Language,
            Style = (Style)FindResource("InputCombo"),
            Margin = new Thickness(0, 0, 0, 22)
        };
        cardStack.Children.Add(_languageInput);

        cardStack.Children.Add(Text(L("publicDomain"), 13, Brushes.White, true, 0, 0, 0, 5));
        cardStack.Children.Add(Text(L("publicDomainHelp"), 11, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false, 0, 0, 0, 10, 680));
        _publicHostInput = new TextBox { Text = draftHost ?? _settings.PublicHost, Height = 40, Margin = new Thickness(0, 0, 0, 17), MaxLength = 253 };
        _publicHostInput.Style = (Style)FindResource("InputBox");
        cardStack.Children.Add(_publicHostInput);

        var ports = new Grid { Margin = new Thickness(0, 0, 0, 5) };
        ports.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ports.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        ports.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var publicPortStack = new StackPanel();
        publicPortStack.Children.Add(Text(L("publicPort"), 12, Brushes.White, true, 0, 0, 0, 7));
        _publicPortInput = new TextBox { Text = "443", Height = 40, Margin = new Thickness(0, 0, 0, 0), MaxLength = 5, IsReadOnly = true, ToolTip = "I link pubblici usano HTTPS sulla porta standard 443." };
        _publicPortInput.Style = (Style)FindResource("InputBox");
        publicPortStack.Children.Add(_publicPortInput);
        Grid.SetColumn(publicPortStack, 0); ports.Children.Add(publicPortStack);

        var serverPortStack = new StackPanel();
        serverPortStack.Children.Add(Text(L("serverPort"), 12, Brushes.White, true, 0, 0, 0, 7));
        _serverPortInput = new TextBox { Text = draftServerPort ?? _settings.ServerPort.ToString(), Height = 40, MaxLength = 5 };
        _serverPortInput.Style = (Style)FindResource("InputBox");
        serverPortStack.Children.Add(_serverPortInput);
        Grid.SetColumn(serverPortStack, 2); ports.Children.Add(serverPortStack);
        cardStack.Children.Add(ports);
        cardStack.Children.Add(Text(L("serverPortHelp"), 11, new SolidColorBrush(Color.FromRgb(161, 176, 159)), false, 0, 8, 0, 15, 680));

        var buttons = new WrapPanel { Margin = new Thickness(0, 2, 0, 10) };
        buttons.Children.Add(Button(L("saveSettings"), SaveSettings_Click, "PrimaryButton", new Thickness(0, 0, 8, 8)));
        buttons.Children.Add(Button(L("checkDns"), CheckDns_Click, "SecondaryButton", new Thickness(0, 0, 8, 8)));
        _dnsResultText = Text("", 11, new SolidColorBrush(Color.FromRgb(181, 241, 122)), false, 0, 5, 0, 0, 680);
        cardStack.Children.Add(buttons);
        cardStack.Children.Add(_dnsResultText);
        page.Children.Add(Panel(cardStack, new Thickness(22), new Thickness(0, 0, 0, 14)));

        page.Children.Add(InfoPanel(L("settingsInfoTitle"), L("settingsInfoBody")));
        page.Children.Add(WarningPanel(L("warningTitlePublic"), L("publicWarning")));

        _languageInput.SelectionChanged += LanguageInput_SelectionChanged;
        return page;
    }

    private void LanguageInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageInput?.SelectedValue is not string code || code == _settings.Language) return;
        var currentHost = _publicHostInput?.Text ?? _settings.PublicHost;
        var currentPublicPort = _publicPortInput?.Text ?? _settings.PublicPort.ToString();
        var currentServerPort = _serverPortInput?.Text ?? _settings.ServerPort.ToString();
        _settings.Language = code;
        LocalizationService.Language = code;
        _settingsService.Save(_settings);
        ApplyStaticTexts();
        RenderCurrentPage(currentHost, currentPublicPort, currentServerPort);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var oldServerPort = _settings.ServerPort;
        var oldHost = _settings.PublicHost;
        var hadShares = _shareManager.Shares.Count > 0;
        try
        {
            var host = SettingsValidator.NormalizeHostname(_publicHostInput?.Text);
            var hostChanged = !string.Equals(oldHost, host, StringComparison.OrdinalIgnoreCase);
            var publicPort = SettingsValidator.ParsePublicPort(_publicPortInput?.Text ?? "443");
            var serverPort = SettingsValidator.ParseServerPort(_serverPortInput?.Text ?? "43821");
            _settings.PublicHost = host;
            _settings.PublicPort = publicPort;
            _settings.ServerPort = serverPort;
            _settingsService.Save(_settings);
            ApplyStaticTexts();
            RenderCurrentPage();
            var message = oldServerPort != serverPort ? L("restartNeeded") : L("saved");
            if (hostChanged && hadShares) message += Environment.NewLine + Environment.NewLine + L("linksKeepOldUrl");
            ShowToast(message, durationSeconds: message.Length > 100 ? 6 : 4);
        }
        catch (Exception exception)
        {
            ShowToast(string.Format(L("invalidSettings"), exception.Message), isError: true);
        }
    }

    private async void CheckDns_Click(object sender, RoutedEventArgs e)
    {
        var host = _publicHostInput?.Text.Trim() ?? string.Empty;
        if (host.Length == 0)
        {
            if (_dnsResultText is not null) _dnsResultText.Text = L("dnsBlank");
            ShowToast(L("dnsBlank"), isError: true);
            return;
        }
        try
        {
            host = SettingsValidator.NormalizeHostname(host);
            if (_dnsResultText is not null) _dnsResultText.Text = L("dnsChecking");
            var addresses = await Dns.GetHostAddressesAsync(host);
            var publicAddresses = addresses.Where(IsPublicAddress).ToArray();
            if (_dnsResultText is not null)
            {
                _dnsResultText.Text = publicAddresses.Length == 0
                    ? L("dnsPrivate")
                    : string.Format(L("dnsPublic"), string.Join(", ", publicAddresses.Select(address => address.ToString())));
            }
            ShowToast(L("feedbackDnsChecked"), isError: publicAddresses.Length == 0);
        }
        catch (Exception exception)
        {
            if (_dnsResultText is not null) _dnsResultText.Text = string.Format(L("dnsFailure"), exception.Message);
            ShowToast(string.Format(L("feedbackError"), exception.Message), isError: true);
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;
            return true;
        }
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        if (bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 || bytes[0] == 169 && bytes[1] == 254) return false;
        if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return false;
        if (bytes[0] == 192 && bytes[1] == 168) return false;
        if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return false;
        return true;
    }

    private async void ToggleInternetSharing_Click(object sender, RoutedEventArgs e)
    {
        if (_temporaryPublicBaseUri is not null && _tunnelService.IsRunning)
        {
            var answer = MessageBox.Show(L("internetShareStopConfirm"), "DropLink", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            try { await _tunnelService.StopAsync(); }
            catch (Exception exception)
            {
                ShowToast(string.Format(L("feedbackError"), exception.Message), isError: true);
                return;
            }
            _temporaryPublicBaseUri = null;
            _shareManager.UpdateUrls(token => ShareUrlBuilder.Build(token, _settings, _server.Port));
            Navigate(PageKind.Shares);
            ShowToast(L("feedbackInternetStopped"));
            return;
        }

        if (!_server.IsRunning)
        {
            ShowToast(L("internetShareNotReady"), isError: true);
            return;
        }

        if (_tunnelToggleButton is not null)
        {
            _tunnelToggleButton.IsEnabled = false;
            _tunnelToggleButton.Content = L("internetShareStarting");
        }

        try
        {
            try
            {
                // Downloads and checksum-verifies zrok automatically if it is not installed.
                _temporaryPublicBaseUri = await _tunnelService.StartAsync(_server.Port);
            }
            catch (ZrokSetupRequiredException)
            {
                // The hosted zrok service requires each user to authorize their own free account once.
                var setupDialog = new ZrokSetupWindow { Owner = this };
                if (setupDialog.ShowDialog() != true) return;

                var accountToken = setupDialog.AccountToken;
                setupDialog.ClearToken();
                if (_tunnelToggleButton is not null) _tunnelToggleButton.Content = L("internetShareConfiguring");
                try
                {
                    await _tunnelService.EnableAsync(accountToken);
                }
                finally
                {
                    // Reduce the lifetime of this secret in application code; zrok manages its own identity.
                    accountToken = string.Empty;
                }

                _temporaryPublicBaseUri = await _tunnelService.StartAsync(_server.Port);
            }

            _shareManager.UpdateUrls(token => new Uri(_temporaryPublicBaseUri!, $"s/{token}").AbsoluteUri);
            Navigate(PageKind.Shares);
            ShowToast(string.Format(L("feedbackInternetStarted"), _temporaryPublicBaseUri.GetLeftPart(UriPartial.Authority)), durationSeconds: 6);
        }
        catch (Exception exception)
        {
            try { await _tunnelService.StopAsync(); } catch { }
            _temporaryPublicBaseUri = null;
            _shareManager.UpdateUrls(token => ShareUrlBuilder.Build(token, _settings, _server.Port));
            Navigate(PageKind.Shares);
            ShowToast(string.Format(L("internetShareFailure"), exception.Message), isError: true, durationSeconds: 7);
        }
        finally
        {
            if (_tunnelToggleButton is not null) _tunnelToggleButton.IsEnabled = true;
        }
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        if (!_server.IsRunning)
        {
            ShowToast(L("serverNotReady"), isError: true);
            return;
        }
        if (_settings.ServerPort != _server.Port)
        {
            ShowToast(L("restartPortMismatch"), isError: true, durationSeconds: 6);
            return;
        }
        var dialog = new OpenFileDialog
        {
            Title = L("dialogTitle"),
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Tutti i file (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var share = _shareManager.CreateShare(dialog.FileName, token =>
                _temporaryPublicBaseUri is not null && _tunnelService.IsRunning
                    ? new Uri(_temporaryPublicBaseUri, $"s/{token}").AbsoluteUri
                    : ShareUrlBuilder.Build(token, _settings, _server.Port));
            Navigate(PageKind.Shares);
            ShowToast(string.Format(L("feedbackShareCreated"), share.Name), durationSeconds: 5);
        }
        catch (Exception exception)
        {
            ShowToast(string.Format(L("shareFailed"), exception.Message), isError: true, durationSeconds: 6);
        }
    }

    private void CopyLink(string url)
    {
        try
        {
            Clipboard.SetText(url);
            ShowToast(L("feedbackCopied"));
        }
        catch { ShowToast(L("clipboardError"), isError: true); }
    }

    private void OpenLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ShowToast(L("feedbackOpened"));
        }
        catch (Exception exception) { ShowToast(string.Format(L("browserError"), exception.Message), isError: true); }
    }

    private void RevokeLink(ShareRecord share)
    {
        if (MessageBox.Show(L("confirmRevoke"), "DropLink", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (_shareManager.Revoke(share.Token))
        {
            RenderCurrentPage();
            ShowToast(L("feedbackShareRevoked"));
        }
    }

    private void ShowToast(string message, bool isError = false, int durationSeconds = 4)
    {
        ToastMessage.Text = message;
        ToastGlyph.Text = isError ? "!" : "✓";
        ToastGlyph.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(255, 194, 115))
            : new SolidColorBrush(Color.FromRgb(181, 241, 122));
        ToastBorder.Background = isError
            ? new SolidColorBrush(Color.FromRgb(48, 39, 20))
            : new SolidColorBrush(Color.FromRgb(24, 41, 27));
        ToastBorder.BorderBrush = isError
            ? new SolidColorBrush(Color.FromRgb(119, 84, 38))
            : new SolidColorBrush(Color.FromRgb(59, 91, 58));
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 2, 10));
        _toastTimer.Start();
    }

    private void UpdateServerStatus(string? error = null)
    {
        var running = error is null && _server.IsRunning;
        ServerStatusDot.Fill = new SolidColorBrush(running ? Color.FromRgb(181, 241, 122) : Color.FromRgb(224, 179, 92));
        TopStatusDot.Fill = ServerStatusDot.Fill;
        ServerStatusText.Text = running ? L("serverRunning") : error is not null ? L("serverError") : L("serverStarting");
        ServerAddressText.Text = $"{L("serverPortLabel")} {_server.Port} · 127.0.0.1 + LAN";
        TopStatusText.Text = _temporaryPublicBaseUri is not null ? L("temporaryInternetLink") : string.IsNullOrWhiteSpace(_settings.PublicHost) ? L("localLink") : L("publicLink");
    }

    private UIElement WarningPanel(string title, string body)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text("⚠  " + title, 12, new SolidColorBrush(Color.FromRgb(244, 208, 128)), true, 0, 0, 0, 6));
        stack.Children.Add(Text(body, 11, new SolidColorBrush(Color.FromRgb(219, 202, 159)), false, 0, 0, 0, 0));
        return new Border { Background = new SolidColorBrush(Color.FromRgb(48, 39, 20)), BorderBrush = new SolidColorBrush(Color.FromRgb(88, 70, 34)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(15, 12, 15, 12), Margin = new Thickness(0, 0, 0, 14), Child = stack };
    }

    private UIElement InfoPanel(string title, string body)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text(title, 14, Brushes.White, true, 0, 0, 0, 8));
        stack.Children.Add(Text(body, 12, new SolidColorBrush(Color.FromRgb(168, 184, 164)), false));
        return Panel(stack, new Thickness(20), new Thickness(0, 0, 0, 14));
    }

    private UIElement StatCard(string glyph, string label, string value)
    {
        var stack = new StackPanel();
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var icon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(34, 52, 32)), Margin = new Thickness(0, 0, 9, 0) };
        icon.Child = Text(glyph, 20, new SolidColorBrush(Color.FromRgb(181, 241, 122)), true, 0, 0, 0, 0, align: TextAlignment.Center);
        top.Children.Add(icon);
        top.Children.Add(Text(label, 10, new SolidColorBrush(Color.FromRgb(168, 184, 164)), false, 0, 0, 0, 0, 260));
        stack.Children.Add(top);
        stack.Children.Add(Text(value, 25, Brushes.White, true, 0, 0, 0, 0, name: "StatValue"));
        return Panel(stack, new Thickness(13), new Thickness(0, 0, 10, 0));
    }

    private static TextBlock? FindNamedText(DependencyObject root, string name)
    {
        foreach (var item in LogicalTreeHelper.GetChildren(root))
        {
            if (item is not DependencyObject child) continue;
            if (child is TextBlock text && text.Name == name) return text;
            var result = FindNamedText(child, name);
            if (result is not null) return result;
        }
        return null;
    }

    private Border Panel(UIElement child, Thickness padding, Thickness margin) => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(20, 28, 21)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(43, 59, 44)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(13),
        Padding = padding,
        Margin = margin,
        Child = child
    };

    private TextBlock Text(string value, double size = 13, Brush? brush = null, bool bold = false,
        double left = 0, double top = 0, double right = 0, double bottom = 0, double maxWidth = double.PositiveInfinity,
        TextAlignment align = TextAlignment.Left, string? name = null)
    {
        var block = new TextBlock
        {
            Text = value,
            FontSize = size,
            Foreground = brush ?? new SolidColorBrush(Color.FromRgb(240, 246, 237)),
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(left, top, right, bottom),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = maxWidth,
            TextAlignment = align,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(name)) block.Name = name;
        return block;
    }

    private Button Button(string content, RoutedEventHandler action, string style, Thickness? margin = null)
    {
        var button = new Button { Content = content, Style = (Style)FindResource(style), Margin = margin ?? new Thickness(0), MinWidth = 65 };
        button.Click += action;
        return button;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{bytes} B" : $"{size:0.0} {units[unit]}";
    }

    private enum PageKind { Shares, Transfers, Settings }
}
