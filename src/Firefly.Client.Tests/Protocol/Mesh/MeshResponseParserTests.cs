using System.Text;
using Firefly.Client.Protocol.Mesh;

namespace Firefly.Client.Tests.Protocol.Mesh;

public class MeshResponseParserTests
{
    [Theory]
    [InlineData("0001", new int[0])]
    [InlineData("0003", new[] { 1 })]
    [InlineData("0005", new[] { 2 })]
    [InlineData("8003", new[] { 1, 15 })]
    public void ParseConnectedPorts_DecodesHardwareCaptures(string hex, int[] expected)
    {
        byte[] payload = Convert.FromHexString(hex);

        Assert.Equal(expected, MeshResponseParser.ParseConnectedPorts(payload));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ParseConnectedPorts_RejectsUnexpectedPayloadLength(int length)
    {
        Assert.Throws<FireflyProtocolException>(
            () => MeshResponseParser.ParseConnectedPorts(new byte[length]));
    }

    [Fact]
    public void ParseMeshDeviceIds_ExtractsIdsAtStrideFiveOffsets()
    {
        byte[] payload = [0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x03, 0x04, 0x00];
        var ids = MeshResponseParser.ParseMeshDeviceIds(payload);
        Assert.Equal([(ushort)0x0102, (ushort)0x0304], ids);
    }

    [Fact]
    public void ParseMeshDeviceIds_DeduplicatesRepeatedIds()
    {
        byte[] payload = [0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00];
        var ids = MeshResponseParser.ParseMeshDeviceIds(payload);
        Assert.Single(ids);
    }

    [Fact]
    public void ParseBatteryLevel_ReadsTwoBytesFourFromTheEnd()
    {
        byte[] frame = new byte[10];
        frame[6] = 0x0C;
        frame[7] = 0x1C;
        Assert.Equal(0x0C1C, MeshResponseParser.ParseBatteryLevel(frame));
    }

    [Fact]
    public void ParseBatteryLevel_ReturnsZeroForShortFrames()
    {
        Assert.Equal(0, MeshResponseParser.ParseBatteryLevel(new byte[3]));
    }

    [Fact]
    public void ParseFirmwareDate_FindsEmbeddedDateString()
    {
        byte[] frame = Encoding.UTF8.GetBytes("junk-2024-05-01-junk");
        Assert.Equal("2024-05-01", MeshResponseParser.ParseFirmwareDate(frame));
    }

    [Fact]
    public void ParseFirmwareDate_ReturnsNullWhenNoDatePresent()
    {
        byte[] frame = [0x01, 0x02, 0x03];
        Assert.Null(MeshResponseParser.ParseFirmwareDate(frame));
    }
}
