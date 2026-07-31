using Firefly.Client.Protocol.Mesh;

namespace Firefly.Client.Models;

/// <summary>Decoded result of a single mesh (V2) request/response exchange.</summary>
/// <param name="Command">The command that was sent.</param>
/// <param name="IsSuccess">True if the device's status byte was this command's success code.</param>
/// <param name="IsRecognizedStatus">False if the status byte didn't match either the success or failure code for this command (unexpected/unknown response).</param>
/// <param name="ChecksumValid">False if the frame's checksum did not match its contents -- treat the response with suspicion.</param>
/// <param name="StatusByte">The raw status/echoed-command byte (frame offset 5).</param>
/// <param name="Data">The payload bytes, header and trailer stripped.</param>
/// <param name="RawFrame">The complete raw response frame, for callers that need to inspect fields this library doesn't parse yet (e.g. PORT_STATUS's per-port breakdown).</param>
public sealed record MeshResponse(
    MeshCommand Command,
    bool IsSuccess,
    bool IsRecognizedStatus,
    bool ChecksumValid,
    byte StatusByte,
    byte[] Data,
    byte[] RawFrame);
