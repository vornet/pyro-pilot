namespace Firefly.Client.Protocol.Single;

/// <summary>
/// Command byte values for the V3 single-device protocol (also used verbatim
/// over classic Bluetooth serial/GATT in the app). Ported from the app's
/// command enum.
/// </summary>
public enum SingleCommand : byte
{
    Login = 0x01,
    ManualFire = 0x04,
    Cue = 0x05,
    Fire = 0x06,
    Test = 0x07,
    Status = 0x08,
}
