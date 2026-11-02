using System.Windows;
using NetworkProfiles.App.ViewModels;

namespace NetworkProfiles.App;

public partial class RulesWindow : Window
{
    public RulesWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
