using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;
using YSWinUI3.Controls;

namespace YSWinUI3.Window;

public sealed class YSWindowController : IDisposable
{
    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly FrameworkElement _root;
    private readonly FrameworkElement _titleBarHost;
    private readonly YSWindowOptions _options;
    private readonly CustomCaptionButtons? _captionButtons;
    private readonly WindowMessageHost _messages;
    private AppWindow? _appWindow;
    private MenuFlyout? _windowMenu;
    private NativeWindowPaintHook? _paintHook;
    private readonly MenuFlyoutItem _restoreMenuItem = new() { Text = "还原" };
    private readonly MenuFlyoutItem _moveMenuItem = new() { Text = "移动" };
    private readonly MenuFlyoutItem _sizeMenuItem = new() { Text = "大小" };
    private readonly MenuFlyoutItem _minimizeMenuItem = new() { Text = "最小化" };
    private readonly MenuFlyoutItem _maximizeMenuItem = new() { Text = "最大化" };
    private readonly MenuFlyoutItem _closeMenuItem = new() { Text = "关闭" };
    private DispatcherQueueTimer? _backdropMonitor;
    private bool _disposed;
    private bool _firstActivation = true;

    public BackdropKind CurrentBackdrop { get; private set; }
    public event EventHandler<BackdropKind>? BackdropChanged;

    private YSWindowController(
        Microsoft.UI.Xaml.Window window,
        FrameworkElement root,
        FrameworkElement titleBarHost,
        CustomCaptionButtons? captionButtons,
        YSWindowOptions options)
    {
        _window = window;
        _root = root;
        _titleBarHost = titleBarHost;
        _options = options;
        _captionButtons = captionButtons;
        CurrentBackdrop = options.Backdrop;

        EnsureAppWindow();
        ConfigureTitleBar();
        ConfigurePresenter();
        ConfigureIcon();
        InitializeWindowMenu();

        if (captionButtons is not null)
        {
            captionButtons.ButtonHeight = options.CaptionButtonHeight;
            captionButtons.ButtonWidth = options.CaptionButtonWidth;
            captionButtons.IsCustomMinButtonEnabled = options.IsMinimizable;
            captionButtons.IsCustomMaxButtonEnabled = options.IsMaximizable;
            captionButtons.IsCustomCloseButtonEnabled = options.IsClosable;
            captionButtons.Attach(window, titleBarHost);
        }

        _messages = new WindowMessageHost(window, window.DispatcherQueue, options, ShowWindowMenu);
        _messages.Attach();

        _window.Activated += OnWindowActivated;
        _window.Closed += OnWindowClosed;
        _root.ActualThemeChanged += OnActualThemeChanged;

        if (_options.UseSystemTheme)
            ApplyTheme(_root.ActualTheme);
        ApplyBackdrop(options.Backdrop, false);

        if (options.MonitorExternalBackdropChanges)
        {
            _backdropMonitor = window.DispatcherQueue.CreateTimer();
            _backdropMonitor.Interval = options.BackdropMonitorInterval;
            _backdropMonitor.IsRepeating = true;
            _backdropMonitor.Tick += OnBackdropMonitorTick;
            _backdropMonitor.Start();
        }

        if (options.CenterOnStartup)
            CenterAndResize(options.InitialWidth, options.InitialHeight);
    }

    public static YSWindowController Attach(
        Microsoft.UI.Xaml.Window window,
        FrameworkElement root,
        FrameworkElement titleBarHost,
        CustomCaptionButtons? captionButtons = null,
        YSWindowOptions? options = null) =>
        new(window, root, titleBarHost, captionButtons, options ?? new YSWindowOptions());

    public void ApplyTheme(ElementTheme theme)
    {
        _root.RequestedTheme = theme;
        if (_root is Panel panel)
            panel.RequestedTheme = theme;
        _captionButtons?.RefreshForTheme(theme == ElementTheme.Default ? _root.ActualTheme : theme);
        ApplyBackdrop(CurrentBackdrop, false);
    }

