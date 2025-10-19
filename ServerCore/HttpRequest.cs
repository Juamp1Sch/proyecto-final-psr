using System.Net;
using System.Text;

namespace ServerCore;

public sealed class HttpRequest
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required string HttpVersion { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
    public required byte[] Body { get; init; }
    public required Dictionary<string, string> QueryParams { get; init; }
    public required IPEndPoint RemoteEndPoint { get; init; }

    public string GetHeader(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : string.Empty;
    }
}

public static class HttpRequestParser
{
    public static HttpRequest Parse(ReadOnlySpan<byte> rawRequest, IPEndPoint remoteEndPoint)
    {
        // Find header/body separator: CRLF CRLF
        int sep = IndexOf(rawRequest, "\r\n\r\n"u8);
        if (sep < 0)
        {
            throw new InvalidOperationException("Invalid HTTP request: missing header terminator");
        }

        ReadOnlySpan<byte> headerBytes = rawRequest[..sep];
        ReadOnlySpan<byte> bodyBytes = rawRequest[(sep + 4)..];

        // Split headers by CRLF
        var headerLines = Encoding.ASCII.GetString(headerBytes).Split("\r\n", StringSplitOptions.None);
        if (headerLines.Length == 0)
        {
            throw new InvalidOperationException("Invalid HTTP request: empty header lines");
        }

        // Request line: METHOD SP PATH SP HTTP/VERSION
        var parts = headerLines[0].Split(' ');
        if (parts.Length < 3)
        {
            throw new InvalidOperationException("Invalid request line");
        }
        string method = parts[0].Trim().ToUpperInvariant();
        string pathWithQuery = parts[1].Trim();
        string httpVersion = parts[2].Trim();

        // Headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < headerLines.Length; i++)
        {
            string line = headerLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            headers[name] = value;
        }

        // Query params
        string path = pathWithQuery;
        var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int qIndex = pathWithQuery.IndexOf('?');
        if (qIndex >= 0)
        {
            path = pathWithQuery[..qIndex];
            string query = pathWithQuery[(qIndex + 1)..];
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                string k = Uri.UnescapeDataString(kv[0]);
                string v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                queryParams[k] = v;
            }
        }

        return new HttpRequest
        {
            Method = method,
            Path = path,
            HttpVersion = httpVersion,
            Headers = headers,
            Body = bodyBytes.ToArray(),
            QueryParams = queryParams,
            RemoteEndPoint = remoteEndPoint
        };
    }

    private static int IndexOf(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        for (int i = 0; i <= span.Length - value.Length; i++)
        {
            if (span[i] == value[0] && span.Slice(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }
        return -1;
    }
}


