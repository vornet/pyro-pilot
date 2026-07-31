namespace Firefly.Client.Models;

/// <summary>Result of a DEVICE_INFO query, plus the underlying response for anything not parsed here.</summary>
public sealed record MeshDeviceInfo(string? FirmwareDate, MeshResponse Response);
