using Firefly.Client.Protocol.Single;
using Firefly.Client.Tests.TestSupport;

namespace Firefly.Client.Tests;

public class FireflySingleClientTests
{
    [Fact]
    public async Task LoginAsync_SendsLoginFrame_AndParsesSuccess()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse([0xBB, 0x01, 0x01, 0x00]); // BB0101 + reserved byte, per MODULE_PASSWORD_SUCCESS
        await using var client = new FireflySingleClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.Equal(SingleCommandBuilder.Login(), transport.SentFrames.Single());
        Assert.True(response.IsValidPrefix);
        Assert.True(response.IsSuccess);
        Assert.Equal((byte)SingleCommand.Login, response.EchoedCommand);
    }

    [Fact]
    public async Task LoginAsync_ParsesFailure()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse([0xBB, 0x01, 0x00, 0x00]); // BB0100, per MODULE_PASSWORD_FAILURE
        await using var client = new FireflySingleClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.False(response.IsSuccess);
    }

    [Fact]
    public async Task ManualFireAsync_SendsPortByte()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse([0xBB, 0x04, 0x01, 0x00]); // per CUE_MANUL_FIRING_STATUS_SUCCESS
        await using var client = new FireflySingleClient(transport);
        await client.ConnectAsync();

        var response = await client.ManualFireAsync(9);

        Assert.Equal(SingleCommandBuilder.ManualFire(9), transport.SentFrames.Single());
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task WriteCueTableAsync_StopsAtFirstFailure()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse([0xBB, 0x05, 0x01, 0x00]); // port 0 succeeds
        transport.EnqueueResponse([0xBB, 0x05, 0x00, 0x00]); // port 1 fails
        await using var client = new FireflySingleClient(transport);
        await client.ConnectAsync();

        var cues = new List<(byte Port, ushort DurationMs)> { (0, 100), (1, 200), (2, 300) };
        var results = await client.WriteCueTableAsync(cues);

        Assert.Equal(2, results.Count); // stopped after the failure, never attempted port 2
        Assert.True(results[0].IsSuccess);
        Assert.False(results[1].IsSuccess);
        Assert.Equal(2, transport.SentFrames.Count);
    }

    [Fact]
    public async Task InvalidPrefix_IsSurfaced()
    {
        var transport = new FakeTransport();
        transport.EnqueueResponse([0x00, 0x01, 0x01, 0x00]);
        await using var client = new FireflySingleClient(transport);
        await client.ConnectAsync();

        var response = await client.LoginAsync();

        Assert.False(response.IsValidPrefix);
    }
}
