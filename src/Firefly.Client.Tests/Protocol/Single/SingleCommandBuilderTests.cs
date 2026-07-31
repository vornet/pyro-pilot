using Firefly.Client.Protocol.Single;
using Firefly.Client.Utilities;

namespace Firefly.Client.Tests.Protocol.Single;

/// <summary>
/// Pins every V3 single-device command encoding to the literal hex strings
/// built by the original app's command-frame builder. Unlike the mesh
/// protocol, these frames carry no checksum, so the full frame is compared directly.
/// </summary>
public class SingleCommandBuilderTests
{
    [Fact]
    public void Login_MatchesAppLiteral()
    {
        // The app's WiFi login flow: "AA0501" + passcode("000000") + "01"
        // (same command builder as the BLE login) plus a literal trailing "04"
        // (the manual-fire command byte) appended by the WiFi caller -- the frame's
        // own length byte (05) does not account for this trailing byte.
        Assert.Equal(HexConvert.FromHex("AA05010000000104"), SingleCommandBuilder.Login());
    }

    [Fact]
    public void ManualFire_MatchesAppLiteral()
    {
        // "AA0677" + passcode + "04" + port("05")
        Assert.Equal(HexConvert.FromHex("AA06770000000405"), SingleCommandBuilder.ManualFire(5));
    }

    [Fact]
    public void Cue_MatchesAppLiteral()
    {
        // "AA0877" + passcode + "05" + port("05") + duration(1500ms -> "05DC")
        Assert.Equal(HexConvert.FromHex("AA0877000000050505DC"), SingleCommandBuilder.Cue(5, 1500));
    }

    [Fact]
    public void Fire_MatchesAppLiteral()
    {
        Assert.Equal(HexConvert.FromHex("AA057700000006"), SingleCommandBuilder.Fire());
    }

    [Fact]
    public void Test_MatchesAppLiteral()
    {
        Assert.Equal(HexConvert.FromHex("AA057700000007"), SingleCommandBuilder.Test());
    }

    [Fact]
    public void Status_MatchesAppLiteral()
    {
        Assert.Equal(HexConvert.FromHex("AA057700000008"), SingleCommandBuilder.Status());
    }
}
