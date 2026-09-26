namespace BrowserMulti;

/// <summary>轻量日志：写入 exe 同目录 logs/，按天分文件，自动清理 30 天前的日志。</summary>
public static class Log
{
    private static readonly object Sync = new();
    private static string _dir = "";
    private static bool _enabled = true;

    public static void Init(bool enabled)
    {
        _enabled = enabled;
        _dir = Path.Combine(AppContext.BaseDirectory, "logs");
        if (!_enabled) return;
        try
        {
            Directory.CreateDirectory(_dir);
            CleanupOldLogs();
            Info($"===== {AppMeta.Name} v{AppMeta.Version} 启动 =====");
        }
        catch { _enabled = false; }
    }

    /// <summary>运行期切换日志开关（设置保存时调用），并保证目录存在。</summary>
    public static void SetEnabled(bool enabled)
    {
        if (enabled && !_enabled)
        {
            _enabled = true;
            try
            {
                if (!string.IsNullOrEmpty(_dir))
                {
                    Directory.CreateDirectory(_dir);
                    Info($"===== {AppMeta.Name} 日志已开启 =====");
                }
            }
            catch { _enabled = false; }
        }
        else
        {
            _enabled = enabled;
        }
    }

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Warn(string msg) => Write("WARN ", msg);
    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", ex == null ? msg : $"{msg} | {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string msg)
    {
        if (!_enabled) return;
        try
        {
            lock (Sync)
            {
                var file = Path.Combine(_dir, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file,
                    $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch { /* 日志失败绝不影响主流程 */ }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            var limit = DateTime.Now.AddDays(-30);
            foreach (var f in Directory.GetFiles(_dir, "*.log"))
                if (File.GetCreationTime(f) < limit)
                    File.Delete(f);
        }
        catch { /* 清理失败忽略 */ }
    }

    public static string LogDirectory => _dir;

    public static string? LatestLogFile()
    {
        try
        {
            return Directory.GetFiles(_dir, "*.log")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch { return null; }
    }
}
