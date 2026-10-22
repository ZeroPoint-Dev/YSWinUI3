using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace YSWinUI3.Navigation;

/// <summary>
/// Applies an optional, Microsoft Store-style card layout to a NavigationView's
/// left-pane menu items. Default NavigationView styling is restored in other
/// pane modes and when this controller is disposed. The style is opt-in through
/// <see cref="Attach(NavigationView, StoreNavigationOptions?)"/>.
/// </summary>
public sealed class StoreNavigationStyle : IDisposable
{
    public static readonly DependencyProperty SelectedIconProperty =
        DependencyProperty.RegisterAttached(
            "SelectedIcon",
            typeof(IconElement),
            typeof(StoreNavigationStyle),
            new PropertyMetadata(null, SelectedIcon_Changed));

    private static readonly ConditionalWeakTable<NavigationViewItem, StoreNavigationStyle> ItemOwners = new();

    private static readonly DependencyProperty[] ItemLayoutProperties =
    {
        FrameworkElement.WidthProperty,
        FrameworkElement.HeightProperty,
        FrameworkElement.MinHeightProperty,
        Control.PaddingProperty,
        FrameworkElement.MarginProperty,
        FrameworkElement.HorizontalAlignmentProperty,
        Control.HorizontalContentAlignmentProperty,
        Control.VerticalContentAlignmentProperty
    };

    private static readonly DependencyProperty[] ContentProperties =
    {
        ContentControl.ContentProperty,
        ContentControl.ContentTemplateProperty,
        ContentControl.ContentTemplateSelectorProperty
    };

    private readonly NavigationView _navigationView;
    private readonly StoreNavigationOptions _options;
    private readonly UISettings _uiSettings = new();
    private readonly Dictionary<NavigationViewItem, ItemState> _items = new();
    private SolidColorBrush? _accentBrush;
    private object _originalOpenPaneLength = DependencyProperty.UnsetValue;
    private object _originalCompactPaneLength = DependencyProperty.UnsetValue;
    private object _originalPaneOpen = DependencyProperty.UnsetValue;
    private object _originalPaneToggleVisibility = DependencyProperty.UnsetValue;
    private object _originalSelectionIndicatorHeight = DependencyProperty.UnsetValue;
    private long _paneDisplayModeCallback;
    private bool _hasSavedPaneWidth;
    private bool _hasSavedCompactPaneWidth;
    private bool _hasSavedPaneOpen;
    private bool _hasSavedPaneToggleVisibility;
    private bool _hasSavedSelectionIndicatorHeight;
    private bool _hadSelectionIndicatorHeight;
    private bool _isCustomLayoutApplied;
    private bool _disposed;

    private const string SelectionIndicatorHeightResourceKey = "NavigationViewSelectionIndicatorHeight";

    private sealed class ItemState
    {
        public required NavigationViewItem Item { get; init; }
        public required object OriginalIconValue { get; init; }
        public required object[] OriginalContentValues { get; init; }
        public required object[] OriginalLayoutValues { get; init; }
        public required object OriginalIconForeground { get; init; }
        public required object OriginalIconHorizontalAlignment { get; init; }
        public required object OriginalIconVerticalAlignment { get; init; }
        public required object OriginalIconOpacity { get; init; }
        public object SelectedIconForeground { get; init; } = DependencyProperty.UnsetValue;
        public object SelectedIconHorizontalAlignment { get; init; } = DependencyProperty.UnsetValue;
        public object SelectedIconVerticalAlignment { get; init; } = DependencyProperty.UnsetValue;
        public object SelectedIconOpacity { get; init; } = DependencyProperty.UnsetValue;
        public IconElement? OriginalIcon { get; init; }
        public IconElement? SelectedIcon { get; init; }
        public Grid? Surface { get; set; }
        public Grid? IconHost { get; set; }
        public StackPanel? Stack { get; set; }
        public FrameworkElement? LabelElement { get; set; }
        public CompositeTransform? IconTransform { get; set; }
        public Storyboard? OpacityAnimation { get; set; }
        public Storyboard? ScaleAnimation { get; set; }
        public Storyboard? LabelAnimation { get; set; }
        public Storyboard? TranslationAnimation { get; set; }
        public bool? VisualSelectionState { get; set; }
        public bool IsSelectedIconVisualActive { get; set; }
        public bool IsHovered { get; set; }
        public bool IsApplied { get; set; }
    }

