using System.Windows;

namespace NetworkProfiles.App;

/// <summary>
/// An OK/Cancel question in the app's own theme (a system message box ignores it).
/// Closing the window or pressing Esc counts as Cancel.
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string message, string title)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
