using Microsoft.UI.Xaml;
using YSWinUI3.Window;

namespace YSWinUI3.Settings;

public sealed class YSAppSettings
{
    public ElementTheme Theme { get; set; } = ElementTheme.Default;
    public BackdropKind Backdrop { get; set; } = BackdropKind.MicaAlt;
    public string NavigationMode { get; set; } = "Auto";
}
