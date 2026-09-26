using System.Text.Json;

namespace BrowserMulti;

public class InstanceManager
{
    private readonly string _configPath;

    public AppConfig Config { get; private set; }

    public InstanceManager()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "instances.json");
        Config = Load();
    }

    public string ConfigPath => _configPath;

    private AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return new AppConfig();

            var json = File.ReadAllText(_configPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonHelper.Options);
            if (cfg == null) return new AppConfig();

            cfg.Settings ??= new AppSettings();
            cfg.Instances ??= new List<InstanceConfig>();
            NormalizeInstances(cfg);
            return cfg;
        }
        catch (Exception ex)
        {
            // 配置损坏时备份原文件并回退，避免用户数据被静默覆盖
            try
            {
                var bak = _configPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(_configPath, bak, true);
                Log.Warn($"配置文件解析失败，已备份至 {Path.GetFileName(bak)}：{ex.Message}");
            }
            catch { /* 备份失败不影响启动 */ }
            return new AppConfig();
        }
    }

    /// <summary>补齐历史版本缺失的字段，保证向后兼容。</summary>
    private static void NormalizeInstances(AppConfig cfg)
    {
        foreach (var inst in cfg.Instances)
        {
            if (string.IsNullOrWhiteSpace(inst.Id))
                inst.Id = Guid.NewGuid().ToString("N");
            if (inst.FpEnabled && string.IsNullOrWhiteSpace(inst.FpSeed))
                inst.FpSeed = Guid.NewGuid().ToString("N");
            inst.Name = string.IsNullOrWhiteSpace(inst.Name) ? "未命名实例" : inst.Name;
        }
    }

    public void Save()
    {
        try
        {
            var tmp = _configPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Config, JsonHelper.Options));
            if (File.Exists(_configPath)) File.Replace(tmp, _configPath, null);
            else File.Move(tmp, _configPath);
        }
        catch (Exception ex)
        {
            Log.Error("保存配置失败", ex);
        }
    }

    public static string GetDataDir(InstanceConfig inst)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data", inst.Id));

    /// <summary>导出实例配置（不含 Cookie 等数据目录内容）。</summary>
    public void ExportBundle(IEnumerable<InstanceConfig> instances, string targetPath)
    {
        var list = instances.ToList();
        var bundle = new ExportBundle
        {
            InstanceCount = list.Count,
            Instances = list.Select(i => new InstanceConfig
            {
                Id = i.Id,
                Name = i.Name,
                BrowserId = i.BrowserId,
                HomePage = i.HomePage,
                UserAgent = i.UserAgent,
                FpEnabled = i.FpEnabled,
                FpSeed = i.FpSeed,
                Group = i.Group,
                Note = i.Note,
                ExtraArgs = i.ExtraArgs,
                ProxyServer = i.ProxyServer,
                CreatedAt = i.CreatedAt
            }).ToList()
        };
        File.WriteAllText(targetPath, JsonSerializer.Serialize(bundle, JsonHelper.Options));
        Log.Info($"导出 {list.Count} 个实例配置到 {targetPath}");
    }

    /// <summary>导入配置，返回 (新增数, 跳过数)。</summary>
    public (int added, int skipped) ImportBundle(string sourcePath, bool regenerateIdsAndSeeds)
    {
        var json = File.ReadAllText(sourcePath);
        var bundle = JsonSerializer.Deserialize<ExportBundle>(json, JsonHelper.Options)
                     ?? throw new InvalidDataException("文件格式不正确");

        int added = 0, skipped = 0;
        foreach (var src in bundle.Instances)
        {
            if (Config.Instances.Any(x => x.Name == src.Name))
            {
                skipped++;
                continue;
            }

            Config.Instances.Add(new InstanceConfig
            {
                Id = regenerateIdsAndSeeds ? Guid.NewGuid().ToString("N") : src.Id,
                Name = src.Name,
                BrowserId = src.BrowserId,
                HomePage = src.HomePage,
                UserAgent = src.UserAgent,
                FpEnabled = src.FpEnabled,
                FpSeed = regenerateIdsAndSeeds || string.IsNullOrWhiteSpace(src.FpSeed)
                    ? Guid.NewGuid().ToString("N")
                    : src.FpSeed,
                Group = src.Group,
                Note = src.Note,
                ExtraArgs = src.ExtraArgs,
                ProxyServer = src.ProxyServer,
                CreatedAt = src.CreatedAt
            });
            added++;
        }
        Save();
        Log.Info($"导入配置：新增 {added} 个，跳过重复 {skipped} 个");
        return (added, skipped);
    }
}
