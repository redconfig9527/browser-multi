namespace BrowserMulti;

public class InstanceDialog : Form
{
    private readonly TextBox _txtName = new();
    private readonly TextBox _txtHome = new();
    private readonly TextBox _txtUa = new();
    private readonly ComboBox _comboBrowser = new();
    private readonly ComboBox _comboUa = new();
    private readonly CheckBox _chkFp = new();

    public string InstanceName => _txtName.Text.Trim();
    public string BrowserId => (_comboBrowser.SelectedItem as BrowserChoice)?.Id ?? "auto";
    public string HomePage => _txtHome.Text.Trim();
    public string UserAgent { get; private set; } = "";
    public bool FpEnabled => _chkFp.Checked;

    public InstanceDialog(string title, string defaultName, string defaultBrowser,
        string defaultHome, string defaultUa, bool defaultFp, List<BrowserChoice> choices)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(500, 330);
        ShowInTaskbar = false;

        var lblName = new Label { Text = "实例名称:", AutoSize = true, Location = new Point(16, 22) };
        _txtName.SetBounds(122, 18, 358, 25);

        var lblBrowser = new Label { Text = "浏览器:", AutoSize = true, Location = new Point(16, 57) };
        _comboBrowser.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboBrowser.SetBounds(122, 53, 358, 25);
        foreach (var c in choices) _comboBrowser.Items.Add(c);
        var idx = choices.FindIndex(c => c.Id == defaultBrowser);
        _comboBrowser.SelectedIndex = idx < 0 ? 0 : idx;

        var lblHome = new Label { Text = "启动页:", AutoSize = true, Location = new Point(16, 92) };
        _txtHome.SetBounds(122, 88, 358, 25);
        _txtHome.PlaceholderText = "https://www.baidu.com（可留空）";

        var lblUa = new Label { Text = "UserAgent:", AutoSize = true, Location = new Point(16, 127) };
        _comboUa.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboUa.SetBounds(122, 123, 358, 25);
        foreach (var p in UaPresets.All) _comboUa.Items.Add(p.Name);

        int presetIdx = UaPresets.All.ToList().FindIndex(p => p.Value == defaultUa);
        bool isCustom = presetIdx < 0 && !string.IsNullOrWhiteSpace(defaultUa);
        _comboUa.SelectedIndex = isCustom
            ? UaPresets.All.Length - 1
            : Math.Max(0, presetIdx);

        _txtUa.SetBounds(122, 156, 358, 25);
        _txtUa.PlaceholderText = "输入完整 User-Agent 字符串";
        _txtUa.Enabled = false;
        _txtUa.Text = defaultUa;
        _comboUa.SelectedIndexChanged += (_, _) =>
        {
            bool custom = ((_comboUa.SelectedItem as string) == "自定义…");
            _txtUa.Enabled = custom;
            if (!custom) _txtUa.Text = "";
        };
        if (isCustom) _txtUa.Enabled = true;

        var tip = new ToolTip();
        tip.SetToolTip(_comboUa, "控制请求头 User-Agent 和 JS navigator.userAgent");

        _chkFp.Text = "启用指纹防护（仅 Edge 有效）";
        _chkFp.AutoSize = true;
        _chkFp.SetBounds(122, 188, 200, 24);
        _chkFp.Checked = defaultFp;

        var lblFpNote = new Label
        {
            Text = "覆盖 Canvas、WebGL、Audio、字体、设备信息、时区/语言、WebRTC 内网 IP；\n" +
                   "Chrome 137+ 已移除扩展加载参数，用 Chrome 启动时防护不生效。",
            AutoSize = true,
            ForeColor = Color.FromArgb(160, 90, 0),
            Location = new Point(122, 212)
        };
        tip.SetToolTip(_chkFp, "为每个实例生成稳定且互不相同的指纹\n覆盖：Canvas、WebGL 渲染器、AudioContext、字体枚举、\nCPU/内存/触点、语言时区、WebRTC 内网 IP、移动端屏幕尺寸\nWebRTC 为 JS 层拦截，请勿关闭\"停用非代理 UDP\"");

        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK };
        btnOk.SetBounds(312, 276, 80, 30);
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        btnCancel.SetBounds(400, 276, 80, 30);

        Controls.AddRange(new Control[]
        {
            lblName, _txtName, lblBrowser, _comboBrowser,
            lblHome, _txtHome, lblUa, _comboUa, _txtUa,
            _chkFp, lblFpNote, btnOk, btnCancel
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

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
            MessageBox.Show("请输入实例名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
            return;
        }

        UserAgent = (_comboUa.SelectedItem as string) == "自定义…"
            ? _txtUa.Text.Trim()
            : UaPresets.All[_comboUa.SelectedIndex].Value;

        if ((_comboUa.SelectedItem as string) == "自定义…" && UserAgent.Length == 0)
        {
            MessageBox.Show("选择了自定义 UserAgent，请输入完整字符串。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
        }
    }
}
