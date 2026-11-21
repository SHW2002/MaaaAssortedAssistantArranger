using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Game_Daily_Routine_Launcher.Tests;

public class TaskLogRelayTests
{
    [Fact]
    public async Task SharedRelayReportsIncomingLogsOnceAndKeepsForwarding()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var upstream = builder.Build();
        upstream.MapPost("/webhook", () => Results.Ok());
        await upstream.StartAsync();

        using var relay = new WebhookRelayService();
        var logsReceived = 0;
        relay.TaskLogReceived += () => Interlocked.Increment(ref logsReceived);
        var port = GetAvailablePort();
        var started = await relay.StartAsync(port, $"{upstream.Urls.Single()}/webhook");
        Assert.True(started.Success, started.Message);

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
        using var missing = await client.PostAsync("/other", new StringContent("ignored"));
        using var empty = await client.PostAsync("/webhook", new StringContent(" "));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(0, Volatile.Read(ref logsReceived));

        using var response = await client.PostAsync("/webhook", new StringContent("{\"message\":\"running\"}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, Volatile.Read(ref logsReceived));
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
