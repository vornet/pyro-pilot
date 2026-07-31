using System.Text;
using System.Text.RegularExpressions;

namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// Parses fields out of mesh (V2) response frames. Ported from the ad-hoc
/// parsing scattered across several screens and helpers in the original app.
/// </summary>
public static partial class MeshResponseParser
{
    /// <summary>
    /// Extracts mesh device IDs from a MESH_LIST response payload (the frame's
    /// <see cref="MeshFrame.Data"/>, header already stripped). Ported from the
    /// app's device-list parsing: device records are 5 bytes wide starting at
    /// payload offset 1, with the 2-byte ID at the start of each record.
    /// </summary>
    public static IReadOnlyList<ushort> ParseMeshDeviceIds(ReadOnlySpan<byte> payload)
    {
        var ids = new List<ushort>();
        int next = 1;
        for (int idx = 0; idx < payload.Length; idx++)
        {
            if (idx != next) continue;
            if (idx + 1 >= payload.Length) break;

            ushort id = (ushort)((payload[idx] << 8) | payload[idx + 1]);
            if (!ids.Contains(id)) ids.Add(id);
            next = idx + 5;
        }
        return ids;
    }

    /// <summary>
    /// Battery reading found 4 bytes before the end of the full raw frame
    /// (i.e. 2 bytes immediately preceding the checksum+end-flag trailer).
    /// Ported from the app's battery-status parsing. Units are whatever the
    /// device firmware uses (observed against a battery-icon threshold of
    /// ~2000-3000 in the app -- likely raw millivolts, but unconfirmed).
    /// </summary>
    public static int ParseBatteryLevel(ReadOnlySpan<byte> fullRawFrame)
    {
        if (fullRawFrame.Length <= 4) return 0;
        int hiIndex = fullRawFrame.Length - 4;
        return (fullRawFrame[hiIndex] << 8) | fullRawFrame[hiIndex + 1];
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex FirmwareDateRegex();

    /// <summary>
    /// Searches a device-info response for an embedded ISO-ish date
    /// (yyyy-MM-dd) by decoding the full raw frame as UTF-8 text. Ported from
    /// the app's firmware-info parsing. Returns null if no date substring is
    /// found.
    /// </summary>
    public static string? ParseFirmwareDate(ReadOnlySpan<byte> fullRawFrame)
    {
        string text = Encoding.UTF8.GetString(fullRawFrame);
        Match match = FirmwareDateRegex().Match(text);
        return match.Success ? match.Value : null;
    }
}
