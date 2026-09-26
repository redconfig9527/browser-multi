namespace BrowserMulti;

public class SettingsDialog : Form
{
    private readonly ComboBox _comboBrowser = new();
    private readonly CheckBox _chkTray = new();
    private readonly CheckBox _chkConfirmDup = new();
    private readonly CheckBox _chkLog = new();

    public string DefaultBrowserId => (_comboBrowser.SelectedItem as BrowserChoice)?.Id ?? "auto";
    public bool MinimizeToTray => _chkTray.Checked;
    public bool ConfirmDuplicateLaunch => _chkConfirmDup.Checked;
    public bool LogEnabled => _chkLog.Checked;

    public SettingsDialog(AppSettings current, List<BrowserChoice> choices)
    {
        Text = "设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 300);

        int y = 22;
        Controls.Add(new Label { Text = "默认浏览器:", AutoSize = true, Location = new Point(20, y + 3) });
        _comboBrowser.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboBrowser.SetBounds(150, y - 2, 288, 25);
        foreach (var c in choices) _comboBrowser.Items.Add(c);
        // 防御：choices 为空时必须至少有一项，否则设置 SelectedIndex 会抛 ArgumentOutOfRangeException
        if (_comboBrowser.Items.Count == 0)
            _comboBrowser.Items.Add(new BrowserChoice("auto", "自动（默认浏览器）"));
        var idx = choices.FindIndex(c => c.Id == current.DefaultBrowserId);
        _comboBrowser.SelectedIndex = idx < 0 ? 0 : idx;
        Controls.Add(_comboBrowser);

        y += 42;
        _chkTray.Text = "关闭窗口时最小化到托盘（不退出程序）";
        _chkTray.AutoSize = true;
        _chkTray.Location = new Point(150, y);
        _chkTray.Checked = current.MinimizeToTray;
        Controls.Add(_chkTray);

        y += 30;
        _chkConfirmDup.Text = "重复启动同一实例时弹窗确认";
        _chkConfirmDup.AutoSize = true;
        _chkConfirmDup.Location = new Point(150, y);
        _chkConfirmDup.Checked = current.ConfirmDuplicateLaunch;
        Controls.Add(_chkConfirmDup);

        y += 30;
        _chkLog.Text = "记录操作日志（logs 目录，保留 30 天）";
        _chkLog.AutoSize = true;
        _chkLog.Location = new Point(150, y);
        _chkLog.Checked = current.LogEnabled;
        Controls.Add(_chkLog);

        y += 42;
        var btnOpenLog = new Button { Text = "打开日志目录", AutoSize = true };
        btnOpenLog.Location = new Point(150, y);
        btnOpenLog.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Log.LogDirectory);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "explorer.exe", $"\"{Log.LogDirectory}\"") { UseShellExecute = true });
            }
            catch { }
        };
        Controls.Add(btnOpenLog);

        var btnOk = new Button { Text = "保存", DialogResult = DialogResult.OK };
        btnOk.SetBounds(272, 252, 80, 30);
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
        btnCancel.SetBounds(360, 252, 80, 30);
        Controls.AddRange(new Control[] { btnOk, btnCancel });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
