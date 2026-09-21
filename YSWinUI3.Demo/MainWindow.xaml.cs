using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using YSWinUI3.Demo.Pages;
using YSWinUI3.Settings;
using YSWinUI3.Window;

namespace YSWinUI3.Demo;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private readonly YSWindowController _windowController;
    private readonly YSSettingsStore _settingsStore = new("YSWinUI3.Demo");

    public YSAppSettings Settings { get; }
    public Frame AppFrame => ContentFrame;

    public MainWindow()
    {
        InitializeComponent();
        Settings = _settingsStore.Load<YSAppSettings>();

        _windowController = YSWindowController.Attach(
            this,
            (FrameworkElement)Content,
            AppTitleBar,
            CaptionButtons,
            new YSWindowOptions
            {
                CaptionButtonHeight = 48,
                CaptionButtonWidth = 46,
                Backdrop = Settings.Backdrop,
                InitialWidth = 1200,
                InitialHeight = 800,
                MinimumWidth = 800,
                MinimumHeight = 600,
                IconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico")
            });

        RootNavView.RegisterPropertyChangedCallback(
            NavigationView.PaneDisplayModeProperty,
            OnPaneDisplayModeChanged);

        if (Content is FrameworkElement root)
            root.ActualThemeChanged += Root_ActualThemeChanged;

        ApplyTheme(Settings.Theme.ToString(), save: false);
        ApplyBackdrop(Settings.Backdrop.ToString(), save: false);
        ApplyNavMode(Settings.NavigationMode, save: false);
        Closed += MainWindow_Closed;
    }

    public void ApplyTheme(string theme, bool save = true)
    {
        ElementTheme targetTheme = theme switch
        {
            "Dark" => ElementTheme.Dark,
            "Light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };

        _windowController.ApplyTheme(targetTheme);
        RootNavView.RequestedTheme = targetTheme;
        ContentFrame.RequestedTheme = targetTheme;
        AppTitleBar.RequestedTheme = targetTheme;

        DispatcherQueue.TryEnqueue(RefreshCaptionButtonsTheme);
        if (save)
        {
            Settings.Theme = targetTheme;
            SaveSettings();
        }
    }

    public void ApplyBackdrop(string mode, bool save = true)
    {
        BackdropKind backdrop = mode switch
        {
            "Mica" => BackdropKind.Mica,
            "Acrylic" => BackdropKind.Acrylic,
            _ => BackdropKind.MicaAlt
        };

        _windowController.ApplyBackdrop(backdrop);
        if (save)
        {
            Settings.Backdrop = backdrop;
            SaveSettings();
        }
    }

    public void ApplyNavMode(string navMode, bool save = true)
    {
        RootNavView.PaneDisplayMode = navMode switch
        {
            "Left" => NavigationViewPaneDisplayMode.Left,
            "Top" => NavigationViewPaneDisplayMode.Top,
            _ => NavigationViewPaneDisplayMode.Auto
        };

        AdjustNavViewMargin();
        if (save)
        {
            Settings.NavigationMode = navMode;
            SaveSettings();
        }
    }

    private void RootNavView_Loaded(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
        UpdateTitleBarMargin();
        AdjustNavViewMargin();
    }

    private void RootNavView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args) => UpdateTitleBarMargin();

    private void UpdateTitleBarMargin()
    {
        AppTitleBar.Margin = RootNavView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top
            ? new Thickness(16, 0, 0, 0)
            : new Thickness(
                RootNavView.CompactPaneLength * (RootNavView.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
                0, 0, 0);
    }

    private void RootNavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    private void RootNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateToPage(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "home": NavigateToPage(typeof(HomePage)); break;
                case "about": NavigateToPage(typeof(AboutPage)); break;
            }
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        RootNavView.IsBackEnabled = ContentFrame.CanGoBack;
        if (e.SourcePageType == typeof(SettingsPage))
        {
            RootNavView.SelectedItem = RootNavView.SettingsItem;
            return;
        }

        SelectItemByTag(e.SourcePageType == typeof(AboutPage) ? "about" : "home");
    }

    private void NavigateToPage(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType) ContentFrame.Navigate(pageType);
    }

    private void OnPaneDisplayModeChanged(DependencyObject sender, DependencyProperty dp) => AdjustNavViewMargin();

    private void AdjustNavViewMargin()
    {
        RootNavView.Margin = RootNavView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top
            ? new Thickness(0, RootNavView.CompactPaneLength, 0, 0)
            : new Thickness(0);
        UpdateTitleBarMargin();
    }

    private void SelectItemByTag(string tag)
    {
        foreach (object item in RootNavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
            {
                RootNavView.SelectedItem = navItem;
                break;
            }
        }
    }

    private void AppTitleBar_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (Content is FrameworkElement root)
        {
            e.Handled = true;
            _windowController.ShowSystemMenu(e.GetPosition(root));
        }
    }

    private void Root_ActualThemeChanged(FrameworkElement sender, object args) => RefreshCaptionButtonsTheme();

    private void RefreshCaptionButtonsTheme()
    {
        if (Content is not FrameworkElement root) return;
        CaptionButtons.RefreshForTheme(root.ActualTheme);
        TitleText.Foreground = new SolidColorBrush(root.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (Content is FrameworkElement root) root.ActualThemeChanged -= Root_ActualThemeChanged;
        _windowController.Dispose();
    }

    private void SaveSettings() => _settingsStore.Save(Settings);
}
