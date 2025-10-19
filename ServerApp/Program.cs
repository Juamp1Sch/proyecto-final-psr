using ServerCore;

string configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "serverconfig.json");

if (!File.Exists(configPath))
{
    Console.WriteLine($"Config not found at {configPath}. Creating a default one.");
    string defaultJson = "{\n  \"port\": 8080,\n  \"webRoot\": \"archivos-a-servir\",\n  \"logDirectory\": \"logs\",\n  \"enableGzip\": true\n}";
    File.WriteAllText(configPath, defaultJson);
}

var config = ServerConfig.LoadFromFile(configPath);
using var server = new SimpleHttpServer(config);
server.Start();
Console.WriteLine($"Server listening on port {config.Port}, serving {config.WebRoot}");
Console.WriteLine("Press Ctrl+C to exit.");

var exit = new ManualResetEvent(false);
Console.CancelKeyPress += (s, e) => { e.Cancel = true; exit.Set(); };
exit.WaitOne();
