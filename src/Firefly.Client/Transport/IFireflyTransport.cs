namespace Firefly.Client.Transport;

/// <summary>
/// Abstraction over the raw TCP socket to a FireFly device, so protocol clients
/// can be unit tested without a real device or network stack.
/// </summary>
public interface IFireflyTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(TimeSpan connectTimeout, CancellationToken cancellationToken = default);

    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Reads exactly <paramref name="count"/> bytes, blocking until they arrive or <paramref name="timeout"/> elapses.</summary>
    Task<byte[]> ReadExactAsync(int count, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads whatever bytes trickle in until the socket briefly goes quiet (no more
    /// buffered bytes available to read without blocking). Used for the mesh (V2)
    /// protocol's variable-length responses, mirroring the original app's polling
    /// read loop. Prefer <see cref="ReadExactAsync"/> when the response length is
    /// known up front (it's more robust than relying on a quiet period).
    /// </summary>
    Task<byte[]> ReadUntilQuietAsync(TimeSpan overallTimeout, CancellationToken cancellationToken = default);

    void Disconnect();
}