    private StoreNavigationStyle(NavigationView navigationView, StoreNavigationOptions options)
    {
        _navigationView = navigationView;
        _options = options;
        ValidateOptions(options);

        _navigationView.Loaded += NavigationView_Loaded;
        _navigationView.SelectionChanged += NavigationView_SelectionChanged;
        _navigationView.DisplayModeChanged += NavigationView_DisplayModeChanged;
        _navigationView.ActualThemeChanged += NavigationView_ActualThemeChanged;
        _paneDisplayModeCallback = _navigationView.RegisterPropertyChangedCallback(
            NavigationView.PaneDisplayModeProperty,
            NavigationView_PaneDisplayModeChanged);
        _uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;

        UpdateAccentColor();
        Refresh();
    }

    /// <summary>Gets the filled or alternate icon shown when a menu item is selected.</summary>
    public static IconElement? GetSelectedIcon(DependencyObject element) =>
        (IconElement?)element.GetValue(SelectedIconProperty);

    /// <summary>Sets the filled or alternate icon shown when a menu item is selected.</summary>
    public static void SetSelectedIcon(DependencyObject element, IconElement? value) =>
        element.SetValue(SelectedIconProperty, value);

    /// <summary>
    /// Enables the custom card layout for this NavigationView when its pane mode
    /// is <see cref="NavigationViewPaneDisplayMode.Left"/>. The returned
    /// controller must be disposed with the owning window.
    /// </summary>
    public static StoreNavigationStyle Attach(
        NavigationView navigationView,
        StoreNavigationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(navigationView);
        return new StoreNavigationStyle(navigationView, options ?? new StoreNavigationOptions());
    }

    /// <summary>Refreshes the layout after menu items are added or removed.</summary>
    public void Refresh()
    {
        if (_disposed)
            return;

        var currentItems = GetNavigationItems().Distinct().ToHashSet();
        foreach (var removedItem in _items.Keys.Where(item => !currentItems.Contains(item)).ToArray())
        {
            RestoreItem(_items[removedItem]);
            _items.Remove(removedItem);
            if (ItemOwners.TryGetValue(removedItem, out var owner) && ReferenceEquals(owner, this))
                ItemOwners.Remove(removedItem);
        }

        foreach (var item in currentItems)
        {
            ItemOwners.Remove(item);
            ItemOwners.Add(item, this);

            if (!_items.TryGetValue(item, out var state))
                _items.Add(item, CreateItemState(item));
            else if (!ReferenceEquals(GetSelectedIcon(item), state.SelectedIcon))
            {
                // SelectedIcon is an attached property and may be replaced at
                // runtime. Detach the old presenter before capturing the new icon.
                RestoreItem(state);
                _items[item] = CreateItemState(item);
            }
        }

        if (_navigationView.PaneDisplayMode == NavigationViewPaneDisplayMode.Left)
            ApplyCustomLayout();
        else
            RestoreCustomLayout();

        RefreshSelection();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _navigationView.Loaded -= NavigationView_Loaded;
        _navigationView.SelectionChanged -= NavigationView_SelectionChanged;
        _navigationView.DisplayModeChanged -= NavigationView_DisplayModeChanged;
        _navigationView.ActualThemeChanged -= NavigationView_ActualThemeChanged;
        _navigationView.UnregisterPropertyChangedCallback(
            NavigationView.PaneDisplayModeProperty,
            _paneDisplayModeCallback);
        _uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;

        RestoreCustomLayout();
        foreach (var state in _items.Values)
            RestoreItem(state);
        foreach (var item in _items.Keys)
        {
            if (ItemOwners.TryGetValue(item, out var owner) && ReferenceEquals(owner, this))
                ItemOwners.Remove(item);
        }
        _items.Clear();
    }

    private static void ValidateOptions(StoreNavigationOptions options)
    {
        if (options.PaneWidth is <= 0 || options.CompactPaneWidth <= 0 ||
            options.ItemWidth <= 0 || options.ItemHeight <= 0 ||
            options.FirstItemTopMargin < 0 || options.SelectionIndicatorHeight <= 0 ||
            options.SurfaceSize <= 0 || options.IconSize <= 0 || options.LabelHeight < 0 ||
            options.LabelFontSize < 0 || options.IconLabelSpacing < 0 ||
            options.UnselectedIconOpacity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Navigation dimensions must be positive and opacity must be between 0 and 1.");
        }
    }

