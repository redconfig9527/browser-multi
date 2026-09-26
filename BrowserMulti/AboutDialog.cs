using System.Diagnostics;
using System.Reflection;

namespace BrowserMulti;

public class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = "关于";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 400);

        var iconBox = new PictureBox
        {
            Image = SystemIcons.Application.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Bounds = new Rectangle(24, 22, 48, 48)
        };
        try
        {
            var ico = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "");
            if (ico != null) iconBox.Image = ico.ToBitmap();
        }
        catch { }
        Controls.Add(iconBox);

        Controls.Add(new Label
        {
            Text = AppMeta.Name,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(88, 24)
        });

        Controls.Add(new Label
        {
            Text = $"版本 {AppMeta.Version}  ·  {AppMeta.EnglishName}",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(90, 52)
        });

        var links = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Location = new Point(24, 90),
            Width = 412
        };

        links.Controls.Add(MakeLabel("作者", AppMeta.Author));
        links.Controls.Add(MakeLabel("许可证", "MIT License"));

        var repoLink = new LinkLabel
        {
            Text = "项目主页：" + AppMeta.RepoUrl,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };
        repoLink.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(AppMeta.RepoUrl) { UseShellExecute = true }); }
            catch { }
        };
        links.Controls.Add(repoLink);
        Controls.Add(links);

        var info = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(250, 250, 250),
            Bounds = new Rectangle(24, 172, 412, 160),
            Font = new Font("Microsoft YaHei UI", 8.5F),
            Text = BuildInfoText()
        };
        Controls.Add(info);

        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK };
        btnOk.SetBounds(356, 348, 80, 30);
        Controls.Add(btnOk);
        AcceptButton = btnOk;

        Controls.Add(new Label
        {
            Text = AppMeta.Copyright,
            AutoSize = true,
            ForeColor = Color.Gray,
            Location = new Point(24, 356)
        });
    }

    private static Label MakeLabel(string key, string value) => new()
    {
        Text = $"{key}：{value}",
        AutoSize = true,
        Margin = new Padding(0, 2, 0, 0)
    };

    private static string BuildInfoText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("功能概览");
        sb.AppendLine("  · 基于独立用户数据目录的浏览器多开，Cookie/登录态隔离且持久");
        sb.AppendLine("  · UserAgent 伪装与内核级指纹防护（Canvas/WebGL/字体/时区/WebRTC）");
        sb.AppendLine("  · 每实例独立指纹种子，跨会话稳定、实例间互不相同");
        sb.AppendLine();
        sb.AppendLine("运行环境");
        sb.AppendLine($"  · .NET {Environment.Version}");
        sb.AppendLine($"  · {Environment.OSVersion}");
        sb.AppendLine($"  · {(Environment.Is64BitProcess ? "64 位" : "32 位")}进程");
        sb.AppendLine($"  · 程序目录 {AppContext.BaseDirectory}");
        sb.AppendLine();
        sb.AppendLine("重要说明");
        sb.AppendLine("  · 指纹防护需使用 Microsoft Edge");
        sb.AppendLine("    （Chrome 137+ 已移除 --load-extension 参数）");
        sb.AppendLine("  · 本工具不含 IP 隔离，代理留空即为直连");
        sb.AppendLine("  · 浏览器本体为系统已安装的 Edge/Chrome，本程序不含内核");
        sb.AppendLine();
        sb.AppendLine("开源致谢");
        sb.AppendLine("  · .NET / Windows Forms（MIT，Microsoft）");
        sb.AppendLine("  · System.Management（MIT，Microsoft）");
        sb.AppendLine();
        sb.AppendLine("请遵守各网站服务条款，本工具仅用于合法的多账号管理场景。");
        return sb.ToString();
    }
}
