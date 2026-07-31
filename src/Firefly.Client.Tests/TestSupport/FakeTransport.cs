using Firefly.Client.Transport;

namespace Firefly.Client.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IFireflyTransport"/> double: records every frame sent
/// and serves pre-enqueued bytes back to reads, so the high-level clients'
/// send/receive framing logic can be tested without a real socket or device.
/// </summary>
internal sealed class FakeTransport : IFireflyTransport
{
    private readonly Queue<byte> _incoming = new();

    public List<byte[]> SentFrames { get; } = [];

    public bool IsConnected { get; private set; }

    public void EnqueueResponse(byte[] bytes)
    {
        foreach (byte b in bytes) _incoming.Enqueue(b);
    }

    public Task ConnectAsync(TimeSpan connectTimeout, CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        SentFrames.Add(data);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadExactAsync(int count, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (count == 0) return Task.FromResult(Array.Empty<byte>());

        var buffer = new byte[count];
        for (int i = 0; i < count; i++)
        {
            if (_incoming.Count == 0)
                throw new IOException($"FakeTransport ran out of buffered response bytes (wanted {count}, had {i}).");
            buffer[i] = _incoming.Dequeue();
        }
        return Task.FromResult(buffer);
    }

    public Task<byte[]> ReadUntilQuietAsync(TimeSpan overallTimeout, CancellationToken cancellationToken = default)
    {
        byte[] remaining = _incoming.ToArray();
        _incoming.Clear();
        return Task.FromResult(remaining);
    }

    public void Disconnect() => IsConnected = false;

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
