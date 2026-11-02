using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Storage;

namespace NetworkProfiles.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "np-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty() =>
        Assert.Empty(new ProfileStore(Path.Combine(_directory, "profiles.json")).Load());

    [Fact]
    public void SaveThenLoad_RoundTripsAllSettings()
    {
        var store = new ProfileStore(Path.Combine(_directory, "nested", "profiles.json"));
        var profile = new NetworkProfile
        {
            Name = "Lab",
            Description = "Bench network",
            Ipv4 = new Ipv4Settings { Mode = ConfigMode.Manual, Addresses = [new("10.10.0.5", "24"), new("10.20.0.5", "24")], Gateway = "10.10.0.1" },
            Dns = new DnsSettings { Mode = ConfigMode.Manual, Servers = ["10.10.0.1"] },
            Proxy = new ProxySettings { Mode = ProxyMode.Manual, Server = "proxy:3128", BypassList = "*.lab", BypassLocal = false },
        };

        store.Save([profile]);
        var loaded = Assert.Single(store.Load());

        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal("Lab", loaded.Name);
        Assert.Equal("Bench network", loaded.Description);
        Assert.Equal(ConfigMode.Manual, loaded.Ipv4.Mode);
        Assert.Equal("10.10.0.1", loaded.Ipv4.Gateway);
        Assert.Equal([new IpAddressEntry("10.10.0.5", "24"), new IpAddressEntry("10.20.0.5", "24")], loaded.Ipv4.Addresses);
        Assert.Equal(["10.10.0.1"], loaded.Dns.Servers);
        Assert.Equal(ProxyMode.Manual, loaded.Proxy.Mode);
        Assert.Equal("proxy:3128", loaded.Proxy.Server);
        Assert.False(loaded.Proxy.BypassLocal);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Save_WritesEnumsAsStrings()
    {
        var store = new ProfileStore(Path.Combine(_directory, "profiles.json"));
        store.Save([new NetworkProfile()]);
        Assert.Contains("\"Automatic\"", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Load_MigratesSingleAddressFromOlderFiles()
    {
        var path = Path.Combine(_directory, "old.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Old", "ipv4": { "mode": "Manual", "address": "192.168.1.10", "subnetMask": "255.255.255.0", "gateway": "192.168.1.1" } } ] }
            """);

        var store = new ProfileStore(path);
        var loaded = Assert.Single(store.Load());
        Assert.Equal([new IpAddressEntry("192.168.1.10", "255.255.255.0")], loaded.Ipv4.Addresses);

        store.Save([loaded]);
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var ipv4 = json.RootElement.GetProperty("profiles")[0].GetProperty("ipv4");
        Assert.False(ipv4.TryGetProperty("address", out _));
        Assert.False(ipv4.TryGetProperty("subnetMask", out _));
        Assert.Single(store.Load()[0].Ipv4.Addresses);
    }

    [Fact]
    public void Load_NonProfileFile_ThrowsInvalidData()
    {
        var path = Path.Combine(_directory, "bad.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "not json");
        Assert.Throws<InvalidDataException>(() => new ProfileStore(path).Load());
    }

    [Fact]
    public void AppSettings_RoundTripAndSurviveCorruptFile()
    {
        var path = Path.Combine(_directory, "settings.json");
        var id = Guid.NewGuid();
        new AppSettings { LastAdapterId = "{abc}", LastAppliedProfileId = id }.Save(path);
        var loaded = AppSettings.Load(path);
        Assert.Equal("{abc}", loaded.LastAdapterId);
        Assert.Equal(id, loaded.LastAppliedProfileId);

        File.WriteAllText(path, "{ broken");
        Assert.Null(AppSettings.Load(path).LastAdapterId);
    }

    [Fact]
    public void AppSettings_RoundTripsRulesWithAdapter()
    {
        var path = Path.Combine(_directory, "rules.json");
        var rule = new NetworkProfiles.Core.AutoSwitch.AutoSwitchRule
        {
            Program = "game.exe",
            ProfileId = Guid.NewGuid(),
            AdapterId = "{0A1B}",
            AdapterName = "Ethernet 2",
            Enabled = false,
        };
        new AppSettings { AutoSwitchRules = [rule], LastAdapterName = "USB LAN" }.Save(path);

        var settings = AppSettings.Load(path);
        Assert.Equal("USB LAN", settings.LastAdapterName);
        var loaded = Assert.Single(settings.AutoSwitchRules);
        Assert.Equal(rule.Id, loaded.Id);
        Assert.Equal("game.exe", loaded.Program);
        Assert.Equal(rule.ProfileId, loaded.ProfileId);
        Assert.Equal("{0A1B}", loaded.AdapterId);
        Assert.Equal("Ethernet 2", loaded.AdapterName);
        Assert.False(loaded.Enabled);
    }
}
