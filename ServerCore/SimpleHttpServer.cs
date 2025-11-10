using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerCore;

public sealed class SimpleHttpServer : IDisposable
{
    private readonly ServerConfig config;
    private readonly DailyLogger logger;
    private TcpListener? listener;
    private volatile bool isRunning;

    public SimpleHttpServer(ServerConfig config)
    {
        this.config = config;
        string logDir = config.LogDirectory ?? Path.Combine(config.ConfigDirectory, "logs");
        logger = new DailyLogger(logDir);
        Directory.CreateDirectory(config.WebRoot);
    }

    public void Start()
    {
        if (isRunning) return;
        isRunning = true;
        listener = new TcpListener(IPAddress.Any, config.Port);
        listener.Start();
        _ = AcceptLoop();
    }

    public void Stop()
    {
        isRunning = false;
        try { listener?.Stop(); } catch { /* ignore */ }
        logger.Dispose();
    }

    private async Task AcceptLoop()
    {
        if (listener == null) return;
        while (isRunning)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch
            {
                if (!isRunning) break;
                continue;
            }
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            var remote = (IPEndPoint)client.Client.RemoteEndPoint!;

            using NetworkStream ns = client.GetStream();
            using var ms = new MemoryStream();
            var buffer = new byte[8192];

            
            int bytesRead;
            int headerEndIndex = -1;
            int contentLength = 0;
            do
            {
                bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (bytesRead <= 0) break;
                ms.Write(buffer, 0, bytesRead);
                if (headerEndIndex < 0)
                {
                    var data = ms.ToArray();
                    headerEndIndex = IndexOf(data, "\r\n\r\n"u8.ToArray());
                    if (headerEndIndex >= 0)
                    {
                        
                        var headerPart = Encoding.ASCII.GetString(data, 0, headerEndIndex);
                        foreach (var line in headerPart.Split("\r\n"))
                        {
                            int c = line.IndexOf(':');
                            if (c > 0 && line[..c].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(line[(c + 1)..].Trim(), out contentLength);
                                break;
                            }
                        }
                    }
                }
            } while (client.Available > 0 || (headerEndIndex >= 0 && (ms.Length - (headerEndIndex + 4)) < contentLength));

            var raw = ms.ToArray();
            if (raw.Length == 0) return;

            HttpRequest request;
            try
            {
                request = HttpRequestParser.Parse(raw, remote);
            }
            catch (Exception ex)
            {
                logger.Log($"{remote.Address} - Bad request: {ex.Message}");
                await WriteBytesAsync(ns, new HttpResponse
                {
                    StatusCode = 400,
                    ReasonPhrase = "Bad Request",
                    ContentType = "text/plain; charset=utf-8",
                    Body = Encoding.UTF8.GetBytes("Bad Request")
                }.ToBytes(false));
                return;
            }

            LogRequest(request);

            HttpResponse response = BuildResponse(request);
            bool gzip = config.EnableGzip && AcceptsGzip(request);
            var bytes = response.ToBytes(gzip);
            await WriteBytesAsync(ns, bytes);
        }
    }

    private void LogRequest(HttpRequest req)
    {
        var sb = new StringBuilder();
        sb.Append($"{req.RemoteEndPoint.Address} {req.Method} {req.Path}");
        if (req.QueryParams.Count > 0)
        {
            sb.Append(" ?");
            sb.Append(string.Join('&', req.QueryParams.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        string contentType = req.GetHeader("Content-Type");
        if (string.Equals(req.Method, "POST", StringComparison.OrdinalIgnoreCase) && req.Body.Length > 0)
        {
            string bodyPreview = contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
                ? Encoding.UTF8.GetString(req.Body)
                : Convert.ToBase64String(req.Body);
            sb.Append(" BODY=").Append(bodyPreview);
        }
        logger.Log(sb.ToString());
    }

    private static bool AcceptsGzip(HttpRequest req)
    {
        var enc = req.GetHeader("Accept-Encoding");
        return enc.Contains("gzip", StringComparison.OrdinalIgnoreCase);
    }

    private HttpResponse BuildResponse(HttpRequest req)
    {
        if (!string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(req.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return new HttpResponse
            {
                StatusCode = 405,
                ReasonPhrase = "Method Not Allowed",
                ContentType = "text/plain; charset=utf-8",
                Body = Encoding.UTF8.GetBytes("Method Not Allowed")
            };
        }

        
        string rel = req.Path.TrimStart('/');
        if (string.IsNullOrEmpty(rel)) rel = "index.html";
        string fullPath = Path.GetFullPath(Path.Combine(config.WebRoot, rel));
        logger.Log($"Map: path='{req.Path}' -> fullPath='{fullPath}'");

        
        string rootFull = Path.GetFullPath(config.WebRoot);
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            logger.Log($"Path traversal blocked. root='{rootFull}' full='{fullPath}'");
            return NotFoundResponse();
        }

        if (!File.Exists(fullPath))
        {
            
            if (Directory.Exists(fullPath))
            {
                string idx = Path.Combine(fullPath, "index.html");
                if (File.Exists(idx)) fullPath = idx;
            }
        }

        if (!File.Exists(fullPath))
        {
            logger.Log($"File not found: '{fullPath}'");
            return NotFoundResponse();
        }

        byte[] body = File.ReadAllBytes(fullPath);
        string ct = MimeTypes.GetContentType(fullPath);
        return new HttpResponse
        {
            StatusCode = 200,
            ReasonPhrase = "OK",
            ContentType = ct,
            Body = body
        };
    }

    private HttpResponse NotFoundResponse()
    {
        string custom404 = Path.Combine(config.WebRoot, "404.html");
        if (File.Exists(custom404))
        {
            return new HttpResponse
            {
                StatusCode = 404,
                ReasonPhrase = "Not Found",
                ContentType = "text/html; charset=utf-8",
                Body = File.ReadAllBytes(custom404)
            };
        }
        return new HttpResponse
        {
            StatusCode = 404,
            ReasonPhrase = "Not Found",
            ContentType = "text/plain; charset=utf-8",
            Body = Encoding.UTF8.GetBytes("404 Not Found")
        };
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] == needle[0])
            {
                bool match = true;
                for (int j = 1; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
        }
        return -1;
    }

    private static Task WriteBytesAsync(NetworkStream ns, byte[] bytes)
    {
        return ns.WriteAsync(bytes, 0, bytes.Length);
    }

    public void Dispose()
    {
        Stop();
    }
}


