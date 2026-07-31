using System.IO.Compression;
using System.Text.Json;
using PyroPilot.Core.Model;

namespace PyroPilot.Core.Persistence;

/// <summary>
/// Reads and writes a show as a single portable ".pyroshow" file: a zip
/// archive containing "show.json" plus a copy of every audio file the show's
/// <see cref="AudioClip"/>s reference, under "audio/". Self-contained so a
/// show can be copied to another machine and still play its audio track.
/// </summary>
public static class ShowPackage
{
    public const string FileExtension = ".pyroshow";

    private const string ManifestEntryName = "show.json";
    private const string AudioFolder = "audio/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Saves <paramref name="show"/> to <paramref name="filePath"/>.
    /// <paramref name="audioSourcesByFileName"/> maps each <see cref="AudioClip.FileName"/>
    /// referenced by the show to a source file to copy in -- pass an empty
    /// dictionary (or omit entries) for audio files already embedded from a
    /// previous load that don't need re-copying, see <see cref="Load"/>.
    /// </summary>
    public static void Save(Show show, string filePath, IReadOnlyDictionary<string, string>? audioSourcesByFileName = null)
    {
        show.ModifiedUtc = DateTimeOffset.UtcNow;

        string tempPath = filePath + ".tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            using (Stream stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, show, JsonOptions);

            if (audioSourcesByFileName is not null)
            {
                foreach ((string fileName, string sourcePath) in audioSourcesByFileName)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(AudioFolder + fileName, CompressionLevel.NoCompression);
                    using Stream entryStream = entry.Open();
                    using FileStream sourceStream = File.OpenRead(sourcePath);
                    sourceStream.CopyTo(entryStream);
                }
            }
        }

        File.Copy(tempPath, filePath, overwrite: true);
        File.Delete(tempPath);
    }

    public static Show Load(string filePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException($"'{filePath}' is not a valid PyroPilot show package (missing {ManifestEntryName}).");

        using Stream stream = manifestEntry.Open();
        return JsonSerializer.Deserialize<Show>(stream, JsonOptions)
            ?? throw new InvalidDataException($"'{filePath}' contains an empty or invalid show manifest.");
    }

    /// <summary>The audio file names embedded in a saved package, so callers can tell which clips already have data on disk.</summary>
    public static IReadOnlyList<string> ListAudioFiles(string filePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        return archive.Entries
            .Where(e => e.FullName.StartsWith(AudioFolder, StringComparison.Ordinal) && e.FullName.Length > AudioFolder.Length)
            .Select(e => e.FullName[AudioFolder.Length..])
            .ToList();
    }

    /// <summary>Extracts one embedded audio file to <paramref name="destinationPath"/> (e.g. into a playback cache directory).</summary>
    public static void ExtractAudioTo(string filePath, string audioFileName, string destinationPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        ZipArchiveEntry entry = archive.GetEntry(AudioFolder + audioFileName)
            ?? throw new FileNotFoundException($"Audio file '{audioFileName}' not found in show package '{filePath}'.");
        entry.ExtractToFile(destinationPath, overwrite: true);
    }
}