    public void ApplyBackdrop(BackdropKind kind, bool raiseEvent = true)
    {
        CurrentBackdrop = kind;

        try
        {
            _window.SystemBackdrop = kind switch
            {
                BackdropKind.Mica => new MicaBackdrop { Kind = MicaKind.Base },
                BackdropKind.Acrylic => new DesktopAcrylicBackdrop(),
                BackdropKind.None => null,
                _ => new MicaBackdrop { Kind = MicaKind.BaseAlt }
            };
        }
        catch
        {
            _window.SystemBackdrop = null;
        }

        DwmBackdropHelper.Apply(_window, kind, _root.ActualTheme == ElementTheme.Dark);
        if (raiseEvent)
            BackdropChanged?.Invoke(this, kind);
    }

    public void SynchronizeBackdrop()
    {
        BackdropKind detected = DetectBackdrop();
        if (detected != CurrentBackdrop)
        {
            CurrentBackdrop = detected;
            BackdropChanged?.Invoke(this, detected);
        }
        DwmBackdropHelper.Apply(_window, CurrentBackdrop, _root.ActualTheme == ElementTheme.Dark);
    }

    private BackdropKind DetectBackdrop() => _window.SystemBackdrop switch
    {
        MicaBackdrop mica when mica.Kind == MicaKind.Base => BackdropKind.Mica,
        MicaBackdrop => BackdropKind.MicaAlt,
        DesktopAcrylicBackdrop => BackdropKind.Acrylic,
        _ => BackdropKind.None
    };

