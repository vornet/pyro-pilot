using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.Core.Tests;

public sealed class PairedDeviceStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsConnectionPreferences()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PyroPilotTests-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "devices.json");
        try
        {
            var expected = new PairedDevice
            {
                Nickname = "Front Yard",
                JoinTitanFireWifi = true,
                AutoConnect = true,
            };

            PairedDeviceStore.Save(filePath, [expected]);
            PairedDevice actual = Assert.Single(PairedDeviceStore.Load(filePath));

            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal("Front Yard", actual.Nickname);
            Assert.True(actual.JoinTitanFireWifi);
            Assert.True(actual.AutoConnect);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        Assert.Empty(PairedDeviceStore.Load(filePath));
    }
}
