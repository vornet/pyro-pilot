using System.Text.Json;
using PyroPilot.Core.Model;

namespace PyroPilot.Core.Persistence;

/// <summary>
/// Reads/writes the operator's global firework library -- a plain JSON file,
/// separate from any show, that a show's <see cref="Show.Library"/> snapshot
/// is drawn from and can be reused across shows.
/// </summary>
public static class FireworkLibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static List<FireworkDefinition> Load(string filePath)
    {
        if (!File.Exists(filePath)) return [];
        using FileStream stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<List<FireworkDefinition>>(stream, JsonOptions) ?? [];
    }

    public static void Save(string filePath, IEnumerable<FireworkDefinition> definitions)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using FileStream stream = File.Create(filePath);
        JsonSerializer.Serialize(stream, definitions.ToList(), JsonOptions);
    }
}
