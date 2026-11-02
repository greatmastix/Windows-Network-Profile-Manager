namespace NetworkProfiles.App.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);

    /// <summary>
    /// Asks whether to keep just-applied settings. Returns false when the
    /// user chooses to revert or does not answer within <paramref name="timeout"/>.
    /// </summary>
    bool AskKeepChanges(string message, TimeSpan timeout);

    string? PickFileToOpen(string title, string filter);
    string? PickFileToSave(string title, string filter, string defaultFileName);
}
