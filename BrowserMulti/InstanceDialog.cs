namespace BrowserMulti;

public class InstanceDialog : Form
{
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtGroup = new();
    private readonly TextBox _txtHome = new();
    private readonly TextBox _txtUa = new();
    private readonly TextBox _txtProxy = new();
    private readonly TextBox _txtExtra = new();
    private readonly TextBox _txtNote = new();
    private readonly ComboBox _comboBrowser = new();
    private readonly ComboBox _comboUa = new();
    private readonly ComboBox _comboTz = new();
    private Label _tzHint = null!;
    private readonly CheckBox _chkFp = new();
    private readonly TabControl _tabs = new();

    public string InstanceName => _txtName.Text.Trim();
    public string GroupName => _txtGroup.Text.Trim();
    public string BrowserId => (_comboBrowser.SelectedItem as BrowserChoice)?.Id ?? "auto";
    public string HomePage => _txtHome.Text.Trim();
    public string UserAgent { get; private set; } = "";
    public string ProxyServer => _txtProxy.Text.Trim();
    public string ExtraArgs => _txtExtra.Text.Trim();
    public string Note => _txtNote.Text.Trim();
    public bool FpEnabled => _chkFp.Checked;

    public InstanceDialog(string title, InstanceConfig? existing, bool defaultFp,
        List<BrowserChoice> choices)
    {
        var src = existing ?? new InstanceConfig();

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 470);

        Controls.Add(new Label { Text = "实例名称:", AutoSize = true, Location = new Point(18, 20) });
        _txtName.SetBounds(110, 16, 200, 25);
        _txtName.Text = src.Name;
        Controls.Add(_txtName);

        Controls.Add(new Label { Text = "分组:", AutoSize = true, Location = new Point(326, 20) });
        _txtGroup.SetBounds(372, 16, 130, 25);
        _txtGroup.Text = src.Group;
        _txtGroup.PlaceholderText = "如：工作号";
        Controls.Add(_txtGroup);

        _tabs.SetBounds(18, 52, 484, 356);
        Controls.Add(_tabs);

        var tabBasic = new TabPage("基本");
        var tabIdentity = new TabPage("身份与环境");
        var tabAdvanced = new TabPage("高级");
        _tabs.TabPages.AddRange(new[] { tabBasic, tabIdentity, tabAdvanced });

