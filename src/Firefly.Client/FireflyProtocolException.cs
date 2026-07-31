namespace Firefly.Client;

/// <summary>
/// Thrown when a response from a FireFly device is malformed or fails validation
/// (bad start/end flag, checksum mismatch, declared-length mismatch, etc).
/// Connection-level failures surface as the underlying <see cref="System.Net.Sockets.SocketException"/>,
/// <see cref="System.IO.IOException"/>, or <see cref="TimeoutException"/> instead.
/// </summary>
public sealed class FireflyProtocolException : Exception
{
    public FireflyProtocolException(string message) : base(message)
    {
    }

    public FireflyProtocolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
