using System.IO;
using System.Net.Sockets;

namespace Game_Daily_Routine_Launcher;

public static class Socks5UdpProbe
{
    // Check UDP ASSOCIATE support before starting the game. This does not prove
    // that a particular upstream node can reach the game's remote UDP endpoint.
    public static async Task CheckAsync(UdpProxyConfig config, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(config.Socks5Host, config.Socks5Port, timeout.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token);
            var greeting = new byte[2];
            await stream.ReadExactlyAsync(greeting, timeout.Token);
            if (greeting[0] != 5 || greeting[1] != 0)
                throw new IOException("上游必须支持无需认证的 SOCKS5，本功能不支持 HTTP 代理或密码认证。");
            await stream.WriteAsync(new byte[] { 5, 3, 0, 1, 0, 0, 0, 0, 0, 0 }, timeout.Token);
            var reply = new byte[4];
            await stream.ReadExactlyAsync(reply, timeout.Token);
            if (reply[0] != 5 || reply[1] != 0 || reply[2] != 0)
                throw new IOException($"SOCKS5 UDP ASSOCIATE 被拒绝，响应码 {reply[1]}。");
            var addressLength = reply[3] switch
            {
                1 => 4,
                4 => 16,
                3 => await ReadDomainLengthAsync(stream, timeout.Token),
                _ => throw new IOException("SOCKS5 UDP 响应地址格式无效。")
            };
            var endpoint = new byte[addressLength + 2];
            await stream.ReadExactlyAsync(endpoint, timeout.Token);
            if (endpoint[^1] == 0 && endpoint[^2] == 0)
                throw new IOException("SOCKS5 UDP 中继端口无效。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("SOCKS5 UDP 检查超时，请确认上游代理已运行、端口正确。");
        }
    }

    private static async Task<int> ReadDomainLengthAsync(NetworkStream stream, CancellationToken ct)
    {
        var length = new byte[1];
        await stream.ReadExactlyAsync(length, ct);
        if (length[0] == 0) throw new IOException("SOCKS5 中继主机名为空。");
        return length[0];
    }
}