        BuildBasicTab(tabBasic, src, choices);
        BuildIdentityTab(tabIdentity, src, defaultFp);
        BuildAdvancedTab(tabAdvanced, src);

        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK };
        btnOk.SetBounds(332, 422, 80, 30);
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        btnCancel.SetBounds(420, 422, 80, 30);
        Controls.AddRange(new Control[] { btnOk, btnCancel });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void BuildBasicTab(TabPage page, InstanceConfig src, List<BrowserChoice> choices)
    {
        page.Controls.Add(new Label { Text = "浏览器:", AutoSize = true, Location = new Point(16, 24) });
        _comboBrowser.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboBrowser.SetBounds(120, 20, 330, 25);
        foreach (var c in choices) _comboBrowser.Items.Add(c);
        // 防御：choices 为空时必须至少有一项，否则设置 SelectedIndex 会抛 ArgumentOutOfRangeException
        if (_comboBrowser.Items.Count == 0)
            _comboBrowser.Items.Add(new BrowserChoice("auto", "自动（默认浏览器）"));
        var idx = choices.FindIndex(c => c.Id == src.BrowserId);
        _comboBrowser.SelectedIndex = idx < 0 ? 0 : idx;
        page.Controls.Add(_comboBrowser);

        page.Controls.Add(new Label { Text = "启动页:", AutoSize = true, Location = new Point(16, 62) });
        _txtHome.SetBounds(120, 58, 330, 25);
        _txtHome.Text = src.HomePage;
        _txtHome.PlaceholderText = "https://www.baidu.com（可留空）";
        page.Controls.Add(_txtHome);

        page.Controls.Add(new Label { Text = "备注:", AutoSize = true, Location = new Point(16, 100) });
        _txtNote.SetBounds(120, 96, 330, 90);
        _txtNote.Multiline = true;
        _txtNote.ScrollBars = ScrollBars.Vertical;
        _txtNote.Text = src.Note;
        _txtNote.PlaceholderText = "仅本工具可见，例如：此实例用于某平台运营";
        page.Controls.Add(_txtNote);

        page.Controls.Add(new Label
        {
            Text = "提示：实例数据保存在程序目录 Data\\ 下，改名不会丢失登录态。",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(16, 200)
        });
    }

    private void BuildIdentityTab(TabPage page, InstanceConfig src, bool defaultFp)
    {
        page.Controls.Add(new Label { Text = "UserAgent:", AutoSize = true, Location = new Point(16, 24) });
        _comboUa.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboUa.SetBounds(120, 20, 330, 25);
        foreach (var p in UaPresets.All) _comboUa.Items.Add(p.Name);
        int presetIdx = UaPresets.All.ToList().FindIndex(p => p.Value == src.UserAgent);
        bool isCustom = presetIdx < 0 && !string.IsNullOrWhiteSpace(src.UserAgent);
        _comboUa.SelectedIndex = isCustom
            ? UaPresets.All.Length - 1
            : (presetIdx >= 0 ? presetIdx : 0);
        page.Controls.Add(_comboUa);

        _txtUa.SetBounds(120, 52, 330, 25);
        _txtUa.PlaceholderText = "输入完整 User-Agent 字符串";
        _txtUa.Text = src.UserAgent;
        _txtUa.Enabled = isCustom;
        page.Controls.Add(_txtUa);

        _comboUa.SelectedIndexChanged += (_, _) =>
        {
            bool custom = (_comboUa.SelectedItem as string) == "自定义…";
            _txtUa.Enabled = custom;
            if (!custom) _txtUa.Text = "";
        };

        _chkFp.Text = "启用指纹防护（仅 Edge 有效）";
        _chkFp.AutoSize = true;
        _chkFp.Location = new Point(120, 92);
        _chkFp.Checked = src.FpEnabled || (string.IsNullOrEmpty(src.FpSeed) && defaultFp);
        page.Controls.Add(_chkFp);

        page.Controls.Add(new Label
        {
            Text = "覆盖 Canvas、WebGL、Audio、字体、设备信息、时区/语言、WebRTC 内网 IP；\n" +
                   "Chrome 137+ 已移除扩展加载参数，用 Chrome 启动时防护不生效。",
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 90, 0),
            Location = new Point(120, 118)
        });

        page.Controls.Add(new Label
        {
            Text = "指纹画像自动跟随 UserAgent 预设，\n保证伪装一致性（如选 iPhone 则屏幕、GPU 同步伪装）。",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(16, 175)
        });
    }

    private void BuildAdvancedTab(TabPage page, InstanceConfig src)
    {
        page.Controls.Add(new Label { Text = "代理服务器:", AutoSize = true, Location = new Point(16, 20) });
        _txtProxy.SetBounds(120, 16, 330, 25);
        _txtProxy.Text = src.ProxyServer;
        _txtProxy.PlaceholderText = "留空为直连，例：http://127.0.0.1:7890";
        page.Controls.Add(_txtProxy);

        page.Controls.Add(new Label
        {
            Text = "格式：http://主机:端口  或  socks5://主机:端口。留空即直连。\n不同实例配置不同代理可实现 IP 隔离。",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(120, 44)
        });

        page.Controls.Add(new Label { Text = "时区:", AutoSize = true, Location = new Point(16, 88) });
        _comboTz.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboTz.SetBounds(120, 84, 330, 25);
        foreach (var t in TimeZoneOptions) _comboTz.Items.Add(t.Display);
        int tzIdx = TimeZoneOptions.FindIndex(t => string.Equals(t.Value, src.TimeZone?.Trim() ?? "",
            StringComparison.OrdinalIgnoreCase));
        _comboTz.SelectedIndex = tzIdx >= 0 ? tzIdx : 0;
        _comboTz.SelectedIndexChanged += (_, _) => UpdateTzHint();
        page.Controls.Add(_comboTz);

        _tzHint = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(120, 112)
        };
        page.Controls.Add(_tzHint);
        UpdateTzHint();

        page.Controls.Add(new Label { Text = "附加参数:", AutoSize = true, Location = new Point(16, 146) });
        _txtExtra.SetBounds(120, 142, 330, 56);
        _txtExtra.Multiline = true;
        _txtExtra.ScrollBars = ScrollBars.Vertical;
        _txtExtra.Text = src.ExtraArgs;
        _txtExtra.PlaceholderText = "例：--window-size=1280,800 --lang=en-US";
        page.Controls.Add(_txtExtra);

        page.Controls.Add(new Label
        {
            Text = "原样追加到命令行。请勿填写 --user-data-dir / --user-agent，\n这两项由本工具管理，重复填写可能导致异常。",
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 90, 0),
            Location = new Point(120, 202)
        });

        page.Controls.Add(new Label { Text = "指纹种子（只读）:", AutoSize = true, Location = new Point(16, 246) });
        page.Controls.Add(new TextBox
        {
            Text = string.IsNullOrWhiteSpace(src.FpSeed) ? "尚未生成" : src.FpSeed,
            ReadOnly = true,
            Bounds = new Rectangle(120, 242, 330, 25),
            BackColor = Color.FromArgb(245, 245, 245)
        });
    }

    /// <summary>时区下拉项：Value 为实际存入配置的值，Display 为中文说明。</summary>
    private static readonly List<(string Value, string Display)> TimeZoneOptions = new()
    {
        ("", "自动（有代理则跟随代理地区，否则中国时区）"),
        ("Asia/Shanghai", "中国 · Asia/Shanghai (UTC+8)"),
        ("Asia/Hong_Kong", "中国香港 · Asia/Hong_Kong (UTC+8)"),
        ("Asia/Taipei", "中国台湾 · Asia/Taipei (UTC+8)"),
        ("Asia/Tokyo", "日本 · Asia/Tokyo (UTC+9)"),
        ("Asia/Seoul", "韩国 · Asia/Seoul (UTC+9)"),
        ("Asia/Singapore", "新加坡 · Asia/Singapore (UTC+8)"),
        ("Asia/Bangkok", "泰国 · Asia/Bangkok (UTC+7)"),
        ("Asia/Kolkata", "印度 · Asia/Kolkata (UTC+5:30)"),
        ("Asia/Dubai", "阿联酋 · Asia/Dubai (UTC+4)"),
        ("Europe/London", "英国 · Europe/London (UTC+0)"),
        ("Europe/Berlin", "德国 · Europe/Berlin (UTC+1)"),
        ("Europe/Paris", "法国 · Europe/Paris (UTC+1)"),
        ("Europe/Moscow", "俄罗斯 · Europe/Moscow (UTC+3)"),
        ("America/New_York", "美东 · America/New_York (UTC-5)"),
        ("America/Chicago", "美中 · America/Chicago (UTC-6)"),
        ("America/Los_Angeles", "美西 · America/Los_Angeles (UTC-8)"),
        ("America/Sao_Paulo", "巴西 · America/Sao_Paulo (UTC-3)"),
        ("Australia/Sydney", "澳大利亚 · Australia/Sydney (UTC+10)"),
        ("off", "不伪装时区"),
    };

    private void UpdateTzHint()
    {
        bool off = GetSelectedTimeZone() == "off";
        bool auto = GetSelectedTimeZone() == "";
        _tzHint.Text = off
            ? "不改写时区，使用浏览器真实时区。"
            : auto
                ? "推荐：自动后时区会与代理出口地区保持一致，避免「境外 IP + 中国时区」的冲突。"
                : "使用固定时区，与代理地区无关。";
        _tzHint.ForeColor = auto ? Color.FromArgb(32, 92, 168) : Color.Gray;
    }

    /// <summary>当前选中的时区配置值。</summary>
    private string GetSelectedTimeZone()
    {
        int i = _comboTz.SelectedIndex;
        return i >= 0 && i < TimeZoneOptions.Count ? TimeZoneOptions[i].Value : "";
    }

    public string TimeZoneValue => GetSelectedTimeZone();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _txtName.Focus();
        _txtName.SelectAll();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (DialogResult != DialogResult.OK) return;

        if (_txtName.Text.Trim().Length == 0)
        {
            Fail("请输入实例名称。");
            e.Cancel = true;
            return;
        }

        int sel = _comboUa.SelectedIndex;
        bool customSelected = sel >= 0 && sel < UaPresets.All.Length
                              && UaPresets.All[sel].Value == UaPresets.CustomKey;
        UserAgent = customSelected
            ? _txtUa.Text.Trim()
            : (sel >= 0 && sel < UaPresets.All.Length ? UaPresets.All[sel].Value : "");

        if (customSelected && UserAgent.Length == 0)
        {
            _tabs.SelectedIndex = 1;
            Fail("选择了自定义 UserAgent，请输入完整字符串。");
            e.Cancel = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(ProxyServer) && !IsValidProxy(ProxyServer))
        {
            _tabs.SelectedIndex = 2;
            Fail("代理格式不正确。\n正确格式：http://127.0.0.1:7890 或 socks5://127.0.0.1:1080");
            e.Cancel = true;
        }
    }

    private static bool IsValidProxy(string s)
    {
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "socks5" && uri.Scheme != "socks4")
            return false;
        if (uri.Host.Length == 0) return false;
        // Uri 会给 http/https 补默认端口，需按用户原始输入判断是否写了端口
        if (uri.IsDefaultPort)
        {
            var afterAuth = uri.OriginalString[(uri.OriginalString.IndexOf("://", StringComparison.Ordinal) + 3)..];
            var at = afterAuth.LastIndexOf('@');
            var hostPart = at >= 0 ? afterAuth[(at + 1)..] : afterAuth;
            var slash = hostPart.IndexOfAny(new[] { '/', '?', '#' });
            if (slash >= 0) hostPart = hostPart[..slash];
            if (!hostPart.Contains(':')) return false;
        }
        return true;
    }

    private static void Fail(string msg)
        => MessageBox.Show(msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
