using System.Buffers.Binary;

namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// Wire frame for the V2 mesh protocol:
/// <c>0x55 | srcAddr(2B) | destAddr(2B) | command(1B) | dataLength(1B) | data(dataLength B) | checksum(1B) | 0x16</c>
/// Ported from the original app's command table and checksum routine.
/// </summary>
public readonly struct MeshFrame
{
    public const byte StartFlag = 0x55;
    public const byte EndFlag = 0x16;

    /// <summary>The address the app always sends from.</summary>
    public const ushort AppSourceAddress = 0x0000;

    /// <summary>Broadcast destination address (all mesh nodes / the responding master).</summary>
    public const ushort BroadcastAddress = 0xFFFF;

    public ushort SourceAddress { get; }
    public ushort DestinationAddress { get; }
    public byte StatusOrCommand { get; }
    public byte[] Data { get; }
    public byte Checksum { get; }
    public bool ChecksumValid { get; }

    private MeshFrame(ushort sourceAddress, ushort destinationAddress, byte statusOrCommand, byte[] data, byte checksum, bool checksumValid)
    {
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        StatusOrCommand = statusOrCommand;
        Data = data;
        Checksum = checksum;
        ChecksumValid = checksumValid;
    }

    /// <summary>
    /// Sum of every byte in the frame up to (not including) the checksum itself,
    /// truncated to one byte. Mirrors the app's checksum routine, which sums the
    /// hex byte pairs as integers and keeps only the low byte of the total.
    /// </summary>
    public static byte ComputeChecksum(ReadOnlySpan<byte> frameWithoutChecksumAndEndFlag)
    {
        int sum = 0;
        foreach (byte b in frameWithoutChecksumAndEndFlag) sum += b;
        return unchecked((byte)sum);
    }

    public static byte[] Encode(ushort destinationAddress, MeshCommand command, ReadOnlySpan<byte> data)
    {
        if (data.Length > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "Mesh frame data cannot exceed 255 bytes (1-byte length field).");

        var body = new byte[7 + data.Length];
        var span = body.AsSpan();
        span[0] = StartFlag;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1, 2), AppSourceAddress);
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(3, 2), destinationAddress);
        span[5] = (byte)command;
        span[6] = (byte)data.Length;
        data.CopyTo(span[7..]);

        byte checksum = ComputeChecksum(body);

        var frame = new byte[body.Length + 2];
        body.CopyTo(frame, 0);
        frame[^2] = checksum;
        frame[^1] = EndFlag;
        return frame;
    }

    /// <summary>
    /// Decodes a complete raw response frame (header + data + checksum + end flag).
    /// Throws <see cref="FireflyProtocolException"/> if the frame is structurally invalid.
    /// A checksum mismatch does not throw -- it's surfaced via <see cref="ChecksumValid"/>
    /// so callers can decide how strict to be.
    /// </summary>
    public static MeshFrame Decode(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 9)
            throw new FireflyProtocolException($"Mesh response frame is too short ({raw.Length} bytes); minimum valid frame is 9 bytes.");
        if (raw[0] != StartFlag)
            throw new FireflyProtocolException($"Mesh response frame has unexpected start flag 0x{raw[0]:X2} (expected 0x{StartFlag:X2}).");
        if (raw[^1] != EndFlag)
            throw new FireflyProtocolException($"Mesh response frame has unexpected end flag 0x{raw[^1]:X2} (expected 0x{EndFlag:X2}).");

        ushort src = BinaryPrimitives.ReadUInt16BigEndian(raw.Slice(1, 2));
        ushort dest = BinaryPrimitives.ReadUInt16BigEndian(raw.Slice(3, 2));
        byte status = raw[5];
        byte declaredLength = raw[6];

        int expectedTotalLength = 7 + declaredLength + 2;
        if (raw.Length != expectedTotalLength)
            throw new FireflyProtocolException(
                $"Mesh response frame length mismatch: header declares {declaredLength} data byte(s) " +
                $"(expects {expectedTotalLength} bytes total) but {raw.Length} byte(s) were provided.");

        var data = raw.Slice(7, declaredLength).ToArray();
        byte checksum = raw[^2];
        byte expectedChecksum = ComputeChecksum(raw[..(raw.Length - 2)]);

        return new MeshFrame(src, dest, status, data, checksum, checksum == expectedChecksum);
    }
}
