using System;

namespace YSWinUI3.Window;

public sealed class YSWindowOptions
{
    public double CaptionButtonHeight { get; set; } = 48;
    public double CaptionButtonWidth { get; set; } = 46;
    public int MinimumWidth { get; set; } = 800;
    public int MinimumHeight { get; set; } = 600;
    public int InitialWidth { get; set; } = 1200;
    public int InitialHeight { get; set; } = 800;
    public bool CenterOnStartup { get; set; } = true;
    public bool EnableWindowMenu { get; set; } = true;
    public bool EnablePaintSuppression { get; set; } = true;
    public bool IsMinimizable { get; set; } = true;
    public bool IsMaximizable { get; set; } = true;
    public bool IsResizable { get; set; } = true;
    public bool IsClosable { get; set; } = true;
    public string? IconPath { get; set; }
    public BackdropKind Backdrop { get; set; } = BackdropKind.MicaAlt;
    public bool UseSystemTheme { get; set; } = true;
    public bool MonitorExternalBackdropChanges { get; set; } = true;
    public TimeSpan BackdropMonitorInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
