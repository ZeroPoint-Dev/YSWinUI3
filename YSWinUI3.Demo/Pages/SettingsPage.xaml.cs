using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace YSWinUI3.Demo.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _backdropComboReady;
    private bool _themeComboReady;
    private bool _navComboReady;

    public SettingsPage() => InitializeComponent();

    private void BackdropModeComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        _backdropComboReady = false;
        SelectByTag(BackdropModeComboBox, App.MainWindowInstance?.Settings.Backdrop.ToString() ?? "MicaAlt", "MicaAlt");
        _backdropComboReady = true;
    }

    private void BackdropModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_backdropComboReady && BackdropModeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            App.MainWindowInstance?.ApplyBackdrop(mode);
    }

    private void ThemeComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        _themeComboReady = false;
        SelectByTag(ThemeComboBox, App.MainWindowInstance?.Settings.Theme.ToString() ?? "Default", "Default");
        _themeComboReady = true;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_themeComboReady && ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            App.MainWindowInstance?.ApplyTheme(theme);
    }

    private void NavModeComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        _navComboReady = false;
        SelectByTag(NavModeComboBox, App.MainWindowInstance?.Settings.NavigationMode ?? "Auto", "Auto");
        _navComboReady = true;
    }

    private void NavModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navComboReady && NavModeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string mode)
            App.MainWindowInstance?.ApplyNavMode(mode);
    }

    private static void SelectByTag(ComboBox comboBox, string tag, string fallback)
    {
        ComboBoxItem? fallbackItem = null;
        foreach (object entry in comboBox.Items)
        {
            if (entry is not ComboBoxItem item) continue;
            if (item.Tag as string == fallback) fallbackItem = item;
            if (item.Tag as string == tag) { comboBox.SelectedItem = item; return; }
        }
        comboBox.SelectedItem = fallbackItem;
    }
}
