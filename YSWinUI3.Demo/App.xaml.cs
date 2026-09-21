using Microsoft.UI.Xaml;

namespace YSWinUI3.Demo;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }
}
