using System.Text;
using Firefly.Client.Utilities;

namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// Builds request frames for the V2 mesh protocol. One method per mesh
/// command, byte-for-byte equivalent to the original app's command table --
/// verified against the app's literal hex strings in the unit tests.
/// </summary>
public static class MeshCommandBuilder
{
    public const int PortCount = 15;

    /// <summary>Fixed 8-byte old+new password field the app always sends as zero. The real device password is not part of login.</summary>
    public static byte[] Login() =>
        MeshFrame.Encode(MeshFrame.BroadcastAddress, MeshCommand.Login, new byte[8]);

    public static byte[] MeshList() =>
        MeshFrame.Encode(MeshFrame.BroadcastAddress, MeshCommand.MeshList, ReadOnlySpan<byte>.Empty);

    public static byte[] PortStatus(ushort deviceId = MeshFrame.BroadcastAddress) =>
        MeshFrame.Encode(deviceId, MeshCommand.PortStatus, ReadOnlySpan<byte>.Empty);

    public static byte[] ManualFire(ushort deviceId, byte port, FireDelayMode fireDelay = FireDelayMode.Normal) =>
        MeshFrame.Encode(deviceId, MeshCommand.ManualFire, [port, (byte)fireDelay]);

    /// <summary>
    /// Start/stop auto-fire. The app always sends this with a 2-byte data value
    /// (observed call sites only ever pass "FFFF"); its real semantics beyond
    /// that aren't documented in the app, so it's exposed as a raw ushort.
    /// </summary>
    public static byte[] StartAutoFire(ushort deviceId, ushort data = 0xFFFF) =>
        MeshFrame.Encode(deviceId, MeshCommand.StartAutoFire, [(byte)(data >> 8), (byte)data]);

    public static byte[] StopAutoFire(ushort deviceId, ushort data = 0xFFFF) =>
        MeshFrame.Encode(deviceId, MeshCommand.StopAutoFire, [(byte)(data >> 8), (byte)data]);

    /// <summary>
    /// "Cue write" -- programs the fire-time table for a device's ports.
    /// <paramref name="portTimesMs"/> must contain exactly <see cref="PortCount"/> (15)
    /// entries, one per physical port, matching the app's fixed 15-port
    /// table.
    /// </summary>
    public static byte[] AddPlanFire(ushort deviceId, IReadOnlyList<int> portTimesMs, FireDelayMode fireDelay = FireDelayMode.Normal)
    {
        ArgumentNullException.ThrowIfNull(portTimesMs);
        if (portTimesMs.Count != PortCount)
            throw new ArgumentException($"Expected exactly {PortCount} port times, got {portTimesMs.Count}.", nameof(portTimesMs));

        var payload = new byte[1 + PortCount * 3];
        payload[0] = 0x01;
        for (int i = 0; i < PortCount; i++)
        {
            PortTimeEntry(portTimesMs[i], fireDelay).CopyTo(payload, 1 + i * 3);
        }

        return MeshFrame.Encode(deviceId, MeshCommand.AddPlanFire, payload);
    }

    /// <summary>
    /// One 3-byte fire-time table entry: 2-byte big-endian time in milliseconds
    /// (zero-padded to 4 hex digits when it would otherwise be shorter) followed
    /// by the 1-byte fire-delay mode, or 0x00 when the time is zero. Ported from
    /// the app's port-time encoding.
    /// </summary>
    public static byte[] PortTimeEntry(int timeMs, FireDelayMode fireDelay)
    {
        if (timeMs < 0) throw new ArgumentOutOfRangeException(nameof(timeMs));

        string hex = timeMs.ToString("X");
        if (hex.Length < 4) hex = hex.PadLeft(4, '0');
        // Values >= 0x10000ms (~65s) would overflow the intended 2-byte time field;
        // the app's format string has the same limitation.
        if (hex.Length > 4)
            throw new ArgumentOutOfRangeException(nameof(timeMs), timeMs, "Port fire time exceeds the 2-byte (0-65535ms) field.");

        byte delayByte = timeMs != 0 ? (byte)fireDelay : (byte)0x00;
        var timeBytes = HexConvert.FromHex(hex);
        return [timeBytes[0], timeBytes[1], delayByte];
    }

    public static byte[] DeletePlan(ushort deviceId) =>
        MeshFrame.Encode(deviceId, MeshCommand.DeletePlan, [0x01]);

    public static byte[] StartPlan(ushort deviceId = MeshFrame.BroadcastAddress) =>
        MeshFrame.Encode(deviceId, MeshCommand.StartPlan, [0x01]);

    public static byte[] ClearPlan(ushort deviceId) =>
        MeshFrame.Encode(deviceId, MeshCommand.ClearPlan, ReadOnlySpan<byte>.Empty);

    public static byte[] FlashLed(ushort deviceId) =>
        MeshFrame.Encode(deviceId, MeshCommand.FlashLed, [0x06, 0x10]);

    /// <summary>
    /// Changes the device's WiFi SSID and password. Payload is the UTF-8 bytes of
    /// each string, hex-encoded and right-padded with ASCII '0' characters to 40
    /// hex characters (20 bytes) each, concatenated -- ported from the app's
    /// build of the MODIFY_PARAM payload.
    /// Rebooting/reassociating after this call is on the caller; the device will
    /// drop the current connection once it applies the new credentials.
    /// </summary>
    public static byte[] ModifyMeshParam(ushort deviceId, string newSsid, string newPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(newSsid);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        string ssidField = RightPadHexField(newSsid);
        string passwordField = RightPadHexField(newPassword);
        byte[] payload = HexConvert.FromHex(ssidField + passwordField);

        return MeshFrame.Encode(deviceId, MeshCommand.ModifyMeshParam, payload);
    }

    private static string RightPadHexField(string value)
    {
        string hex = HexConvert.ToHex(Encoding.UTF8.GetBytes(value));
        if (hex.Length > 40)
            throw new ArgumentException($"Value is too long once UTF-8/hex encoded ({hex.Length} chars, max 40): \"{value}\"", nameof(value));
        return hex.PadRight(40, '0');
    }

    public static byte[] DeviceInfo(ushort deviceId = MeshFrame.BroadcastAddress) =>
        MeshFrame.Encode(deviceId, MeshCommand.DeviceInfo, ReadOnlySpan<byte>.Empty);
}
