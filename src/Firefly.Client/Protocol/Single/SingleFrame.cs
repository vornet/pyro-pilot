namespace Firefly.Client.Protocol.Single;

/// <summary>
/// Wire frame for the V3 single-device protocol:
/// <c>0xAA | payloadLength(1B) | payload </c>, where payload is
/// <c>[frameType(1B)] [passcode(3B)] [command(1B)] [command-specific data...]</c>.
/// Ported from the original app's command-frame builder. Every observed
/// frame carries the fixed 3-byte passcode "000000" (a separate internal
/// constant in the app -- not the user-visible device password, which
/// appears to only matter for ModifyMeshParam-style config on the mesh
/// protocol; this passcode is hardcoded/constant in every build of the app
/// that was examined).
/// </summary>
public static class SingleFrame
{
    public const byte StartByte = 0xAA;
    public const byte ResponseStartByte = 0xBB;

    /// <summary>Frame-type byte used only for the LOGIN request; every other command uses <see cref="CommandFrameType"/>.</summary>
    public const byte LoginFrameType = 0x01;

    /// <summary>Frame-type byte used for all non-login commands.</summary>
    public const byte CommandFrameType = 0x77;

    public static readonly byte[] Passcode = [0x00, 0x00, 0x00];

    public static byte[] Encode(byte frameType, SingleCommand command, ReadOnlySpan<byte> extraData)
    {
        var payload = new byte[1 + Passcode.Length + 1 + extraData.Length];
        var span = payload.AsSpan();
        span[0] = frameType;
        Passcode.CopyTo(span.Slice(1, Passcode.Length));
        span[1 + Passcode.Length] = (byte)command;
        extraData.CopyTo(span[(2 + Passcode.Length)..]);

        if (payload.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(extraData), "V3 frame payload cannot exceed 255 bytes (1-byte length field).");

        var frame = new byte[2 + payload.Length];
        frame[0] = StartByte;
        frame[1] = (byte)payload.Length;
        payload.CopyTo(frame, 2);
        return frame;
    }
}
