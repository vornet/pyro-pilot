using Firefly.Client.Protocol.Single;

namespace Firefly.Client.Models;

/// <summary>
/// Decoded result of a V3 single-device request/response exchange.
///
/// CAVEAT: the original app always reads a fixed 4 bytes for every V3
/// response regardless of command, and the only response payloads visible
/// as literal constants in the app are 3 bytes (e.g. "BB0101"). Byte-for-byte semantics beyond
/// [0xBB][echoedCommand][resultFlag] are inferred, not confirmed against a
/// real device -- <see cref="RawFrame"/> and <see cref="ExtraByte"/> are
/// exposed so callers can inspect/adjust once validated against hardware,
/// particularly for the STATUS command's port-state reporting.
/// </summary>
/// <param name="Command">The command that was sent.</param>
/// <param name="IsValidPrefix">False if the response didn't start with 0xBB.</param>
/// <param name="EchoedCommand">Byte 1 of the response -- observed to echo back the command byte that was sent.</param>
/// <param name="IsSuccess">True if byte 2 of the response was 0x01 (observed pattern: 0x01 = success, 0x00 = failure/retry).</param>
/// <param name="ExtraByte">Byte 3 of the response -- meaning unconfirmed; likely carries additional state for multi-bit responses like STATUS.</param>
/// <param name="RawFrame">The complete 4-byte raw response.</param>
public sealed record SingleResponse(
    SingleCommand Command,
    bool IsValidPrefix,
    byte EchoedCommand,
    bool IsSuccess,
    byte ExtraByte,
    byte[] RawFrame)
{
    public static SingleResponse Decode(SingleCommand command, byte[] rawFrame)
    {
        ArgumentNullException.ThrowIfNull(rawFrame);
        if (rawFrame.Length != 4)
            throw new FireflyProtocolException($"Expected a 4-byte V3 response, got {rawFrame.Length} byte(s).");

        bool validPrefix = rawFrame[0] == SingleFrame.ResponseStartByte;
        return new SingleResponse(command, validPrefix, rawFrame[1], rawFrame[2] == 0x01, rawFrame[3], rawFrame);
    }
}
