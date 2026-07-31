using Firefly.Client.Protocol.Mesh;
using Firefly.Client.Utilities;

namespace Firefly.Client.Tests.Protocol.Mesh;

/// <summary>
/// Verifies the checksum + end-flag trailer against a hand-computed example,
/// and round-trips Encode/Decode.
/// </summary>
public class MeshFrameTests
{
    [Fact]
    public void Checksum_MatchesHandComputedSum()
    {
        // MESH_LIST base frame: 55 00 00 FF FF 02 00
        // Sum = 0x55+0x00+0x00+0xFF+0xFF+0x02+0x00 = 597 (0x255); low byte = 0x55.
        byte[] body = HexConvert.FromHex("550000FFFF0200");
        Assert.Equal(0x55, MeshFrame.ComputeChecksum(body));
    }

    [Fact]
    public void MeshList_TrailerIsChecksumThenEndFlag()
    {
        byte[] frame = MeshCommandBuilder.MeshList();
        Assert.Equal(0x55, frame[^2]);
        Assert.Equal(MeshFrame.EndFlag, frame[^1]);
    }

    [Fact]
    public void EncodeThenDecode_RoundTrips()
    {
        byte[] data = [0x01, 0x02, 0x03];
        byte[] frame = MeshFrame.Encode(0x0102, MeshCommand.ManualFire, data);

        MeshFrame decoded = MeshFrame.Decode(frame);

        Assert.Equal(0x0102, decoded.DestinationAddress);
        Assert.Equal((byte)MeshCommand.ManualFire, decoded.StatusOrCommand);
        Assert.Equal(data, decoded.Data);
        Assert.True(decoded.ChecksumValid);
    }

    [Fact]
    public void Decode_FlagsChecksumMismatchWithoutThrowing()
    {
        byte[] frame = MeshFrame.Encode(0xFFFF, MeshCommand.MeshList, ReadOnlySpan<byte>.Empty);
        frame[^2] ^= 0xFF; // corrupt the checksum byte

        MeshFrame decoded = MeshFrame.Decode(frame);
        Assert.False(decoded.ChecksumValid);
    }

    [Fact]
    public void Decode_ThrowsOnBadStartFlag()
    {
        byte[] frame = MeshCommandBuilder.MeshList();
        frame[0] = 0x00;
        Assert.Throws<FireflyProtocolException>(() => MeshFrame.Decode(frame));
    }

    [Fact]
    public void Decode_ThrowsOnLengthMismatch()
    {
        byte[] frame = MeshCommandBuilder.PortStatus(0x0102);
        var truncated = frame[..^1]; // drop the end flag so lengths no longer line up
        Assert.Throws<FireflyProtocolException>(() => MeshFrame.Decode(truncated));
    }
}