    private IEnumerable<NavigationViewItem> GetNavigationItems()
    {
        foreach (object entry in _navigationView.MenuItems)
            if (entry is NavigationViewItem item)
                yield return item;

        foreach (object entry in _navigationView.FooterMenuItems)
            if (entry is NavigationViewItem item)
                yield return item;

        if (_options.StyleSettingsItem && _navigationView.IsSettingsVisible &&
            _navigationView.SettingsItem is NavigationViewItem settingsItem)
            yield return settingsItem;
    }

    private ItemState CreateItemState(NavigationViewItem item)
    {
        var selectedIcon = GetSelectedIcon(item);
        return new ItemState
        {
            Item = item,
            OriginalIconValue = item.ReadLocalValue(NavigationViewItem.IconProperty),
            OriginalContentValues = ContentProperties.Select(item.ReadLocalValue).ToArray(),
            OriginalLayoutValues = ItemLayoutProperties.Select(item.ReadLocalValue).ToArray(),
            OriginalIconForeground = item.Icon?.ReadLocalValue(IconElement.ForegroundProperty) ?? DependencyProperty.UnsetValue,
            OriginalIconHorizontalAlignment = item.Icon?.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty) ?? DependencyProperty.UnsetValue,
            OriginalIconVerticalAlignment = item.Icon?.ReadLocalValue(FrameworkElement.VerticalAlignmentProperty) ?? DependencyProperty.UnsetValue,
            OriginalIconOpacity = item.Icon?.ReadLocalValue(UIElement.OpacityProperty) ?? DependencyProperty.UnsetValue,
            OriginalIcon = item.Icon,
            SelectedIcon = selectedIcon,
            SelectedIconForeground = selectedIcon?.ReadLocalValue(IconElement.ForegroundProperty) ?? DependencyProperty.UnsetValue,
            SelectedIconHorizontalAlignment = selectedIcon?.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty) ?? DependencyProperty.UnsetValue,
            SelectedIconVerticalAlignment = selectedIcon?.ReadLocalValue(FrameworkElement.VerticalAlignmentProperty) ?? DependencyProperty.UnsetValue,
            SelectedIconOpacity = selectedIcon?.ReadLocalValue(UIElement.OpacityProperty) ?? DependencyProperty.UnsetValue
        };
    }

    private void ApplyCustomLayout()
    {
        if (!_isCustomLayoutApplied)
        {
            _originalOpenPaneLength = _navigationView.ReadLocalValue(NavigationView.OpenPaneLengthProperty);
            _hasSavedPaneWidth = true;
            _originalCompactPaneLength = _navigationView.ReadLocalValue(NavigationView.CompactPaneLengthProperty);
            _hasSavedCompactPaneWidth = true;
            _originalPaneOpen = _navigationView.ReadLocalValue(NavigationView.IsPaneOpenProperty);
            _hasSavedPaneOpen = true;

            if (_options.HidePaneToggle)
            {
                _originalPaneToggleVisibility = _navigationView.ReadLocalValue(NavigationView.IsPaneToggleButtonVisibleProperty);
                _hasSavedPaneToggleVisibility = true;
            }

            var resources = _navigationView.Resources;
            _hadSelectionIndicatorHeight = resources.ContainsKey(SelectionIndicatorHeightResourceKey);
            _originalSelectionIndicatorHeight = _hadSelectionIndicatorHeight
                ? resources[SelectionIndicatorHeightResourceKey]
                : DependencyProperty.UnsetValue;
            _hasSavedSelectionIndicatorHeight = true;

            _isCustomLayoutApplied = true;
        }

        if (_options.PaneWidth is double paneWidth)
            _navigationView.OpenPaneLength = paneWidth;

        _navigationView.CompactPaneLength = _options.CompactPaneWidth;
        _navigationView.IsPaneOpen = true;

        if (_options.HidePaneToggle)
            _navigationView.IsPaneToggleButtonVisible = false;

        _navigationView.Resources[SelectionIndicatorHeightResourceKey] = _options.SelectionIndicatorHeight;

        foreach (var state in _items.Values)
            ApplyItem(state);
    }

    private void ApplyItem(ItemState state)
    {
        if (state.IsApplied)
            return;

        var item = state.Item;
        var content = item.Content;
        var contentTemplate = item.ContentTemplate;
        var contentTemplateSelector = item.ContentTemplateSelector;

        // Remove the native content and icon first so their UIElements can be
        // hosted by the custom centered card.
        item.Content = null;
        item.ContentTemplate = null;
        item.ContentTemplateSelector = null;
        item.Icon = null;

        var iconHost = new Grid
        {
            Width = _options.IconSize,
            Height = _options.IconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconTransform = new CompositeTransform
        {
            CenterX = _options.IconSize / 2,
            CenterY = _options.IconSize / 2
        };
        iconHost.RenderTransform = iconTransform;

        if (state.OriginalIcon is not null)
        {
            state.OriginalIcon.HorizontalAlignment = HorizontalAlignment.Center;
            state.OriginalIcon.VerticalAlignment = VerticalAlignment.Center;
            iconHost.Children.Add(state.OriginalIcon);
        }

        if (state.SelectedIcon is not null && !ReferenceEquals(state.SelectedIcon, state.OriginalIcon))
        {
            state.SelectedIcon.HorizontalAlignment = HorizontalAlignment.Center;
            state.SelectedIcon.VerticalAlignment = VerticalAlignment.Center;
            state.SelectedIcon.Opacity = 0;
            iconHost.Children.Add(state.SelectedIcon);
        }

        var labelElement = CreateLabelElement(content, contentTemplate, contentTemplateSelector);
        labelElement.HorizontalAlignment = HorizontalAlignment.Center;
        labelElement.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = _options.IconLabelSpacing
        };
        stack.Children.Add(iconHost);
        stack.Children.Add(labelElement);

        var surface = new Grid
        {
            Width = _options.SurfaceSize,
            Height = _options.SurfaceSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        surface.Children.Add(stack);

        item.Content = surface;
        item.Width = _options.ItemWidth;
        item.Height = _options.ItemHeight;
        item.MinHeight = _options.ItemHeight;
        item.Padding = new Thickness(0);
        item.Margin = ReferenceEquals(item, _navigationView.MenuItems.FirstOrDefault())
            ? new Thickness(0, _options.FirstItemTopMargin, 0, 0)
            : new Thickness(0);
        item.HorizontalAlignment = HorizontalAlignment.Center;
        item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        item.VerticalContentAlignment = VerticalAlignment.Stretch;

        state.Surface = surface;
        state.IconHost = iconHost;
        state.Stack = stack;
        state.LabelElement = labelElement;
        state.IconTransform = iconTransform;
        state.VisualSelectionState = null;
        state.IsSelectedIconVisualActive = false;
        state.IsApplied = true;

        item.Loaded += Item_Loaded;
        item.SizeChanged += Item_SizeChanged;
        item.PointerPressed += Item_PointerPressed;
        item.PointerReleased += Item_PointerReleased;
        item.PointerCanceled += Item_PointerCanceled;
        item.PointerCaptureLost += Item_PointerCanceled;
        item.PointerEntered += Item_PointerEntered;
        item.PointerExited += Item_PointerExited;
        surface.SizeChanged += Surface_SizeChanged;
    }

    private FrameworkElement CreateLabelElement(object? content, DataTemplate? template, DataTemplateSelector? templateSelector)
    {
        string? text = content as string;
        if ((content is null || text is not null) && template is null && templateSelector is null)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                Height = _options.LabelHeight,
                FontSize = _options.LabelFontSize,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        return new ContentPresenter
        {
            Content = content,
            ContentTemplate = template,
            ContentTemplateSelector = templateSelector,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void RestoreCustomLayout()
    {
        if (!_isCustomLayoutApplied)
            return;

        foreach (var state in _items.Values)
            RestoreItem(state);

        if (_hasSavedPaneWidth)
            RestoreValue(_navigationView, NavigationView.OpenPaneLengthProperty, _originalOpenPaneLength);

        if (_hasSavedCompactPaneWidth)
            RestoreValue(_navigationView, NavigationView.CompactPaneLengthProperty, _originalCompactPaneLength);

        if (_hasSavedPaneOpen)
            RestoreValue(_navigationView, NavigationView.IsPaneOpenProperty, _originalPaneOpen);

        if (_hasSavedPaneToggleVisibility)
            RestoreValue(_navigationView, NavigationView.IsPaneToggleButtonVisibleProperty, _originalPaneToggleVisibility);

        if (_hasSavedSelectionIndicatorHeight)
        {
            if (_hadSelectionIndicatorHeight)
                _navigationView.Resources[SelectionIndicatorHeightResourceKey] = _originalSelectionIndicatorHeight;
            else
                _navigationView.Resources.Remove(SelectionIndicatorHeightResourceKey);
        }

        _hasSavedPaneWidth = false;
        _hasSavedCompactPaneWidth = false;
        _hasSavedPaneOpen = false;
        _hasSavedPaneToggleVisibility = false;
        _hasSavedSelectionIndicatorHeight = false;
        _isCustomLayoutApplied = false;
    }

    private void RestoreItem(ItemState state)
    {
        if (!state.IsApplied)
            return;

        var item = state.Item;
        item.Loaded -= Item_Loaded;
        item.SizeChanged -= Item_SizeChanged;
        item.PointerPressed -= Item_PointerPressed;
        item.PointerReleased -= Item_PointerReleased;
        item.PointerCanceled -= Item_PointerCanceled;
        item.PointerCaptureLost -= Item_PointerCanceled;
        item.PointerEntered -= Item_PointerEntered;
        item.PointerExited -= Item_PointerExited;
        if (state.Surface is not null)
            state.Surface.SizeChanged -= Surface_SizeChanged;

        StopAnimation(state, opacity: true, scale: true, label: true, translation: true);
        if (state.IconHost is not null)
            state.IconHost.Children.Clear();
        if (state.LabelElement is ContentPresenter labelPresenter)
            labelPresenter.Content = null;
        if (state.Stack is not null)
            state.Stack.Children.Clear();
        if (state.Surface is not null)
            state.Surface.Children.Clear();

        item.Content = null;
        item.ContentTemplate = null;
        item.ContentTemplateSelector = null;
        RestoreValue(item, ContentProperties[0], state.OriginalContentValues[0]);
        RestoreValue(item, ContentProperties[1], state.OriginalContentValues[1]);
        RestoreValue(item, ContentProperties[2], state.OriginalContentValues[2]);

        RestoreIconForeground(state.OriginalIcon, state.OriginalIconForeground);
        RestoreIconAlignment(state.OriginalIcon,
            state.OriginalIconHorizontalAlignment,
            state.OriginalIconVerticalAlignment);
        if (state.OriginalIcon is not null)
            RestoreValue(state.OriginalIcon, UIElement.OpacityProperty, state.OriginalIconOpacity);
        if (!ReferenceEquals(state.OriginalIcon, state.SelectedIcon))
        {
            RestoreIconForeground(state.SelectedIcon, state.SelectedIconForeground);
            RestoreIconAlignment(state.SelectedIcon,
                state.SelectedIconHorizontalAlignment,
                state.SelectedIconVerticalAlignment);
            if (state.SelectedIcon is not null)
                RestoreValue(state.SelectedIcon, UIElement.OpacityProperty, state.SelectedIconOpacity);
        }
        RestoreValue(item, NavigationViewItem.IconProperty, state.OriginalIconValue);

        for (var index = 0; index < ItemLayoutProperties.Length; index++)
            RestoreValue(item, ItemLayoutProperties[index], state.OriginalLayoutValues[index]);

        state.Surface = null;
        state.IconHost = null;
        state.Stack = null;
        state.LabelElement = null;
        state.IconTransform = null;
        state.VisualSelectionState = null;
        state.IsSelectedIconVisualActive = false;
        state.IsApplied = false;
        state.IsHovered = false;
    }

    private static void RestoreValue(DependencyObject target, DependencyProperty property, object value)
    {
        if (ReferenceEquals(value, DependencyProperty.UnsetValue))
        {
            target.ClearValue(property);
            return;
        }

        if (value is BindingBase binding && target is FrameworkElement element)
            element.SetBinding(property, binding);
        else
            target.SetValue(property, value);
    }

    private static void RestoreIconForeground(IconElement? icon, object value)
    {
        if (icon is not null)
            RestoreValue(icon, IconElement.ForegroundProperty, value);
    }

    private static void RestoreIconAlignment(IconElement? icon, object horizontal, object vertical)
    {
        if (icon is null)
            return;

        RestoreValue(icon, FrameworkElement.HorizontalAlignmentProperty, horizontal);
        RestoreValue(icon, FrameworkElement.VerticalAlignmentProperty, vertical);
    }

    private void RefreshSelection(bool animateSelectionChanges = false)
    {
        foreach (var state in _items.Values)
            UpdateItemVisual(
                state,
                ReferenceEquals(_navigationView.SelectedItem, state.Item) || state.Item.IsSelected,
                animateSelectionChanges);
    }

    private void UpdateItemVisual(ItemState state, bool selected, bool animateSelectionChange)
    {
        if (!state.IsApplied)
            return;

        var previousSelection = state.VisualSelectionState;
        state.VisualSelectionState = selected;

        if (!previousSelection.HasValue)
        {
            SetItemSelectionVisual(state, selected);
            return;
        }

        // Refresh can run again from a loaded, theme, or layout notification
        // while a selection transition is in flight. Preserve that transition
        // unless the actual selected item changed.
        if (previousSelection.Value == selected)
            return;

        if (!animateSelectionChange || !_uiSettings.AnimationsEnabled)
        {
            SetItemSelectionVisual(state, selected);
            return;
        }

        StopAnimation(state, label: true, translation: true);

        if (state.LabelElement is not null)
            state.LabelElement.Visibility = Visibility.Visible;

        if (selected)
        {
            // Keep the outline icon in place while the text fades. Once the
            // label is gone, switch to the filled icon and dive it into the
            // center of the card, matching FDMCL's left-navigation motion.
            AnimateLabelOpacity(state, 0, TimeSpan.FromMilliseconds(125), () =>
            {
                if (!state.IsApplied || state.VisualSelectionState != true)
                    return;

                ApplyIconSelectionVisual(state, selected: true);
                AnimateIconTranslation(state, 8, TimeSpan.FromMilliseconds(190));
            });
            return;
        }

        // Restore the outline icon immediately while the label returns and the
        // icon rises back to its original position.
        ApplyIconSelectionVisual(state, selected: false);
        AnimateLabelOpacity(state, 1, TimeSpan.FromMilliseconds(150));
        AnimateIconTranslation(state, 0, TimeSpan.FromMilliseconds(190));
    }

    private void SetItemSelectionVisual(ItemState state, bool selected)
    {
        StopAnimation(state, label: true, translation: true);
        ApplyIconSelectionVisual(state, selected);

        if (state.LabelElement is not null)
        {
            state.LabelElement.Visibility = Visibility.Visible;
            state.LabelElement.Opacity = selected ? 0 : 1;
        }

        if (state.IconTransform is not null)
            state.IconTransform.TranslateY = selected ? 8 : 0;
    }

    private void ApplyIconSelectionVisual(ItemState state, bool selected)
    {
        bool hasSeparateSelectedIcon = state.SelectedIcon is not null &&
                                       !ReferenceEquals(state.SelectedIcon, state.OriginalIcon);
        double normalOpacity = state.IsHovered ? 1 : _options.UnselectedIconOpacity;

        StopAnimation(state, opacity: true);

        if (state.OriginalIcon is not null)
            state.OriginalIcon.Opacity = hasSeparateSelectedIcon && selected
                ? 0
                : (selected ? 1 : normalOpacity);

        if (hasSeparateSelectedIcon && state.SelectedIcon is not null)
            state.SelectedIcon.Opacity = selected ? 1 : 0;

        var tintedIcon = hasSeparateSelectedIcon ? state.SelectedIcon : state.OriginalIcon;
        if (tintedIcon is not null)
        {
            if (selected)
            {
                UpdateAccentColor();
                tintedIcon.Foreground = _accentBrush;
            }
            else if (ReferenceEquals(tintedIcon, state.SelectedIcon))
            {
                RestoreIconForeground(tintedIcon, state.SelectedIconForeground);
            }
            else
            {
                RestoreIconForeground(tintedIcon, state.OriginalIconForeground);
            }
        }

        state.IsSelectedIconVisualActive = selected;
    }

    private void UpdateAccentColor()
    {
        if (_disposed)
            return;

        var accentType = _navigationView.ActualTheme == ElementTheme.Dark
            ? UIColorType.AccentLight1
            : UIColorType.AccentDark1;
        Color color = _uiSettings.GetColorValue(accentType);

        if (_accentBrush is null)
            _accentBrush = new SolidColorBrush(color);
        else
            _accentBrush.Color = color;

        foreach (var state in _items.Values)
        {
            if (!state.IsApplied || !state.IsSelectedIconVisualActive)
                continue;

            var icon = state.SelectedIcon is not null && !ReferenceEquals(state.SelectedIcon, state.OriginalIcon)
                ? state.SelectedIcon
                : state.OriginalIcon;
            if (icon is not null)
                icon.Foreground = _accentBrush;
        }
    }

    private bool IsSelected(NavigationViewItem item) =>
        ReferenceEquals(_navigationView.SelectedItem, item) || item.IsSelected;

    private void CenterSurface(ItemState state)
    {
        if (!state.IsApplied || state.Surface is null || state.Item.ActualWidth <= 0 ||
            state.Surface.ActualWidth <= 0 || state.Surface.ActualHeight <= 0)
            return;

        state.Surface.RenderTransform = null;
        try
        {
            var center = state.Surface.TransformToVisual(state.Item).TransformPoint(
                new Windows.Foundation.Point(state.Surface.ActualWidth / 2, state.Surface.ActualHeight / 2));
            state.Surface.RenderTransform = new TranslateTransform
            {
                X = state.Item.ActualWidth / 2 - center.X
            };
        }
        catch (InvalidOperationException)
        {
            // NavigationView can disconnect item templates while changing modes;
            // Loaded and SizeChanged recenter the tile after layout resumes.
        }
    }

    private ItemState? FindState(object sender) =>
        sender is NavigationViewItem item && _items.TryGetValue(item, out var state) ? state : null;

    private void NavigationView_Loaded(object sender, RoutedEventArgs args) => Refresh();

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) =>
        RefreshSelection(animateSelectionChanges: true);

    private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        foreach (var state in _items.Values)
            CenterSurface(state);
    }

    private void NavigationView_ActualThemeChanged(FrameworkElement sender, object args) => UpdateAccentColor();

    private static void SelectedIcon_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is NavigationViewItem item && ItemOwners.TryGetValue(item, out var owner))
            owner.Refresh();
    }

    private void NavigationView_PaneDisplayModeChanged(DependencyObject sender, DependencyProperty property) => Refresh();

    private void UISettings_ColorValuesChanged(UISettings sender, object args) =>
        _navigationView.DispatcherQueue.TryEnqueue(UpdateAccentColor);

    private void Item_Loaded(object sender, RoutedEventArgs args)
    {
        if (FindState(sender) is { } state)
            CenterSurface(state);
    }

    private void Item_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (FindState(sender) is { } state)
            CenterSurface(state);
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        var state = _items.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.Surface, sender));
        if (state is not null)
            CenterSurface(state);
    }

    private void Item_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (FindState(sender) is not { } state)
            return;

        AnimateIconScale(state, 0.93, TimeSpan.FromMilliseconds(120));
        SetAnimatedIconState(state, IsSelected(state.Item) ? "PressedSelected" : "Pressed");
    }

    private void Item_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (FindState(sender) is not { } state)
            return;

        AnimateIconScale(state, 1, TimeSpan.FromMilliseconds(180));
        if (!IsSelected(state.Item))
            SetAnimatedIconState(state, "Normal");
    }

    private void Item_PointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (FindState(sender) is not { } state)
            return;

        state.IsHovered = false;
        AnimateNavigationItemHoverOpacity(state, hovered: false);
        AnimateIconScale(state, 1, TimeSpan.FromMilliseconds(180));
        SetAnimatedIconState(state, "Normal");
    }

    private void Item_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (FindState(sender) is { } state)
        {
            state.IsHovered = true;
            AnimateNavigationItemHoverOpacity(state, hovered: true);
            SetAnimatedIconState(state, IsSelected(state.Item) ? "PointerOverSelected" : "PointerOver");
        }
    }

    private void Item_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (FindState(sender) is { } state)
        {
            state.IsHovered = false;
            AnimateNavigationItemHoverOpacity(state, hovered: false);
            SetAnimatedIconState(state, "Normal");
        }
    }

    private void AnimateNavigationItemHoverOpacity(ItemState state, bool hovered)
    {
        if (!state.IsApplied || IsSelected(state.Item) || state.OriginalIcon is null)
            return;

        AnimateIconOpacity(
            state,
            hovered ? 1 : _options.UnselectedIconOpacity,
            TimeSpan.FromMilliseconds(hovered ? 150 : 180));
    }

    private static void SetAnimatedIconState(ItemState state, string stateName)
    {
        if (state.OriginalIcon is AnimatedIcon animatedIcon)
            AnimatedIcon.SetState(animatedIcon, stateName);
    }

    private void AnimateIconOpacity(ItemState state, double target, TimeSpan duration)
    {
        if (state.OriginalIcon is null)
            return;

        StopAnimation(state, opacity: true);
        if (!_uiSettings.AnimationsEnabled)
        {
            state.OriginalIcon.Opacity = target;
            return;
        }

        var from = state.OriginalIcon.Opacity;
        var animation = new DoubleAnimation
        {
            From = from,
            To = target,
            Duration = new Duration(duration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, state.OriginalIcon);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        state.OpacityAnimation = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(state.OpacityAnimation, storyboard))
                return;

            storyboard.Stop();
            state.OriginalIcon.Opacity = target;
            state.OpacityAnimation = null;
        };
        storyboard.Begin();
    }

    private void AnimateIconScale(ItemState state, double target, TimeSpan duration)
    {
        var transform = state.IconTransform;
        if (transform is null)
            return;

        StopAnimation(state, scale: true);
        if (!_uiSettings.AnimationsEnabled)
        {
            transform.ScaleX = target;
            transform.ScaleY = target;
            return;
        }

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var scaleX = new DoubleAnimation
        {
            From = transform.ScaleX,
            To = target,
            Duration = new Duration(duration),
            EasingFunction = easing,
            EnableDependentAnimation = true
        };
        var scaleY = new DoubleAnimation
        {
            From = transform.ScaleY,
            To = target,
            Duration = new Duration(duration),
            EasingFunction = easing,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(scaleX, transform);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");
        Storyboard.SetTarget(scaleY, transform);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");

        var storyboard = new Storyboard();
        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);
        state.ScaleAnimation = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(state.ScaleAnimation, storyboard))
                return;

            storyboard.Stop();
            transform.ScaleX = target;
            transform.ScaleY = target;
            state.ScaleAnimation = null;
        };
        storyboard.Begin();
    }

    private void AnimateLabelOpacity(ItemState state, double target, TimeSpan duration, Action? completed = null)
    {
        if (state.LabelElement is null)
            return;

        StopAnimation(state, label: true);
        var animation = new DoubleAnimation
        {
            From = state.LabelElement.Opacity,
            To = target,
            Duration = new Duration(duration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, state.LabelElement);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        state.LabelAnimation = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(state.LabelAnimation, storyboard))
                return;

            storyboard.Stop();
            state.LabelElement.Opacity = target;
            state.LabelAnimation = null;
            completed?.Invoke();
        };
        storyboard.Begin();
    }

    private void AnimateIconTranslation(ItemState state, double target, TimeSpan duration)
    {
        var transform = state.IconTransform;
        if (transform is null)
            return;

        StopAnimation(state, translation: true);
        var animation = new DoubleAnimation
        {
            From = transform.TranslateY,
            To = target,
            Duration = new Duration(duration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, "TranslateY");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        state.TranslationAnimation = storyboard;
        storyboard.Completed += (_, _) =>
        {
            if (!ReferenceEquals(state.TranslationAnimation, storyboard))
                return;

            storyboard.Stop();
            transform.TranslateY = target;
            state.TranslationAnimation = null;
        };
        storyboard.Begin();
    }

    private static void StopAnimation(
        ItemState state,
        bool opacity = false,
        bool scale = false,
        bool label = false,
        bool translation = false)
    {
        if (opacity && state.OpacityAnimation is { } opacityAnimation)
        {
            opacityAnimation.Stop();
            state.OpacityAnimation = null;
        }

        if (scale && state.ScaleAnimation is { } scaleAnimation)
        {
            scaleAnimation.Stop();
            state.ScaleAnimation = null;
        }

        if (label && state.LabelAnimation is { } labelAnimation)
        {
            labelAnimation.Stop();
            state.LabelAnimation = null;
        }

        if (translation && state.TranslationAnimation is { } translationAnimation)
        {
            translationAnimation.Stop();
            state.TranslationAnimation = null;
        }
    }
}
