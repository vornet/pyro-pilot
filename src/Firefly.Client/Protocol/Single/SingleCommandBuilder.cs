namespace Firefly.Client.Protocol.Single;

/// <summary>
/// Builds request frames for the V3 single-device protocol. One method per
/// command value, byte-for-byte equivalent to the original app's
/// command-frame builder -- verified against the app's literal hex strings
/// in the unit tests.
/// </summary>
public static class SingleCommandBuilder
{
    /// <summary>
    /// The frame the app actually sends over TCP for WiFi V3 login is NOT the
    /// same as its plain command-frame builder alone produces (that 7-byte
    /// value -- "AA050100000001" -- is only what the app's BLE login path
    /// uses). The WiFi path appends a further literal byte, the manual-fire
    /// command byte (0x04), after the already-complete 7-byte frame, without
    /// updating the frame's declared length byte (which stays 0x05,
    /// describing only the first 5 payload bytes). The app's own
    /// trailing-byte concatenation looks like reused-constant cruft rather
    /// than a deliberate field, and its purpose isn't documented anywhere
    /// else in the app -- it might be inert padding the firmware ignores, or
    /// it might matter. Reproduced here because it's what a real GT2404 in
    /// WiFi mode actually receives on the wire.
    /// </summary>
    private const byte WifiLoginTrailingByte = (byte)SingleCommand.ManualFire;

    public static byte[] Login()
    {
        byte[] frame = SingleFrame.Encode(SingleFrame.LoginFrameType, SingleCommand.Login, ReadOnlySpan<byte>.Empty);
        byte[] withTrailingByte = new byte[frame.Length + 1];
        frame.CopyTo(withTrailingByte, 0);
        withTrailingByte[^1] = WifiLoginTrailingByte;
        return withTrailingByte;
    }

    public static byte[] ManualFire(byte port) =>
        SingleFrame.Encode(SingleFrame.CommandFrameType, SingleCommand.ManualFire, [port]);

    /// <summary>Programs the fire-time (cue) delay for a single port.</summary>
    public static byte[] Cue(byte port, ushort durationMs) =>
        SingleFrame.Encode(SingleFrame.CommandFrameType, SingleCommand.Cue, [port, (byte)(durationMs >> 8), (byte)durationMs]);

    /// <summary>Fires the programmed show/cue sequence.</summary>
    public static byte[] Fire() =>
        SingleFrame.Encode(SingleFrame.CommandFrameType, SingleCommand.Fire, ReadOnlySpan<byte>.Empty);

    public static byte[] Test() =>
        SingleFrame.Encode(SingleFrame.CommandFrameType, SingleCommand.Test, ReadOnlySpan<byte>.Empty);

    public static byte[] Status() =>
        SingleFrame.Encode(SingleFrame.CommandFrameType, SingleCommand.Status, ReadOnlySpan<byte>.Empty);
}
