namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// Success/failure status byte for each mesh command, taken verbatim from the
/// literal constants in the original app's source rather than computed from a
/// formula. Most follow the pattern success = cmd | 0x80, failure = cmd | 0xC0,
/// but ModifyMeshParam's failure code (0xCD) does not -- the app declares it
/// explicitly rather than 0xD4, which the formula would predict, so this table
/// preserves that as observed instead of "correcting" it.
/// </summary>
public static class MeshCommandCodes
{
    private static readonly Dictionary<MeshCommand, (byte Success, byte Failure)> Codes = new()
    {
        [MeshCommand.Login] = (0x81, 0xC1),
        [MeshCommand.MeshList] = (0x82, 0xC2),
        [MeshCommand.PortStatus] = (0x83, 0xC3),
        [MeshCommand.ManualFire] = (0x84, 0xC4),
        [MeshCommand.AutoIgnite] = (0x85, 0xC5),
        [MeshCommand.StartAutoFire] = (0x86, 0xC6),
        [MeshCommand.StopAutoFire] = (0x87, 0xC7),
        [MeshCommand.AddPlanFire] = (0x88, 0xC8),
        [MeshCommand.DeletePlan] = (0x89, 0xC9),
        [MeshCommand.ClearPlan] = (0x8A, 0xCA),
        [MeshCommand.StartPlan] = (0x8B, 0xCB),
        [MeshCommand.FlashLed] = (0x92, 0xD2),
        [MeshCommand.ModifyMeshParam] = (0x94, 0xCD), // anomalous failure code, see remarks above
        [MeshCommand.DeviceInfo] = (0x95, 0xD5),
    };

    public static byte Success(MeshCommand command) => Codes[command].Success;

    public static byte Failure(MeshCommand command) => Codes[command].Failure;

    public static bool TryGetCommand(byte statusByte, out MeshCommand command, out bool isSuccess)
    {
        foreach (var (cmd, (success, failure)) in Codes)
        {
            if (statusByte == success)
            {
                command = cmd;
                isSuccess = true;
                return true;
            }
            if (statusByte == failure)
            {
                command = cmd;
                isSuccess = false;
                return true;
            }
        }

        command = default;
        isSuccess = false;
        return false;
    }
}
