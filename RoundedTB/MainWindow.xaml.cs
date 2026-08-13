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
        public int version = -1;
        private bool _lastTrayLight = false; // 上次托盘图标用的主题(暗色=false/亮色=true),避免每帧重建图标
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

            // 右上角关闭按钮 = 只隐藏设置窗口,程序继续在托盘运行(对齐 R3.1 / ModernWpf 行为)。
            // WPFUI 的 TitleBar 在 ApplicationNavigation 模式下,关闭按钮默认会直接 Application.Shutdown(),
            // 必须用 CloseActionOverride 接管。注意:它传的是内部 _parent 字段(懒赋值,可能为 null),需防御。
            mainTitleBar.CloseActionOverride = (tb, win) =>
            {
                (win ?? Window.GetWindow(tb) ?? this).Hide();
            };

            // 左键单击托盘图标:显示/隐藏设置窗口。
            mainTitleBar.NotifyIconClick += (s, e) => ShowMenuItem_Click(null, null);


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
            background = new Background();
            interaction = new Interaction();

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

            if (System.IO.File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "RoundedTB.lnk")) && !IsRunningAsUWP())
            {
                StartupCheckBox.IsChecked = true;
                ShowMenuItem.Header = Localization.Get("Menu_Show");
            }
            taskbarThread.WorkerSupportsCancellation = true;
            taskbarThread.WorkerReportsProgress = true;
            taskbarThread.DoWork +=background.DoWork;

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
                        SimpleTaskbarLayout = new Types.SegmentSettings{ CornerRadius = 7, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 7, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 7, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                        DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 7, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
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
            if (activeSettings.SimpleTaskbarLayout == null) activeSettings.SimpleTaskbarLayout = new Types.SegmentSettings { CornerRadius = 7, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicAppListLayout == null) activeSettings.DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 7, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicTrayLayout == null) activeSettings.DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 7, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };
            if (activeSettings.DynamicWidgetsLayout == null) activeSettings.DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 7, MarginTop = 3, MarginLeft = 3, MarginRight = 3, MarginBottom = 3 };

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
            compositionFixCheckBox.IsChecked = activeSettings.CompositionCompat;
            autoHideComboBox.SelectedIndex = activeSettings.AutoHide;
            taskbarDetails = Taskbar.GenerateTaskbarInfo();

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
                if (light == _lastTrayLight)
                {
                    return;
                }
                _lastTrayLight = light;

                Uri resLight = new("pack://application:,,,/res/traylight.ico");
                Uri resDark = new("pack://application:,,,/res/traydark.ico");
                mainTitleBar.NotifyIconImage = new System.Windows.Media.Imaging.BitmapImage(light ? resLight : resDark);
                // WPFUI 的 NotifyIconImage 依赖属性没有变更回调,设置后不会自动刷新托盘图标,
                // 必须显式 ResetIcon() 重建(它在 InitializeNotifyIcon 时读取当前 NotifyIconImage)。
                mainTitleBar.ResetIcon();
            }
            catch (Exception)
            {
                // 设置失败时保持默认图标,不影响主功能。
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
                taskbarThread.RunWorkerAsync((mt, ml, mb, mr, 0));
            }
            else
            {
                taskbarThread.CancelAsync();
                while (taskbarThread.IsBusy == true)
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(100);
                }
                taskbarThread.RunWorkerAsync((mt, ml, mb, mr, 0));
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

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (shouldReallyDieNoReally == false)
            {
                e.Cancel = true;
                Visibility = Visibility.Hidden;
                ShowMenuItem.Header = Localization.Get("Menu_Show");
            }
            else
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
                // Close any popups - leave main window for now
                for (int windowCount = App.Current.Windows.Count - 1; windowCount >= 0; windowCount--)
                {
                    App.Current.Windows[windowCount].Close();
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
                if (System.IO.File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "RoundedTB.lnk")))
                {
                    System.IO.File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "RoundedTB.lnk"));
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
                string rtbStartupLink = Path.Combine(shortcutFolder, "RoundedTB.lnk");
                // Create the shortcut via the WScript.Shell COM object, called through late-bound
                // "dynamic" so we don't need a design-time COM reference (the .NET Core MSBuild
                // cannot resolve COM references - see MSB4803). Behaviour is identical to the old
                // WshShell/IWshShortcut code.
                dynamic shellClass = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shellClass.CreateShortcut(rtbStartupLink);
                shortcut.TargetPath = Environment.GetCommandLineArgs()[0];
                shortcut.IconLocation = Environment.GetCommandLineArgs()[0];
                shortcut.Arguments = "";
                shortcut.Description = "Start RoundedTB";
                shortcut.Save();
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
                    StartupCheckBox.Content = Localization.Get("Menu_RunAtStartup");
                    break;

                case StartupTaskState.DisabledByUser:
                    StartupCheckBox.IsChecked = false;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Visible;
                        ShowMenuItem.Header = Localization.Get("Menu_Hide");
                    }
                    StartupCheckBox.Content = Localization.Get("Menu_StartupUnavailable");
                    break;

                case StartupTaskState.EnabledByPolicy:
                    StartupCheckBox.IsChecked = true;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Hidden;
                        ShowMenuItem.Header = Localization.Get("Menu_Show");
                    }
                    StartupCheckBox.Content = Localization.Get("Menu_StartupMandatory");
                    break;

                case StartupTaskState.DisabledByPolicy:
                    StartupCheckBox.IsChecked = false;
                    StartupCheckBox.IsEnabled = false;
                    if (clean)
                    {
                        Visibility = Visibility.Visible;
                        ShowMenuItem.Header = Localization.Get("Menu_Hide");
                    }
                    StartupCheckBox.Content = Localization.Get("Menu_StartupUnavailable");
                    break;

                case StartupTaskState.Enabled:
                    StartupCheckBox.IsChecked = true;
                    StartupCheckBox.IsEnabled = true;
                    if (clean)
                    {
                        Visibility = Visibility.Hidden;
                        ShowMenuItem.Header = Localization.Get("Menu_Show");
                    }
                    StartupCheckBox.Content = Localization.Get("Menu_RunAtStartup");
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

        private void DebugMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IntPtr hwndNext = LocalPInvoke.FindWindowExA(taskbarDetails[0].TaskbarHwnd, IntPtr.Zero, "Start", null);
            List<IntPtr> floatingMilkshakesBitsOfTaskbar = new List<IntPtr>();
            floatingMilkshakesBitsOfTaskbar.Add(hwndNext);
            while (true) 
            {
                hwndNext = LocalPInvoke.FindWindowExA(taskbarDetails[0].TaskbarHwnd, hwndNext, null, null);
                if (floatingMilkshakesBitsOfTaskbar.Contains(hwndNext))
                {
                    break;
                }
                floatingMilkshakesBitsOfTaskbar.Add(hwndNext);

            }
            foreach (IntPtr hwnd in floatingMilkshakesBitsOfTaskbar)
            {
                LocalPInvoke.GetWindowRect(hwnd, out LocalPInvoke.RECT rect);
                LocalPInvoke.MoveWindow(hwnd, rect.Left + 50, rect.Top, (rect.Right + 50) - (rect.Left + 50), rect.Bottom - rect.Top, true);
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
            Debug.WriteLine("AAAAA");
            base.OnSourceInitialized(e);


            IntPtr handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(interaction.HwndHook);
            bool wtf = LocalPInvoke.RegisterHotKey(handle, 9000, 0x8, 0x71);
            Debug.WriteLine("KEY: " + wtf);
            Debug.WriteLine(handle);
            Debug.WriteLine((int)Types.KeyModifier.WinKey);
            Debug.WriteLine(System.Windows.Forms.Keys.J.GetHashCode());
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
