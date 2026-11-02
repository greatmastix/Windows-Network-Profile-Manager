using CommunityToolkit.Mvvm.ComponentModel;
using NetworkProfiles.Core.Models;

namespace NetworkProfiles.App.ViewModels;

public sealed partial class AddressEntryViewModel(string address, string subnetMask) : ObservableObject
{
    [ObservableProperty] private string _address = address;
    [ObservableProperty] private string _subnetMask = subnetMask;

    public IpAddressEntry ToModel() => new(Address.Trim(), SubnetMask.Trim());
}
