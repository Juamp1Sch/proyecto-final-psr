using System.Net.Sockets;
using System.Text;
using ServerCore;

// Simple integration test harness that starts the server and issues a few requests

string baseDir = AppContext.BaseDirectory;
string repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
string webRootAbs = Path.Combine(repoRoot, "archivos-a-servir");
string configPath = Path.Combine(baseDir, "serverconfig.json");
string jsonConfig = $"{{\n  \"port\": 9090,\n  \"webRoot\": \"{webRootAbs.Replace("\\", "\\\\")}\",\n  \"logDirectory\": \"logs\",\n  \"enableGzip\": true\n}}";
File.WriteAllText(configPath, jsonConfig);

Console.WriteLine($"webRoot: {webRootAbs}");
Console.WriteLine($"webRoot exists: {Directory.Exists(webRootAbs)}");
Console.WriteLine($"index.html exists: {File.Exists(Path.Combine(webRootAbs, "index.html"))}");

var config = ServerConfig.LoadFromFile(configPath);
using var server = new SimpleHttpServer(config);
server.Start();

await Task.Delay(300); // give it a moment

static async Task<string> RawHttp(string host, int port, string request)
{
    using var client = new TcpClient();
    await client.ConnectAsync(host, port);
    using var ns = client.GetStream();
    var reqBytes = Encoding.ASCII.GetBytes(request);
    await ns.WriteAsync(reqBytes, 0, reqBytes.Length);
    await ns.FlushAsync();
    var buf = new byte[8192];
    using var ms = new MemoryStream();
    int read;
    do
    {
        read = await ns.ReadAsync(buf, 0, buf.Length);
        if (read > 0) ms.Write(buf, 0, read);
    } while (read > 0);
    return Encoding.UTF8.GetString(ms.ToArray());
}

string host = "127.0.0.1";
int port = config.Port;

// GET /
string getRoot = $"GET / HTTP/1.1\r\nHost: {host}\r\nAccept-Encoding: gzip\r\n\r\n";
string resp1 = await RawHttp(host, port, getRoot);
Console.WriteLine($"GET / -> {resp1.Split('\n')[0].Trim()}");

// GET /nope.txt (expect 404)
string get404 = $"GET /nope.txt HTTP/1.1\r\nHost: {host}\r\n\r\n";
string resp2 = await RawHttp(host, port, get404);
Console.WriteLine($"GET /nope.txt -> {resp2.Split('\n')[0].Trim()}");

// GET with query params (logged)
string getQuery = $"GET /?a=1&b=dos HTTP/1.1\r\nHost: {host}\r\n\r\n";
string resp3 = await RawHttp(host, port, getQuery);
Console.WriteLine($"GET /?a=1&b=dos -> {resp3.Split('\n')[0].Trim()}");

// POST logs body
string body = "x=1&y=2";
string post = $"POST / HTTP/1.1\r\nHost: {host}\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}";
string resp4 = await RawHttp(host, port, post);
Console.WriteLine($"POST / -> {resp4.Split('\n')[0].Trim()}");

await Task.Delay(200);
