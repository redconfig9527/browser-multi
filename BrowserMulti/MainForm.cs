using System.Diagnostics;
using System.Management;
using System.Text;

namespace BrowserMulti;

public class MainForm : Form
{
    private const string DataDirKey = "--user-data-dir=";
    private const int SortableColumnCount = 8;

    private readonly InstanceManager _manager;
    private readonly List<BrowserInfo> _browsers;
    private readonly List<BrowserChoice> _browserChoices = new();
    private readonly ToolTip _toolTip = new();

    private ListView _list = null!;
    private TextBox _txtSearch = null!;
    private Label _fpWarnLabel = null!;
    private Label _statusLabel = null!;
    private Label _emptyHint = null!;
    private Panel _listHost = null!;
    private System.Windows.Forms.Timer _timer = null!;
    private readonly NotifyIcon _tray = new();
    private bool _reallyExit;
    private bool _closing;
    private bool _trayHintShown;
    private int _sortColumn = -1;
    private bool _sortAscending = true;

    // 列宽权重: 实例名称, 分组, 浏览器, UserAgent, 指纹, 状态, 占用, 启动页
    private static readonly int[] ColWeights = { 15, 9, 7, 12, 7, 7, 7, 20 };

    public MainForm()
    {
        _browsers = BrowserDetector.Detect();
        BuildBrowserChoices();
        _manager = new InstanceManager();
        Log.Init(_manager.Config.Settings.LogEnabled);

        Text = AppMeta.Name;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 520);
        ApplyWindowSettings();
        Font = new Font("Microsoft YaHei UI", 9F);
        try
        {
            var appIcon = string.IsNullOrEmpty(Environment.ProcessPath)
                ? null
                : Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            Icon = appIcon ?? SystemIcons.Application;
        }
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

        if (_browsers.Count == 0)
            MessageBox.Show(
                "未检测到 Microsoft Edge 或 Google Chrome。\n\n本工具通过调用系统已安装的浏览器实现多开，请先安装其中之一。",
                "未找到浏览器", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        Shown += (_, _) => ShowFirstRunGuideIfNeeded();
    }

    private void ApplyWindowSettings()
    {
        var s = _manager.Config.Settings;
        int w = Math.Max(s.WindowWidth, MinimumSize.Width);
        int h = Math.Max(s.WindowHeight, MinimumSize.Height);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);

