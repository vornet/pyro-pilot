using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.Core.Tests;

public class ShowPackageTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("pyropilot-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void SaveThenLoad_RoundTripsShowMetadataAndTracks()
    {
        var firework = new FireworkDefinition
        {
            Name = "Golden Willow",
            DurationMs = 4500,
            PreviewImageData = [137, 80, 78, 71],
            PreviewImageFileName = "willow.png",
            VideoUrl = "https://www.youtube.com/watch?v=example",
            Effect = new FireworkEffect { Shape = BurstShape.Chrysanthemum, ParticleCount = 240 },
        };
        var show = new Show
        {
            Name = "Fourth of July",
            Library = [firework],
            Devices = [new PairedDevice { Nickname = "Front Yard", Protocol = DeviceProtocol.Mesh, MeshDeviceId = 0x0101 }],
            Tracks =
            [
                new Track
                {
                    Name = "Fire 1",
                    Kind = TrackKind.Fire,
                    Clips =
                    [
                        new FireCue
                        {
                            FireworkDefinitionId = firework.Id,
                            Port = 3,
                            StartMs = 1000,
                            DurationMs = 4500,
                            Label = "Opener",
                            LaunchPosition = new SpatialPoint { X = 12, Z = -4 },
                            TiltDegrees = 8,
                            SimulationSeed = 42,
                        },
                    ],
                },
                new Track
                {
                    Name = "Music",
                    Kind = TrackKind.Audio,
                    Clips = [new AudioClip { FileName = "song.mp3", StartMs = 0, DurationMs = 60000 }],
                },
            ],
        };

        string path = Path.Combine(_tempDir, "show" + ShowPackage.FileExtension);
        ShowPackage.Save(show, path);

        Show loaded = ShowPackage.Load(path);

        Assert.Equal(show.Name, loaded.Name);
        Assert.Equal(show.Id, loaded.Id);
        Assert.Equal(2, loaded.Tracks.Count);
        Assert.Single(loaded.Library);
        Assert.Equal(firework.Name, loaded.Library[0].Name);
        Assert.Equal(BurstShape.Chrysanthemum, loaded.Library[0].Effect.Shape);
        Assert.Equal(240, loaded.Library[0].Effect.ParticleCount);
        Assert.Equal(firework.PreviewImageData, loaded.Library[0].PreviewImageData);
        Assert.Equal("willow.png", loaded.Library[0].PreviewImageFileName);
        Assert.Equal(firework.VideoUrl, loaded.Library[0].VideoUrl);

        var loadedCue = Assert.IsType<FireCue>(loaded.Tracks[0].Clips[0]);
        Assert.Equal(3, loadedCue.Port);
        Assert.Equal("Opener", loadedCue.Label);
        Assert.Equal(12, loadedCue.LaunchPosition.X);
        Assert.Equal(-4, loadedCue.LaunchPosition.Z);
        Assert.Equal(8, loadedCue.TiltDegrees);
        Assert.Equal(42, loadedCue.SimulationSeed);

        var loadedAudio = Assert.IsType<AudioClip>(loaded.Tracks[1].Clips[0]);
        Assert.Equal("song.mp3", loadedAudio.FileName);
    }

    [Fact]
    public void Save_CopiesReferencedAudioFileIntoThePackage()
    {
        string sourceAudioPath = Path.Combine(_tempDir, "source.mp3");
        byte[] fakeAudioBytes = [1, 2, 3, 4, 5];
        File.WriteAllBytes(sourceAudioPath, fakeAudioBytes);

        var show = new Show { Name = "With Audio" };
        string packagePath = Path.Combine(_tempDir, "show" + ShowPackage.FileExtension);

        ShowPackage.Save(show, packagePath, new Dictionary<string, string> { ["song.mp3"] = sourceAudioPath });

        Assert.Contains("song.mp3", ShowPackage.ListAudioFiles(packagePath));

        string extractedPath = Path.Combine(_tempDir, "extracted.mp3");
        ShowPackage.ExtractAudioTo(packagePath, "song.mp3", extractedPath);
        Assert.Equal(fakeAudioBytes, File.ReadAllBytes(extractedPath));
    }

    [Fact]
    public void Load_ThrowsInvalidDataException_WhenFileIsNotAShowPackage()
    {
        string path = Path.Combine(_tempDir, "not-a-show.txt");
        File.WriteAllText(path, "hello");

        Assert.Throws<InvalidDataException>(() => ShowPackage.Load(path));
    }
}
