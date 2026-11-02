using System.Windows;
using Microsoft.Win32;

namespace NetworkProfiles.App.Services;

/// <summary>Questions as themed app windows; file pickers are the standard Windows ones.</summary>
public sealed class WindowDialogService : IDialogService
{
    private static Window? Owner =>
        Application.Current.MainWindow is { IsVisible: true } window ? window : null;

    public bool Confirm(string message, string title) => ShowOwned(new ConfirmWindow(message, title));

    public bool AskKeepChanges(string message, TimeSpan timeout) => ShowOwned(new KeepChangesWindow(message, timeout));

    private static bool ShowOwned(Window dialog)
    {
        if (Owner is { } owner)
            dialog.Owner = owner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }

    public string? PickFileToOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFileToSave(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
}
