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

            // 手动创建主窗口(不通过 StartupUri)。
            var mainWindow = new MainWindow();

            // 预热窗口一次,让托盘右键菜单"启动即用":WPF ContextMenu 在从未显示/激活的 Hidden
            // 窗口上 Popup 不渲染。之前的做法是直接 Show()+Hide(),启动瞬间窗口一闪而过,且在
            // 首次启动(欢迎窗口关闭后)会留下一个白色空窗口。改为:透明度 0 地 Show+Hide,
            // 窗口从未真正可见,但呈现源/布局已初始化,菜单可正常渲染。
            mainWindow.Opacity = 0;
            mainWindow.Show();
            mainWindow.Hide();
            mainWindow.Opacity = 1;

            // 首次启动:欢迎窗口已在构造函数里弹过并关闭,此时再显示设置窗口(内容已初始化,
            // 不会显示成白色空窗口)。
            if (mainWindow.isFirstLaunch)
            {
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 兜底:任何退出路径都恢复任务栏(OnClosing 已做,此处幂等补漏,覆盖可能漏掉的退出路径)。
            try
            {
                foreach (Window w in Windows)
                {
                    if (w is MainWindow mw)
                    {
                        foreach (var tb in mw.taskbarDetails)
                        {
                            Taskbar.ResetTaskbar(tb, mw.activeSettings);
                        }
                        break;
                    }
                }
            }
            catch
            {
            }
            base.OnExit(e);
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
