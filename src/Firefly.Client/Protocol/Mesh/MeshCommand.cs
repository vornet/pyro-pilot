namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// Command byte values for the V2 "mesh" WiFi protocol (TitanFire SSID,
/// 192.168.8.1:7008), ported from the original app's mesh command table.
/// </summary>
public enum MeshCommand : byte
{
    Login = 0x01,
    MeshList = 0x02,
    PortStatus = 0x03,
    ManualFire = 0x04,

    /// <summary>
    /// Declared in the app's command constants but never actually issued by
    /// its command table -- likely vestigial. Kept here for
    /// completeness/response-code recognition only.
    /// </summary>
    AutoIgnite = 0x05,

    StartAutoFire = 0x06,
    StopAutoFire = 0x07,

    /// <summary>"Cue write" -- programs the per-port fire-time table (the app's equivalent add-plan-fire command).</summary>
    AddPlanFire = 0x08,

    DeletePlan = 0x09,
    ClearPlan = 0x0A,
    StartPlan = 0x0B,
    FlashLed = 0x12,
    ModifyMeshParam = 0x14,
    DeviceInfo = 0x15,
}