    public void CenterAndResize(int width, int height)
    {
        EnsureAppWindow();
        if (_appWindow is null)
            return;

        var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 workArea = display.WorkArea;
        _appWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    private void ConfigureTitleBar()
    {
        _titleBarHost.Height = _options.CaptionButtonHeight;
        _window.ExtendsContentIntoTitleBar = true;
        _window.SetTitleBar(_titleBarHost);

        if (_appWindow is not null && AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void ConfigurePresenter()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsMinimizable = _options.IsMinimizable;
            presenter.IsMaximizable = _options.IsMaximizable;
            presenter.IsResizable = _options.IsResizable;
        }
    }

    private void ConfigureIcon()
    {
        if (_appWindow is null || string.IsNullOrWhiteSpace(_options.IconPath))
            return;

        try
        {
            string path = Path.GetFullPath(_options.IconPath);
            if (File.Exists(path))
                _appWindow.SetIcon(path);
        }
        catch
        {
            // An optional icon must not prevent the host window from starting.
        }
    }

    private void EnsureAppWindow()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(_window);
        if (hwnd != IntPtr.Zero)
            _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _captionButtons?.RefreshForTheme(sender.ActualTheme);
        DwmBackdropHelper.Apply(_window, CurrentBackdrop, sender.ActualTheme == ElementTheme.Dark);
    }
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        SynchronizeBackdrop();
        if (_firstActivation)
        {
            _firstActivation = false;
            if (_options.CenterOnStartup)
                CenterAndResize(_options.InitialWidth, _options.InitialHeight);
            _captionButtons?.RefreshForTheme(_root.ActualTheme);
        }
        if (_paintHook is null && _options.EnablePaintSuppression)
        {
            _paintHook = new NativeWindowPaintHook(WindowNative.GetWindowHandle(_window));
            _paintHook.Attach();
        }
    }
    private void OnWindowClosed(object sender, WindowEventArgs args) => Dispose();
    private void OnBackdropMonitorTick(DispatcherQueueTimer sender, object args) => SynchronizeBackdrop();

    private void InitializeWindowMenu()
    {
        if (!_options.EnableWindowMenu || _windowMenu is not null)
            return;

        _windowMenu = new MenuFlyout();
        _restoreMenuItem.Icon = CreateWindowMenuIcon("\uE923", 7.5);
        _moveMenuItem.Icon = CreateWindowMenuIcon("\uE7C2");
        _sizeMenuItem.Icon = CreateWindowMenuIcon("\uE8CB");
        _minimizeMenuItem.Icon = CreateWindowMenuIcon("\uE921", 7.5);
        _maximizeMenuItem.Icon = CreateWindowMenuIcon("\uE922", 7.5);
        _closeMenuItem.Icon = CreateWindowMenuIcon("\uE8BB", 7.5);

        _restoreMenuItem.Click += (_, _) => GetPresenter()?.Restore();
        _moveMenuItem.Click += (_, _) => PostSystemCommand(0xF010);
        _sizeMenuItem.Click += (_, _) => PostSystemCommand(0xF000);
        _minimizeMenuItem.Click += (_, _) => GetPresenter()?.Minimize();
        _maximizeMenuItem.Click += (_, _) => GetPresenter()?.Maximize();
        _closeMenuItem.Click += (_, _) => _window.Close();

        _windowMenu.Items.Add(_restoreMenuItem);
        _windowMenu.Items.Add(_moveMenuItem);
        _windowMenu.Items.Add(_sizeMenuItem);
        _windowMenu.Items.Add(_minimizeMenuItem);
        _windowMenu.Items.Add(_maximizeMenuItem);
        _windowMenu.Items.Add(new MenuFlyoutSeparator());
        _windowMenu.Items.Add(_closeMenuItem);
    }

    private static FontIcon CreateWindowMenuIcon(string glyph, double fontSize = 12)
    {
        return new FontIcon
        {
            Glyph = glyph,
            FontSize = fontSize,
            Width = 12,
            Height = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void UpdateWindowMenuState()
    {
        OverlappedPresenter? presenter = GetPresenter();
        if (presenter is null)
        {
            _restoreMenuItem.IsEnabled = false;
            _moveMenuItem.IsEnabled = false;
            _sizeMenuItem.IsEnabled = false;
            _minimizeMenuItem.IsEnabled = false;
            _maximizeMenuItem.IsEnabled = false;
            return;
        }

        bool restored = presenter.State == OverlappedPresenterState.Restored;
        bool minimized = presenter.State == OverlappedPresenterState.Minimized;
        bool maximized = presenter.State == OverlappedPresenterState.Maximized;
        _restoreMenuItem.IsEnabled = !restored;
        _moveMenuItem.IsEnabled = restored;
        _sizeMenuItem.IsEnabled = restored && presenter.IsResizable;
        _minimizeMenuItem.IsEnabled = presenter.IsMinimizable && !minimized;
        _maximizeMenuItem.IsEnabled = presenter.IsMaximizable && !maximized;
        _closeMenuItem.IsEnabled = _options.IsClosable;
    }

    private void ShowWindowMenu(bool atCursor)
    {
        InitializeWindowMenu();
        if (_windowMenu is null || _root.XamlRoot is null)
            return;
        UpdateWindowMenuState();
        var point = new NativePoint();
        if (!atCursor || !GetCursorPos(out point) || !ScreenToClient(WindowNative.GetWindowHandle(_window), ref point))
        {
            point.X = 8;
            point.Y = (int)_titleBarHost.ActualHeight;
        }
        double scale = _root.XamlRoot?.RasterizationScale ?? 1;
        _windowMenu.ShowAt(_root, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(point.X / scale, point.Y / scale),
            ShowMode = FlyoutShowMode.Transient
        });
    }

    public void ShowSystemMenu(Windows.Foundation.Point position)
    {
        InitializeWindowMenu();
        if (_windowMenu is null || _root.XamlRoot is null)
            return;
        UpdateWindowMenuState();
        _windowMenu.ShowAt(_root, new FlyoutShowOptions
        {
            Position = position,
            ShowMode = FlyoutShowMode.Transient
        });
    }

    private OverlappedPresenter? GetPresenter() => _appWindow?.Presenter as OverlappedPresenter;

    private void PostSystemCommand(uint command)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(_window);
        if (hwnd != IntPtr.Zero)
            _ = PostMessage(hwnd, 0x0112, new IntPtr(command), IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _root.ActualThemeChanged -= OnActualThemeChanged;
        _window.Activated -= OnWindowActivated;
        _window.Closed -= OnWindowClosed;
        _paintHook?.Detach();
        _paintHook = null;
        _captionButtons?.Dispose();
        _messages.Dispose();
        if (_backdropMonitor is not null)
        {
            _backdropMonitor.Stop();
            _backdropMonitor.Tick -= OnBackdropMonitorTick;
            _backdropMonitor = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
}
