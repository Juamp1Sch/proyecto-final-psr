using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerCore;

public sealed class ServerConfig
{
    public required int Port { get; init; }
    public required string WebRoot { get; init; }
    public string? LogDirectory { get; init; }
    public bool EnableGzip { get; init; } = true;

    [JsonIgnore]
    public required string ConfigDirectory { get; init; }

    public static ServerConfig LoadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Config file not found", configPath);
        }

        string json = File.ReadAllText(configPath);
        var baseOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var model = JsonSerializer.Deserialize<ServerConfigIntermediate>(json, baseOptions)
                    ?? throw new InvalidOperationException("Invalid config JSON");

        string configDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

        // Resolve paths relative to the config file directory when not rooted
        string webRoot = model.WebRoot;
        if (!Path.IsPathRooted(webRoot))
        {
            webRoot = Path.GetFullPath(Path.Combine(configDir, webRoot));
        }

        string? logDir = model.LogDirectory;
        if (!string.IsNullOrWhiteSpace(logDir) && !Path.IsPathRooted(logDir))
        {
            logDir = Path.GetFullPath(Path.Combine(configDir, logDir));
        }

        return new ServerConfig
        {
            Port = model.Port,
            WebRoot = webRoot,
            LogDirectory = logDir,
            EnableGzip = model.EnableGzip,
            ConfigDirectory = configDir
        };
    }

    private sealed class ServerConfigIntermediate
    {
        public int Port { get; set; } = 8080;
        public string WebRoot { get; set; } = "archivos-a-servir";
        public string? LogDirectory { get; set; } = "logs";
        public bool EnableGzip { get; set; } = true;
    }
}




