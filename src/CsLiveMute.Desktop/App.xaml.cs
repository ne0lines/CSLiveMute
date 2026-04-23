using System.Windows;
using CsLiveMute.Desktop.Infrastructure;
using CsLiveMute.Desktop.Services;
using CsLiveMute.Desktop.ViewModels;

namespace CsLiveMute.Desktop;

public partial class App : Application
{
    private AppRuntime? _runtime;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var settingsStore = new AppSettingsStore();
        _runtime = new AppRuntime(settingsStore);
        await _runtime.InitializeAsync();

        var viewModel = new MainWindowViewModel(_runtime);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
        }

        base.OnExit(e);
    }
}
