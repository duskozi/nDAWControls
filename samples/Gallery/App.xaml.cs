using System.Windows;

namespace nDAWControls.Gallery;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // nDAWControls.Gallery.exe --screenshots <folder> saves a PNG of every catalog entry and of both
        // gallery views, in every skin, and exits. The documentation website uses these images.
        if (e.Args.Length >= 2 && e.Args[0] == "--screenshots")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = Screenshots.RunAsync(e.Args[1]);
            return;
        }

        new MainWindow().Show();
    }
}
