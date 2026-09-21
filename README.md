# YSWinUI3

YSWinUI3 是面向 WinUI 3 桌面应用的窗口基础类库，提供可复用的窗口配置、无边框标题栏、系统窗口按钮、DWM 材质、主题同步、窗口菜单、窗口尺寸控制和导航辅助功能。

## 环境要求

- Windows 10 1809（Build 17763）或更高版本
- .NET 8
- WinUI 3 / Windows App SDK 1.8
- x64 架构

## 安装

```xml
<PackageReference Include="YSWinUI3" Version="0.1.0" />
```

类库依赖 Windows App SDK。调用程序仍需要按照自己的部署方式配置 Windows App SDK 初始化和运行时。

## 窗口配置

### 1. 添加标题栏布局

窗口需要把内容延伸到标题栏，并在标题栏布局中放置 `CustomCaptionButtons`。标题栏宿主可以使用任意 `FrameworkElement`，通常使用一个位于窗口顶部的 `Grid`。

```xml
<Window
    x:Class="SampleApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ys="using:YSWinUI3.Controls">

    <Grid x:Name="RootGrid" Background="Transparent">
        <Frame x:Name="ContentFrame" />

        <Grid x:Name="AppTitleBar"
              Height="48"
              VerticalAlignment="Top"
              Canvas.ZIndex="1"
              Background="Transparent">
            <TextBlock Text="Sample App"
                       Margin="16,0,0,0"
                       VerticalAlignment="Center" />

            <ys:CustomCaptionButtons x:Name="CaptionButtons"
                                     HorizontalAlignment="Right"
                                     VerticalAlignment="Stretch" />
        </Grid>
    </Grid>
</Window>
```

标题栏宿主必须覆盖窗口顶部区域，并且不要把自定义标题栏按钮放在另一个会拦截鼠标输入的控件后面。

### 2. 在窗口构造函数中附加控制器

```csharp
using Microsoft.UI.Xaml;
using YSWinUI3.Controls;
using YSWinUI3.Window;

public sealed partial class MainWindow : Window
{
    private readonly YSWindowController _windowController;

    public MainWindow()
    {
        InitializeComponent();

        _windowController = YSWindowController.Attach(
            this,
            RootGrid,
            AppTitleBar,
            CaptionButtons,
            new YSWindowOptions
            {
                CaptionButtonHeight = 48,
                CaptionButtonWidth = 46,
                InitialWidth = 1200,
                InitialHeight = 800,
                MinimumWidth = 800,
                MinimumHeight = 600,
                CenterOnStartup = true,
                Backdrop = BackdropKind.MicaAlt,
                UseSystemTheme = true,
                MonitorExternalBackdropChanges = true,
                IsMinimizable = true,
                IsMaximizable = true,
                IsResizable = true,
                IsClosable = true,
                EnableWindowMenu = true,
                EnablePaintSuppression = true
            });

        Closed += (_, _) => _windowController.Dispose();
    }
}
```

`Attach` 会完成标题栏接管、系统窗口按钮配置、窗口最小尺寸、窗口居中、系统菜单、DWM 材质应用、主题同步、材质变化检测和原生消息清理。

## 材质与主题

可用材质由 `BackdropKind` 定义：

```csharp
_windowController.ApplyBackdrop(BackdropKind.MicaAlt);
_windowController.ApplyBackdrop(BackdropKind.Mica);
_windowController.ApplyBackdrop(BackdropKind.Acrylic);
_windowController.ApplyBackdrop(BackdropKind.None);
```

类库会同步 WinUI `SystemBackdrop` 和 DWM 窗口材质。默认启用外部材质检测；检测到窗口材质发生变化时会重新应用 DWM 设置，并通过 `BackdropChanged` 通知调用方：

```csharp
_windowController.BackdropChanged += (_, backdrop) =>
{
    // 根据 backdrop 更新应用状态或设置界面。
};
```

主题可直接应用到窗口根元素和标题栏按钮：

```csharp
_windowController.ApplyTheme(ElementTheme.Dark);
_windowController.ApplyTheme(ElementTheme.Light);
_windowController.ApplyTheme(ElementTheme.Default);
```

## 标题栏按钮

`CustomCaptionButtons` 提供最小化、最大化和关闭按钮，按钮尺寸可以在 XAML 或代码中设置：

```csharp
CaptionButtons.ButtonHeight = 52;
CaptionButtons.ButtonWidth = 48;
```

也可以按窗口能力禁用按钮：

```csharp
CaptionButtons.IsCustomMinButtonEnabled = true;
CaptionButtons.IsCustomMaxButtonEnabled = false;
CaptionButtons.IsCustomCloseButtonEnabled = true;
```

控件支持主题刷新：

```csharp
CaptionButtons.RefreshForTheme(ElementTheme.Dark);
```

## 窗口操作

```csharp
_windowController.CenterAndResize(1440, 900);
_windowController.ShowSystemMenu(new Windows.Foundation.Point(0, 48));
```

窗口右键系统菜单默认启用。若应用需要自行实现菜单，可设置 `EnableWindowMenu = false`。

## 导航辅助

`NavigationService` 可以把 `NavigationView` 与 `Frame` 连接起来，并自动同步选中项和返回状态：

```csharp
using YSWinUI3.Navigation;

private NavigationService _navigation = null!;

private void ConfigureNavigation()
{
    _navigation = new NavigationService(RootNavigationView, ContentFrame)
        .Register("home", typeof(HomePage))
        .Register("about", typeof(AboutPage))
        .RegisterSettings(typeof(SettingsPage));

    _navigation.Navigate("home");
}
```

窗口关闭时释放服务：

```csharp
_navigation.Dispose();
```

## 设置持久化

`YSSettingsStore` 使用应用的本地数据目录保存 JSON 设置：

```csharp
using YSWinUI3.Settings;
using YSWinUI3.Window;

var store = new YSSettingsStore("SampleApp");
var settings = store.Load<AppSettings>();

settings.Backdrop = BackdropKind.MicaAlt;
store.Save(settings);
```

`YSAppSettings` 可直接作为设置模型；也可以使用自己的可序列化设置类型：

```csharp
public sealed class AppSettings
{
    public ElementTheme Theme { get; set; } = ElementTheme.Default;
    public BackdropKind Backdrop { get; set; } = BackdropKind.MicaAlt;
    public bool IsCompactMode { get; set; }
}
```

## 生命周期注意事项

- 在 `InitializeComponent()` 完成后调用 `YSWindowController.Attach`。
- 传入的根元素、标题栏宿主和 `CustomCaptionButtons` 必须属于当前窗口的可视化树。
- 在窗口 `Closed` 事件中调用 `Dispose()`。
- 若手动改变系统材质，调用 `SynchronizeBackdrop()` 可立即同步 DWM 状态。
- 使用自定义图标时，把图标路径传给 `YSWindowOptions.IconPath`。

## 示例项目

解决方案中的 `YSWinUI3.Demo` 展示了窗口标题栏、导航、主题切换、材质切换、系统菜单和设置持久化的完整用法。
