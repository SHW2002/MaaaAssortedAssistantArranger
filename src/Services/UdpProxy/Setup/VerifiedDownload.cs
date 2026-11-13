using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Game_Daily_Routine_Launcher;

internal sealed class VerifiedDownload(string path, FileStream stream) : IDisposable
{
    public string Path { get; } = path;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };

    public static async Task<VerifiedDownload> GetAsync(string url, string hash, string directory,
        CancellationToken cancellationToken)
    {
        var path = System.IO.Path.Combine(directory, System.IO.Path.GetFileName(new Uri(url).AbsolutePath));
        using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(file, cancellationToken);
        }
        // Keep the verified bytes locked against writes/deletion until the installer exits.
        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"下载文件 SHA-256 不匹配，已拒绝执行：{System.IO.Path.GetFileName(path)}");
            return new(path, input);
        }
        catch { input.Dispose(); throw; }
    }

    public void Dispose() => stream.Dispose();
}
