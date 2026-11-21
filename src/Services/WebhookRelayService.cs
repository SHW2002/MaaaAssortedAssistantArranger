using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Game_Daily_Routine_Launcher;

public sealed class WebhookRelayService : IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IHost? _host;
    private WebhookRelayRoute? _route;

    public bool IsRunning => _host is not null;
    public int Port { get; private set; }
    internal event Action? TaskLogReceived;

    public async Task<WebhookRelayResult> StartAsync(int port, string sourceUrl, Func<string, string?, Task>? logAsync = null)
    {
        await _gate.WaitAsync();
        try
        {
            if (!WebhookRelayRoute.TryCreate(sourceUrl, out var route, out var routeError))
                return new WebhookRelayResult(false, $"Webhook 中转启动失败：{routeError}");

            if (IsRunning && Port == port && route.Equals(_route))
                return new WebhookRelayResult(true, $"Webhook 中转已运行，端口 {port}。");

            await StopCoreAsync().ConfigureAwait(false);
            _route = route;

            var builder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(port);
                    });
                    webBuilder.Configure(app =>
                    {
                        app.Run(context => HandleRequestAsync(context, logAsync ?? ((_, _) => Task.CompletedTask)));
                    });
                });

            try
            {
                _host = builder.Build();
                await _host.StartAsync();
                Port = port;
                return new WebhookRelayResult(true, $"Webhook 中转已启动，监听端口 {port}，路径 {route.LocalPath}。");
            }
            catch (Exception ex)
            {
                _host = null;
                _route = null;
                return new WebhookRelayResult(false, $"Webhook 中转启动失败：{ex.Message}");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        if (_host is null)
            return;

        try
        {
            await _host.StopAsync();
        }
        catch
        {
        }
        finally
        {
            _host.Dispose();
            _host = null;
            _route = null;
        }
    }

    private async Task HandleRequestAsync(HttpContext context, Func<string, string?, Task> logAsync)
    {
        try
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await context.Response.WriteAsync("Only POST is supported.");
                return;
            }

            var route = _route;
            if (route is null)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Relay route is not configured.");
                return;
            }

            if (!route.Matches(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Request path does not match configured source url.");
                return;
            }

            var url = route.BuildForwardUrl(context.Request);
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var forwardedBody = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(forwardedBody))
                TaskLogReceived?.Invoke();
            await logAsync($"Webhook 中转接收：url={url}", forwardedBody);
            await logAsync("Webhook 中转已根据原请求 URL 映射真实地址，并原样转发请求 Body。", forwardedBody);

            var result = await ForwardAsync(url, forwardedBody);
            await logAsync(result.Message, BuildForwardLogBody(result));

            if (!result.Success)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsync("Forward failed.");
                return;
            }

            await context.Response.WriteAsync("OK");
        }
        catch (Exception ex)
        {
            RuntimeLogService.WriteException("Webhook 中转处理请求失败", ex);
            Debug.WriteLine($"Webhook relay error: {ex.Message}");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(ex.Message);
            }
        }
    }

    private async Task<WebhookRelayResult> ForwardAsync(string url, string bodyText)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(bodyText, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request);
            return new WebhookRelayResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode
                    ? $"Webhook 中转转发成功：url={url}，状态 {response.StatusCode}。"
                    : $"Webhook 中转转发失败：url={url}，状态 {response.StatusCode}。",
                url,
                bodyText);
        }
        catch (Exception ex)
        {
            RuntimeLogService.WriteException("Webhook 中转转发失败", ex);
            Debug.WriteLine($"Webhook relay forward failed: {ex.Message}");
            return new WebhookRelayResult(false, $"Webhook 中转转发失败：url={url}，原因：{ex.Message}", url, bodyText);
        }
    }

    private static string BuildForwardLogBody(WebhookRelayResult result)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(result.ForwardedUrl))
            builder.AppendLine($"实际转发URL：{result.ForwardedUrl}");

        if (!string.IsNullOrWhiteSpace(result.ForwardedBody))
        {
            builder.AppendLine("实际转发Body：");
            builder.AppendLine(result.ForwardedBody);
        }

        return builder.ToString().TrimEnd();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _httpClient.Dispose();
        _gate.Dispose();
    }
}

public sealed record WebhookRelayRoute(string ForwardBaseUrl, string LocalPath)
{
    public static bool TryCreate(string sourceUrl, out WebhookRelayRoute route, out string error)
    {
        route = new WebhookRelayRoute(string.Empty, string.Empty);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            error = "未配置原请求 URL。";
            return false;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "原请求 URL 必须是有效的 HTTP 或 HTTPS 地址。";
            return false;
        }

        route = new WebhookRelayRoute($"{uri.Scheme}://{uri.Authority}", uri.AbsolutePath);
        return true;
    }

    public bool Matches(HttpRequest request)
        => request.Path.Equals(LocalPath, StringComparison.OrdinalIgnoreCase);

    public string BuildForwardUrl(HttpRequest request)
        => $"{ForwardBaseUrl}{request.Path}{request.QueryString}";
}
