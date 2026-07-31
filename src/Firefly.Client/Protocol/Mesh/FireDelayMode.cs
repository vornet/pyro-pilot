namespace Firefly.Client.Protocol.Mesh;

/// <summary>
/// The per-port "fire delay" byte appended to timing commands, toggled in the
/// app by a UI switch. The exact real-world meaning of "E mode" isn't
/// documented in the app beyond the toggle label; treat these as opaque
/// device-defined mode bytes.
/// </summary>
public enum FireDelayMode : byte
{
    Normal = 0x20,
    EMode = 0x02,
}
