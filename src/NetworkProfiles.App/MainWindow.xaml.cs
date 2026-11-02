using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using NetworkProfiles.App.ViewModels;

namespace NetworkProfiles.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>Raised when closing the window only hid it; the app keeps running in the tray.</summary>
    public event EventHandler? HiddenToTray;

    /// <summary>Set when the app is really exiting, so closing is not turned into hiding.</summary>
    public bool AllowClose { get; set; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        // Keep the adapter list current when cables, Wi-Fi or addresses change.
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private RulesWindow? _rules;

    private void OnOpenRules(object sender, RoutedEventArgs e) => ShowRules();

    public void ShowRules()
    {
        if (_rules is null)
        {
            _rules = new RulesWindow(_viewModel);
            if (IsVisible)
                _rules.Owner = this;
            else
                _rules.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _rules.Closed += (_, _) => _rules = null;
        }
        _rules.Show();
        _rules.Activate();
    }

    private void OnNetworkChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (!_viewModel.IsBusy)
                _viewModel.RefreshAdaptersCommand.Execute(null);
        });

    protected override void OnClosing(CancelEventArgs e)
    {
        if (AllowClose)
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            base.OnClosing(e);
            return;
        }

        // Closing hides to the tray; the tray menu's Exit quits the app.
        e.Cancel = true;
        _viewModel.SaveNow();
        Hide();
        HiddenToTray?.Invoke(this, EventArgs.Empty);
        base.OnClosing(e);
    }
}
