using NetworkProfiles.Core.Models;
using NetworkProfiles.Core.Storage;

namespace NetworkProfiles.Core.Tests;

public class ProfileImporterTests
{
    [Fact]
    public void ClashingIdsAndNames_AreMadeUnique()
    {
        var existing = new NetworkProfile { Name = "Office" };
        var sameId = new NetworkProfile { Id = existing.Id, Name = "Office" };
        var sameNameAgain = new NetworkProfile { Name = "office" };
        var fresh = new NetworkProfile { Name = "Home" };

        var result = ProfileImporter.PrepareForImport([sameId, sameNameAgain, fresh], [existing]);

        Assert.Equal(3, result.Count);
        Assert.NotEqual(existing.Id, result[0].Id);
        Assert.Equal("Office (2)", result[0].Name);
        Assert.Equal("office (3)", result[1].Name);
        Assert.Equal("Home", result[2].Name);
        Assert.Equal(3, result.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void BlankName_GetsDefault()
    {
        var result = ProfileImporter.PrepareForImport([new NetworkProfile { Name = " " }], []);
        Assert.Equal("Imported profile", result[0].Name);
    }
}
