using System.Windows;
using System.Windows.Threading;

namespace NetworkProfiles.App;

/// <summary>
/// Asks the user to confirm new network settings and reverts them when the
/// countdown runs out, like Windows does after a display resolution change.
/// Closing the window any other way counts as Revert.
/// </summary>
public partial class KeepChangesWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _secondsLeft;

    public KeepChangesWindow(string message, TimeSpan timeout)
    {
        InitializeComponent();
        MessageText.Text = message;
        _secondsLeft = Math.Max(1, (int)timeout.TotalSeconds);
        UpdateCountdown();

        _timer.Tick += (_, _) =>
        {
            _secondsLeft--;
            if (_secondsLeft <= 0)
                Finish(keep: false);
            else
                UpdateCountdown();
        };
        Loaded += (_, _) =>
        {
            _timer.Start();
            Activate();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void UpdateCountdown() =>
        CountdownText.Text = $"Reverting to the previous settings in {_secondsLeft} s.";

    private void OnKeep(object sender, RoutedEventArgs e) => Finish(keep: true);

    private void OnRevert(object sender, RoutedEventArgs e) => Finish(keep: false);

    private void Finish(bool keep)
    {
        _timer.Stop();
        DialogResult = keep;
    }
}
