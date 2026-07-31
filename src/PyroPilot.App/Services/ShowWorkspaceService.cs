using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.App.Services;

/// <summary>
/// Owns the currently open <see cref="Show"/>: which file it came from, save/
/// load through <see cref="ShowPackage"/>, and resolving a locally-playable
/// path for an <see cref="AudioClip"/> whether or not the show has been saved
/// yet.
/// </summary>
public sealed class ShowWorkspaceService
{
    private readonly Dictionary<string, string> _pendingAudioSources = new();

    public Show Show { get; private set; } = new();
    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }

    /// <summary>Raised after New()/Load() replace the whole Show, so bound view models know to rebuild from it.</summary>
    public event Action? ShowReplaced;

    public void New()
    {
        Show = new Show();
        FilePath = null;
        _pendingAudioSources.Clear();
        IsDirty = false;
        ShowReplaced?.Invoke();
    }

    public void Load(string filePath)
    {
        Show = ShowPackage.Load(filePath);
        FilePath = filePath;
        _pendingAudioSources.Clear();
        IsDirty = false;
        ShowReplaced?.Invoke();
    }

    public void Save(string? filePath = null)
    {
        string target = filePath ?? FilePath ?? throw new InvalidOperationException("No file path specified for a new show.");
        ShowPackage.Save(Show, target, _pendingAudioSources);
        FilePath = target;
        _pendingAudioSources.Clear();
        IsDirty = false;
    }

    public void MarkDirty() => IsDirty = true;

    /// <summary>Registers an audio file to be copied into the package on next save; returns the package-relative file name to store on an AudioClip.</summary>
    public string ImportAudio(string sourcePath)
    {
        string fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
        _pendingAudioSources[fileName] = sourcePath;
        MarkDirty();
        return fileName;
    }

    /// <summary>A local, playable path for an AudioClip's file name -- the not-yet-saved import source, or an extracted cache copy of a saved package's audio.</summary>
    public string ResolveAudioPlaybackPath(string fileName)
    {
        if (_pendingAudioSources.TryGetValue(fileName, out string? sourcePath)) return sourcePath;

        if (FilePath is null)
            throw new InvalidOperationException($"Audio file '{fileName}' has no source, and the show hasn't been saved yet.");

        AppPaths.EnsureDirectoriesExist();
        string cachePath = Path.Combine(AppPaths.AudioCacheDirectory, fileName);
        if (!File.Exists(cachePath))
            ShowPackage.ExtractAudioTo(FilePath, fileName, cachePath);
        return cachePath;
    }
}
