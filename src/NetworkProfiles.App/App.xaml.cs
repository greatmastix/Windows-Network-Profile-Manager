using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NetworkProfiles.App.Services;
using NetworkProfiles.App.ViewModels;
using NetworkProfiles.Core.Execution;
using NetworkProfiles.Core.Storage;

namespace NetworkProfiles.App;

public partial class App : Application
{
    private TrayIcon? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private readonly DispatcherTimer _autoSwitchTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;

    // Per session, so each signed-in user gets their own instance.
    private const string InstanceName = @"Local\NetworkProfiles.App";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

        // Without the service, adapter changes need administrator rights: restart elevated (one UAC prompt).
        // Done before the single-instance check, so the elevated copy isn't mistaken for a second one.
        if (!AdapterSettingsWriter.IsElevated && !Elevation.IsServiceInstalled && Elevation.TryRestartElevated(e.Args))
        {
            Shutdown();
            return;
        }

        // Only one copy runs; starting it again brings the existing window forward.
        _singleInstance = new Mutex(initiallyOwned: true, InstanceName, out var isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!isFirst)
        {
            if (!e.Args.Contains(StartupRegistration.MinimizedArgument))
                _showSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_showSignal,
            (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, Timeout.Infinite, executeOnlyOnce: false);

        // The app keeps running in the tray when the window is closed.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var adapterWriter = new AdapterSettingsWriter(new NetworkConfigurator(new NetshRunner()));
        _viewModel = new MainViewModel(
            new ProfileStore(ProfileStore.DefaultPath),
            AppSettings.DefaultPath,
            new AdapterService(),
            adapterWriter,
            new ProfileApplier(adapterWriter, new ProxyService()),
            new WindowDialogService());

        _window = new MainWindow(_viewModel);
        MainWindow = _window;
        _tray = new TrayIcon(_viewModel, ShowMainWindow, () => _window.ShowRules(), ExitApp);
        _window.HiddenToTray += OnHiddenToTray;
        _viewModel.Notification += (_, n) =>
        {
            // The window shows the status line itself; only pop up a notification when it is hidden.
            if (_window?.IsVisible != true)
                _tray?.ShowMessage(n.Message, n.IsError);
        };

        _viewModel.Load();

        _autoSwitchTimer.Tick += async (_, _) => await _viewModel.CheckAutoSwitchAsync();
        _autoSwitchTimer.Start();

        // Started at sign-in: stay in the tray.
        if (!e.Args.Contains(StartupRegistration.MinimizedArgument))
            _window.Show();
    }

    private void OnHiddenToTray(object? sender, EventArgs e)
    {
        if (_viewModel is null || _viewModel.Settings.TrayHintShown)
            return;
        _viewModel.Settings.TrayHintShown = true;
        _viewModel.SaveSettings();
        _tray?.ShowMessage("Network Profiles is still running here. Right-click the icon to switch profiles, or choose Exit to quit.", isError: false);
    }

    private void ShowMainWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    /// <summary>Title bar in the page colour with a matching border (Windows 11; ignored elsewhere), as in LTC-MTC.</summary>
    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper((Window)sender).Handle;
        int dark = 1, caption = 0x0e0c0c, border = 0x2f2a2a;   // COLORREF is 0x00BBGGRR
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));      // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));   // DWMWA_CAPTION_COLOR
        DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));    // DWMWA_BORDER_COLOR
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    private void ExitApp()
    {
        _autoSwitchTimer.Stop();
        _viewModel?.SaveNow();
        _viewModel?.SaveSettings();
        _tray?.Dispose();
        _tray = null;
        if (_window is not null)
            _window.AllowClose = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _showSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
