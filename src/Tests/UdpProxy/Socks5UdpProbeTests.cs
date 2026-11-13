using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Game_Daily_Routine_Launcher.Tests;

public class Socks5UdpProbeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AcceptsFragmentedIpv4DomainAndIpv6RelayReplies(int addressType)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var config = new UdpProxyConfig { Socks5Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var greeting = new byte[3];
            await stream.ReadExactlyAsync(greeting);
            Assert.Equal(new byte[] { 5, 1, 0 }, greeting);
            await stream.WriteAsync(new byte[] { 5, 0 });
            var request = new byte[10];
            await stream.ReadExactlyAsync(request);
            Assert.Equal(new byte[] { 5, 3, 0, 1, 0, 0, 0, 0, 0, 0 }, request);
            var address = addressType switch
            {
                1 => new byte[] { 127, 0, 0, 1 },
                3 => new byte[] { 3, (byte)'a', (byte)'b', (byte)'c' },
                _ => IPAddress.IPv6Loopback.GetAddressBytes()
            };
            foreach (var value in new byte[] { 5, 0, 0, (byte)addressType }.Concat(address).Concat(new byte[] { 0x1f, 0x90 }))
                await stream.WriteAsync(new[] { value });
        });
        await Socks5UdpProbe.CheckAsync(config, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsAuthenticationAndUdpAssociateRefusal(bool authenticationRequired)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var config = new UdpProxyConfig { Socks5Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            await stream.ReadExactlyAsync(new byte[3]);
            await stream.WriteAsync(new byte[] { 5, authenticationRequired ? (byte)2 : (byte)0 });
            if (authenticationRequired) return;
            await stream.ReadExactlyAsync(new byte[10]);
            await stream.WriteAsync(new byte[] { 5, 7, 0, 1 });
        });
        await Assert.ThrowsAsync<IOException>(() => Socks5UdpProbe.CheckAsync(config, CancellationToken.None));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancellationInterruptsStalledHandshake()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var cancellation = new CancellationTokenSource();
        var config = new UdpProxyConfig { Socks5Port = ((IPEndPoint)listener.LocalEndpoint).Port };
        var check = Socks5UdpProbe.CheckAsync(config, cancellation.Token);
        using var client = await listener.AcceptTcpClientAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
