using System.IO.Compression;
using System.Text;

namespace ServerCore;

public sealed class HttpResponse
{
    public int StatusCode { get; init; } = 200;
    public string ReasonPhrase { get; init; } = "OK";
    public string ContentType { get; init; } = "text/plain; charset=utf-8";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; init; } = Array.Empty<byte>();

    public byte[] ToBytes(bool gzip)
    {
        byte[] bodyBytes = Body;
        var headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = ContentType,
            ["Connection"] = "close"
        };

        if (gzip && bodyBytes.Length > 0)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            {
                gz.Write(bodyBytes, 0, bodyBytes.Length);
            }
            bodyBytes = ms.ToArray();
            headers["Content-Encoding"] = "gzip";
        }

        headers["Content-Length"] = bodyBytes.Length.ToString();

        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {StatusCode} {ReasonPhrase}\r\n");
        foreach (var kv in headers)
        {
            sb.Append(kv.Key).Append(": ").Append(kv.Value).Append("\r\n");
        }
        sb.Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var result = new byte[headerBytes.Length + bodyBytes.Length];
        Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
        Buffer.BlockCopy(bodyBytes, 0, result, headerBytes.Length, bodyBytes.Length);
        return result;
    }
}

public static class MimeTypes
{
    private static readonly Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".txt"] = "text/plain; charset=utf-8",
        [".ico"] = "image/x-icon"
    };

    public static string GetContentType(string path)
    {
        string ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return "application/octet-stream";
        return map.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
    }
}




