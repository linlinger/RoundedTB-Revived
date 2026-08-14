using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RoundedTB
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局未处理异常写日志:便于诊断偶发闪退(如 Win11 26H1 上可能伴随 Explorer 崩溃的闪退)。
            DispatcherUnhandledException += (s, ex) => LogGlobalException("DispatcherUnhandledException", ex.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogGlobalException("AppDomain.UnhandledException", ex.ExceptionObject as Exception ?? new Exception(ex.ExceptionObject?.ToString()));
            TaskScheduler.UnobservedTaskException += (s, ex) => { LogGlobalException("UnobservedTaskException", ex.Exception); ex.SetObserved(); };

            // 单实例:任务管理器里只允许一个 RoundedTB。已有实例则通知它显示设置窗口,
            // 本实例直接退出。
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, @"RoundedTBRevived_SingleInstance", out createdNew);
            if (!createdNew)
            {
                NotifyExistingInstance();
                Shutdown();
                return;
            }

            // 必须在任何窗口创建前加载语言,这样 XAML 里的 {l:Loc ...} 才能取到对应文本。
            Localization.Init();

            // 语言文件存在但格式不合法:用内置双语文案提示(不依赖可能损坏的翻译文件),已回退英文。
            if (Localization.HasLanguageError)
            {
                string message =
                    "Language file is invalid: " + Localization.ErrorFile +
                    "\nFalling back to English.\n\n" +
                    "语言文件错误:" + Localization.ErrorFile + "\n已回退到英文。";
                MessageBox.Show(message, "RoundedTB Revived", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            WPFUI.Theme.Watcher.Start();

            // 手动创建主窗口(不通过 StartupUri),避免启动瞬间闪过一个窗口;
            // MainWindow 在 OnSourceInitialized 里会自动隐藏到托盘。
            new MainWindow();
        }

        /// <summary>全局未处理异常写入 rtb.log(不依赖 mw 实例,程序早期也可用)。</summary>
        private static void LogGlobalException(string source, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] GLOBAL {source}: {ex}\n");
            }
            catch
            {
                // 日志失败不影响程序
            }
        }

        /// <summary>通过窗口标题技巧让已有实例显示设置窗口(与 MainWindow 的单实例逻辑一致)。</summary>
        private void NotifyExistingInstance()
        {
            try
            {
                List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                foreach (IntPtr hwnd in windowList)
                {
                    System.Text.StringBuilder windowClass = new System.Text.StringBuilder(1024);
                    System.Text.StringBuilder windowTitle = new System.Text.StringBuilder(1024);
                    LocalPInvoke.GetClassName(hwnd, windowClass, 1024);
                    LocalPInvoke.GetWindowText(hwnd, windowTitle, 1024);
                    if (windowClass.ToString().Contains("HwndWrapper[RoundedTB.exe") && windowTitle.ToString() == "RoundedTB")
                    {
                        LocalPInvoke.SetWindowText(hwnd, "RoundedTB_SettingsRequest");
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // 通知失败不影响(已有实例仍在运行)
            }
        }
    }
}
