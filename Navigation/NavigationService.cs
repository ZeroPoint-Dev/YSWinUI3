using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace YSWinUI3.Navigation;

public sealed class NavigationService : IDisposable
{
    private readonly NavigationView _navigationView;
    private readonly Frame _frame;
    private readonly Dictionary<string, Type> _routes = new(StringComparer.OrdinalIgnoreCase);
    private Type? _settingsPage;

    public NavigationService(NavigationView navigationView, Frame frame)
    {
        _navigationView = navigationView;
        _frame = frame;
        _navigationView.SelectionChanged += OnSelectionChanged;
        _navigationView.BackRequested += OnBackRequested;
        _frame.Navigated += OnNavigated;
    }

    public NavigationService Register(string tag, Type pageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(pageType);
        _routes[tag] = pageType;
        return this;
    }

    public NavigationService RegisterSettings(Type pageType)
    {
        _settingsPage = pageType;
        return this;
    }

    public bool Navigate(string tag, object? parameter = null)
    {
        if (!_routes.TryGetValue(tag, out Type? pageType)) return false;
        return Navigate(pageType, parameter);
    }

    public bool Navigate(Type pageType, object? parameter = null)
    {
        if (_frame.CurrentSourcePageType == pageType) return false;
        return _frame.Navigate(pageType, parameter);
    }

    public bool GoBack()
    {
        if (!_frame.CanGoBack) return false;
        _frame.GoBack();
        return true;
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => GoBack();

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected && _settingsPage is not null)
        {
            Navigate(_settingsPage);
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is not null)
            Navigate(item.Tag.ToString()!);
    }

    private void OnNavigated(object sender, NavigationEventArgs args)
    {
        _navigationView.IsBackEnabled = _frame.CanGoBack;
        if (_settingsPage == args.SourcePageType)
        {
            _navigationView.SelectedItem = _navigationView.SettingsItem;
            return;
        }

        foreach (object item in _navigationView.MenuItems)
        {
            if (item is NavigationViewItem navigationItem &&
                navigationItem.Tag is not null &&
                _routes.TryGetValue(navigationItem.Tag.ToString()!, out Type? pageType) &&
                pageType == args.SourcePageType)
            {
                _navigationView.SelectedItem = navigationItem;
                break;
            }
        }
    }

    public void Dispose()
    {
        _navigationView.SelectionChanged -= OnSelectionChanged;
        _navigationView.BackRequested -= OnBackRequested;
        _frame.Navigated -= OnNavigated;
    }
}
