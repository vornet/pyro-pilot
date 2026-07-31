namespace PyroPilot.App.Services;

/// <summary>Well-known on-disk locations for PyroPilot's user-level (non-show) data.</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PyroPilot");

    public static string LibraryFilePath => Path.Combine(DataDirectory, "library.json");

    public static string AudioCacheDirectory => Path.Combine(DataDirectory, "audio-cache");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(AudioCacheDirectory);
    }
}
