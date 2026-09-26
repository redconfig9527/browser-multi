using System.Diagnostics;
using System.Management;
using System.Text;

namespace BrowserMulti;

public class MainForm : Form
{
    private const string DataDirKey = "--user-data-dir=";

    private readonly InstanceManager _manager;
    private readonly List<BrowserInfo> _browsers;
    private readonly List<BrowserChoice> _browserChoices = new();
    private readonly ToolTip _toolTip = new();

    private ListView _list = null!;
    private ComboBox _browserCombo = null!;
    private Label _fpWarnLabel = null!;
    private Label _statusLabel = null!;
    private System.Windows.Forms.Timer _timer = null!;
    private readonly NotifyIcon _tray = new();
    private bool _reallyExit;
    private bool _trayHintShown;

    // 列宽权重: 实例名称, 浏览器, UserAgent, 指纹防护, 启动页, 状态, 磁盘占用, 创建时间
    private static readonly int[] ColWeights = { 15, 8, 11, 9, 17, 8, 8, 12 };

    public MainForm()
    {
        _browsers = BrowserDetector.Detect();
        _manager = new InstanceManager();

        Text = "浏览器多开管理器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(920, 600);
        Font = new Font("Microsoft YaHei UI", 9F);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application; }
        catch { Icon = SystemIcons.Application; }

