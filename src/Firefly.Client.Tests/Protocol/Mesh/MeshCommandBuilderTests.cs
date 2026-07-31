using Firefly.Client.Protocol.Mesh;
using Firefly.Client.Utilities;

namespace Firefly.Client.Tests.Protocol.Mesh;

/// <summary>
/// Pins every mesh (V2) command encoding to the literal hex strings found in
/// the original app's command table (before its checksum + end-flag trailer,
/// which is verified separately in MeshFrameTests). This is the ground truth
/// for correctness in the absence of a real device to test against.
/// </summary>
public class MeshCommandBuilderTests
{
    private const ushort DeviceId = 0x0102;

    private static byte[] WithoutTrailer(byte[] frame) => frame[..^2];

    [Fact]
    public void Login_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.Login();
        Assert.Equal(HexConvert.FromHex("550000FFFF01080000000000000000"), WithoutTrailer(frame));
    }

    [Fact]
    public void MeshList_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.MeshList();
        Assert.Equal(HexConvert.FromHex("550000FFFF0200"), WithoutTrailer(frame));
    }

    [Fact]
    public void PortStatus_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.PortStatus(DeviceId);
        Assert.Equal(HexConvert.FromHex("55000001020300"), WithoutTrailer(frame));
    }

    [Fact]
    public void ManualFire_MatchesAppLiteral()
    {
        // port=5, default (Normal) fire delay -> "0402" + port("05") + delay("20")
        byte[] frame = MeshCommandBuilder.ManualFire(DeviceId, port: 5);
        Assert.Equal(HexConvert.FromHex("550000010204020520"), WithoutTrailer(frame));
    }

    [Fact]
    public void ManualFire_EModeUsesEModeDelayByte()
    {
        byte[] frame = MeshCommandBuilder.ManualFire(DeviceId, port: 5, fireDelay: FireDelayMode.EMode);
        Assert.Equal(HexConvert.FromHex("550000010204020502"), WithoutTrailer(frame));
    }

    [Fact]
    public void StartAutoFire_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.StartAutoFire(DeviceId, 0xFFFF);
        Assert.Equal(HexConvert.FromHex("55000001020602FFFF"), WithoutTrailer(frame));
    }

    [Fact]
    public void StopAutoFire_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.StopAutoFire(DeviceId, 0xFFFF);
        Assert.Equal(HexConvert.FromHex("55000001020702FFFF"), WithoutTrailer(frame));
    }

    [Fact]
    public void DeletePlan_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.DeletePlan(DeviceId);
        Assert.Equal(HexConvert.FromHex("550000010209 0101".Replace(" ", "")), WithoutTrailer(frame));
    }

    [Fact]
    public void StartPlan_DefaultsToBroadcast_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.StartPlan();
        Assert.Equal(HexConvert.FromHex("550000FFFF0B0101"), WithoutTrailer(frame));
    }

    [Fact]
    public void ClearPlan_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.ClearPlan(DeviceId);
        Assert.Equal(HexConvert.FromHex("5500000102 0A00".Replace(" ", "")), WithoutTrailer(frame));
    }

    [Fact]
    public void FlashLed_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.FlashLed(DeviceId);
        Assert.Equal(HexConvert.FromHex("5500000102 12020610".Replace(" ", "")), WithoutTrailer(frame));
    }

    [Fact]
    public void DeviceInfo_DefaultsToBroadcast_MatchesAppLiteral()
    {
        byte[] frame = MeshCommandBuilder.DeviceInfo();
        Assert.Equal(HexConvert.FromHex("550000FFFF1500"), WithoutTrailer(frame));
    }

    [Fact]
    public void ModifyMeshParam_MatchesAppEncoding()
    {
        // ssid="AB", password="12345678" -> UTF-8 hex, each right-padded with ASCII '0' to 40 hex chars.
        byte[] frame = MeshCommandBuilder.ModifyMeshParam(DeviceId, "AB", "12345678");

        string ssidField = "4142".PadRight(40, '0'); // "AB" -> 0x41 0x42
        string passwordField = "3132333435363738".PadRight(40, '0'); // "12345678"
        string expectedHeader = "5500000102" + "1428"; // cmd 0x14, len 0x28 (40 bytes = two 20-byte fields)

        Assert.Equal(HexConvert.FromHex(expectedHeader + ssidField + passwordField), WithoutTrailer(frame));
    }

    [Fact]
    public void PortTimeEntry_PadsShortTimesToTwoBytes()
    {
        // 30ms -> hex "1E" padded to "001E", plus the Normal-mode delay byte "20"
        byte[] entry = MeshCommandBuilder.PortTimeEntry(30, FireDelayMode.Normal);
        Assert.Equal(HexConvert.FromHex("001E20"), entry);
    }

    [Fact]
    public void PortTimeEntry_ZeroTimeUsesZeroDelayByte()
    {
        byte[] entry = MeshCommandBuilder.PortTimeEntry(0, FireDelayMode.Normal);
        Assert.Equal(HexConvert.FromHex("000000"), entry);
    }

    [Fact]
    public void AddPlanFire_BuildsFixedFifteenPortTable()
    {
        var times = Enumerable.Range(0, MeshCommandBuilder.PortCount).Select(i => i * 30).ToList();
        byte[] frame = MeshCommandBuilder.AddPlanFire(DeviceId, times);

        byte[] body = WithoutTrailer(frame);
        // header(7) + prefix(1) + 15*3 data bytes
        Assert.Equal(7 + 1 + MeshCommandBuilder.PortCount * 3, body.Length);
        Assert.Equal((byte)MeshCommand.AddPlanFire, body[5]);
        Assert.Equal((byte)(1 + MeshCommandBuilder.PortCount * 3), body[6]); // declared length = 0x2E for the app's fixed 15-port case
        Assert.Equal(0x2E, body[6]);
        Assert.Equal(0x01, body[7]); // fixed prefix byte the app always sends
    }

    [Fact]
    public void AddPlanFire_RejectsWrongPortCount()
    {
        Assert.Throws<ArgumentException>(() => MeshCommandBuilder.AddPlanFire(DeviceId, new List<int> { 0, 30 }));
    }
}
