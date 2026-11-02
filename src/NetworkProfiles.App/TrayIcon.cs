using System.Windows;
using NetworkProfiles.App.ViewModels;
using Forms = System.Windows.Forms;

namespace NetworkProfiles.App;

/// <summary>
/// Notification-area icon with a menu for switching profiles without opening
/// the main window. Profiles apply to the adapter used most recently.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Action _showWindow;
    private readonly Action _showRules;
    private readonly Action _exit;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new() { Renderer = new DarkMenuRenderer() };

    public TrayIcon(MainViewModel viewModel, Action showWindow, Action showRules, Action exit)
    {
        _viewModel = viewModel;
        _showWindow = showWindow;
        _showRules = showRules;
        _exit = exit;

        _menu.Opening += (_, _) => BuildMenu();

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Network Profiles",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                _showWindow();
        };
    }

    public void ShowMessage(string message, bool isError) =>
        _icon.ShowBalloonTip(5000, "Network Profiles", message, isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var adapter = _viewModel.TrayAdapter;
        var header = adapter is not null ? $"Apply to {adapter.Name}"
            : _viewModel.TrayAdapterName is { } missing ? $"{missing} is not connected"
            : "No adapter selected";
        _menu.Items.Add(new Forms.ToolStripMenuItem(header) { Enabled = false });
        _menu.Items.Add(new Forms.ToolStripSeparator());

        if (_viewModel.Profiles.Count == 0)
            _menu.Items.Add(new Forms.ToolStripMenuItem("No profiles yet") { Enabled = false });

        foreach (var profile in _viewModel.Profiles)
        {
            var id = profile.Id;
            var item = new Forms.ToolStripMenuItem(profile.Name)
            {
                Checked = _viewModel.Settings.LastAppliedProfileId == id,
                Enabled = adapter is not null && !_viewModel.IsBusy && !profile.HasErrors,
                ToolTipText = profile.Summary,
            };
            item.Click += async (_, _) =>
            {
                var (message, isError) = await _viewModel.ApplyFromTrayAsync(id);
                ShowMessage(message, isError);
            };
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new Forms.ToolStripSeparator());
        var autoSwitch = new Forms.ToolStripMenuItem("Auto-switch") { Checked = _viewModel.AutoSwitchEnabled };
        autoSwitch.Click += (_, _) => _viewModel.AutoSwitchEnabled = !_viewModel.AutoSwitchEnabled;
        _menu.Items.Add(autoSwitch);
        var rules = new Forms.ToolStripMenuItem("Auto-switch rules…");
        rules.Click += (_, _) => _showRules();
        _menu.Items.Add(rules);

        _menu.Items.Add(new Forms.ToolStripSeparator());
        var open = new Forms.ToolStripMenuItem("Open Network Profiles");
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        open.Click += (_, _) => _showWindow();
        _menu.Items.Add(open);
        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => _exit();
        _menu.Items.Add(exit);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        using var stream = resource.Stream;
        return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _menu.Dispose();
    }
}
