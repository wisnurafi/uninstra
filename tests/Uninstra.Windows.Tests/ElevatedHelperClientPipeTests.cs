namespace Uninstra.Windows.Tests;

using System.IO.Pipes;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;
using Uninstra.Windows.Services;
using Xunit;

/// <summary>
/// Exercises ElevatedHelperClient against an in-process fake pipe server.
/// Regression guard for the disposal-order fix: a completed request must not
/// leak ObjectDisposedException ("Cannot access a closed pipe") from teardown.
/// NOTE: uses the production pipe name because the client hardcodes it. Close
/// any running Uninstra.ElevatedHelper before running these tests.
/// </summary>
public class ElevatedHelperClientPipeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Accepts one connection, echoes an ElevatedResponse for the received line.</summary>
    private static Task StartFakeServerOnce(string responseJson, CancellationTokenSource cts) => Task.Run(async () =>
    {
        await using var server = new NamedPipeServerStream(
            "Uninstra.ElevatedHelper", PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(cts.Token);
        var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(server, leaveOpen: true);
        await reader.ReadLineAsync(cts.Token); // consume the single-line JSON request
        await writer.WriteLineAsync(responseJson.AsMemory(), cts.Token);
    }, cts.Token);

    private static ElevatedRequest MakeRequest() => new()
    {
        RequestId = "req-0000000000000001",
        SessionId = "TESTSESSION01",
        Nonce = "ABCD1234ABCD1234",
        OperationType = ElevatedOperationType.MoveToQuarantine,
        Payload = @"{""source"":""C:\\test\\x.txt""}",
        Timestamp = DateTime.UtcNow
    };

    [Fact]
    public async Task SendAsync_RoundTripsResponse_WithoutTeardownExceptions()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var request = MakeRequest();
        var response = new ElevatedResponse
        {
            RequestId = request.RequestId,
            Success = true,
            Message = "ok"
        };
        var serverTask = StartFakeServerOnce(
            JsonSerializer.Serialize(response, JsonOptions), cts);

        var client = new ElevatedHelperClient(
            NullLogger<ElevatedHelperClient>.Instance);

        // Must return normally — no ObjectDisposedException escaping teardown.
        var result = await client.SendAsync(request, cts.Token);

        result.Success.Should().BeTrue();
        result.RequestId.Should().Be(request.RequestId);
        result.Message.Should().Be("ok");
    }

    [Fact]
    public async Task SendAsync_NoServer_ReturnsGracefulUnavailableError()
    {
        var client = new ElevatedHelperClient(
            NullLogger<ElevatedHelperClient>.Instance);

        var result = await client.SendAsync(MakeRequest());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("HELPER_UNAVAILABLE");
    }
}
