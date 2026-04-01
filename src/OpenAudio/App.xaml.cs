using System.Windows;
using OpenAudio.Services;
using OpenAudio.ViewModels;

namespace OpenAudio;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logger = new SessionLogger();
        var vbCableDetector = new VbCableDetector(logger);
        var audioDeviceService = new AudioDeviceService(vbCableDetector, logger);
        var applicationSourceService = new ApplicationSourceService(vbCableDetector, logger);

        _mainViewModel = new MainViewModel(vbCableDetector, audioDeviceService, applicationSourceService, logger);

        MainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        base.OnExit(e);
    }
}

