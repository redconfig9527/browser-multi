using System.Text.Encodings.Web;
using System.Text.Json;

namespace BrowserMulti;

public class InstanceManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _configPath;

    public AppConfig Config { get; }

    public InstanceManager()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "instances.json");
        Config = Load();
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath)) ?? new AppConfig();
        }
        catch
        {
            // 配置损坏时回退到空配置，避免启动崩溃
        }
        return new AppConfig();
    }

    public void Save()
    {
        File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, JsonOptions));
    }

    public static string GetDataDir(InstanceConfig inst)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data", inst.Id));
    }
}
