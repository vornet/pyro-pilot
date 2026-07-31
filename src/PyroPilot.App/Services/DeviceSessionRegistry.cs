using System.Collections.Concurrent;

namespace PyroPilot.App.Services;

/// <summary>
/// Shared table of currently-connected device sessions, keyed by
/// <see cref="PyroPilot.Core.Model.PairedDevice.Id"/>. The Devices screen
/// populates this as the operator connects; the Show Editor's live-fire
/// playback reads from it so a timeline cue can drive a real device without
/// the two screens needing a direct reference to each other.
/// </summary>
public sealed class DeviceSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, IDeviceSession> _sessions = new();

    public void Register(Guid deviceId, IDeviceSession session) => _sessions[deviceId] = session;

    public void Unregister(Guid deviceId) => _sessions.TryRemove(deviceId, out _);

    public bool TryGet(Guid deviceId, out IDeviceSession session) => _sessions.TryGetValue(deviceId, out session!);
}