        BuildUi();
        ReloadList();
        RefreshStates();
        UpdateFpWarning();

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) => RefreshStates();
        _timer.Start();

        string names = _browsers.Count > 0
            ? string.Join("、", _browsers.Select(b => b.Name))
            : "未检测到浏览器（请安装 Edge 或 Chrome）";
        SetStatus("已检测到浏览器: " + names,
            string.Join("\n", _browsers.Select(b => $"{b.Name} → {b.ExePath}")));
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // ---------- 顶部：浏览器选择 + 实例管理 ----------
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(12, 12, 12, 8)
        };
        toolbar.Controls.Add(new Label
        {
            Text = "默认浏览器:",
            AutoSize = true,
            Margin = new Padding(3, 9, 3, 0)
        });
        _browserCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 230,
            Margin = new Padding(3, 5, 16, 0)
        };
        _browserChoices.Add(new BrowserChoice("auto", "自动（优先 Chrome，其次 Edge）"));
        foreach (var b in _browsers)
            _browserChoices.Add(new BrowserChoice(b.Id, b.Name));
        foreach (var c in _browserChoices)
            _browserCombo.Items.Add(c);
        _browserCombo.SelectedIndex = 0;
        _browserCombo.SelectedIndexChanged += (_, _) => UpdateFpWarning();
        toolbar.Controls.Add(_browserCombo);

        _fpWarnLabel = new Label
        {
            Text = "⚠ 指纹防护仅 Edge 有效",
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 60, 20),
            Margin = new Padding(0, 9, 16, 0),
            Visible = false
        };
        toolbar.Controls.Add(_fpWarnLabel);

        toolbar.Controls.Add(MakeButton("新建实例", AddInstance, 100));
        toolbar.Controls.Add(MakeButton("编辑", EditInstance, 76));
        toolbar.Controls.Add(MakeButton("删除", DeleteInstance, 76));
        root.Controls.Add(toolbar, 0, 0);

        // ---------- 中部：实例列表 ----------
        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true
        };
        _list.Columns.Add("实例名称", 150);
        _list.Columns.Add("浏览器", 90);
        _list.Columns.Add("UserAgent", 130);
        _list.Columns.Add("指纹防护", 80);
        _list.Columns.Add("启动页", 190);
        _list.Columns.Add("状态", 70);
        _list.Columns.Add("占用", 80);
        _list.Columns.Add("创建时间", 110);
        _list.DoubleClick += (_, _) => LaunchSelected();
        root.Controls.Add(_list, 0, 1);
        SizeChanged += (_, _) => FitColumns();

        // ---------- 底部：启动操作 + 状态栏 ----------
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12, 10, 12, 12)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var bflow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };
        bflow.Controls.Add(MakeButton("启动选中", LaunchSelected, 100));
        bflow.Controls.Add(MakeButton("全部启动", LaunchAll, 100));
        bflow.Controls.Add(MakeButton("打开数据目录", OpenDataDir, 112));
        bflow.Controls.Add(MakeButton("发送桌面快捷方式", CreateShortcuts, 140));

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Margin = new Padding(16, 8, 4, 0)
        };
        bottom.Controls.Add(bflow, 0, 0);
        bottom.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(bottom, 0, 2);

        var menu = new ContextMenuStrip();
        menu.Items.Add("启动", null, (_, _) => LaunchSelected());
        menu.Items.Add("打开数据目录", null, (_, _) => OpenDataDir());
        menu.Items.Add("发送桌面快捷方式", null, (_, _) => CreateShortcuts());
        menu.Items.Add("清理缓存（保留登录态）", null, (_, _) => ClearCache());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("编辑", null, (_, _) => EditInstance());
        menu.Items.Add("删除", null, (_, _) => DeleteInstance());
        _list.ContextMenuStrip = menu;

        _tray.Icon = Icon;
        _tray.Text = "浏览器多开管理器";
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowFromTray();
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出程序", null, (_, _) =>
        {
            _reallyExit = true;
            _tray.Visible = false;
            Close();
        });
        _tray.ContextMenuStrip = trayMenu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _tray.ShowBalloonTip(2000, "浏览器多开管理器",
                    "程序已最小化到托盘，双击托盘图标可重新打开。", ToolTipIcon.Info);
                _trayHintShown = true;
            }
            return;
        }
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }

    private static Button MakeButton(string text, Action action, int width)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(width, 32),
            Margin = new Padding(3, 4, 3, 0)
        };
        btn.Click += (_, _) => action();
        return btn;
    }

    /// <summary>选中的浏览器不支持扩展加载时，在工具栏提示指纹防护会失效。</summary>
    private void UpdateFpWarning()
    {
        var id = (_browserCombo.SelectedItem as BrowserChoice)?.Id ?? "auto";
        bool chromeSelected = id == "chrome"
            || (id == "auto" && _browsers.Any(b => b.Id == "chrome") && !_browsers.Any(b => b.Id == "edge"));
        _fpWarnLabel.Visible = chromeSelected;
        if (chromeSelected)
            _toolTip.SetToolTip(_fpWarnLabel,
                "Chrome 137 起已移除 --load-extension 参数，指纹防护扩展不会加载。\n如需指纹防护，请把默认浏览器切换为 Microsoft Edge。");
    }

    private void FitColumns()
    {
        if (_list.Width < 50) return;
        int total = _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
        int sum = ColWeights.Sum();
        for (int i = 0; i < _list.Columns.Count; i++)
            _list.Columns[i].Width = Math.Max(56, total * ColWeights[i] / sum);
    }

    private void ReloadList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var inst in _manager.Config.Instances)
        {
            var browserName = inst.BrowserId == "auto"
                ? "自动"
                : _browsers.FirstOrDefault(b => b.Id == inst.BrowserId)?.Name ?? "自动";

            var item = new ListViewItem(inst.Name);
            item.SubItems.Add(browserName);
            item.SubItems.Add(UaPresets.DisplayName(inst.UserAgent));
            item.SubItems.Add(inst.FpEnabled ? "已开启" : "已关闭");
            item.SubItems.Add(string.IsNullOrWhiteSpace(inst.HomePage) ? "—" : inst.HomePage);
            item.SubItems.Add("检测中…");
            item.SubItems.Add("计算中…");
            item.SubItems.Add(inst.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            item.Tag = inst;
            item.UseItemStyleForSubItems = false;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        FitColumns();
        RefreshSizesAsync();
    }

    /// <summary>后台统计各实例数据目录占用，避免大目录遍历卡住 UI。</summary>
    private void RefreshSizesAsync()
    {
        var snapshot = _manager.Config.Instances.ToList();
        Task.Run(() =>
        {
            var sizes = new Dictionary<string, long>();
            foreach (var inst in snapshot)
                sizes[inst.Id] = GetDirSize(InstanceManager.GetDataDir(inst));
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                foreach (ListViewItem item in _list.Items)
                {
                    if (item.Tag is InstanceConfig ci && sizes.TryGetValue(ci.Id, out var sz))
                        item.SubItems[6].Text = sz == 0 ? "—" : FormatSize(sz);
                }
            });
        });
    }

    private static long GetDirSize(string path)
    {
        long total = 0;
        try
        {
            if (!Directory.Exists(path)) return 0;
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { /* 单文件被锁等忽略 */ }
            }
        }
        catch { /* 目录不可访问时返回已统计部分 */ }
        return total;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1 << 30 => $"{bytes / (double)(1 << 30):F1} GB",
        >= 1 << 20 => $"{bytes / (double)(1 << 20):F1} MB",
        >= 1 << 10 => $"{bytes / (double)(1 << 10):F0} KB",
        _ => $"{bytes} B"
    };

    private void RefreshStates()
    {
        var running = GetRunningDataDirs();
        int runningCount = 0;
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is not InstanceConfig inst) continue;
            bool isRunning = running.Contains(InstanceManager.GetDataDir(inst));
            if (isRunning) runningCount++;
            item.SubItems[5].Text = isRunning ? "运行中" : "未运行";
            item.SubItems[5].ForeColor = isRunning ? Color.FromArgb(0, 138, 92) : Color.Gray;
        }
        Text = $"浏览器多开管理器 - {_manager.Config.Instances.Count} 个实例，{runningCount} 个运行中";
    }

    /// <summary>通过 WMI 查询浏览器进程命令行，提取所有 --user-data-dir 值。</summary>
    private static HashSet<string> GetRunningDataDirs()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process " +
                "WHERE Name='msedge.exe' OR Name='chrome.exe' OR Name='chromium.exe'");
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["CommandLine"] is not string cmd) continue;
                int i = cmd.IndexOf(DataDirKey, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                int start = i + DataDirKey.Length;
                string dir;
                if (start < cmd.Length && cmd[start] == '"')
                {
                    int end = cmd.IndexOf('"', start + 1);
                    if (end < 0) continue;
                    dir = cmd.Substring(start + 1, end - start - 1);
                }
                else
                {
                    int end = start;
                    while (end < cmd.Length && cmd[end] != ' ') end++;
                    dir = cmd.Substring(start, end - start);
                }
                if (dir.Length > 0)
                    set.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)));
            }
        }
        catch
        {
            // WMI 不可用时静默降级：状态列全部显示未运行，不影响启动功能
        }
        return set;
    }

    private BrowserInfo? ResolveBrowser(InstanceConfig inst)
    {
        var id = inst.BrowserId;
        if (id == "auto")
            id = (_browserCombo.SelectedItem as BrowserChoice)?.Id ?? "auto";
        if (id == "auto")
            return _browsers.FirstOrDefault(b => b.Id == "chrome") ?? _browsers.FirstOrDefault();
        return _browsers.FirstOrDefault(b => b.Id == id) ?? _browsers.FirstOrDefault();
    }

    /// <summary>构造实例的浏览器启动参数（启动与快捷方式共用）。</summary>
    private static string BuildArgs(InstanceConfig inst, string dataDir)
    {
        var args = new StringBuilder();
        args.Append("--user-data-dir=\"").Append(dataDir).Append("\"");
        args.Append(" --no-first-run --no-default-browser-check");
        if (!string.IsNullOrWhiteSpace(inst.ProxyServer))
            args.Append(" --proxy-server=\"").Append(inst.ProxyServer).Append("\"");
        if (!string.IsNullOrWhiteSpace(inst.UserAgent))
        {
            args.Append(" --user-agent=\"").Append(inst.UserAgent).Append("\"");
            args.Append(" --disable-features=UserAgentClientHint");
        }
        if (inst.FpEnabled)
            args.Append(" --load-extension=\"").Append(FingerprintExtension.GetDir(inst)).Append("\"");
        if (!string.IsNullOrWhiteSpace(inst.HomePage))
            args.Append(' ').Append(inst.HomePage.Trim());
        return args.ToString();
    }

    /// <summary>确保实例的防指纹扩展文件已生成（种子缺失时自动补发并保存）。</summary>
    private void EnsureFpFiles(InstanceConfig inst)
    {
        if (!inst.FpEnabled) return;
        if (string.IsNullOrWhiteSpace(inst.FpSeed))
        {
            inst.FpSeed = Guid.NewGuid().ToString("N");
            _manager.Save();
        }
        FingerprintExtension.Write(inst);
    }

    private void Launch(InstanceConfig inst)
    {
        var browser = ResolveBrowser(inst);
        if (browser == null)
        {
            MessageBox.Show("未找到可用的浏览器，请安装 Edge 或 Chrome。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string dataDir = InstanceManager.GetDataDir(inst);
        Directory.CreateDirectory(dataDir);
        EnsureFpFiles(inst);

        if (GetRunningDataDirs().Contains(Path.TrimEndingDirectorySeparator(dataDir)) &&
            MessageBox.Show($"实例「{inst.Name}」正在运行。\n继续将为它再打开一个浏览器窗口（两个窗口共享同一份数据）。\n\n是否继续？",
                "实例已运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            SetStatus($"「{inst.Name}」已在运行，已取消重复启动");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(browser.ExePath, BuildArgs(inst, dataDir))
            {
                UseShellExecute = true
            });
            SetStatus($"已启动「{inst.Name}」（{browser.Name}）");
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LaunchSelected()
    {
        var targets = _list.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is InstanceConfig)
            .Select(i => (InstanceConfig)i.Tag!)
            .ToList();
        if (targets.Count == 0)
        {
            SetStatus("请先选中要启动的实例（可按住 Ctrl 多选）");
            return;
        }
        foreach (var t in targets)
            Launch(t);
        RefreshStates();
    }

    private void LaunchAll()
    {
        if (_manager.Config.Instances.Count == 0)
        {
            SetStatus("还没有实例，点击「新建实例」创建一个");
            return;
        }
        foreach (var inst in _manager.Config.Instances)
            Launch(inst);
        RefreshStates();
    }

    private void AddInstance()
    {
        int n = _manager.Config.Instances.Count + 1;
        var defName = $"实例 {n}";
        while (_manager.Config.Instances.Any(i => i.Name == defName))
        {
            n++;
            defName = $"实例 {n}";
        }

        using var dlg = new InstanceDialog("新建实例", defName, "auto", "", "", true, _browserChoices);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (_manager.Config.Instances.Any(i => i.Name == dlg.InstanceName))
        {
            MessageBox.Show("已存在同名实例，请换一个名称。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _manager.Config.Instances.Add(new InstanceConfig
        {
            Name = dlg.InstanceName,
            BrowserId = dlg.BrowserId,
            HomePage = dlg.HomePage,
            UserAgent = dlg.UserAgent,
            FpEnabled = dlg.FpEnabled,
            FpSeed = Guid.NewGuid().ToString("N")
        });
        _manager.Save();
        ReloadList();
        SetStatus($"已创建「{dlg.InstanceName}」，双击列表行即可启动");
    }

    private void EditInstance()
    {
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not InstanceConfig inst)
        {
            SetStatus("请先选中一个实例");
            return;
        }

        using var dlg = new InstanceDialog("编辑实例", inst.Name, inst.BrowserId,
            inst.HomePage, inst.UserAgent, inst.FpEnabled, _browserChoices);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (_manager.Config.Instances.Any(i => i != inst && i.Name == dlg.InstanceName))
        {
            MessageBox.Show("已存在同名实例，请换一个名称。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        inst.Name = dlg.InstanceName;
        inst.BrowserId = dlg.BrowserId;
        inst.HomePage = dlg.HomePage;
        inst.UserAgent = dlg.UserAgent;
        inst.FpEnabled = dlg.FpEnabled;
        if (inst.FpEnabled && string.IsNullOrWhiteSpace(inst.FpSeed))
            inst.FpSeed = Guid.NewGuid().ToString("N");
        _manager.Save();
        ReloadList();
    }

    private void DeleteInstance()
    {
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not InstanceConfig inst)
        {
            SetStatus("请先选中一个实例");
            return;
        }

        if (MessageBox.Show($"确定删除实例「{inst.Name}」吗？", "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var deleteData = MessageBox.Show(
            "是否同时删除该实例的数据目录？\n\n选择「是」：Cookie、登录态、缓存将全部丢失；\n选择「否」：保留数据目录，之后重建同名实例仍可复用。",
            "删除数据", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        _manager.Config.Instances.Remove(inst);
        if (deleteData)
        {
            try { Directory.Delete(InstanceManager.GetDataDir(inst), true); }
            catch { /* 文件被占用等情况下忽略，目录可稍后手动删除 */ }
            try { Directory.Delete(FingerprintExtension.GetDir(inst), true); }
            catch { /* 同上 */ }
        }
        _manager.Save();
        ReloadList();
        SetStatus($"已删除「{inst.Name}」");
    }

    private void OpenDataDir()
    {
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not InstanceConfig inst)
        {
            SetStatus("请先选中一个实例");
            return;
        }
        string dir = InstanceManager.GetDataDir(inst);
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    /// <summary>清理实例缓存（Cache/Code Cache/GPUCache/Service Worker 存储），保留 Cookie 与登录态。</summary>
    private void ClearCache()
    {
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not InstanceConfig inst)
        {
            SetStatus("请先选中一个实例");
            return;
        }

        if (GetRunningDataDirs().Contains(InstanceManager.GetDataDir(inst)))
        {
            MessageBox.Show("该实例的浏览器正在运行，请先关闭它再清理缓存（否则部分文件被占用无法删除）。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string dir = InstanceManager.GetDataDir(inst);
        long freed = 0;
        foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", Path.Combine("Service Worker", "CacheStorage") })
        {
            var p = Path.Combine(dir, sub);
            try
            {
                if (Directory.Exists(p))
                {
                    freed += GetDirSize(p);
                    Directory.Delete(p, true);
                }
            }
            catch { /* 被占用等情况下跳过该目录 */ }
        }

        RefreshSizesAsync();
        SetStatus($"已清理「{inst.Name}」缓存 {FormatSize(freed)}，登录态与 Cookie 已保留");
    }

    /// <summary>为选中的实例在桌面创建 .lnk 快捷方式，之后双击快捷方式即可启动，无需打开本程序。</summary>
    private void CreateShortcuts()
    {
        var targets = _list.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is InstanceConfig)
            .Select(i => (InstanceConfig)i.Tag!)
            .ToList();
        if (targets.Count == 0)
        {
            SetStatus("请先选中要生成快捷方式的实例（可按住 Ctrl 多选）");
            return;
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        int ok = 0, fail = 0;

        foreach (var inst in targets)
        {
            var browser = ResolveBrowser(inst);
            if (browser == null)
            {
                fail++;
                continue;
            }

            string lnkPath = Path.Combine(desktop, $"浏览器 - {inst.Name}.lnk");
            try
            {
                EnsureFpFiles(inst);
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("系统缺少 WScript.Shell 组件");
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.TargetPath = browser.ExePath;
                sc.Arguments = BuildArgs(inst, InstanceManager.GetDataDir(inst));
                sc.WorkingDirectory = Path.GetDirectoryName(browser.ExePath) ?? "";
                sc.IconLocation = $"{browser.ExePath},0";
                sc.Description = $"多开实例「{inst.Name}」- 独立数据目录，Cookie 隔离";
                sc.Save();
                ok++;
            }
            catch
            {
                fail++;
            }
        }

        SetStatus(fail == 0
            ? $"已在桌面创建 {ok} 个快捷方式，之后双击即可直接启动"
            : $"成功 {ok} 个，失败 {fail} 个（桌面不可写或浏览器未找到）");
        if (ok > 0)
            MessageBox.Show($"已在桌面创建 {ok} 个快捷方式。\n\n以后双击桌面上的「浏览器 - 实例名」即可直接启动该实例，无需再打开本程序。登录态和数据与之前完全一致。",
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetStatus(string text, string? tooltip = null)
    {
        _statusLabel.Text = text;
        _toolTip.SetToolTip(_statusLabel, tooltip ?? text);
    }
}
