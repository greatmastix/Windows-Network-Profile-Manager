using System.Windows;
using Microsoft.Win32;

namespace NetworkProfiles.App.Services;

public sealed class MessageBoxDialogService : IDialogService
{
    private static Window? Owner =>
        Application.Current.MainWindow is { IsVisible: true } window ? window : null;

    public bool Confirm(string message, string title) =>
        (Owner is { } owner
            ? MessageBox.Show(owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
            : MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel))
        == MessageBoxResult.OK;

    public bool AskKeepChanges(string message, TimeSpan timeout)
    {
        var dialog = new KeepChangesWindow(message, timeout);
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