        // 位置恢复时做越界保护（比如换过显示器）
        if (s.WindowX >= 0 && s.WindowY >= 0 &&
            s.WindowX < area.Right - 200 && s.WindowY < area.Bottom - 100)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(s.WindowX, s.WindowY);
        }
        Size = new Size(w, h);
        if (s.WindowMaximized) WindowState = FormWindowState.Maximized;
    }

    private void SaveWindowSettings()
    {
        var s = _manager.Config.Settings;
        s.WindowMaximized = WindowState == FormWindowState.Maximized;
        if (WindowState == FormWindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowX = Location.X;
            s.WindowY = Location.Y;
        }
        _manager.Save();
    }

    private void ShowFirstRunGuideIfNeeded()
    {
        if (_manager.Config.Instances.Count > 0) return;
        var r = MessageBox.Show(
            "欢迎使用 " + AppMeta.Name + "！\n\n" +
            "三步开始：\n" +
            "  1. 点击「新建实例」创建你的第一个环境\n" +
            "  2. 双击列表中的实例即可启动（数据互相隔离）\n" +
            "  3. 点「发送桌面快捷方式」后可脱离本工具直接启动\n\n" +
            "建议默认浏览器选择 Microsoft Edge（指纹防护仅 Edge 支持）。\n\n" +
            "是否现在创建第一个实例？",
            "首次使用", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r == DialogResult.Yes) AddInstance();
    }

    // ==================== 界面构建 ====================

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 菜单
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 工具栏
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));// 列表
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 底部
        Controls.Add(root);

        root.Controls.Add(BuildMenuBar(), 0, 0);
        root.Controls.Add(BuildToolbar(), 0, 1);

        _listHost = new Panel { Dock = DockStyle.Fill };
        BuildList();
        BuildEmptyHint();
        root.Controls.Add(_listHost, 0, 2);

        root.Controls.Add(BuildBottomBar(), 0, 3);

        menuStrip.MenuActivate += (_, _) => { };
    }

    private readonly MenuStrip menuStrip = new();

    private MenuStrip BuildMenuBar()
    {
        menuStrip.Dock = DockStyle.Fill;
        menuStrip.Padding = new Padding(6, 2, 0, 2);

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(MenuItem("新建实例", Keys.Control | Keys.N, AddInstance));
        file.DropDownItems.Add(MenuItem("编辑选中", Keys.F2, EditInstance));
        file.DropDownItems.Add(MenuItem("删除选中", Keys.None, DeleteInstance));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("导入配置…", Keys.None, ImportConfig));
        file.DropDownItems.Add(MenuItem("导出配置…", Keys.None, ExportConfig));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("打开数据目录", Keys.None, OpenDataDir));
        file.DropDownItems.Add(MenuItem("打开程序目录", Keys.None, OpenAppDir));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("退出", Keys.None, () => { _reallyExit = true; Close(); }));

        var run = new ToolStripMenuItem("运行(&R)");
        run.DropDownItems.Add(MenuItem("启动选中", Keys.Control | Keys.Enter, LaunchSelected));
        run.DropDownItems.Add(MenuItem("全部启动", Keys.None, LaunchAll));
        run.DropDownItems.Add(new ToolStripSeparator());
        run.DropDownItems.Add(MenuItem("发送桌面快捷方式", Keys.None, CreateShortcuts));
        run.DropDownItems.Add(MenuItem("清理缓存（保留登录态）", Keys.None, ClearCache));

        var tools = new ToolStripMenuItem("工具(&T)");
        tools.DropDownItems.Add(MenuItem("设置…", Keys.None, OpenSettings));
        tools.DropDownItems.Add(MenuItem("打开日志目录", Keys.None, OpenLogDir));

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(MenuItem("使用说明", Keys.F1, ShowHelp));
        help.DropDownItems.Add(MenuItem("检查更新", Keys.None, CheckUpdate));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(MenuItem("关于 " + AppMeta.Name, Keys.None, ShowAbout));

        menuStrip.Items.AddRange(new ToolStripItem[] { file, run, tools, help });
        MainMenuStrip = menuStrip;
        return menuStrip;
    }

    private static ToolStripMenuItem MenuItem(string text, Keys shortcut, Action action)
    {
        var item = new ToolStripMenuItem(text);
        // ShortcutKeys 只接受「功能键」或「含 Ctrl/Alt/Shift 的组合键」；
        // 裸 Delete/Enter/字母 会抛 InvalidEnumArgumentException，故此处按类型放行：
        //   - 含修饰键（Ctrl/Alt/Shift）
        //   - 纯功能键（F1~F24，键值 0x70 起）—— 原实现误把 F1/F2 排除了
        if (shortcut != Keys.None && IsValidShortcut(shortcut))
        {
            item.ShortcutKeys = shortcut;
            item.ShowShortcutKeys = true;
        }
        // 无效组合（如裸 Delete）：不设 ShortcutKeys，仅由 ProcessCmdKey 统一处理
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>判断是否为 ShortcutKeys 可接受的组合（防 InvalidEnumArgumentException）。</summary>
    private static bool IsValidShortcut(Keys shortcut)
    {
        var mods = shortcut & Keys.Modifiers;
        if (mods != Keys.None) return true;                 // Ctrl/Alt/Shift 组合
        var key = shortcut & Keys.KeyCode;
        return key >= Keys.F1 && key <= Keys.F24;           // 纯功能键
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 4)
        };

        _txtSearch = new TextBox
        {
            Width = 190,
            Margin = new Padding(3, 3, 3, 0),
            PlaceholderText = "搜索实例名称 / 分组…"
        };
        _txtSearch.TextChanged += (_, _) => ReloadList();
        toolbar.Controls.Add(_txtSearch);

        toolbar.Controls.Add(MakeButton("新建实例", AddInstance, 96, Keys.Control | Keys.N));
        toolbar.Controls.Add(MakeButton("编辑", EditInstance, 72));
        toolbar.Controls.Add(MakeButton("删除", DeleteInstance, 72));

        _fpWarnLabel = new Label
        {
            Text = "⚠ 指纹防护仅 Edge 有效",
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 60, 20),
            Margin = new Padding(14, 9, 0, 0),
            Visible = false
        };
        toolbar.Controls.Add(_fpWarnLabel);
        return toolbar;
    }

    private void BuildList()
    {
        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true,
            Sorting = SortOrder.None
        };
        _list.Columns.Add("实例名称", 140);
        _list.Columns.Add("分组", 80);
        _list.Columns.Add("浏览器", 70);
        _list.Columns.Add("UserAgent", 120);
        _list.Columns.Add("指纹", 60);
        _list.Columns.Add("状态", 66);
        _list.Columns.Add("占用", 74);
        _list.Columns.Add("启动页", 170);

        _list.DoubleClick += (_, _) => LaunchSelected();
        _list.ColumnClick += (_, e) => OnColumnClick(e.Column);
        _list.SelectedIndexChanged += (_, _) => UpdateSelectionInfo();
        _listHost.Controls.Add(_list);
        SizeChanged += (_, _) => FitColumns();
    }

    private void BuildEmptyHint()
    {
        _emptyHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Visible = false,
            Font = new Font("Microsoft YaHei UI", 10F),
            Text = "还没有实例\n\n点击左上角「新建实例」开始，或从菜单「文件 → 导入配置」导入\n\n每个实例拥有独立的 Cookie 与登录态，互不影响"
        };
        _listHost.Controls.Add(_emptyHint);
    }

    private Control BuildBottomBar()
    {
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12, 8, 12, 12)
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
        bflow.Controls.Add(MakeButton("启动选中", LaunchSelected, 96));
        bflow.Controls.Add(MakeButton("全部启动", LaunchAll, 88));
        bflow.Controls.Add(MakeButton("桌面快捷方式", CreateShortcuts, 106));
        bflow.Controls.Add(MakeButton("清缓存", ClearCache, 76));

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

        BuildContextMenu();
        return bottom;
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("启动", null, (_, _) => LaunchSelected());
        menu.Items.Add("发送桌面快捷方式", null, (_, _) => CreateShortcuts());
        menu.Items.Add("清理缓存（保留登录态）", null, (_, _) => ClearCache());
        menu.Items.Add("打开数据目录", null, (_, _) => OpenDataDir());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("复制实例信息", null, (_, _) => CopyInstanceInfo());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("编辑", null, (_, _) => EditInstance());
        menu.Items.Add("删除", null, (_, _) => DeleteInstance());
        _list.ContextMenuStrip = menu;

        _tray.Icon = Icon ?? SystemIcons.Application;
        _tray.Text = AppMeta.Name;
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowFromTray();
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
        trayMenu.Items.Add("全部启动", null, (_, _) => LaunchAll());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出程序", null, (_, _) =>
        {
            _reallyExit = true;
            _tray.Visible = false;
            Close();
        });
        _tray.ContextMenuStrip = trayMenu;
    }

    private static Button MakeButton(string text, Action action, int width, Keys shortcut = Keys.None)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(width, 32),
            Margin = new Padding(3, 4, 3, 0)
        };
        btn.Click += (_, _) => action();
        if (shortcut != Keys.None)
            btn.Tag = shortcut;
        return btn;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 裸 Enter：有选中项时启动选中实例
        if (keyData == Keys.Enter && _list.Focused && _list.SelectedItems.Count > 0)
        {
            LaunchSelected();
            return true;
        }
        // 裸 Delete：删除选中实例
        if (keyData == Keys.Delete && _list.Focused && _list.SelectedItems.Count > 0)
        {
            DeleteInstance();
            return true;
        }
        // F2：编辑选中
        if (keyData == Keys.F2 && _list.SelectedItems.Count == 1)
        {
            EditInstance();
            return true;
        }
        if (keyData == (Keys.Control | Keys.F))
        {
            _txtSearch.Focus();
            _txtSearch.SelectAll();
            return true;
        }
        if (keyData == Keys.F5)
        {
            RefreshStates();
            RefreshSizesAsync();
            SetStatus("已刷新");
            return true;
        }
        if (keyData == Keys.F1)
        {
            ShowHelp();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ==================== 列表与状态 ====================

    private void FitColumns()
    {
        if (_list.Width < 50) return;
        int cols = _list.Columns.Count;
        if (cols == 0) return;
        int total = _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
        if (total <= 0) return;

        // 权重数组与列数可能不一致（增删列时），此处按较小值取用，缺失的按平均权重兜底
        int n = Math.Min(cols, ColWeights.Length);
        int sum = 0;
        for (int i = 0; i < n; i++) sum += ColWeights[i];
        if (n < cols) sum += (cols - n) * 10; // 未定义权重的列按 10 计

        for (int i = 0; i < cols; i++)
        {
            int w = i < ColWeights.Length ? ColWeights[i] : 10;
            _list.Columns[i].Width = Math.Max(50, total * w / sum);
        }
    }

    private string FilterText => _txtSearch.Text.Trim();

    private List<InstanceConfig> VisibleInstances()
    {
        var q = FilterText;
        var all = _manager.Config.Instances.AsEnumerable();
        if (q.Length > 0)
        {
            all = all.Where(i =>
                i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Group.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Note.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.HomePage.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (_sortColumn >= 0)
        {
            Func<InstanceConfig, object> key = _sortColumn switch
            {
                0 => i => i.Name,
                1 => i => i.Group,
                2 => i => i.BrowserId,
                3 => i => UaPresets.DisplayName(i.UserAgent),
                4 => i => i.FpEnabled ? 1 : 0,
                5 => i => i.LastLaunchedAt ?? DateTime.MinValue,
                6 => i => i.CreatedAt,
                _ => i => i.Name
            };
            all = _sortAscending
                ? all.OrderBy(key, Comparer<object>.Create(CompareObj))
                : all.OrderByDescending(key, Comparer<object>.Create(CompareObj));
        }
        return all.ToList();
    }

    private static int CompareObj(object? a, object? b) => (a, b) switch
    {
        (string x, string y) => string.Compare(x, y, StringComparison.CurrentCulture),
        (int x, int y) => x.CompareTo(y),
        (DateTime x, DateTime y) => x.CompareTo(y),
        _ => 0
    };

    private void OnColumnClick(int column)
    {
        if (column >= SortableColumnCount) return;
        if (_sortColumn == column) _sortAscending = !_sortAscending;
        else { _sortColumn = column; _sortAscending = true; }
        ReloadList();
    }

    private void ReloadList()
    {
        var running = GetRunningDataDirs();
        var items = VisibleInstances();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var inst in items)
        {
            var browserName = inst.BrowserId == "auto"
                ? "自动"
                : _browsers.FirstOrDefault(b => b.Id == inst.BrowserId)?.Name ?? "自动";

            bool isRunning = running.Contains(_manager.GetDataDir(inst));

            var item = new ListViewItem(inst.Name);
            item.SubItems.Add(string.IsNullOrWhiteSpace(inst.Group) ? "—" : inst.Group);
            item.SubItems.Add(browserName);
            item.SubItems.Add(UaPresets.DisplayName(inst.UserAgent));
            item.SubItems.Add(inst.FpEnabled ? "已开启" : "已关闭");
            item.SubItems.Add(isRunning ? "运行中" : "未运行");
            item.SubItems.Add("计算中…");
            item.SubItems.Add(string.IsNullOrWhiteSpace(inst.HomePage) ? "—" : inst.HomePage);
            item.Tag = inst;
            item.UseItemStyleForSubItems = false;
            item.SubItems[5].ForeColor = isRunning ? Color.FromArgb(0, 138, 92) : Color.Gray;
            item.SubItems[4].ForeColor = inst.FpEnabled ? Color.FromArgb(32, 92, 168) : Color.Gray;
            if (!string.IsNullOrWhiteSpace(inst.Note))
                item.ToolTipText = inst.Note;
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        FitColumns();
        RefreshSizesAsync();
        UpdateEmptyHint();
        UpdateTitle();
    }

    private void UpdateEmptyHint()
    {
        bool noneAtAll = _manager.Config.Instances.Count == 0;
        bool noneMatched = _list.Items.Count == 0 && !noneAtAll;
        _emptyHint.Visible = noneAtAll || noneMatched;
        _emptyHint.Text = noneAtAll
            ? "还没有实例\n\n点击左上角「新建实例」开始，或从菜单「文件 → 导入配置」导入\n\n每个实例拥有独立的 Cookie 与登录态，互不影响"
            : $"没有匹配「{FilterText}」的实例\n\n换个关键词试试";
        _list.Visible = !_emptyHint.Visible;
    }

    private void UpdateTitle()
    {
        var s = _manager.Config.Settings;
        int total = _manager.Config.Instances.Count;
        int shown = _list.Items.Count;
        int running = _list.Items.Cast<ListViewItem>()
            .Count(i => i.SubItems[5].Text == "运行中");

        string suffix = shown == total
            ? $"共 {total} 个实例，{running} 个运行中"
            : $"显示 {shown}/{total} 个，{running} 个运行中";

        Text = $"{AppMeta.Name} v{AppMeta.Version} - {suffix}";
    }

    private void UpdateSelectionInfo()
    {
        int n = _list.SelectedItems.Count;
        if (n == 0) return;
        SetStatus(n == 1
            ? $"已选中「{_list.SelectedItems[0].Text}」"
            : $"已选中 {n} 个实例");
    }

    private void RefreshStates()
    {
        var running = GetRunningDataDirs();
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is not InstanceConfig inst) continue;
            bool isRunning = running.Contains(_manager.GetDataDir(inst));
            item.SubItems[5].Text = isRunning ? "运行中" : "未运行";
            item.SubItems[5].ForeColor = isRunning ? Color.FromArgb(0, 138, 92) : Color.Gray;
        }
        UpdateTitle();
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
        catch (Exception ex)
        {
            Log.Warn("查询进程列表失败：" + ex.Message);
        }
        return set;
    }

    /// <summary>后台统计各实例数据目录占用，避免大目录遍历卡住 UI。</summary>
    private void RefreshSizesAsync()
    {
        var snapshot = _manager.Config.Instances.ToList();
        Task.Run(() =>
        {
            var sizes = new Dictionary<string, long>();
            foreach (var inst in snapshot)
                sizes[inst.Id] = GetDirSize(_manager.GetDataDir(inst));

            // 后台线程不能直接读窗体的 IsDisposed / 调 BeginInvoke：
            // 窗体可能已销毁，此处统一 try 兜底，避免 ObjectDisposedException 冒泡到全局异常。
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    if (IsDisposed) return;
                    foreach (ListViewItem item in _list.Items)
                    {
                        if (item.Tag is InstanceConfig ci && sizes.TryGetValue(ci.Id, out var sz))
                            item.SubItems[6].Text = sz == 0 ? "—" : FormatSize(sz);
                    }
                });
            }
            catch (ObjectDisposedException) { /* 窗体已关闭，忽略 */ }
            catch (InvalidOperationException) { /* 句柄未创建，忽略 */ }
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
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F1} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F0} KB",
        _ => $"{bytes} B"
    };

    // ==================== 启动逻辑 ====================

    private BrowserInfo? ResolveBrowser(InstanceConfig inst)
    {
        var id = inst.BrowserId;
        if (id == "auto")
            id = _manager.Config.Settings.DefaultBrowserId;
        if (id == "auto")
        {
            // 自动模式优先 Edge：指纹防护扩展仅 Edge 支持（Chrome 137+ 已移除 --load-extension）
            var fpEdge = _browsers.FirstOrDefault(b => b.Id == "edge");
            if (inst.FpEnabled && fpEdge != null) return fpEdge;
            return _browsers.FirstOrDefault(b => b.Id == "chrome") ?? _browsers.FirstOrDefault();
        }
        return _browsers.FirstOrDefault(b => b.Id == id) ?? _browsers.FirstOrDefault();
    }

    /// <summary>确保实例的防指纹扩展文件已生成。</summary>
    private void EnsureFpFiles(InstanceConfig inst)
    {
        if (!inst.FpEnabled) return;
        if (string.IsNullOrWhiteSpace(inst.FpSeed))
        {
            inst.FpSeed = Guid.NewGuid().ToString("N");
            _manager.Save();
        }
        var tz = ResolveTimeZone(inst);
        FingerprintExtension.Write(inst, _manager.BaseDir, tz);
    }

    /// <summary>
    /// 解析实例应使用的时区：
    ///   TimeZone == "off"  → 返回 NoTimeZoneTag（不伪装）
    ///   TimeZone 非空      → 手动指定值
    ///   TimeZone 为空      → 自动：有代理则跟随代理出口地区（带缓存），否则用默认时区
    /// </summary>
    private string ResolveTimeZone(InstanceConfig inst)
    {
        var tz = inst.TimeZone?.Trim() ?? "";
        if (FingerprintExtension.IsNoTimeZone(tz))
            return FingerprintExtension.NoTimeZoneTag;
        if (tz.Length > 0)
            return tz;

        if (string.IsNullOrWhiteSpace(inst.ProxyServer))
            return TimeZoneMap.DefaultTimeZone;

        var geo = GetProxyGeo(inst.ProxyServer);
        if (geo.Success)
        {
            Log.Info($"实例 {inst.Name} 时区跟随代理地区：{geo.Detail} → {geo.TimeZone}");
            return geo.TimeZone;
        }

        Log.Warn($"实例 {inst.Name} 代理地区探测失败（{geo.Detail}），时区回退为 {TimeZoneMap.DefaultTimeZone}");
        SetStatus($"提示：「{inst.Name}」无法确认代理地区，时区已按默认处理");
        return TimeZoneMap.DefaultTimeZone;
    }

    /// <summary>代理地区探测结果缓存（key = 代理地址），避免每次启动都联网。</summary>
    private readonly Dictionary<string, ProxyGeoLookup.GeoResult> _geoCache = new(StringComparer.OrdinalIgnoreCase);

    private ProxyGeoLookup.GeoResult GetProxyGeo(string proxyServer)
    {
        if (_geoCache.TryGetValue(proxyServer, out var cached)) return cached;
        var r = ProxyGeoLookup.Lookup(proxyServer);
        _geoCache[proxyServer] = r;
        return r;
    }

    /// <summary>构造实例的浏览器启动参数（启动与快捷方式共用）。</summary>
    private string BuildArgs(InstanceConfig inst, string dataDir)
    {
        var args = new StringBuilder();
        args.Append("--user-data-dir=\"").Append(dataDir).Append("\"");
        args.Append(" --no-first-run --no-default-browser-check");
        if (!string.IsNullOrWhiteSpace(inst.ProxyServer))
            args.Append(" --proxy-server=\"").Append(inst.ProxyServer.Trim()).Append("\"");
        if (!string.IsNullOrWhiteSpace(inst.UserAgent))
        {
            args.Append(" --user-agent=\"").Append(inst.UserAgent.Trim()).Append("\"");
            args.Append(" --disable-features=UserAgentClientHint");
        }
        if (inst.FpEnabled)
            args.Append(" --load-extension=\"").Append(FingerprintExtension.GetDir(inst, _manager.BaseDir)).Append("\"");
        if (!string.IsNullOrWhiteSpace(inst.ExtraArgs))
            args.Append(' ').Append(inst.ExtraArgs.Trim());
        if (!string.IsNullOrWhiteSpace(inst.HomePage))
        {
            var home = inst.HomePage.Trim();
            // 启动页必须作为 URL 传入：以 '-' 开头的会被浏览器当成开关，加引号兜底空格
            if (home.StartsWith('-'))
                Log.Warn($"实例 {inst.Name} 的启动页以 '-' 开头，已忽略：{home}");
            else
                args.Append(" \"").Append(home.Replace("\"", "")).Append('"');
        }
        return args.ToString();
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

        string dataDir = _manager.GetDataDir(inst);
        try
        {
            Directory.CreateDirectory(dataDir);
            EnsureFpFiles(inst);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法准备实例数据目录：\n{ex.Message}\n\n请确认程序目录可写。", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log.Error("准备数据目录失败", ex);
            return;
        }

        if (_manager.Config.Settings.ConfirmDuplicateLaunch &&
            GetRunningDataDirs().Contains(Path.TrimEndingDirectorySeparator(dataDir)) &&
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
            inst.LastLaunchedAt = DateTime.Now;
            _manager.Save();
            SetStatus($"已启动「{inst.Name}」（{browser.Name}）");
            Log.Info($"启动实例 {inst.Name}，使用 {browser.Name}");
        }
        catch (Exception ex)
        {
            Log.Error($"启动实例 {inst.Name} 失败", ex);
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
        foreach (var t in targets) Launch(t);
        RefreshStates();
    }

    private void LaunchAll()
    {
        // 只启动当前可见（筛选结果）的实例
        var targets = VisibleInstances();
        if (targets.Count == 0)
        {
            SetStatus(_manager.Config.Instances.Count == 0
                ? "还没有实例，点击「新建实例」创建一个"
                : "当前筛选条件下没有可启动的实例");
            return;
        }
        if (targets.Count > 5 &&
            MessageBox.Show($"即将启动 {targets.Count} 个实例，可能占用较多内存（每个约 300-500MB）。\n\n是否继续？",
                "确认批量启动", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var inst in targets) Launch(inst);
        RefreshStates();
    }

    // ==================== 实例增删改 ====================

    private void AddInstance()
    {
        int n = _manager.Config.Instances.Count + 1;
        var defName = $"实例 {n}";
        while (_manager.Config.Instances.Any(i => i.Name == defName))
        {
            n++;
            defName = $"实例 {n}";
        }

        var template = new InstanceConfig
        {
            Name = defName,
            BrowserId = "auto",
            FpEnabled = true,
            FpSeed = Guid.NewGuid().ToString("N")
        };

        using var dlg = new InstanceDialog("新建实例", template, true, _browserChoices);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (_manager.Config.Instances.Any(i => i.Name == dlg.InstanceName))
        {
            MessageBox.Show("已存在同名实例，请换一个名称。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var inst = new InstanceConfig
        {
            Name = dlg.InstanceName,
            Group = dlg.GroupName,
            BrowserId = dlg.BrowserId,
            HomePage = dlg.HomePage,
            UserAgent = dlg.UserAgent,
            FpEnabled = dlg.FpEnabled,
            FpSeed = Guid.NewGuid().ToString("N"),
            ProxyServer = dlg.ProxyServer,
            TimeZone = dlg.TimeZoneValue,
            ExtraArgs = dlg.ExtraArgs,
            Note = dlg.Note
        };
        _manager.Config.Instances.Add(inst);
        _manager.Save();
        ReloadList();
        SetStatus($"已创建「{dlg.InstanceName}」，双击列表行即可启动");
        Log.Info($"新建实例 {inst.Name}");
    }

    private void EditInstance()
    {
        if (!TryGetSingleSelected(out var inst)) return;

        if (IsInstanceRunning(inst))
        {
            if (MessageBox.Show($"实例「{inst.Name}」正在运行。\n修改配置需重启该实例才会生效。\n\n是否继续编辑？",
                    "实例运行中", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        using var dlg = new InstanceDialog("编辑实例 - " + inst.Name, inst, true, _browserChoices);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (_manager.Config.Instances.Any(i => i != inst && i.Name == dlg.InstanceName))
        {
            MessageBox.Show("已存在同名实例，请换一个名称。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        inst.Name = dlg.InstanceName;
        inst.Group = dlg.GroupName;
        inst.BrowserId = dlg.BrowserId;
        inst.HomePage = dlg.HomePage;
        inst.UserAgent = dlg.UserAgent;
        inst.FpEnabled = dlg.FpEnabled;
        inst.ProxyServer = dlg.ProxyServer;
        inst.TimeZone = dlg.TimeZoneValue;
        inst.ExtraArgs = dlg.ExtraArgs;
        inst.Note = dlg.Note;
        if (inst.FpEnabled && string.IsNullOrWhiteSpace(inst.FpSeed))
            inst.FpSeed = Guid.NewGuid().ToString("N");

        _manager.Save();
        ReloadList();
        SetStatus($"已更新「{inst.Name}」配置");
        Log.Info($"编辑实例 {inst.Name}");
    }

    private void DeleteInstance()
    {
        var targets = _list.SelectedItems.Cast<ListViewItem>()
            .Where(i => i.Tag is InstanceConfig)
            .Select(i => (InstanceConfig)i.Tag!)
            .ToList();
        if (targets.Count == 0)
        {
            SetStatus("请先选中一个实例");
            return;
        }

        string msg = targets.Count == 1
            ? $"确定删除实例「{targets[0].Name}」吗？"
            : $"确定删除选中的 {targets.Count} 个实例吗？\n\n" +
              string.Join("\n", targets.Select(t => "  · " + t.Name));

        var running = targets.Where(IsInstanceRunning).ToList();
        if (running.Count > 0)
            msg += $"\n\n注意：其中 {running.Count} 个实例的浏览器正在运行。";

        // 破坏性操作：默认焦点放在「否」，避免误按回车
        if (MessageBox.Show(msg, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        var deleteData = MessageBox.Show(
            "是否同时删除这些实例的数据目录？\n\n" +
            "选择「是」：Cookie、登录态、缓存将全部丢失，不可恢复；\n" +
            "选择「否」：仅从列表移除，数据目录保留在 Data\\ 下（之后可重新导入复用）。",
            "删除数据", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        foreach (var inst in targets)
        {
            _manager.Config.Instances.Remove(inst);
            if (!deleteData) continue;
            try { Directory.Delete(_manager.GetDataDir(inst), true); }
            catch (Exception ex) { Log.Warn($"删除数据目录失败 {inst.Name}：{ex.Message}"); }
            try { Directory.Delete(FingerprintExtension.GetDir(inst, _manager.BaseDir), true); }
            catch { /* 同上 */ }
        }

        _manager.Save();
        ReloadList();
        SetStatus($"已删除 {targets.Count} 个实例");
        Log.Info($"删除 {targets.Count} 个实例");
    }

    private bool TryGetSingleSelected(out InstanceConfig inst)
    {
        inst = null!;
        if (_list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not InstanceConfig i)
        {
            SetStatus("请先选中一个实例");
            return false;
        }
        inst = i;
        return true;
    }

    private bool IsInstanceRunning(InstanceConfig inst)
        => GetRunningDataDirs().Contains(_manager.GetDataDir(inst));

    // ==================== 工具功能 ====================

    private void OpenDataDir()
    {
        if (!TryGetSingleSelected(out var inst)) return;
        string dir = _manager.GetDataDir(inst);
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("打开数据目录失败", ex); }
    }

    private void OpenAppDir()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"\"{AppContext.BaseDirectory.TrimEnd('\\')}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("打开程序目录失败", ex); }
    }

    private void OpenLogDir()
    {
        try
        {
            Directory.CreateDirectory(Log.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"\"{Log.LogDirectory.TrimEnd('\\')}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("打开日志目录失败", ex); }
    }

    private void CopyInstanceInfo()
    {
        if (!TryGetSingleSelected(out var inst)) return;
        var sb = new StringBuilder();
        sb.AppendLine($"实例名称：{inst.Name}");
        sb.AppendLine($"分组：{(string.IsNullOrWhiteSpace(inst.Group) ? "—" : inst.Group)}");
        sb.AppendLine($"浏览器：{(inst.BrowserId == "auto" ? "自动" : inst.BrowserId)}");
        sb.AppendLine($"UserAgent：{UaPresets.DisplayName(inst.UserAgent)}");
        sb.AppendLine($"指纹防护：{(inst.FpEnabled ? "已开启" : "已关闭")}");
        sb.AppendLine($"代理：{(string.IsNullOrWhiteSpace(inst.ProxyServer) ? "直连" : inst.ProxyServer)}");
        var tzShow = string.IsNullOrWhiteSpace(inst.TimeZone)
            ? (string.IsNullOrWhiteSpace(inst.ProxyServer) ? "自动（中国时区）" : "自动（跟随代理地区）")
            : inst.TimeZone == "off" ? "不伪装" : inst.TimeZone;
        sb.AppendLine($"时区：{tzShow}");
        sb.AppendLine($"启动页：{(string.IsNullOrWhiteSpace(inst.HomePage) ? "—" : inst.HomePage)}");
        sb.AppendLine($"数据目录：{_manager.GetDataDir(inst)}");
        sb.AppendLine($"创建时间：{inst.CreatedAt:yyyy-MM-dd HH:mm}");
        try
        {
            Clipboard.SetText(sb.ToString());
            SetStatus("实例信息已复制到剪贴板");
        }
        catch (Exception ex) { Log.Error("复制到剪贴板失败", ex); }
    }

    private void ClearCache()
    {
        if (!TryGetSingleSelected(out var inst)) return;
        ClearCacheFor(inst);
    }

    private void ClearCacheFor(InstanceConfig inst)
    {
        if (IsInstanceRunning(inst))
        {
            MessageBox.Show("该实例的浏览器正在运行，请先关闭它再清理缓存（否则部分文件被占用无法删除）。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string dir = _manager.GetDataDir(inst);
        long freed = 0;
        int failed = 0;
        foreach (var sub in new[]
                 {
                     "Cache", "Code Cache", "GPUCache", "GrShaderCache",
                     Path.Combine("Service Worker", "CacheStorage"),
                     Path.Combine("Service Worker", "ScriptCache")
                 })
        {
            var p = Path.Combine(dir, sub);
            try
            {
                if (!Directory.Exists(p)) continue;
                freed += GetDirSize(p);
                Directory.Delete(p, true);
            }
            catch { failed++; }
        }

        RefreshSizesAsync();
        string tail = failed > 0 ? $"，{failed} 个目录被占用已跳过" : "";
        SetStatus($"已清理「{inst.Name}」缓存 {FormatSize(freed)}{tail}，登录态与 Cookie 已保留");
        Log.Info($"清理实例 {inst.Name} 缓存 {FormatSize(freed)}");
    }

    /// <summary>为选中的实例在桌面创建 .lnk 快捷方式。</summary>
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
            if (browser == null) { fail++; continue; }

            string lnkPath = Path.Combine(desktop, $"浏览器 - {SanitizeFileName(inst.Name)}.lnk");
            try
            {
                EnsureFpFiles(inst);
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) throw new InvalidOperationException("系统缺少 WScript.Shell 组件");
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.TargetPath = browser.ExePath;
                sc.Arguments = BuildArgs(inst, _manager.GetDataDir(inst));
                sc.WorkingDirectory = Path.GetDirectoryName(browser.ExePath) ?? "";
                sc.IconLocation = $"{browser.ExePath},0";
                sc.Description = $"多开实例「{inst.Name}」- 独立数据目录，Cookie 隔离";
                sc.Save();
                ok++;
            }
            catch (Exception ex)
            {
                Log.Error($"创建快捷方式失败 {inst.Name}", ex);
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

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    // ==================== 导入导出与设置 ====================

    private void ExportConfig()
    {
        if (_manager.Config.Instances.Count == 0)
        {
            MessageBox.Show("还没有实例可以导出。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "导出实例配置",
            Filter = "配置文件 (*.bmconfig)|*.bmconfig|JSON 文件 (*.json)|*.json",
            FileName = $"BrowserMulti-配置-{DateTime.Now:yyyyMMdd}.bmconfig",
            DefaultExt = "bmconfig"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _manager.ExportBundle(_manager.Config.Instances, dlg.FileName);
            var r = MessageBox.Show(
                $"已导出 {_manager.Config.Instances.Count} 个实例的配置。\n\n" +
                "说明：导出文件仅包含配置信息（名称、UA、指纹种子、代理、备注等），\n" +
                "不包含 Cookie、登录态和缓存数据。\n\n" +
                "是否打开文件所在目录？",
                "导出成功", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("explorer.exe",
                    $"/select,\"{dlg.FileName}\"") { UseShellExecute = true });
            SetStatus("配置已导出：" + Path.GetFileName(dlg.FileName));
        }
        catch (Exception ex)
        {
            Log.Error("导出配置失败", ex);
            MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportConfig()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "导入实例配置",
            Filter = "配置文件 (*.bmconfig;*.json)|*.bmconfig;*.json|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var mode = MessageBox.Show(
            "导入方式：\n\n" +
            "「是」= 保留原指纹种子（同一台电脑迁移，指纹不变）\n" +
            "「否」= 为每个实例生成新指纹（换电脑使用，避免与来源环境重复）\n\n" +
            "同名实例将自动跳过。",
            "选择导入方式", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (mode == DialogResult.Cancel) return;
        try
        {
            var (added, skipped) = _manager.ImportBundle(dlg.FileName, regenerateIdsAndSeeds: mode == DialogResult.No);
            ReloadList();
            string msg = $"导入完成：新增 {added} 个实例";
            if (skipped > 0) msg += $"，跳过同名 {skipped} 个";
            SetStatus(msg);
            MessageBox.Show(msg + "\n\n注意：导入的只是配置，各实例的 Cookie 与登录态需要重新登录。",
                "导入结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log.Error("导入配置失败", ex);
            MessageBox.Show("导入失败：" + ex.Message + "\n\n请确认文件是本工具导出的配置文件。",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>根据检测到的浏览器构建下拉选项（自动 + 各已装浏览器）。</summary>
    private void BuildBrowserChoices()
    {
        _browserChoices.Clear();
        _browserChoices.Add(new BrowserChoice("auto", "自动（默认浏览器）"));
        foreach (var b in _browsers)
            _browserChoices.Add(new BrowserChoice(b.Id, b.Name));
    }

    private void OpenSettings()
    {
        var s = _manager.Config.Settings;
        using var dlg = new SettingsDialog(s, _browserChoices);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        s.DefaultBrowserId = dlg.DefaultBrowserId;
        s.MinimizeToTray = dlg.MinimizeToTray;
        s.ConfirmDuplicateLaunch = dlg.ConfirmDuplicateLaunch;
        s.LogEnabled = dlg.LogEnabled;
        _manager.Save();
        Log.SetEnabled(s.LogEnabled);
        UpdateFpWarning();
        SetStatus("设置已保存");
        Log.Info("设置已更新");
    }

    // ==================== 帮助 ====================

    private void ShowAbout()
    {
        using var dlg = new AboutDialog();
        dlg.ShowDialog(this);
    }

    private void ShowHelp()
    {
        var text =
            $"{AppMeta.Name} v{AppMeta.Version}  使用说明\n" +
            "─────────────────────────────\n\n" +
            "【它是什么】\n" +
            "通过给每个浏览器实例指定独立的用户数据目录，实现多环境并行使用，\n" +
            "Cookie、登录态、扩展完全隔离且长期保留（非无痕模式）。\n\n" +
            "【快速开始】\n" +
            "1. 点「新建实例」，填写名称（可选分组、启动页）\n" +
            "2. 需要伪装身份时，在「身份与环境」页选择 UserAgent 预设\n" +
            "3. 双击列表中的实例即可启动\n" +
            "4. 点「桌面快捷方式」后可脱离本工具直接启动\n\n" +
            "【常用快捷键】\n" +
            "  Ctrl+N      新建实例\n" +
            "  F2          编辑选中\n" +
            "  Delete      删除选中\n" +
            "  Ctrl+F      搜索实例\n" +
            "  F5          刷新状态\n" +
            "  Enter       启动选中\n" +
            "  F1          本帮助\n\n" +
            "【数据在哪里】\n" +
            "  配置：程序目录\\instances.json\n" +
            "  数据：程序目录\\Data\\<实例ID>\\\n" +
            "  日志：程序目录\\logs\\\n" +
            "整个程序目录可直接拷贝迁移，Cookie 需在新机器重新登录。\n\n" +
            "【指纹防护说明】\n" +
            "  覆盖：Canvas、WebGL、AudioContext、字体枚举、语言时区、\n" +
            "        WebRTC 内网 IP、CPU/内存/触点、移动端屏幕\n" +
            "  仅对 Microsoft Edge 生效（Chrome 137+ 已移除扩展加载参数）\n" +
            "  每个实例指纹独立且稳定，不会每次启动都变\n\n" +
            "【时区与代理】\n" +
            "  实例的时区可自动跟随代理出口地区，避免「境外 IP + 中国时区」\n" +
            "  这种一眼可见的矛盾。在实例编辑的「高级」页可切换：\n" +
            "    · 自动  —— 有代理则查询代理地区并套用对应时区\n" +
            "    · 指定  —— 固定使用某个时区（与代理无关）\n" +
            "    · 不伪装 —— 保持浏览器真实时区\n" +
            "  注意：查询代理地区需要联网，失败时会回退为中国时区。\n\n" +
            "【已知限制】\n" +
            "  · 代理留空即直连；同 IP 多账号仍可能被平台关联，建议配合独立代理\n" +
            "  · 跨电脑迁移时 Cookie 与密码因系统加密需重新登录\n" +
            "  · 浏览器内核为系统已安装的 Edge/Chrome，本程序不含内核";

        using var dlg = new Form
        {
            Text = "使用说明",
            ClientSize = new Size(600, 520),
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new Size(480, 400),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false
        };
        var tb = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F),
            Text = text
        };
        var close = new Button { Text = "关闭", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        dlg.Controls.Add(tb);
        dlg.Controls.Add(close);
        dlg.AcceptButton = close;
        dlg.CancelButton = close;
        dlg.ShowDialog(this);
    }

    private void CheckUpdate()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppMeta.RepoUrl + "/releases") { UseShellExecute = true });
            SetStatus("已打开项目发布页，可查看是否有新版本");
        }
        catch (Exception ex) { Log.Error("打开发布页失败", ex); }
    }

    // ==================== 托盘与窗口 ====================

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var settings = _manager.Config.Settings;

        if (!_reallyExit && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _tray.ShowBalloonTip(2000, AppMeta.Name,
                    "程序已最小化到托盘，双击托盘图标可重新打开。", ToolTipIcon.Info);
                _trayHintShown = true;
            }
            return;
        }

        // 幂等：托盘菜单退出会先 Dispose 一次，这里再走一次需避免重复记录/重复释放
        if (!_closing)
        {
            _closing = true;
            _timer?.Stop();
            _timer?.Dispose();
            SaveWindowSettings();
            Log.Info("程序退出");
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnFormClosing(e);
    }

    private void SetStatus(string text, string? tooltip = null)
    {
        _statusLabel.Text = text;
        _toolTip.SetToolTip(_statusLabel, tooltip ?? text);
    }

    private void UpdateFpWarning()
    {
        var id = _manager.Config.Settings.DefaultBrowserId;
        bool chromeSelected = id == "chrome"
            || (id == "auto" && _browsers.Any(b => b.Id == "chrome") && !_browsers.Any(b => b.Id == "edge"));
        _fpWarnLabel.Visible = chromeSelected;
        if (chromeSelected)
            _toolTip.SetToolTip(_fpWarnLabel,
                "Chrome 137 起已移除 --load-extension 参数，指纹防护扩展不会加载。\n如需指纹防护，请在「工具 → 设置」把默认浏览器切换为 Microsoft Edge。");
    }
}
