namespace PyroPilot.App.Services;

/// <summary>
/// Protocol-agnostic view of a connection to one FireFly device, wrapping
/// whichever of <see cref="Firefly.Client.FireflyMeshClient"/> or
/// <see cref="Firefly.Client.FireflySingleClient"/> matches the paired
/// device's protocol so the UI/playback layer doesn't need to branch on it.
/// </summary>
public interface IDeviceSession : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <returns>True if login succeeded.</returns>
    Task<bool> LoginAsync(CancellationToken cancellationToken = default);

    /// <returns>True if the device acknowledged the fire command.</returns>
    Task<bool> ManualFireAsync(int port, CancellationToken cancellationToken = default);
}
