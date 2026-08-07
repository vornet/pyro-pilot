namespace PyroPilot.Core.Model;

/// <summary>Which FireFly wire protocol a <see cref="PairedDevice"/> speaks.</summary>
public enum DeviceProtocol
{
    /// <summary>V2 mesh protocol (FireFly Plus / GT2404-A), SSID contains "TitanFire".</summary>
    Mesh,

    /// <summary>V3 single-device protocol (standalone FireFly / GT2404), SSID contains "GT2404".</summary>
    Single,
}

/// <summary>
/// A device the operator has connected to and saved connection details for.
/// Cues on a Fire track reference one of these plus a port number.
/// </summary>
public sealed class PairedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nickname { get; set; } = "Device";
    public DeviceProtocol Protocol { get; set; } = DeviceProtocol.Mesh;
    public string Host { get; set; } = "192.168.8.1";
    public int Port { get; set; } = 7008;

    /// <summary>Join the standard TitanFire access point before opening the device socket.</summary>
    public bool JoinTitanFireWifi { get; set; }

    /// <summary>Reconnect this device when PyroPilot starts.</summary>
    public bool AutoConnect { get; set; }

    /// <summary>Mesh device id (from the mesh list), null/unused for <see cref="DeviceProtocol.Single"/>.</summary>
    public ushort? MeshDeviceId { get; set; }

    public int PortCount { get; set; } = 15;
}
