using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Interop;
using DesktopBridge;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using System.Diagnostics;
using Microsoft.Win32;
using System.Text;
using WPFUI;
using System.Windows.Forms;
using System.Windows.Media;

namespace RoundedTB
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    ///
    /// Maintained by the RoundedTB Revived project.
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool isWindows11;
        public List<Types.Taskbar> taskbarDetails = new List<Types.Taskbar>();
        public bool shouldReallyDieNoReally = false;
        public string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb.json");
        public string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb.log");
        public Types.Settings activeSettings = new Types.Settings();
        public BackgroundWorker taskbarThread = new BackgroundWorker();
        public IntPtr hwndDesktopButton = IntPtr.Zero;
        public int lastDynDistance = 0;
        public int numberToForceRefresh = 0;
        public bool isCentred = false;
        public bool isAlreadyRunning = false;
        public Background background;
        public Interaction interaction;
        private HwndSource source;
        public int selectedSegment = 0; // 0 = Simple, 1 = AppList, 2 = Tray, 3 = Widgets
        public int version = ChannelInfo.Version; // 由构建通道决定(Master=R4/3, Canary/Dev=-1)
        // 上次托盘图标用的主题(null=未初始化)。初始必须为 null:若初始为 false,暗色主题(light=false)
        // 的首次 TrayIconCheck 会被当成"主题没变"而 return,导致托盘图标从不创建。
        private bool? _lastTrayLight = null;
        private TrayIcon _trayIcon;              // 自实现 Shell_NotifyIcon 托盘图标
        private System.Drawing.Icon _trayIconImage; // 当前托盘图标句柄持有者(避免句柄被 GC 回收)
        // 托盘右键菜单里的控件(ContextMenu 在 Window.Resources,Resources 里的 x:Name 不生成字段,
        // 需在构造里通过 FindName 提取)。
        private bool _isRestoringUi = false; // 恢复配置到 UI 控件时置位,避免触发 Checked 事件副作用(如弹 TTB 兼容窗口)
        private System.Windows.Threading.DispatcherTimer _trayMenuWatchTimer; // 托盘菜单外点击自动关闭的鼠标监控
        private System.Windows.Controls.MenuItem StartupCheckBox;
        private System.Windows.Controls.MenuItem ShowMenuItem;
        private System.Windows.Controls.MenuItem ResetDefaultsMenuItem;
        private System.Windows.Controls.MenuItem ExitMenuItem;
        // Restart bookkeeping for the taskbar worker. An unhandled exception used to end the
        // loop silently, leaving the app running but doing nothing at all. (移植自 gniang Phase 1)
        private const int MaxWorkerRestarts = 5;
        private static readonly TimeSpan WorkerRestartWindow = TimeSpan.FromMinutes(1);
        private int workerRestartCount = 0;
        private DateTime workerRestartWindowStart = DateTime.MinValue;
        private object workerArguments = null;
        /// <summary>
        /// Versions:
        /// -1: Canary
        ///  0: R3.0
        ///  1: P3.1B
        ///  2: R3.1
        ///  3: R4
        /// </summary>

        public MainWindow()
        {
            WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, this);

            InitializeComponent();

            // 提取托盘右键菜单里的控件(Resources 里的 x:Name 不生成字段,且 FindName 在
            // ResourceDictionary 里不可靠,直接用 Items 顺序取:Startup/Show/Reset/Exit)。
            var trayContextMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayContextMenu");
            if (trayContextMenu != null)
            {
                StartupCheckBox = trayContextMenu.Items[0] as System.Windows.Controls.MenuItem;
                ShowMenuItem = trayContextMenu.Items[1] as System.Windows.Controls.MenuItem;
                ResetDefaultsMenuItem = trayContextMenu.Items[2] as System.Windows.Controls.MenuItem;
                ExitMenuItem = trayContextMenu.Items[3] as System.Windows.Controls.MenuItem;
            }

            // 按构建通道设置标题栏图标(Icon 需要 ImageSource;x:Static 返回 string 会抛异常,
            // 因此用代码设置 BitmapImage)
            mainTitleBar.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(ChannelInfo.IconUri));

            // 右上角关闭按钮 = 只隐藏设置窗口,程序继续在托盘运行(对齐 R3.1 / ModernWpf 行为)。
            // WPFUI 的 TitleBar 在 ApplicationNavigation 模式下,关闭按钮默认会直接 Application.Shutdown(),
            // 必须用 CloseActionOverride 接管。注意:它传的是内部 _parent 字段(懒赋值,可能为 null),需防御。
            mainTitleBar.CloseActionOverride = (tb, win) =>
            {
                (win ?? Window.GetWindow(tb) ?? this).Hide();
            };


            // Check OS build, as behaviours rather-annoyingly differ between Windows 11 and Windows 10
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var buildNumber = registryKey.GetValue("CurrentBuild").ToString();
            if (Convert.ToInt32(buildNumber) >= 21996)
            {
                isWindows11 = true;
            }
            else
            {
                isWindows11 = false;
                activeSettings.IsWindows11 = false;
                dynamicCheckBox.Content = Localization.Get("Menu_SplitMode");
                fillAltTabCheckBox.Content = Localization.Get("Menu_FillAltTabUnavailable");
            }

            // Initialise functions
            background = new Background(this);
            interaction = new Interaction(this);

            // 自实现托盘图标 + Win+F2 热键:直接在构造里初始化,不依赖 SourceInitialized/
            // OnSourceInitialized——Hidden 窗口从不触发该事件,这是托盘图标"死活看不到"的根因。
            // EnsureHandle() 强制创建窗口 HWND(Hidden 窗口下 .Handle 可能返回 0)。
            IntPtr trayHandle = new WindowInteropHelper(this).EnsureHandle();
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb-tray.log"), $"[{DateTime.Now:HH:mm:ss.fff}] TRAY-init-begin handle=0x{trayHandle.ToInt64():X}\n"); } catch { }
            source = HwndSource.FromHwnd(trayHandle);
            source.AddHook(interaction.HwndHook);
            _trayIcon = new TrayIcon(trayHandle);
            source.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (_trayIcon != null && _trayIcon.HandleWindowMessage(h, msg, w, l))
                {
                    handled = true;
                }
                return IntPtr.Zero;
            });
            _trayIcon.LeftClick += () => Dispatcher.Invoke(() => ShowMenuItem_Click(null, null));
            _trayIcon.RightClick += () => Dispatcher.Invoke(ShowTrayMenu);
            _trayIcon.Show();
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb-tray.log"), $"[{DateTime.Now:HH:mm:ss.fff}] TRAY-Show-called\n"); } catch { }
            TrayIconCheck(); // 首次设置托盘图标(按主题)
            LocalPInvoke.RegisterHotKey(trayHandle, 9000, 0x8, 0x71);
            Visibility = Visibility.Hidden;
            Opacity = 1;

            // Check if RoundedTB is already running, and if it is, do nothing.
            Process[] matchingProcesses = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
            
            if (matchingProcesses.Length > 1)
            {
                List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                foreach (IntPtr hwnd in windowList)
                {
                    StringBuilder windowClass = new StringBuilder(1024);
                    StringBuilder windowTitle = new StringBuilder(1024);
                    try
                    {
                        LocalPInvoke.GetClassName(hwnd, windowClass, 1024);
                        LocalPInvoke.GetWindowText(hwnd, windowTitle, 1024);

                        if (windowClass.ToString().Contains("HwndWrapper[RoundedTB.exe") && windowTitle.ToString() == "RoundedTB")
                        {
                            LocalPInvoke.SetWindowText(hwnd, "RoundedTB_SettingsRequest");
                        }
                    }
                    catch (Exception) { }
                }
                shouldReallyDieNoReally = true;
                isAlreadyRunning = true;
                Close();
                return;
            }
            // 托盘图标在窗口加载后(而非构造时)设置,避免 TitleBar 尚未创建 TaskbarIcon 时
            // ResetIcon 造成双重创建/闪烁。
            Loaded += (s, e) => TrayIconCheck();

            if (IsRunningAsUWP())
            {
                #pragma warning disable CS4014
                StartupInit(true);
                configPath = Path.Combine(Windows.Storage.ApplicationData.Current.RoamingFolder.Path, "rtb.json");
                logPath = Path.Combine(Windows.Storage.ApplicationData.Current.RoamingFolder.Path, "rtb.log");
            }

            // 任务管理器"启动"页显示快捷方式文件名,故用 "RoundedTB Revived.lnk";兼容旧版 "RoundedTB.lnk"
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if ((System.IO.File.Exists(Path.Combine(startupFolder, "RoundedTB Revived.lnk")) || System.IO.File.Exists(Path.Combine(startupFolder, "RoundedTB.lnk"))) && !IsRunningAsUWP())
            {
                StartupCheckBox.IsChecked = true;
                ShowMenuItem.Header = Localization.Get("Menu_Show");
            }
            taskbarThread.WorkerSupportsCancellation = true;
            taskbarThread.WorkerReportsProgress = true;
            taskbarThread.DoWork +=background.DoWork;
            taskbarThread.RunWorkerCompleted += TaskbarThread_RunWorkerCompleted;

            // Load settings into memory/UI
            interaction.FileSystem();
            if (!IsRunningAsUWP())
            {
                interaction.AddLog($"RoundedTB started!");
            }
            else
            {
                interaction.AddLog($"RoundedTB started in UWP mode!");
            }
            activeSettings = interaction.ReadJSON();

            if (isWindows11)
            {
                activeSettings.IsWindows11 = true;
            }
            else
            {
                activeSettings.IsWindows11 = false;
            }
            // Default settings
            if (activeSettings == null)
            {
                
                if (isWindows11) // Default settings for Windows 11
                {
                    activeSettings = new Types.Settings()
                    {
                        SimpleTaskbarLayout = new Types.SegmentSettings{ CornerRadius = 20, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 20, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 20, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 20, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        IsDynamic = false,
                        IsCentred = false,
                        IsWindows11 = true,
                        ShowTray = false,
                        ShowWidgets = false,
                        CompositionCompat = false,
                        IsNotFirstLaunch = false,
                        FillOnMaximise = true,
                        FillOnTaskSwitch = true,
                        ShowSegmentsOnHover = false,
                        AutoHide = 0
                    };
                }
                else // Default settings for Windows 10
                {
                    activeSettings = new Types.Settings()
                    {
                        SimpleTaskbarLayout = new Types.SegmentSettings { CornerRadius = 16, MarginLeft = 2, MarginTop = 2, MarginRight = 2, MarginBottom = 2 },
                        DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 16, MarginLeft = 2, MarginTop = 2, MarginRight = 2, MarginBottom = 2 },
                        DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 16, MarginLeft = 2, MarginTop = 2, MarginRight = 2, MarginBottom = 2 },
                        DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 16, MarginLeft = 2, MarginTop = 2, MarginRight = 2, MarginBottom = 2 },
                        IsDynamic = false,
                        IsCentred = false,
                        IsWindows11 = false,
                        ShowTray = false,
                        ShowWidgets = false,
                        CompositionCompat = false,
                        IsNotFirstLaunch = false,
                        FillOnMaximise = true,
                        FillOnTaskSwitch = false,
                        ShowSegmentsOnHover = false,
                        AutoHide = 0
                    };
                }
            }

            // Older config files were saved with a different settings schema (no per-segment
            // layouts). Default any missing segment layout so the app still applies cleanly
            // when a user upgrades from an older build.
            if (activeSettings.SimpleTaskbarLayout == null) activeSettings.SimpleTaskbarLayout = new Types.SegmentSettings { CornerRadius = 20, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicAppListLayout == null) activeSettings.DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 20, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicTrayLayout == null) activeSettings.DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 20, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicWidgetsLayout == null) activeSettings.DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 20, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };

            if (version != activeSettings.Version && version != -1)
            {
                activeSettings.IsNotFirstLaunch = false;
            }
            activeSettings.Version = version;


            interaction.AddLog($"Settings loaded:");
            interaction.AddLog(
                $"SimpleTaskbarLayout: {activeSettings.SimpleTaskbarLayout}\n" +
                $"DynamicAppListLayout: {activeSettings.DynamicAppListLayout}\n" +
                $"DynamicTrayLayout: {activeSettings.DynamicTrayLayout}\n" +
                $"DynamicWidgetsLayout: {activeSettings.DynamicWidgetsLayout}\n" +
                $"IsDynamic: {activeSettings.IsDynamic}\n" +
                $"IsCentred: {activeSettings.IsCentred}\n" +
                $"ShowTray: {activeSettings.ShowTray}\n" +
                $"ShowWidgets: {activeSettings.ShowWidgets}\n" +
                $"CompositionCompat: {activeSettings.CompositionCompat}\n" +
                $"IsNotFirstLaunch: {activeSettings.IsNotFirstLaunch}\n" +
                $"FillOnMaximise: {activeSettings.FillOnMaximise}\n" +
                $"FillOnTaskSwitch: {activeSettings.FillOnTaskSwitch}\n" +
                $"ShowTrayOnHover: {activeSettings.ShowSegmentsOnHover}\n"
                );

            // Checks if advanced margins are configured
            if (activeSettings.IsDynamic)
            {
                cornerRadiusInput.Text = activeSettings.DynamicAppListLayout.CornerRadius.ToString();
                cornerRadiusSlider.Value = activeSettings.DynamicAppListLayout.CornerRadius;
                mTopInput.Text = activeSettings.DynamicAppListLayout.MarginTop.ToString();
                mLeftInput.Text = activeSettings.DynamicAppListLayout.MarginLeft.ToString();
                mBottomInput.Text = activeSettings.DynamicAppListLayout.MarginBottom.ToString();
                mRightInput.Text = activeSettings.DynamicAppListLayout.MarginRight.ToString();

                selectedSegment = 1;
            }
            else
            {
                cornerRadiusInput.Text = activeSettings.SimpleTaskbarLayout.CornerRadius.ToString();
                cornerRadiusSlider.Value = activeSettings.SimpleTaskbarLayout.CornerRadius;
                mTopInput.Text = activeSettings.SimpleTaskbarLayout.MarginTop.ToString();
                mLeftInput.Text = activeSettings.SimpleTaskbarLayout.MarginLeft.ToString();
                mBottomInput.Text = activeSettings.SimpleTaskbarLayout.MarginBottom.ToString();
                mRightInput.Text = activeSettings.SimpleTaskbarLayout.MarginRight.ToString();

                selectedSegment = 0;
            }

            // Get whether or not the taskbar is centred.
            // (The "TaskbarAl" registry value may be absent on some Windows 11 builds/images;
            // CheckIfCentred falls back to the OS default - centred on Win11 - in that case.)
            isCentred = Taskbar.CheckIfCentred();
            interaction.AddLog($"Taskbar centred? {isCentred}");
            if (!isWindows11)
            {
                activeSettings.IsCentred = false;
            }

            // Copy and apply settings to UI
            dynamicCheckBox.IsChecked = activeSettings.IsDynamic;
            centredCheckBox.IsChecked = activeSettings.IsCentred;
            showTrayCheckBox.IsChecked = activeSettings.ShowTray;
            showWidgetsCheckBox.IsChecked = activeSettings.ShowWidgets;
            fillMaximisedCheckBox.IsChecked = activeSettings.FillOnMaximise;
            fillAltTabCheckBox.IsChecked = activeSettings.FillOnTaskSwitch;
            showSegmentsOnHoverCheckBox.IsChecked = activeSettings.ShowSegmentsOnHover;
            // 恢复配置到 UI 时避免触发 Checked 事件(会弹 TranslucentTB 兼容说明窗口)。
            _isRestoringUi = true;
            compositionFixCheckBox.IsChecked = activeSettings.CompositionCompat;
            _isRestoringUi = false;
            // 老配置可能存 AutoHide=2(原版第三项占位,从未实现),现在下拉只剩两项,
            // 归一为 1 避免 SelectedIndex 越界。
            if (activeSettings.AutoHide > 1)
            {
                activeSettings.AutoHide = 1;
            }
            autoHideComboBox.SelectedIndex = activeSettings.AutoHide;
            taskbarDetails = Taskbar.GenerateTaskbarInfo();
            // 启动时先恢复上次可能残留的任务栏(上次被任务管理器强制结束/异常退出未清理),
            // 再应用新的 region,避免重启后任务栏仍被旧 region 裁剪。
            foreach (Types.Taskbar tb in taskbarDetails)
            {
                Taskbar.ResetTaskbar(tb, activeSettings);
            }

            ApplyButton_Click(null, null);


            if (!activeSettings.FillOnMaximise)
            {
                activeSettings.FillOnTaskSwitch = false;
                fillAltTabCheckBox.IsEnabled = false;
            }

            //Showhide the split mode help button
            if (!isWindows11 && activeSettings.IsDynamic)
            {
                splitHelpButton.Visibility = Visibility.Visible;
            }
            else
            {
                splitHelpButton.Visibility = Visibility.Hidden;
            }

            if (activeSettings.IsNotFirstLaunch != true)
            {
                activeSettings.IsNotFirstLaunch = true;
                AboutWindow aw = new AboutWindow();
                aw.expander0.IsExpanded = true;
                aw.ShowDialog();
                try
                {
                    Visibility = Visibility.Visible;
                }
                catch (InvalidOperationException)
                {

                }
                ShowMenuItem.Header = Localization.Get("Menu_Hide");
            }

            AutoHide(true, taskbarDetails);

            UpdateUi();

        }

        public void UpdateUi()
        {
            if (!activeSettings.ShowTray || activeSettings.ShowSegmentsOnHover)
            {
                trayRectStandIn.Opacity = 0.5;
            }
            else
            {
                trayRectStandIn.Opacity = 1;
            }

            if (!activeSettings.ShowWidgets || activeSettings.ShowSegmentsOnHover)
            {
                widgetsRectStandIn.Opacity = 0.5;
            }
            else
            {
                widgetsRectStandIn.Opacity = 1;
            }

            if (activeSettings.IsCentred && activeSettings.IsWindows11 && activeSettings.IsDynamic)
            {
                taskbarRectStandIn.Margin = new Thickness(126, 0, 126, 5);
                trayRectStandIn.Visibility = Visibility.Visible;
                widgetsRectStandIn.Visibility = Visibility.Visible;
            }
            else if (activeSettings.IsDynamic)
            {
                taskbarRectStandIn.Margin = new Thickness(5, 0, 247, 5);
                trayRectStandIn.Visibility = Visibility.Visible;
                widgetsRectStandIn.Visibility = Visibility.Hidden;
            }
            else
            {
                taskbarRectStandIn.Margin = new Thickness(5, 210, 5, 5);
                trayRectStandIn.Visibility = Visibility.Hidden;
                widgetsRectStandIn.Visibility = Visibility.Hidden;

            }
        }

        public void AutoHide(bool enabled, List<Types.Taskbar> taskbarDetails)
        {
            // "自动隐藏任务栏"设置:只有配置开启了自动隐藏(AutoHide>0)才设置 autohide;
            // 否则始终确保任务栏显示(取消可能残留的 autohide 状态)。
            // 注意:MainWindow 构造末尾会无条件调用 AutoHide(true),所以绝不能在这里
            // 对"配置未开启自动隐藏"的情况也设置 autohide,否则启动时任务栏会被自动收起。
            try
            {
                foreach (Types.Taskbar taskbar in taskbarDetails)
                {
                    if (activeSettings.AutoHide > 0)
                    {
                        // 启用:设 autohide;取消(退出时):清空 autohide/always-on-top。
                        // ABM_SETSTATE 的 lParam 是"要设置的 ABS 标志",设 0 表示恢复正常显示。
                        Taskbar.SetTaskbarState(enabled ? LocalPInvoke.AppBarStates.AutoHide : (LocalPInvoke.AppBarStates)0, taskbar.TaskbarHwnd);
                    }
                    else
                    {
                        // 配置没开自动隐藏:始终确保任务栏显示。
                        Taskbar.SetTaskbarState((LocalPInvoke.AppBarStates)0, taskbar.TaskbarHwnd);
                    }
                }
            }
            catch (Exception aaaa)
            {
                interaction.AddLog(aaaa.Message);
            }
        }

        public void TrayIconCheck()
        {
            try
            {
                // 图标文件:traylight.ico 实际是黑色图标(亮色任务栏可见),traydark.ico 实际是白色图标(暗色任务栏可见)。
                // 对齐原版 R3.1 行为:亮色主题 → 黑图标,暗色主题 → 白图标。
                // 判断依据用注册表 AppsUseLightTheme(1=亮/0=暗),因为 WPFUI 的
                // Theme.Manager.GetSystemTheme() 实测与系统相反(暗色系统返回 Light),不可靠。
                bool light = false;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object val = key?.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            light = Convert.ToInt32(val) == 1;
                        }
                    }
                }
                catch (Exception)
                {
                    // 读不到注册表时按暗色处理(保持默认白色图标)。
                }

                // 主题没变就不刷新,避免 Background 每秒调用时反复重建托盘图标(会闪烁)。
                if (_lastTrayLight.HasValue && light == _lastTrayLight.Value)
                {
                    return;
                }
                _lastTrayLight = light;

                if (_trayIcon != null)
                {
                    // 加载主题对应的托盘图标(TrayLight.ico=黑/TrayDark.ico=白)并设置到 Shell_NotifyIcon。
                    // 注意:pack URI 资源匹配大小写敏感,必须与 res/ 下实际文件名一致(此前小写
                    // "traylight.ico" 找不到资源,GetResourceStream 抛异常被吞,图标从不加载)。
                    Uri iconUri = light
                        ? new Uri("pack://application:,,,/res/TrayLight.ico")
                        : new Uri("pack://application:,,,/res/TrayDark.ico");
                    _trayIconImage?.Dispose();
                    using (System.IO.Stream iconStream = System.Windows.Application.GetResourceStream(iconUri).Stream)
                    {
                        _trayIconImage = new System.Drawing.Icon(iconStream);
                    }
                    // 必须保留 _trayIconImage 引用,否则图标句柄被 GC 回收后托盘图标消失。
                    _trayIcon.SetIcon(_trayIconImage.Handle, "RoundedTB Revived");
                }
            }
            catch (Exception ex)
            {
                // 设置失败时保持默认图标,不影响主功能;记录以便排查托盘图标不显示。
                try { interaction.AddLog($"TrayIconCheck failed: {ex.Message}"); } catch { }
            }
        }


        public void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            int mt = 0;
            int ml = 0;
            int mb = 0;
            int mr = 0;



            {
                if ((!int.TryParse(mTopInput.Text, out mt) && mTopInput.Text != string.Empty)
                || (!int.TryParse(mLeftInput.Text, out ml) && mLeftInput.Text != string.Empty)
                || (!int.TryParse(mBottomInput.Text, out mb) && mBottomInput.Text != string.Empty)
                || (!int.TryParse(mRightInput.Text, out mr) && mRightInput.Text != string.Empty))
                {
                    return;
                }
            }

            activeSettings.AutoHide = autoHideComboBox.SelectedIndex;
            activeSettings.IsDynamic = (bool)dynamicCheckBox.IsChecked;
            activeSettings.IsCentred = Taskbar.CheckIfCentred();
            activeSettings.ShowTray = (bool)showTrayCheckBox.IsChecked;
            activeSettings.ShowWidgets = (bool)showWidgetsCheckBox.IsChecked;
            activeSettings.CompositionCompat = (bool)compositionFixCheckBox.IsChecked;
            activeSettings.FillOnMaximise = (bool)fillMaximisedCheckBox.IsChecked;
            activeSettings.FillOnTaskSwitch = (bool)fillAltTabCheckBox.IsChecked;
            activeSettings.ShowSegmentsOnHover = (bool)showSegmentsOnHoverCheckBox.IsChecked;

            try
            {
                foreach (Types.Taskbar taskbar in taskbarDetails)
                {
                    int isFullTest = taskbar.TrayRect.Left - taskbar.AppListRect.Right;
                    if (!activeSettings.IsDynamic || (isFullTest <= taskbar.ScaleFactor * 25 && isFullTest > 0 && taskbar.TrayRect.Left != 0))
                    {
                        Taskbar.UpdateSimpleTaskbar(taskbar, activeSettings);
                    }
                    else
                    {
                        Taskbar.UpdateDynamicTaskbar(taskbar, activeSettings);
                    }
                }
            }
            catch (InvalidOperationException aaaa)
            {
                interaction.AddLog(aaaa.Message);
            }


            if (taskbarThread.IsBusy == false)
            {
                StartTaskbarWorker((mt, ml, mb, mr, 0));
            }
            else
            {
                taskbarThread.CancelAsync();
                while (taskbarThread.IsBusy == true)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                StartTaskbarWorker((mt, ml, mb, mr, 0));
            }

            if (activeSettings.AutoHide < 1)
            {
                AutoHide(false, taskbarDetails);
            }
            else
            {
                AutoHide(true, taskbarDetails);
            }
            interaction.WriteJSON();
            TrayIconCheck();
            UpdateUi();

        }

        /// <summary>
        /// 还原为默认设置(圆角与 Windows 11 窗口圆角一致,8px),填回输入控件后应用并保存。
        /// 入口:设置界面"还原默认设置"按钮 + 托盘右键菜单项。
        /// </summary>
        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            activeSettings = Interaction.CreateDefaultSettings(isWindows11);
            activeSettings.IsWindows11 = isWindows11;
            activeSettings.Version = version;

            // UpdateUi 只更新预览矩形,这里手动把默认值填回输入控件
            cornerRadiusInput.Text = activeSettings.SimpleTaskbarLayout.CornerRadius.ToString();
            cornerRadiusSlider.Value = activeSettings.SimpleTaskbarLayout.CornerRadius;
            mTopInput.Text = activeSettings.SimpleTaskbarLayout.MarginTop.ToString();
            mLeftInput.Text = activeSettings.SimpleTaskbarLayout.MarginLeft.ToString();
            mBottomInput.Text = activeSettings.SimpleTaskbarLayout.MarginBottom.ToString();
            mRightInput.Text = activeSettings.SimpleTaskbarLayout.MarginRight.ToString();
            autoHideComboBox.SelectedIndex = activeSettings.AutoHide;
            dynamicCheckBox.IsChecked = activeSettings.IsDynamic;
            showTrayCheckBox.IsChecked = activeSettings.ShowTray;
            showWidgetsCheckBox.IsChecked = activeSettings.ShowWidgets;
            compositionFixCheckBox.IsChecked = activeSettings.CompositionCompat;
            fillMaximisedCheckBox.IsChecked = activeSettings.FillOnMaximise;
            fillAltTabCheckBox.IsChecked = activeSettings.FillOnTaskSwitch;
            showSegmentsOnHoverCheckBox.IsChecked = activeSettings.ShowSegmentsOnHover;

            ApplyButton_Click(null, null);
        }

        /// <summary>
        /// 弹出托盘右键菜单:按系统亮暗主题设置菜单颜色;设 PlacementTarget 保证点击菜单外能自动关闭。
        /// </summary>
        private void ShowTrayMenu()
        {
            try
            {
                // 按系统主题(AppsUseLightTheme)切换菜单颜色(亮色/暗色)。
                bool light = false;
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object val = key?.GetValue("AppsUseLightTheme");
                        if (val != null) light = Convert.ToInt32(val) == 1;
                    }
                }
                catch (Exception) { }

                Resources["TrayMenuBgBrush"] = new System.Windows.Media.SolidColorBrush(
                    light ? System.Windows.Media.Color.FromRgb(0xFA, 0xFA, 0xFA) : System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20));
                Resources["TrayMenuBorderBrush"] = new System.Windows.Media.SolidColorBrush(
                    light ? System.Windows.Media.Color.FromRgb(0xD0, 0xD0, 0xD0) : System.Windows.Media.Color.FromRgb(0x3D, 0x3D, 0x3D));
                Resources["TrayMenuFgBrush"] = new System.Windows.Media.SolidColorBrush(
                    light ? System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A) : System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8));
                Resources["TrayMenuHoverBrush"] = new System.Windows.Media.SolidColorBrush(
                    light ? System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0) : System.Windows.Media.Color.FromRgb(0x3D, 0x3D, 0x3D));

                var trayMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayContextMenu");
                trayMenu.PlacementTarget = this;
                trayMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                trayMenu.IsOpen = true;

                // 托盘应用的 WPF ContextMenu 默认点击外部不关闭,启动鼠标监控:
                // 检测到鼠标移出菜单窗口即关闭(行为同系统 TrackPopupMenu / TTB)。
                _trayMenuWatchTimer?.Stop();
                _trayMenuWatchTimer = new System.Windows.Threading.DispatcherTimer();
                _trayMenuWatchTimer.Interval = TimeSpan.FromMilliseconds(100);
                _trayMenuWatchTimer.Tick += (s, ev) =>
                {
                    var menu = (System.Windows.Controls.ContextMenu)FindResource("TrayContextMenu");
                    if (menu == null || !menu.IsOpen)
                    {
                        _trayMenuWatchTimer.Stop();
                        _trayMenuWatchTimer = null;
                        return;
                    }
                    var src = System.Windows.PresentationSource.FromVisual(menu) as System.Windows.Interop.HwndSource;
                    if (src == null)
                    {
                        return;
                    }
                    LocalPInvoke.GetWindowRect(src.Handle, out LocalPInvoke.RECT rect);
                    LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT pt);
                    bool mouseOutside =
                        pt.x < rect.Left || pt.x > rect.Right || pt.y < rect.Top || pt.y > rect.Bottom;
                    if (mouseOutside)
                    {
                        // 仅在菜单外"点击"(左/右键按下)才关闭;鼠标只是移出悬停别处不关闭。
                        bool leftDown = (LocalPInvoke.GetAsyncKeyState(0x01) & 0x8000) != 0;
                        bool rightDown = (LocalPInvoke.GetAsyncKeyState(0x02) & 0x8000) != 0;
                        if (leftDown || rightDown)
                        {
                            menu.IsOpen = false;
                            _trayMenuWatchTimer.Stop();
                            _trayMenuWatchTimer = null;
                        }
                    }
                };
                _trayMenuWatchTimer.Start();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Starts the taskbar worker, remembering its arguments so it can be restarted after a fault.
        /// </summary>
        private void StartTaskbarWorker(object arguments)
        {
            workerArguments = arguments;
            taskbarThread.RunWorkerAsync(arguments);
        }

        /// <summary>
        /// BackgroundWorker swallows exceptions thrown from DoWork and surfaces them here. Without
        /// this handler the loop just stopped and RoundedTB sat there doing nothing, so record the
        /// fault and bring the loop back - with a cap so a permanent fault can't spin forever.
        /// (移植自 gniang Phase 1)
        /// </summary>
        private void TaskbarThread_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled || shouldReallyDieNoReally)
            {
                return;
            }

            if (e.Error != null)
            {
                interaction.AddLog($"Taskbar worker faulted: {e.Error}");
                Debug.WriteLine($"Taskbar worker faulted: {e.Error}");
            }

            DateTime now = DateTime.UtcNow;
            if (now - workerRestartWindowStart > WorkerRestartWindow)
            {
                workerRestartWindowStart = now;
                workerRestartCount = 0;
            }

            if (workerRestartCount >= MaxWorkerRestarts)
            {
                interaction.AddLog("Taskbar worker restarted too often - giving up.");
                Debug.WriteLine("Taskbar worker restarted too often - giving up");
                SetTrayStatus("RoundedTB Revived - stopped after repeated errors");
                return;
            }
            workerRestartCount++;

            // Restart off the current callback so the worker is no longer marked busy.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (shouldReallyDieNoReally || taskbarThread.IsBusy)
                {
                    return;
                }
                try
                {
                    taskbarThread.RunWorkerAsync(workerArguments);
                }
                catch (Exception ex)
                {
                    interaction.AddLog($"Failed to restart taskbar worker: {ex.Message}");
                    Debug.WriteLine($"Failed to restart taskbar worker: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// Surfaces a degraded state on the tray icon, or clears it when passed null.
        /// Safe to call from the worker thread. (移植自 gniang Phase 1)
        /// </summary>
        public void SetTrayStatus(string status)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _trayIcon?.SetTip(string.IsNullOrEmpty(status) ? "RoundedTB Revived" : status);
                }
                catch (Exception)
                {
                    // Cosmetic only - never let this take the app down.
                }
            }));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // 点叉已被 mainTitleBar.CloseActionOverride 拦截为 Hide,不会走到这里。
            // 走到这里 = 外部 WM_CLOSE(任务管理器"结束任务"/Alt+F4/系统注销关机)或托盘 Exit。
            // 一律恢复任务栏并退出:若仅隐藏,任务管理器会判定"未响应"并强制结束进程
            // (TerminateProcess,无法捕获),导致任务栏 region 残留、托盘图标随进程消失。
            {


                try
                {
                    taskbarThread.CancelAsync();
                }
                catch (Exception aaaa)
                {
                    interaction.AddLog(aaaa.Message);
                }
                while (taskbarThread.IsBusy == true)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }

                try
                {
                    foreach (var tbDeets in taskbarDetails)
                    {
                        Taskbar.ResetTaskbar(tbDeets, activeSettings);
                    }
                    if (activeSettings.AutoHide > 0)
                    {
                        AutoHide(false, taskbarDetails);
                    }
                }
                catch (InvalidOperationException aaaa)
                {
                    interaction.AddLog($"Taskbar structure changed on exit:\n{aaaa.Message}");
                }
                interaction.AddLog("Exiting RoundedTB.");
            }
            if (!isAlreadyRunning)
            {
                interaction.WriteJSON();
            }

            // 清理托盘图标(Shell_NotifyIcon NIM_DELETE)
            try
            {
                _trayIcon?.Dispose();
                _trayIconImage?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 唯一真正退出程序的入口:关闭设置窗口/About 等不应退出(它们走 OnClosing 的隐藏分支)。
            shouldReallyDieNoReally = true;

            Close();
            // ShutdownMode 为 OnExplicitShutdown,关闭窗口不会自动退出,必须显式 Shutdown。
            System.Windows.Application.Current.Shutdown();
        }

        public void ShowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (IsVisible == false)
            {
                Visibility = Visibility.Visible;
                ShowMenuItem.Header = Localization.Get("Menu_Hide");
            }
            else
            {
                // Close any popups (About etc.) - keep this window: closing it would run
                // OnClosing cleanup, which disposes the tray icon.
                for (int windowCount = App.Current.Windows.Count - 1; windowCount >= 0; windowCount--)
                {
                    Window w = App.Current.Windows[windowCount];
                    if (!ReferenceEquals(w, this))
                    {
                        w.Close();
                    }
                }
                Visibility = Visibility.Hidden;
                ShowMenuItem.Header = Localization.Get("Menu_Show");
            }
        }

        private async void Startup_Clicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Startup toggled");
            if (IsRunningAsUWP())
            {
                await StartupToggle();
                await StartupInit(false);
            }
            else
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string newLink = Path.Combine(startupFolder, "RoundedTB Revived.lnk");
                string legacyLink = Path.Combine(startupFolder, "RoundedTB.lnk");
                if (System.IO.File.Exists(newLink) || System.IO.File.Exists(legacyLink))
                {
                    if (System.IO.File.Exists(newLink)) { System.IO.File.Delete(newLink); }
                    if (System.IO.File.Exists(legacyLink)) { System.IO.File.Delete(legacyLink); }
                }
                else
                {
                    EnableStartup();
                }
            }
        }

        public void EnableStartup()
        {
            try
            {
                string shortcutFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (!Directory.Exists(shortcutFolder))
                {
                    Directory.CreateDirectory(shortcutFolder);
                }
                // 快捷方式文件名 = 任务管理器"启动"页显示的名称
                string rtbStartupLink = Path.Combine(shortcutFolder, "RoundedTB Revived.lnk");
                // Create the shortcut via the WScript.Shell COM object, called through late-bound
                // "dynamic" so we don't need a design-time COM reference (the .NET Core MSBuild
                // cannot resolve COM references - see MSB4803). Behaviour is identical to the old
                // WshShell/IWshShortcut code.
                dynamic shellClass = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shellClass.CreateShortcut(rtbStartupLink);
                // 用 Environment.ProcessPath(当前进程的 exe 路径)而不是 GetCommandLineArgs()[0]:
                // 后者在部分启动方式下返回 dll 路径,导致任务管理器把启动项显示成 "RoundedTB.dll"。
                string exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
                shortcut.TargetPath = exePath;
                shortcut.IconLocation = exePath;
                shortcut.Arguments = "";
                shortcut.Description = "RoundedTB Revived";
                shortcut.Save();
                // 迁移:删除旧版 "RoundedTB.lnk",避免残留旧启动项(名称显示为 "RoundedTB")
                string legacyLink = Path.Combine(shortcutFolder, "RoundedTB.lnk");
                if (System.IO.File.Exists(legacyLink) && !string.Equals(legacyLink, rtbStartupLink, StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Delete(legacyLink);
                }
            }
            catch (Exception)
            {
            }
        }

        async Task StartupToggle()
        {
            StartupTask startupTask = await StartupTask.GetAsync("RTB"); // Pass the task ID you specified in the appxmanifest file
            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    StartupTaskState newState = await startupTask.RequestEnableAsync();
                    StartupCheckBox.IsEnabled = true;
                    break;

                case StartupTaskState.DisabledByUser:
                    StartupCheckBox.IsEnabled = false;
                    break;

                case StartupTaskState.EnabledByPolicy:
                    StartupCheckBox.IsEnabled = false;
                    break;

                case StartupTaskState.DisabledByPolicy:
                    StartupCheckBox.IsEnabled = false;
                    break;

                case StartupTaskState.Enabled:
                    startupTask.Disable();
                    StartupCheckBox.IsEnabled = true;
                    break;
            }
        }

        async Task StartupInit(bool clean)
        {
            StartupTask startupTask = await StartupTask.GetAsync("RTB");
            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    StartupCheckBox.IsChecked = false;
                    StartupCheckBox.IsEnabled = true;
                    if (clean)
                    {
                        Visibility = Visibility.Visible;
                        ShowMenuItem.Header = Localization.Get("Menu_Hide");
                    }
                    StartupCheckBox.Header = Localization.Get("Menu_RunAtStartup");
                    break;

                case StartupTaskState.DisabledByUser:
                    StartupCheckBox.IsChecked = false;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Visible;
                        ShowMenuItem.Header = Localization.Get("Menu_Hide");
                    }
                    StartupCheckBox.Header = Localization.Get("Menu_StartupUnavailable");
                    break;

                case StartupTaskState.EnabledByPolicy:
                    StartupCheckBox.IsChecked = true;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Hidden;
                        ShowMenuItem.Header = Localization.Get("Menu_Show");
                    }
                    StartupCheckBox.Header = Localization.Get("Menu_StartupMandatory");
                    break;

                case StartupTaskState.DisabledByPolicy:
                    StartupCheckBox.IsChecked = false;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Visible;
                        ShowMenuItem.Header = Localization.Get("Menu_Hide");
                    }
                    StartupCheckBox.Header = Localization.Get("Menu_StartupUnavailable");
                    break;

                case StartupTaskState.Enabled:
                    StartupCheckBox.IsChecked = true;
                    StartupCheckBox.IsEnabled = true;
                    if (clean)
                    {
                        Visibility = Visibility.Hidden;
                        ShowMenuItem.Header = Localization.Get("Menu_Show");
                    }
                    StartupCheckBox.Header = Localization.Get("Menu_RunAtStartup");
                    break;
            }
        }

        // Checks if running as a UWP app
        public bool IsRunningAsUWP()
        {
            try
            {
                Helpers helpers = new Helpers();
                return helpers.IsRunningAsUwp();
            }
            catch (Exception)
            {
                return false;
            }

        }

        private async void ContextMenu_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsRunningAsUWP())
            {
                await StartupInit(false);
            }
        }

        private void dynamicCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            centredCheckBox.IsEnabled = true;
            showSegmentsOnHoverCheckBox.IsEnabled = true;
            showSegmentsOnHoverCheckBox.IsChecked = false;
            showTrayCheckBox.IsEnabled = true;
            showTrayCheckBox.IsChecked = true;
            
            if (!isWindows11)
            {
                splitHelpButton.Visibility = Visibility.Visible;
                if (Opacity > 0.5)
                {
                    splitHelpButton_Click(null, null);
                }
            }

        }

        private void dynamicCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

            centredCheckBox.IsEnabled = false;
            centredCheckBox.IsChecked = false;
            showSegmentsOnHoverCheckBox.IsEnabled = false;
            showSegmentsOnHoverCheckBox.IsChecked = false;
            showTrayCheckBox.IsEnabled = false;
            showTrayCheckBox.IsChecked = false;
            
            if (!isWindows11)
            {
                splitHelpButton.Visibility = Visibility.Hidden;
            }
        }

        private void cornerRadiusSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            int check = Convert.ToInt32(Math.Round(cornerRadiusSlider.Value));
            cornerRadiusInput.Text = check.ToString();

            switch (selectedSegment)
            {
                default:
                    break;

                case 0:
                    activeSettings.SimpleTaskbarLayout.CornerRadius = check;
                    break;

                case 1:
                    activeSettings.DynamicAppListLayout.CornerRadius = check;
                    break;

                case 2:
                    activeSettings.DynamicTrayLayout.CornerRadius = check;
                    break;

                case 3:
                    activeSettings.DynamicWidgetsLayout.CornerRadius = check;
                    break;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 托盘图标/热键已在构造函数中初始化(Hidden 窗口下本事件可能永不触发)。
            // 若仍触发,仅保证窗口隐藏,不重复初始化。
            Visibility = Visibility.Hidden;
            Opacity = 1;
        }

        private void splitHelpButton_Click(object sender, RoutedEventArgs e)
        {
            Infobox ib = new Infobox();
            ib.Title = Localization.Get("Help_SplitTitle");
            ib.titleBlock.Text = Localization.Get("Help_SplitHeader");
            ib.bodyBlock.Text = Localization.Get("Help_SplitBody");
            ib.ShowDialog();
        }

        private void compositionFixCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // 恢复配置期间不弹窗(仅用户手动勾选时显示说明)。
            if (_isRestoringUi) return;
            if (Opacity > 0.01)
            {
                Infobox ib = new Infobox();
                ib.Height = 450;
                ib.Title = Localization.Get("Help_CompatTitle");
                ib.titleBlock.Text = Localization.Get("Help_CompatHeader");
                ib.bodyBlock.Text = Localization.Get("Help_CompatBody");
                ib.ShowDialog();
            }
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aw = new AboutWindow();
            aw.ShowDialog();
        }

        private void fillMaximisedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (isWindows11)
            {
                fillAltTabCheckBox.IsEnabled = true;
            }
        }

        private void fillMaximisedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            fillAltTabCheckBox.IsEnabled = false;
            fillAltTabCheckBox.IsChecked = false;

        }

        private void showSegmentsOnHoverCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            showTrayCheckBox.IsEnabled = false;
            showTrayCheckBox.IsChecked = false;

            showWidgetsCheckBox.IsEnabled = false;
            showWidgetsCheckBox.IsChecked = false;
        }

        private void showSegmentsOnHoverCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            showTrayCheckBox.IsEnabled = true;
            showTrayCheckBox.IsChecked = true;

            showWidgetsCheckBox.IsEnabled = true;
            showWidgetsCheckBox.IsChecked = true;
        }

        private void taskbarRectStandIn_Click(object sender, RoutedEventArgs e)
        {
            taskbarRectStandIn.Appearance = WPFUI.Common.Appearance.Primary;
            trayRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            widgetsRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            dynamicCheckBox.Visibility = Visibility.Visible;
            showTrayCheckBox.Visibility = Visibility.Hidden;
            showWidgetsCheckBox.Visibility = Visibility.Hidden;

            if (activeSettings.IsDynamic)
            {
                selectedSegment = 1;

                cornerRadiusInput.Text = activeSettings.DynamicAppListLayout.CornerRadius.ToString();
                cornerRadiusSlider.Value = activeSettings.DynamicAppListLayout.CornerRadius;
                mTopInput.Text = activeSettings.DynamicAppListLayout.MarginTop.ToString();
                mLeftInput.Text = activeSettings.DynamicAppListLayout.MarginLeft.ToString();
                mBottomInput.Text = activeSettings.DynamicAppListLayout.MarginBottom.ToString();
                mRightInput.Text = activeSettings.DynamicAppListLayout.MarginRight.ToString();
            }
            else
            {
                selectedSegment = 0;

                cornerRadiusInput.Text = activeSettings.SimpleTaskbarLayout.CornerRadius.ToString();
                cornerRadiusSlider.Value = activeSettings.SimpleTaskbarLayout.CornerRadius;
                mTopInput.Text = activeSettings.SimpleTaskbarLayout.MarginTop.ToString();
                mLeftInput.Text = activeSettings.SimpleTaskbarLayout.MarginLeft.ToString();
                mBottomInput.Text = activeSettings.SimpleTaskbarLayout.MarginBottom.ToString();
                mRightInput.Text = activeSettings.SimpleTaskbarLayout.MarginRight.ToString();
            }
        }

        private void trayRectStandIn_Click(object sender, RoutedEventArgs e)
        {
            taskbarRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            trayRectStandIn.Appearance = WPFUI.Common.Appearance.Primary;
            widgetsRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            dynamicCheckBox.Visibility = Visibility.Hidden;
            showTrayCheckBox.Visibility = Visibility.Visible;
            showWidgetsCheckBox.Visibility = Visibility.Hidden;

            selectedSegment = 2;

            cornerRadiusInput.Text = activeSettings.DynamicTrayLayout.CornerRadius.ToString();
            cornerRadiusSlider.Value = activeSettings.DynamicTrayLayout.CornerRadius;
            mTopInput.Text = activeSettings.DynamicTrayLayout.MarginTop.ToString();
            mLeftInput.Text = activeSettings.DynamicTrayLayout.MarginLeft.ToString();
            mBottomInput.Text = activeSettings.DynamicTrayLayout.MarginBottom.ToString();
            mRightInput.Text = activeSettings.DynamicTrayLayout.MarginRight.ToString();
        }

        private void widgetsRectStandIn_Click(object sender, RoutedEventArgs e)
        {
            taskbarRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            trayRectStandIn.Appearance = WPFUI.Common.Appearance.Secondary;
            widgetsRectStandIn.Appearance = WPFUI.Common.Appearance.Primary;
            dynamicCheckBox.Visibility = Visibility.Hidden;
            showTrayCheckBox.Visibility = Visibility.Hidden;
            showWidgetsCheckBox.Visibility = Visibility.Visible;

            selectedSegment = 3;

            cornerRadiusInput.Text = activeSettings.DynamicWidgetsLayout.CornerRadius.ToString();
            cornerRadiusSlider.Value = activeSettings.DynamicWidgetsLayout.CornerRadius;
            mTopInput.Text = activeSettings.DynamicWidgetsLayout.MarginTop.ToString();
            mLeftInput.Text = activeSettings.DynamicWidgetsLayout.MarginLeft.ToString();
            mBottomInput.Text = activeSettings.DynamicWidgetsLayout.MarginBottom.ToString();
            mRightInput.Text = activeSettings.DynamicWidgetsLayout.MarginRight.ToString();
        }

        private void mTopInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(mTopInput.Text, out int check) && mTopInput.Text != string.Empty)
            {
                switch (selectedSegment)
                {
                    default:
                        break;

                    case 0:
                        activeSettings.SimpleTaskbarLayout.MarginTop = check;
                        break;

                    case 1:
                        activeSettings.DynamicAppListLayout.MarginTop = check;
                        break;

                    case 2:
                        activeSettings.DynamicTrayLayout.MarginTop = check;
                        break;

                    case 3:
                        activeSettings.DynamicWidgetsLayout.MarginTop = check;
                        break;
                }
            }
        }

        private void mBottomInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(mBottomInput.Text, out int check) && mBottomInput.Text != string.Empty)
            {
                switch (selectedSegment)
                {
                    default:
                        break;

                    case 0:
                        activeSettings.SimpleTaskbarLayout.MarginBottom = check;
                        break;

                    case 1:
                        activeSettings.DynamicAppListLayout.MarginBottom = check;
                        break;

                    case 2:
                        activeSettings.DynamicTrayLayout.MarginBottom = check;
                        break;

                    case 3:
                        activeSettings.DynamicWidgetsLayout.MarginBottom = check;
                        break;
                }
            }
        }

        private void mLeftInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(mLeftInput.Text, out int check) && mLeftInput.Text != string.Empty)
            {
                switch (selectedSegment)
                {
                    default:
                        break;

                    case 0:
                        activeSettings.SimpleTaskbarLayout.MarginLeft = check;
                        break;

                    case 1:
                        activeSettings.DynamicAppListLayout.MarginLeft = check;
                        break;

                    case 2:
                        activeSettings.DynamicTrayLayout.MarginLeft = check;
                        break;

                    case 3:
                        activeSettings.DynamicWidgetsLayout.MarginLeft = check;
                        break;
                }
            }
        }

        private void mRightInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(mRightInput.Text, out int check) && mRightInput.Text != string.Empty)
            {
                switch (selectedSegment)
                {
                    default:
                        break;

                    case 0:
                        activeSettings.SimpleTaskbarLayout.MarginRight = check;
                        break;

                    case 1:
                        activeSettings.DynamicAppListLayout.MarginRight = check;
                        break;

                    case 2:
                        activeSettings.DynamicTrayLayout.MarginRight = check;
                        break;

                    case 3:
                        activeSettings.DynamicWidgetsLayout.MarginRight = check;
                        break;
                }
            }
        }

        private void cornerRadiusInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(cornerRadiusInput.Text, out int check) && cornerRadiusInput.Text != string.Empty)
            {
                switch (selectedSegment)
                {
                    default:
                        break;

                    case 0:
                        activeSettings.SimpleTaskbarLayout.CornerRadius = check;
                        break;

                    case 1:
                        activeSettings.DynamicAppListLayout.CornerRadius = check;
                        break;

                    case 2:
                        activeSettings.DynamicTrayLayout.CornerRadius = check;
                        break;

                    case 3:
                        activeSettings.DynamicWidgetsLayout.CornerRadius = check;
                        break;
                }

                cornerRadiusSlider.Value = check;
            }
        }

        private void cornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            cornerRadiusInput.Text = Math.Round(cornerRadiusSlider.Value).ToString();
        }
    }
}
