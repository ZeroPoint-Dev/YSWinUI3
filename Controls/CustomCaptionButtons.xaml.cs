using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace YSWinUI3.Controls;

public sealed partial class CustomCaptionButtons : UserControl, IDisposable
{
    private static readonly TimeSpan CaptionHoverEnterAnimationDuration = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan CaptionHoverExitAnimationDuration = TimeSpan.FromMilliseconds(130);
    private const double InactiveGlyphOpacity = 0.48;
    private const string InactiveCaptionForegroundResourceKey = "WindowCaptionForegroundDisabled";

    public static readonly DependencyProperty ButtonHeightProperty =
        DependencyProperty.Register(nameof(ButtonHeight), typeof(double), typeof(CustomCaptionButtons), new PropertyMetadata(48d, OnButtonLayoutChanged));

    public static readonly DependencyProperty ButtonWidthProperty =
        DependencyProperty.Register(nameof(ButtonWidth), typeof(double), typeof(CustomCaptionButtons), new PropertyMetadata(46d, OnButtonLayoutChanged));

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    public double ButtonWidth
    {
        get => (double)GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    private static void OnButtonLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomCaptionButtons buttons)
        {
            buttons.ApplyButtonLayout();
            buttons.SyncNonClientRegions();
        }
    }

    private void ApplyButtonLayout()
    {
        if (RootGrid is null)
            return;

        RootGrid.Height = ButtonHeight;
        RootGrid.Width = ButtonWidth * 3;
        RootGrid.MinWidth = RootGrid.Width;
        MinColumn.Width = new GridLength(ButtonWidth);
        MaxColumn.Width = new GridLength(ButtonWidth);
        CloseColumn.Width = new GridLength(ButtonWidth);
    }

    public static readonly DependencyProperty ButtonForegroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonForegroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(Colors.Black, OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonBackgroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonBackgroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(Colors.Transparent, OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonHoverForegroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonHoverForegroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(Colors.Black, OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonHoverBackgroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonHoverBackgroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(ColorHelper.FromArgb(0x14, 0x00, 0x00, 0x00), OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonPressedForegroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonPressedForegroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(Colors.Black, OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonPressedBackgroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonPressedBackgroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(ColorHelper.FromArgb(0x22, 0x00, 0x00, 0x00), OnVisualPropertyChanged));

    public static readonly DependencyProperty ButtonDisabledForegroundColorProperty =
        DependencyProperty.Register(
            nameof(ButtonDisabledForegroundColor),
            typeof(Windows.UI.Color),
            typeof(CustomCaptionButtons),
            new PropertyMetadata(ColorHelper.FromArgb(0x66, 0x00, 0x00, 0x00), OnVisualPropertyChanged));

    public Windows.UI.Color ButtonDisabledForegroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonDisabledForegroundColorProperty);
        set => SetValue(ButtonDisabledForegroundColorProperty, value);
    }

    public Windows.UI.Color ButtonForegroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonForegroundColorProperty);
        set => SetValue(ButtonForegroundColorProperty, value);
    }

    public Windows.UI.Color ButtonBackgroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonBackgroundColorProperty);
        set => SetValue(ButtonBackgroundColorProperty, value);
    }

    public Windows.UI.Color ButtonHoverForegroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonHoverForegroundColorProperty);
        set => SetValue(ButtonHoverForegroundColorProperty, value);
    }

    public Windows.UI.Color ButtonHoverBackgroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonHoverBackgroundColorProperty);
        set => SetValue(ButtonHoverBackgroundColorProperty, value);
    }

    public Windows.UI.Color ButtonPressedForegroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonPressedForegroundColorProperty);
        set => SetValue(ButtonPressedForegroundColorProperty, value);
    }

    public Windows.UI.Color ButtonPressedBackgroundColor
    {
        get => (Windows.UI.Color)GetValue(ButtonPressedBackgroundColorProperty);
        set => SetValue(ButtonPressedBackgroundColorProperty, value);
    }

    private bool _isCustomMinButtonEnabled = true;
    public bool IsCustomMinButtonEnabled
    {
        get => _isCustomMinButtonEnabled;
        set
        {
            if (_isCustomMinButtonEnabled == value) return;
            _isCustomMinButtonEnabled = value;
            OnButtonEnabledStateChanged();
        }
    }

    private bool _isCustomMaxButtonEnabled = true;
    public bool IsCustomMaxButtonEnabled
    {
        get => _isCustomMaxButtonEnabled;
        set
        {
            if (_isCustomMaxButtonEnabled == value) return;
            _isCustomMaxButtonEnabled = value;
            OnButtonEnabledStateChanged();
        }
    }

    private bool _isCustomCloseButtonEnabled = true;
    public bool IsCustomCloseButtonEnabled
    {
        get => _isCustomCloseButtonEnabled;
        set
        {
            if (_isCustomCloseButtonEnabled == value) return;
            _isCustomCloseButtonEnabled = value;
            OnButtonEnabledStateChanged();
        }
    }

    private static readonly Windows.UI.Color CloseHoverBackgroundColor =
        ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C);

    private static readonly Windows.UI.Color ClosePressedBackgroundColor =
        ColorHelper.FromArgb(0xFF, 0xC7, 0x3C, 0x31);

    private static readonly Windows.UI.Color ClosePressedForegroundColor =
        ColorHelper.FromArgb(0x95, 0xFF, 0xFF, 0xFF);

    private Microsoft.UI.Xaml.Window? _window;
    private FrameworkElement? _titleBarHost;
    private AppWindow? _appWindow;
    private InputNonClientPointerSource? _nonClientPointerSource;
    private IntPtr _hWnd;

    private SUBCLASSPROC? _subclassProc;
    private bool _attached;
    private bool _loaded;
    private bool _syncingRegions;
    private bool _isWindowActive = true;
    private bool _hasPointerMotionSample;
    private bool _hasRecentPointerSpeed;
    private NativePoint _lastPointerPosition;
    private long _lastPointerMotionTimestamp;
    private double _pointerSpeedPixelsPerSecond;

    private readonly SolidColorBrush _minBackgroundBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _maxBackgroundBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _closeBackgroundBrush = new(Colors.Transparent);
    private Storyboard? _minBackgroundAnimation;
    private Storyboard? _maxBackgroundAnimation;
    private Storyboard? _closeBackgroundAnimation;

    private VisualStateKind _minState = VisualStateKind.Normal;
    private VisualStateKind _maxState = VisualStateKind.Normal;
    private VisualStateKind _closeState = VisualStateKind.Normal;

    private ClientButtonKind? _capturedClientButton;
    private uint? _capturedPointerId;
    private bool _maxNcPressed;

    public CustomCaptionButtons()
    {
        InitializeComponent();
        MinButtonHost.Background = _minBackgroundBrush;
        MaxButtonHost.Background = _maxBackgroundBrush;
        CloseButtonHost.Background = _closeBackgroundBrush;
        ApplyButtonLayout();

        Loaded += CustomCaptionButtons_Loaded;
        Unloaded += CustomCaptionButtons_Unloaded;

        RootGrid.SizeChanged += AnySizeChanged;
        MinButtonHost.SizeChanged += AnySizeChanged;
        MaxButtonHost.SizeChanged += AnySizeChanged;
        CloseButtonHost.SizeChanged += AnySizeChanged;
    }

    private void CustomCaptionButtons_Loaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        UpdateMaxHitTestMode();
        UpdateToolTips();
        UpdateMaxGlyph();
        ApplyAllVisuals();
        SyncNonClientRegions();

        if (_window is not null && !_attached)
        {
            AttachCore();
        }
    }

    private void CustomCaptionButtons_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        StopBackgroundAnimations();
    }

    private void AnySizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncNonClientRegions();
    }

    public void Attach(Microsoft.UI.Xaml.Window window, FrameworkElement titleBarHost)
    {
        _window = window;

        if (_titleBarHost is not null)
        {
            _titleBarHost.SizeChanged -= TitleBarHost_SizeChanged;
        }

        _titleBarHost = titleBarHost;
        _titleBarHost.SizeChanged += TitleBarHost_SizeChanged;

        if (_loaded && !_attached)
        {
            AttachCore();
        }
    }

    private void TitleBarHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SyncNonClientRegions();
    }

    private void AttachCore()
    {
        if (_window is null || _attached)
            return;

        _hWnd = WindowNative.GetWindowHandle(_window);
        _isWindowActive = GetForegroundWindow() == _hWnd;
        UpdatePointerMotionSample();
        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(windowId);

        _subclassProc = WindowSubclassProc;
        if (!SetWindowSubclass(_hWnd, _subclassProc, 1, 0))
        {
            throw new InvalidOperationException("SetWindowSubclass failed.");
        }

        _window.Activated += Window_Activated;
        _appWindow.Changed += AppWindow_Changed;

        if (_nonClientPointerSource is not null)
        {
            _nonClientPointerSource.PointerEntered += NonClientPointerSource_PointerEntered;
            _nonClientPointerSource.PointerExited += NonClientPointerSource_PointerExited;
            _nonClientPointerSource.PointerMoved += NonClientPointerSource_PointerMoved;
            _nonClientPointerSource.PointerPressed += NonClientPointerSource_PointerPressed;
            _nonClientPointerSource.PointerReleased += NonClientPointerSource_PointerReleased;
            _nonClientPointerSource.ExitedMoveSize += NonClientPointerSource_ExitedMoveSize;
        }

        _attached = true;

        UpdateMaxHitTestMode();
        UpdateToolTips();
        UpdateMaxGlyph();
        ApplyAllVisuals();
        SyncNonClientRegions();
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        QueueWindowActivationVisualRefresh();
    }

    private void QueueWindowActivationVisualRefresh()
    {
        DispatcherQueue.TryEnqueue(RefreshWindowActivationVisuals);
    }

    private void RefreshWindowActivationVisuals()
    {
        if (!_attached || _hWnd == IntPtr.Zero)
            return;

        // Query the actual foreground window after activation messages have finished
        // processing, then repaint every caption button even if the active state did
        // not change. This also repairs a stale inactive glyph after rapid activation.
        _isWindowActive = GetForegroundWindow() == _hWnd;
        UpdateMaxGlyph();
        ApplyAllVisuals();
        SyncNonClientRegions();
    }

    public void RefreshForTheme(ElementTheme theme)
    {
        bool dark = theme == ElementTheme.Dark;

        ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        ButtonBackgroundColor = Colors.Transparent;
        ButtonHoverForegroundColor = dark ? Colors.White : Colors.Black;
        ButtonPressedForegroundColor = dark ? Colors.White : Colors.Black;
        ButtonHoverBackgroundColor = dark
            ? ColorHelper.FromArgb(0x14, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x14, 0x00, 0x00, 0x00);
        ButtonPressedBackgroundColor = dark
            ? ColorHelper.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x22, 0x00, 0x00, 0x00);
        ButtonDisabledForegroundColor = dark
            ? ColorHelper.FromArgb(0x66, 0xFF, 0xFF, 0xFF)
            : ColorHelper.FromArgb(0x66, 0x00, 0x00, 0x00);

        ApplyAllVisuals();
    }

    private OverlappedPresenter? GetOverlappedPresenter()
    {
        if (_appWindow?.Presenter is not null &&
            _appWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
        {
            return _appWindow.Presenter.As<OverlappedPresenter>();
        }

        return null;
    }

    private bool IsMaximized()
    {
        var presenter = GetOverlappedPresenter();
        return presenter is not null && presenter.State == OverlappedPresenterState.Maximized;
    }

    private bool CanMinimize()
    {
        var presenter = GetOverlappedPresenter();
        return IsCustomMinButtonEnabled && presenter is not null && presenter.IsMinimizable;
    }

    private bool CanMaximizeOrRestore()
    {
        var presenter = GetOverlappedPresenter();
        return IsCustomMaxButtonEnabled && presenter is not null && presenter.IsMaximizable;
    }

    private bool CanClose()
    {
        return IsCustomCloseButtonEnabled;
    }

    private void PerformMinimize()
    {
        var presenter = GetOverlappedPresenter();
        if (presenter is not null && presenter.IsMinimizable)
        {
            presenter.Minimize();
        }
    }

    private void OnButtonEnabledStateChanged()
    {
        ReleaseClientPointerCaptures();

        _minState = VisualStateKind.Normal;
        _maxState = VisualStateKind.Normal;
        _closeState = VisualStateKind.Normal;
        _maxNcPressed = false;

        UpdateMaxHitTestMode();
        UpdateToolTips();
        UpdateMaxGlyph();
        ApplyAllVisuals();
        SyncNonClientRegions();
    }

    private void UpdateMaxHitTestMode()
    {
        MaxButtonHost.IsHitTestVisible = !CanMaximizeOrRestore();
    }

    private void UpdateToolTips()
    {
        ToolTipService.SetToolTip(
            MinButtonHost,
            CanMinimize() ? "最小化" : null);

        ToolTipService.SetToolTip(
            CloseButtonHost,
            CanClose() ? "关闭" : null);

        ToolTipService.SetToolTip(
            MaxButtonHost,
            CanMaximizeOrRestore()
                ? (IsMaximized() ? "向下还原" : "最大化")
                : null);
    }

    public void SyncNonClientRegions()
    {
        if (!_attached || !_loaded || _nonClientPointerSource is null || RootGrid.XamlRoot is null)
            return;

        if (_syncingRegions)
            return;

        _syncingRegions = true;

        try
        {
            var passthroughRects = new List<RectInt32>();

            if (TryGetPixelRect(MinButtonHost, out RectInt32 minRect))
            {
                passthroughRects.Add(minRect);
            }

            if (TryGetPixelRect(CloseButtonHost, out RectInt32 closeRect))
            {
                passthroughRects.Add(closeRect);
            }

            if (!CanMaximizeOrRestore() && TryGetPixelRect(MaxButtonHost, out RectInt32 disabledMaxRect))
            {
                passthroughRects.Add(disabledMaxRect);
            }

            _nonClientPointerSource.SetRegionRects(
                NonClientRegionKind.Passthrough,
                passthroughRects.ToArray());

            if (CanMaximizeOrRestore() && TryGetPixelRect(MaxButtonHost, out RectInt32 maxRect))
            {
                _nonClientPointerSource.SetRegionRects(
                    NonClientRegionKind.Maximize,
                    new[] { maxRect });
            }
            else
            {
                _nonClientPointerSource.SetRegionRects(
                    NonClientRegionKind.Maximize,
                    Array.Empty<RectInt32>());
            }
        }
        finally
        {
            _syncingRegions = false;
        }
    }

    public void Dispose()
    {
        ReleaseClientPointerCaptures();
        StopBackgroundAnimations();

        RootGrid.SizeChanged -= AnySizeChanged;
        MinButtonHost.SizeChanged -= AnySizeChanged;
        MaxButtonHost.SizeChanged -= AnySizeChanged;
        CloseButtonHost.SizeChanged -= AnySizeChanged;

        if (_titleBarHost is not null)
        {
            _titleBarHost.SizeChanged -= TitleBarHost_SizeChanged;
        }

        if (_window is not null)
        {
            _window.Activated -= Window_Activated;
        }

        if (_nonClientPointerSource is not null)
        {
            _nonClientPointerSource.PointerEntered -= NonClientPointerSource_PointerEntered;
            _nonClientPointerSource.PointerExited -= NonClientPointerSource_PointerExited;
            _nonClientPointerSource.PointerMoved -= NonClientPointerSource_PointerMoved;
            _nonClientPointerSource.PointerPressed -= NonClientPointerSource_PointerPressed;
            _nonClientPointerSource.PointerReleased -= NonClientPointerSource_PointerReleased;
            _nonClientPointerSource.ExitedMoveSize -= NonClientPointerSource_ExitedMoveSize;
        }

        if (_attached && _subclassProc is not null && _hWnd != IntPtr.Zero)
        {
            RemoveWindowSubclass(_hWnd, _subclassProc, 1);
        }

        if (_appWindow is not null)
        {
            _appWindow.Changed -= AppWindow_Changed;
        }

        _attached = false;
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomCaptionButtons host)
        {
            host.ApplyAllVisuals();
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateMaxGlyph();
            ApplyAllVisuals();
            SyncNonClientRegions();
        });
    }

    private void UpdateMaxGlyph()
    {
        MaxGlyph.Text = IsMaximized() ? "\uE923" : "\uE922";
        UpdateToolTips();
    }

    private Windows.UI.Color GetDisabledForegroundColor()
    {
        return ButtonDisabledForegroundColor;
    }

    private void ApplyAllVisuals()
    {
        ApplyMinVisual();
        ApplyMaxVisual();
        ApplyCloseVisual();
    }

    private void ApplyMinVisual(bool animateHover = false)
    {
        if (!CanMinimize())
        {
            SetBackgroundColor(_minBackgroundBrush, ref _minBackgroundAnimation, Colors.Transparent, animate: false);
            MinGlyph.Foreground = new SolidColorBrush(GetDisabledForegroundColor());
            MinGlyph.Opacity = 1;
            return;
        }

        ApplyStandardVisual(
            MinGlyph,
            _minBackgroundBrush,
            ref _minBackgroundAnimation,
            _minState,
            animateHover);
    }

    private void ApplyMaxVisual(bool animateHover = false)
    {
        if (!CanMaximizeOrRestore())
        {
            SetBackgroundColor(_maxBackgroundBrush, ref _maxBackgroundAnimation, Colors.Transparent, animate: false);
            MaxGlyph.Foreground = new SolidColorBrush(GetDisabledForegroundColor());
            MaxGlyph.Opacity = 1;
            return;
        }

        ApplyStandardVisual(
            MaxGlyph,
            _maxBackgroundBrush,
            ref _maxBackgroundAnimation,
            _maxState,
            animateHover);
    }

    private void ApplyCloseVisual(bool animateHover = false)
    {
        if (!CanClose())
        {
            SetBackgroundColor(_closeBackgroundBrush, ref _closeBackgroundAnimation, Colors.Transparent, animate: false);
            CloseGlyph.Foreground = new SolidColorBrush(GetDisabledForegroundColor());
            CloseGlyph.Opacity = 1;
            return;
        }

        SetBackgroundColor(
            _closeBackgroundBrush,
            ref _closeBackgroundAnimation,
            GetCloseBackground(_closeState),
            animateHover,
            GetHoverAnimationDuration(_closeState));
        CloseGlyph.Foreground = GetCaptionForegroundBrush(GetCloseForeground(_closeState), _closeState);
        CloseGlyph.Opacity = 1;
    }

    private void ApplyStandardVisual(
        TextBlock glyph,
        SolidColorBrush backgroundBrush,
        ref Storyboard? backgroundAnimation,
        VisualStateKind state,
        bool animateHover)
    {
        SetBackgroundColor(
            backgroundBrush,
            ref backgroundAnimation,
            GetStandardBackground(state),
            animateHover,
            GetHoverAnimationDuration(state));
        glyph.Foreground = GetCaptionForegroundBrush(GetStandardForeground(state), state);
        glyph.Opacity = 1;
    }

    private Brush GetCaptionForegroundBrush(Windows.UI.Color activeColor, VisualStateKind state)
    {
        if (_isWindowActive || state != VisualStateKind.Normal)
            return new SolidColorBrush(activeColor);

        if (Application.Current.Resources.TryGetValue(
                InactiveCaptionForegroundResourceKey,
                out object inactiveCaptionForeground) &&
            inactiveCaptionForeground is Brush inactiveCaptionBrush)
        {
            return inactiveCaptionBrush;
        }

        if (_appWindow?.TitleBar.ButtonInactiveForegroundColor is Windows.UI.Color titleBarInactiveColor)
        {
            return new SolidColorBrush(titleBarInactiveColor) { Opacity = InactiveGlyphOpacity };
        }

        // The WinUI theme resource above is normally available. Keep the title bar
        // legible with the same inactive treatment if a host app omits it.
        return new SolidColorBrush(activeColor) { Opacity = InactiveGlyphOpacity };
    }

    private static bool IsHoverTransition(VisualStateKind previous, VisualStateKind next)
    {
        return (previous == VisualStateKind.Normal && next == VisualStateKind.Hover) ||
               (previous == VisualStateKind.Hover && next == VisualStateKind.Normal);
    }

    private TimeSpan GetHoverAnimationDuration(VisualStateKind state)
    {
        return state == VisualStateKind.Hover
            ? GetHoverEnterAnimationDuration()
            : CaptionHoverExitAnimationDuration;
    }

    private TimeSpan GetHoverEnterAnimationDuration()
    {
        if (!_hasRecentPointerSpeed ||
            Stopwatch.GetElapsedTime(_lastPointerMotionTimestamp) > TimeSpan.FromMilliseconds(250))
        {
            return CaptionHoverEnterAnimationDuration;
        }

        // Faster pointer movement makes the hover response shorter. Keep the range
        // bounded so slow movements remain visible and fast ones still interpolate.
        double durationMilliseconds = Math.Clamp(
            80d - (_pointerSpeedPixelsPerSecond / 25d),
            20d,
            80d);
        return TimeSpan.FromMilliseconds(durationMilliseconds);
    }

    private void UpdatePointerMotionSample()
    {
        if (!GetCursorPos(out NativePoint currentPosition))
            return;

        long timestamp = Stopwatch.GetTimestamp();
        if (_hasPointerMotionSample)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(_lastPointerMotionTimestamp, timestamp);
            if (elapsed > TimeSpan.Zero && elapsed <= TimeSpan.FromMilliseconds(250))
            {
                double deltaX = currentPosition.X - _lastPointerPosition.X;
                double deltaY = currentPosition.Y - _lastPointerPosition.Y;
                _pointerSpeedPixelsPerSecond = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) /
                                               elapsed.TotalSeconds;
                _hasRecentPointerSpeed = true;
            }
            else
            {
                _pointerSpeedPixelsPerSecond = 0;
                _hasRecentPointerSpeed = false;
            }
        }

        _lastPointerPosition = currentPosition;
        _lastPointerMotionTimestamp = timestamp;
        _hasPointerMotionSample = true;
    }

    private void SetBackgroundColor(
        SolidColorBrush brush,
        ref Storyboard? currentAnimation,
        Windows.UI.Color targetColor,
        bool animate,
        TimeSpan animationDuration = default)
    {
        // Read the currently rendered value before stopping the previous animation so
        // a quick pointer reversal starts from the on-screen color without jumping.
        Windows.UI.Color currentColor = brush.Color;
        currentAnimation?.Stop();
        currentAnimation = null;

        if (!animate || currentColor == targetColor)
        {
            brush.Color = targetColor;
            return;
        }

        // The base value is the destination. FillBehavior.Stop then leaves the brush
        // at that value after the short pointer-over transition completes.
        brush.Color = targetColor;

        var colorAnimation = new ColorAnimation
        {
            From = currentColor,
            To = targetColor,
            Duration = new Duration(animationDuration == default
                ? CaptionHoverExitAnimationDuration
                : animationDuration),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            EnableDependentAnimation = true,
            FillBehavior = FillBehavior.Stop
        };
        Storyboard.SetTarget(colorAnimation, brush);
        Storyboard.SetTargetProperty(colorAnimation, "Color");

        var storyboard = new Storyboard();
        storyboard.Children.Add(colorAnimation);
        currentAnimation = storyboard;
        storyboard.Begin();
    }

    private void StopBackgroundAnimations()
    {
        _minBackgroundAnimation?.Stop();
        _maxBackgroundAnimation?.Stop();
        _closeBackgroundAnimation?.Stop();
        _minBackgroundAnimation = null;
        _maxBackgroundAnimation = null;
        _closeBackgroundAnimation = null;
    }

    private Windows.UI.Color GetStandardBackground(VisualStateKind state)
    {
        return state switch
        {
            VisualStateKind.Hover => ButtonHoverBackgroundColor,
            VisualStateKind.Pressed => ButtonPressedBackgroundColor,
            _ => ButtonBackgroundColor
        };
    }

    private Windows.UI.Color GetStandardForeground(VisualStateKind state)
    {
        return state switch
        {
            VisualStateKind.Hover => ButtonHoverForegroundColor,
            VisualStateKind.Pressed => ButtonPressedForegroundColor,
            _ => ButtonForegroundColor
        };
    }

    private Windows.UI.Color GetCloseBackground(VisualStateKind state)
    {
        return state switch
        {
            VisualStateKind.Hover => CloseHoverBackgroundColor,
            VisualStateKind.Pressed => ClosePressedBackgroundColor,
            _ => Colors.Transparent
        };
    }

    private Windows.UI.Color GetCloseForeground(VisualStateKind state)
    {
        return state switch
        {
            VisualStateKind.Hover => Colors.White,
            VisualStateKind.Pressed => ClosePressedForegroundColor,
            _ => ButtonForegroundColor
        };
    }

    private void SetClientState(ClientButtonKind kind, VisualStateKind state)
    {
        switch (kind)
        {
            case ClientButtonKind.Minimize:
            {
                if (_minState == state) return;
                bool animateMinHover = IsHoverTransition(_minState, state);
                _minState = state;
                ApplyMinVisual(animateMinHover);
                break;
            }

            case ClientButtonKind.Close:
            {
                if (_closeState == state) return;
                bool animateCloseHover = IsHoverTransition(_closeState, state);
                _closeState = state;
                ApplyCloseVisual(animateCloseHover);
                break;
            }
        }
    }

    private void SetMaxState(VisualStateKind state)
    {
        if (_maxState == state)
            return;

        bool animateHover = IsHoverTransition(_maxState, state);
        _maxState = state;
        ApplyMaxVisual(animateHover);
    }

    private bool IsPointerInside(FrameworkElement element, PointerRoutedEventArgs e)
    {
        Point p = e.GetCurrentPoint(element).Position;
        return p.X >= 0 && p.Y >= 0 && p.X <= element.ActualWidth && p.Y <= element.ActualHeight;
    }

    private void BeginClientPress(ClientButtonKind kind, FrameworkElement element, PointerRoutedEventArgs e)
    {
        _capturedClientButton = kind;
        _capturedPointerId = e.Pointer.PointerId;
        element.CapturePointer(e.Pointer);
        SetClientState(kind, VisualStateKind.Pressed);
        e.Handled = true;
    }

    private void EndClientPress(ClientButtonKind kind, FrameworkElement element, PointerRoutedEventArgs e, Action action)
    {
        if (_capturedClientButton != kind || _capturedPointerId != e.Pointer.PointerId)
            return;

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse &&
            e.GetCurrentPoint(element).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
        {
            return;
        }

        bool inside = IsPointerInside(element, e);

        _capturedClientButton = null;
        _capturedPointerId = null;
        element.ReleasePointerCapture(e.Pointer);

        SetClientState(kind, inside ? VisualStateKind.Hover : VisualStateKind.Normal);

        if (inside)
        {
            action();
        }

        e.Handled = true;
    }

    private void CancelClientPress(ClientButtonKind kind)
    {
        if (_capturedClientButton == kind)
        {
            _capturedClientButton = null;
            _capturedPointerId = null;
        }

        SetClientState(kind, VisualStateKind.Normal);
    }

    private void ReleaseClientPointerCaptures()
    {
        _capturedClientButton = null;
        _capturedPointerId = null;
        MinButtonHost.ReleasePointerCaptures();
        CloseButtonHost.ReleasePointerCaptures();
    }

    private static bool IsPrimaryPointerPress(FrameworkElement element, PointerRoutedEventArgs e)
    {
        return e.Pointer.PointerDeviceType != PointerDeviceType.Mouse ||
               e.GetCurrentPoint(element).Properties.IsLeftButtonPressed;
    }

    private void ClearMaxHoverFromClientSide()
    {
        if (!CanMaximizeOrRestore())
            return;

        if (_maxNcPressed || _maxState != VisualStateKind.Normal)
        {
            _maxNcPressed = false;
            SetMaxState(VisualStateKind.Normal);
        }
    }

    private void ClearInactiveClientHover(ClientButtonKind active)
    {
        if (active != ClientButtonKind.Minimize &&
            _capturedClientButton != ClientButtonKind.Minimize &&
            _minState != VisualStateKind.Normal)
        {
            SetClientState(ClientButtonKind.Minimize, VisualStateKind.Normal);
        }

        if (active != ClientButtonKind.Close &&
            _capturedClientButton != ClientButtonKind.Close &&
            _closeState != VisualStateKind.Normal)
        {
            SetClientState(ClientButtonKind.Close, VisualStateKind.Normal);
        }
    }

    private void SyncClientPointerState(
        ClientButtonKind kind,
        FrameworkElement element,
        PointerRoutedEventArgs e)
    {
        bool inside = IsPointerInside(element, e);

        if (_capturedClientButton == kind)
        {
            SetClientState(kind, inside ? VisualStateKind.Pressed : VisualStateKind.Normal);
            e.Handled = true;
            return;
        }

        SetClientState(kind, inside ? VisualStateKind.Hover : VisualStateKind.Normal);
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_capturedClientButton is not null)
            return;

        if (CanMinimize())
        {
            SetClientState(ClientButtonKind.Minimize, VisualStateKind.Normal);
        }

        if (CanClose())
        {
            SetClientState(ClientButtonKind.Close, VisualStateKind.Normal);
        }
    }

    private void MinButtonHost_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMinimize())
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Minimize);
        SyncClientPointerState(ClientButtonKind.Minimize, MinButtonHost, e);
    }

    private void MinButtonHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMinimize())
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Minimize);
        SyncClientPointerState(ClientButtonKind.Minimize, MinButtonHost, e);
    }

    private void MinButtonHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMinimize() || !IsPrimaryPointerPress(MinButtonHost, e))
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Minimize);
        BeginClientPress(ClientButtonKind.Minimize, MinButtonHost, e);
    }

    private void MinButtonHost_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMinimize())
            return;

        if (_capturedClientButton != ClientButtonKind.Minimize)
        {
            SetClientState(ClientButtonKind.Minimize, VisualStateKind.Normal);
        }
    }

    private void MinButtonHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!CanMinimize())
            return;

        EndClientPress(ClientButtonKind.Minimize, MinButtonHost, e, PerformMinimize);
    }

    private void MinButtonHost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CancelClientPress(ClientButtonKind.Minimize);
    }

    private void MinButtonHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CancelClientPress(ClientButtonKind.Minimize);
    }

    private void CloseButtonHost_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!CanClose())
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Close);
        SyncClientPointerState(ClientButtonKind.Close, CloseButtonHost, e);
    }

    private void CloseButtonHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!CanClose())
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Close);
        SyncClientPointerState(ClientButtonKind.Close, CloseButtonHost, e);
    }

    private void CloseButtonHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!CanClose() || !IsPrimaryPointerPress(CloseButtonHost, e))
            return;

        ClearMaxHoverFromClientSide();
        ClearInactiveClientHover(ClientButtonKind.Close);
        BeginClientPress(ClientButtonKind.Close, CloseButtonHost, e);
    }

    private void CloseButtonHost_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!CanClose())
            return;

        if (_capturedClientButton != ClientButtonKind.Close)
        {
            SetClientState(ClientButtonKind.Close, VisualStateKind.Normal);
        }
    }

    private void CloseButtonHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!CanClose())
            return;

        EndClientPress(ClientButtonKind.Close, CloseButtonHost, e, () => _window?.Close());
    }

    private void CloseButtonHost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CancelClientPress(ClientButtonKind.Close);
    }

    private void CloseButtonHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CancelClientPress(ClientButtonKind.Close);
    }

    private void ResetMaxVisual()
    {
        _maxNcPressed = false;
        SetMaxState(VisualStateKind.Normal);
    }

    private bool IsMaxRegion(NonClientPointerEventArgs args)
    {
        return args.RegionKind == NonClientRegionKind.Maximize;
    }

    private void NonClientPointerSource_PointerEntered(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
    {
        if (!CanMaximizeOrRestore() || !IsMaxRegion(args))
            return;

        SetMaxState(_maxNcPressed ? VisualStateKind.Pressed : VisualStateKind.Hover);
    }

    private void NonClientPointerSource_PointerExited(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
    {
        if (!IsMaxRegion(args))
            return;

        ResetMaxVisual();
    }

    private void NonClientPointerSource_PointerMoved(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
    {
        if (!CanMaximizeOrRestore() || !IsMaxRegion(args))
            return;

        SetMaxState(_maxNcPressed
            ? (args.IsPointInRegion ? VisualStateKind.Pressed : VisualStateKind.Normal)
            : (args.IsPointInRegion ? VisualStateKind.Hover : VisualStateKind.Normal));
    }

    private void NonClientPointerSource_PointerPressed(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
    {
        if (!CanMaximizeOrRestore() || !IsMaxRegion(args))
            return;

        _maxNcPressed = true;
        SetMaxState(VisualStateKind.Pressed);
    }

    private void NonClientPointerSource_PointerReleased(InputNonClientPointerSource sender, NonClientPointerEventArgs args)
    {
        if (!IsMaxRegion(args))
            return;

        _maxNcPressed = false;
        SetMaxState(args.IsPointInRegion ? VisualStateKind.Hover : VisualStateKind.Normal);
    }

    private void NonClientPointerSource_ExitedMoveSize(InputNonClientPointerSource sender, ExitedMoveSizeEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ResetMaxVisual();
            UpdateMaxGlyph();
            SyncNonClientRegions();
        });
    }

    private bool TryGetPixelRect(FrameworkElement element, out RectInt32 rect)
    {
        rect = default;

        if (element.XamlRoot is null ||
            element.Visibility != Visibility.Visible ||
            element.ActualWidth <= 0 ||
            element.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            double scale = element.XamlRoot.RasterizationScale;
            if (scale <= 0)
            {
                scale = 1.0;
            }

            GeneralTransform transform = element.TransformToVisual(null);
            var dipRect = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            rect = new RectInt32(
                (int)Math.Round(dipRect.X * scale),
                (int)Math.Round(dipRect.Y * scale),
                (int)Math.Round(dipRect.Width * scale),
                (int)Math.Round(dipRect.Height * scale));

            return rect.Width > 0 && rect.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private IntPtr WindowSubclassProc(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        if (msg is WM_MOUSEMOVE or WM_NCMOUSEMOVE)
        {
            UpdatePointerMotionSample();
        }

        if (msg is WM_ACTIVATE or WM_NCACTIVATE)
        {
            QueueWindowActivationVisualRefresh();
        }

        switch (msg)
        {
            case WM_CANCELMODE:
            case WM_CAPTURECHANGED:
                ResetMaxVisual();
                break;

            case WM_SIZE:
                DispatcherQueue.TryEnqueue(() =>
                {
                    ResetMaxVisual();
                    UpdateMaxGlyph();
                    ApplyMaxVisual();
                    SyncNonClientRegions();
                });
                break;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private enum VisualStateKind
    {
        Normal,
        Hover,
        Pressed
    }

    private enum ClientButtonKind
    {
        Minimize,
        Close
    }

    private const uint WM_SIZE = 0x0005;
    private const uint WM_ACTIVATE = 0x0006;
    private const uint WM_CANCELMODE = 0x001F;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_CAPTURECHANGED = 0x0215;
    private const uint WM_NCACTIVATE = 0x0086;
    private const uint WM_NCMOUSEMOVE = 0x00A0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SUBCLASSPROC(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SUBCLASSPROC pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
