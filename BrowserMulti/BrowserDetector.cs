using Microsoft.Win32;

namespace BrowserMulti;

public static class BrowserDetector
{
    public static List<BrowserInfo> Detect()
    {
        var result = new List<BrowserInfo>();

        Add(result, "edge", "Microsoft Edge", FindByAppPaths("msedge.exe"),
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe");

        Add(result, "chrome", "Google Chrome", FindByAppPaths("chrome.exe"),
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe"));

        return result;
    }

    private static string? FindByAppPaths(string exeName)
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
                if (key?.GetValue(null) is string path && File.Exists(path))
                    return path;
            }
            catch
            {
                // 注册表不可读时忽略，走常规路径探测
            }
        }
        return null;
    }

    private static void Add(List<BrowserInfo> list, string id, string name, string? registryPath, params string[] fallbackPaths)
    {
        var path = registryPath;
        if (path == null)
        {
            foreach (var f in fallbackPaths)
            {
                if (File.Exists(f))
                {
                    path = f;
                    break;
                }
            }
        }
        if (path != null)
            list.Add(new BrowserInfo { Id = id, Name = name, ExePath = path });
    }
}
