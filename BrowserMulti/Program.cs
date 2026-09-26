namespace BrowserMulti;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 全局异常兜底：避免任何未处理异常导致程序直接消失
        Application.ThreadException += (_, e) => HandleFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleFatal(e.ExceptionObject as Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void HandleFatal(Exception? ex)
    {
        if (ex == null) return;
        Log.Error("未处理异常", ex);
        try
        {
            MessageBox.Show(
                $"程序遇到一个错误：\n\n{ex.Message}\n\n详细信息已写入 logs 目录。\n程序会尽量继续运行。",
                AppMeta.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { /* 连弹窗都失败时静默 */ }
    }
}
