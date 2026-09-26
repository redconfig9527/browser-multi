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
    /// <summary>分组/标签，用于归类（如"工作号""小号"），可留空</summary>
    public string Group { get; set; } = "";
    /// <summary>备注，仅本工具可见</summary>
    public string Note { get; set; } = "";
    /// <summary>附加启动参数，高级用户使用</summary>
    public string ExtraArgs { get; set; } = "";
    /// <summary>每个实例独立代理（--proxy-server），留空为直连</summary>
    public string ProxyServer { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLaunchedAt { get; set; }
}

public class AppSettings
{
    public string DefaultBrowserId { get; set; } = "auto";
    public bool MinimizeToTray { get; set; } = true;
    public bool ConfirmDuplicateLaunch { get; set; } = true;
    public bool AutoCleanCacheOnStart { get; set; }
    public int WindowWidth { get; set; } = 980;
    public int WindowHeight { get; set; } = 620;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public bool WindowMaximized { get; set; }
    public bool LogEnabled { get; set; } = true;
    public string DefaultFpEnabled { get; set; } = "true";
}

public class AppConfig
{
    public int ConfigVersion { get; set; } = 2;
    public AppSettings Settings { get; set; } = new();
    public List<InstanceConfig> Instances { get; set; } = new();
}

/// <summary>用于导入导出的轻量载体（不含数据目录内容）。</summary>
public class ExportBundle
{
    public string Format { get; set; } = "BrowserMulti-Config";
    public int Version { get; set; } = 1;
    public string ExportedBy { get; set; } = "jtxu9527";
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public int InstanceCount { get; set; }
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

public static class AppMeta
{
    public const string Name = "浏览器多开管理器";
    public const string EnglishName = "BrowserMulti";
    public const string Version = "1.6.0";
    public const string Author = "jtxu9527";
    public const string RepoUrl = "https://github.com/redconfig9527/browser-multi";
    public const string Copyright = "Copyright © 2026 jtxu9527 · MIT License";
}

public static class JsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
