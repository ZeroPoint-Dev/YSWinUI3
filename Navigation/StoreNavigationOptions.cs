namespace YSWinUI3.Navigation;

/// <summary>Layout options for the opt-in Microsoft Store-style left navigation.</summary>
public sealed class StoreNavigationOptions
{
    /// <summary>Open-pane width while the custom left navigation is active. Set null to keep the current width.</summary>
    public double? PaneWidth { get; set; } = 72;
    public double CompactPaneWidth { get; set; } = 48;

    public double ItemWidth { get; set; } = 72;
    public double ItemHeight { get; set; } = 68;
    public double FirstItemTopMargin { get; set; } = 3;
    public double SurfaceSize { get; set; } = 64;
    public double IconSize { get; set; } = 24;
    public double LabelHeight { get; set; } = 14;
    public double LabelFontSize { get; set; } = 11;
    public double IconLabelSpacing { get; set; } = 2;
    public double UnselectedIconOpacity { get; set; } = 0.68;
    public double SelectionIndicatorHeight { get; set; } = 24;

    /// <summary>Include the built-in NavigationView settings item when it is visible.</summary>
    public bool StyleSettingsItem { get; set; } = true;

    /// <summary>Hide the pane toggle while the custom left-pane layout is active.</summary>
    public bool HidePaneToggle { get; set; } = true;
}
