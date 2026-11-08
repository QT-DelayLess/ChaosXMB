using System.Windows;

namespace ChaosXMB;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Beklenmeyen hata: {args.Exception.Message}", "ChaosXMB", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
