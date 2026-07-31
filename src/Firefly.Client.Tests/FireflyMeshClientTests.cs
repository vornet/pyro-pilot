using Firefly.Client.Protocol.Mesh;
using Firefly.Client.Tests.TestSupport;

namespace Firefly.Client.Tests;

public class FireflyMeshClientTests
{
    [Fact]
    public async Task LoginAsync_SendsLoginFrame_AndParsesSuccess()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Success(MeshCommand.Login), []));
        await using var client = new FireflyMeshClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.Equal(MeshCommandBuilder.Login(), transport.SentFrames.Single());
        Assert.True(response.IsSuccess);
        Assert.True(response.IsRecognizedStatus);
        Assert.True(response.ChecksumValid);
    }

    [Fact]
    public async Task LoginAsync_ParsesFailure()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Failure(MeshCommand.Login), []));
        await using var client = new FireflyMeshClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.False(response.IsSuccess);
        Assert.True(response.IsRecognizedStatus);
    }

    [Fact]
    public async Task GetMeshListAsync_ParsesDeviceIdsFromResponse()
    {
        var transport = new FakeTransport();
        // payload: filler byte, id 0x0102 at offset 1-2, three filler bytes, id 0x0304 at offset 6-7, filler
        byte[] payload = [0x00, 0x01, 0x02, 0x00, 0x00, 0x00, 0x03, 0x04, 0x00];
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Success(MeshCommand.MeshList), payload));
        await using var client = new FireflyMeshClient(transport);
        await client.ConnectAsync();

        var devices = await client.GetMeshListAsync();

        Assert.Equal([(ushort)0x0102, (ushort)0x0304], devices);
    }

    [Fact]
    public async Task ManualFireAsync_SendsPortAndConfiguredFireDelay()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Success(MeshCommand.ManualFire), []));
        await using var client = new FireflyMeshClient(transport) { FireDelayMode = FireDelayMode.EMode };
        await client.ConnectAsync();

        await client.ManualFireAsync(0x0102, port: 7);

        byte[] sent = transport.SentFrames.Single();
        Assert.Equal(MeshCommandBuilder.ManualFire(0x0102, 7, FireDelayMode.EMode), sent);
    }

    [Fact]
    public async Task UnrecognizedStatusByte_IsSurfacedAsUnrecognizedNotSuccess()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)0x7E, [])); // not a known success/failure code
        await using var client = new FireflyMeshClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.False(response.IsSuccess);
        Assert.False(response.IsRecognizedStatus);
    }

    [Fact]
    public async Task Exchanges_AreSerialized_OneAtATime()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Success(MeshCommand.Login), []));
        transport.EnqueueResponse(MeshFrame.Encode(0x0000, (MeshCommand)MeshCommandCodes.Success(MeshCommand.MeshList), []));
        await using var client = new FireflyMeshClient(transport);
        await client.ConnectAsync();

        var loginTask = client.LoginAsync();
        var listTask = client.GetMeshListAsync();
        await Task.WhenAll(loginTask, listTask);

        Assert.Equal(2, transport.SentFrames.Count);
    }
}
