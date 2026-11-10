using System.Windows;
namespace Threadlet;
public partial class App : Application
{
    public bool LaunchWindowOnStartup { get; set; } = true;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (LaunchWindowOnStartup) { MainWindow = new MainWindow(); MainWindow.Show(); }
    }
}
