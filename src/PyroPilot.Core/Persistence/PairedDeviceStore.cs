using System.Text.Json;
using PyroPilot.Core.Model;

namespace PyroPilot.Core.Persistence;

/// <summary>Persists devices independently of any one show.</summary>
public static class PairedDeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static List<PairedDevice> Load(string filePath)
    {
        if (!File.Exists(filePath)) return [];
        using FileStream stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<List<PairedDevice>>(stream, JsonOptions) ?? [];
    }

    public static void Save(string filePath, IEnumerable<PairedDevice> devices)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using FileStream stream = File.Create(filePath);
        JsonSerializer.Serialize(stream, devices.ToList(), JsonOptions);
    }
}
