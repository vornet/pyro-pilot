namespace Firefly.Client.Utilities;

/// <summary>
/// Hex encoding helpers matching the on-wire representation used throughout the
/// original Titan Fireworks Android app (uppercase, unseparated hex digit pairs).
/// </summary>
public static class HexConvert
{
    public static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);

    public static byte[] FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length % 2 != 0)
            throw new ArgumentException($"Hex string must have an even number of characters, got {hex.Length}.", nameof(hex));
        return Convert.FromHexString(hex);
    }
}
