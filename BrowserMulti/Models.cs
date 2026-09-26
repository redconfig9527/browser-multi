using System.Text.Encodings.Web;
using System.Text.Json;

namespace BrowserMulti;

public class InstanceConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string BrowserId { get; set; } = "auto";
    public string HomePage { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public bool FpEnabled { get; set; } = true;
    public string FpSeed { get; set; } = "";
    // v2 预留：每个实例独立代理（--proxy-server），第一版不开放编辑
    public string ProxyServer { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AppConfig
{
    public List<InstanceConfig> Instances { get; set; } = new();
}

public class BrowserInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
}

public record BrowserChoice(string Id, string Display)
{
    public override string ToString() => Display;
}

public record UaPreset(string Name, string Value);

public static class UaPresets
{
    public const string CustomKey = "##custom##";

    public static readonly UaPreset[] All =
    {
        new("跟随浏览器默认", ""),
        new("Chrome · Windows", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"),
        new("Chrome · macOS", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"),
        new("Edge · Windows", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0"),
        new("Safari · iPhone", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1"),
        new("Chrome · Android", "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36"),
        new("自定义…", CustomKey)
    };

    public static string DisplayName(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "默认";
        return All.FirstOrDefault(p => p.Value == ua)?.Name ?? "自定义";
    }
}
