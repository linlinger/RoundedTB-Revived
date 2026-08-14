using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace RoundedTB
{
    public class Background
    {
        // Just have a reference point for the Dispatcher
        public MainWindow mw;
        bool redrawOverride = false;
        int infrequentCount = 0;
        int loopLogCount = 0; // [DEBUG] 节流状态日志计数

        // 移植自 gniang (Phase 1):句柄缺失(Explorer 崩溃/重启)时指数退避重建,
        // 而不是每 100ms 重建 CUIAutomation + AppListXaml 直到它回来。
        private const int RegenBackoffInitialTicks = 1;  // 100ms
        private const int RegenBackoffMaxTicks = 50;     // 5s
        private int regenBackoffTicks;
        private int regenCooldownTicks;
        private bool regenDegraded;

        // 移植自 gniang (Phase 1):hover 状态是瞬态的,绝不写进持久化配置,
        // 否则保存/退出时会把 ShowTray/ShowWidgets 临时值覆盖进用户的设置。
        private bool hoverShowTray;
        private bool hoverShowWidgets;

        public Background(MainWindow mw = null)
        {
            this.mw = mw;
        }


        // Main method for the BackgroundWorker - runs indefinitely
        public void DoWork(object sender, DoWorkEventArgs e)
        {
            mw.interaction.AddLog("in bw");
            BackgroundWorker worker = sender as BackgroundWorker;
            while (true)
            {
                try
                {
                    if (worker.CancellationPending == true)
                    {
                        mw.interaction.AddLog("cancelling");
                        e.Cancel = true;
                        break;
                    }

                    // Primary loop for the running process
                    else
                    {
                        // Section for running less important things without requiring an additional thread
                        infrequentCount++;
                        if (infrequentCount == 10)
                        {
                            // Check to see if settings need to be shown
                            List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                            foreach (IntPtr hwnd in windowList)
                            {
                                StringBuilder windowClass = new StringBuilder(1024);
                                StringBuilder windowTitle = new StringBuilder(1024);
                                try
                                {
                                    LocalPInvoke.GetClassName(hwnd, windowClass, 1024);
                                    LocalPInvoke.GetWindowText(hwnd, windowTitle, 1024);

                                    if (windowClass.ToString().Contains("HwndWrapper[RoundedTB.exe") && windowTitle.ToString() == "RoundedTB_SettingsRequest")
                                    {
                                        mw.Dispatcher.Invoke(() =>
                                        {
                                            if (mw.Visibility != Visibility.Visible)
                                            {
                                                mw.ShowMenuItem_Click(null, null);
                                            }
                                        });
                                        LocalPInvoke.SetWindowText(hwnd, "RoundedTB");
                                    }
                                }
                                catch (Exception) { }
                            }

                            // Update tray icon
                            mw.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    mw.TrayIconCheck();

                                }
                                catch (Exception)
                                {

                                }
                            });

                            // On Windows 11 22H2+ the legacy app-list window rects no longer move as
                            // apps open and close, so periodically re-derive the true content bounds
                            // from UI Automation and force a redraw when they change.
                            foreach (Types.Taskbar tb in mw.taskbarDetails)
                            {
                                if (RefreshContentBounds(tb))
                                {
                                    tb.Ignored = true;
                                }
                            }

                            infrequentCount = 0;
                        }

                        // Check if the taskbar is centred, and if it is, directly update the settings; using an interim bool to avoid delaying because I'm lazy
                        bool isCentred = Taskbar.CheckIfCentred();
                        mw.activeSettings.IsCentred = isCentred;

                        // Work with static values to avoid some null reference exceptions
                        List<Types.Taskbar> taskbars = mw.taskbarDetails;
                        Types.Settings settings = mw.activeSettings;

                        // If the number of taskbars has changed, regenerate taskbar information
                        if (Taskbar.TaskbarCountOrHandleChanged(taskbars.Count, taskbars[0].TaskbarHwnd))
                        {
                            // Forcefully reset taskbars if the taskbar count or main taskbar handle has changed
                            taskbars = Taskbar.GenerateTaskbarInfo();
                            Debug.WriteLine("Regenerating taskbar info");

                            // Explorer 恢复会改变主句柄,这个分支通常在恢复时触发,清掉残留的退避。
                            if (TaskbarHandlesAreValid(taskbars))
                            {
                                ResetRegenBackoff();
                            }
                        }

                        for (int current = 0; current < taskbars.Count; current++)
                        {
                            if (taskbars[current].TaskbarHwnd == IntPtr.Zero || taskbars[current].AppListHwnd == IntPtr.Zero)
                            {
                                // Explorer 大概率挂了/重启中。指数退避重试,而不是每 100ms 重建 UIA。
                                if (regenCooldownTicks > 0)
                                {
                                    regenCooldownTicks--;
                                    break;
                                }

                                taskbars = Taskbar.GenerateTaskbarInfo();
                                Debug.WriteLine("Regenerating taskbar info due to a missing handle");

                                if (TaskbarHandlesAreValid(taskbars))
                                {
                                    ResetRegenBackoff();
                                }
                                else
                                {
                                    EscalateRegenBackoff();
                                }
                                break;
                            }
                            // Get the latest quick details of this taskbar
                            Types.Taskbar newTaskbar = Taskbar.GetQuickTaskbarRects(taskbars[current].TaskbarHwnd, taskbars[current].TrayHwnd, taskbars[current].AppListHwnd);

                            // [DEBUG] 每 10 帧输出一次主循环状态,用于定位悬停/最大化恢复不工作
                            if (ChannelInfo.VerboseLogging && loopLogCount++ % 10 == 0)
                            {
                                mw.interaction.AddLog($"bw[{current}]: segHover={settings.ShowSegmentsOnHover} trayLeft={taskbars[current].TrayRect.Left} trayRect=({taskbars[current].TrayRect.Left},{taskbars[current].TrayRect.Top})-({taskbars[current].TrayRect.Right},{taskbars[current].TrayRect.Bottom}) dyn={settings.IsDynamic} fill={Taskbar.TaskbarShouldBeFilled(taskbars[current].TaskbarHwnd, settings)}");
                            }


                            // If the taskbar's monitor has a maximised window, reset it so it's "filled"
                            if (Taskbar.TaskbarShouldBeFilled(taskbars[current].TaskbarHwnd, settings))
                            {
                                if (taskbars[current].Ignored == false)
                                {
                                    Taskbar.ResetTaskbar(taskbars[current], settings);
                                    taskbars[current].Ignored = true;
                                }
                                continue;
                            }

                            // Showhide tray on hover. The result goes into effectiveSettings, never into
                            // the persisted settings object - otherwise a WriteJSON while hovering would
                            // overwrite the user's own ShowTray/ShowWidgets choices. (移植自 gniang Phase 1)
                            Types.Settings effectiveSettings = settings;
                            if (settings.ShowSegmentsOnHover)
                            {
                                // TrayNotifyWnd 的窗口矩形在 Win11 22H2+ 上 Y 坐标会偏移(偏下),
                                // 直接用 GetWindowRect 的值会检测不到鼠标悬停。托盘区的 Y 范围改用
                                // 任务栏自身的矩形,只取托盘窗口的 X 范围。
                                LocalPInvoke.RECT currentTrayRect = taskbars[current].TrayRect;
                                LocalPInvoke.RECT currentTaskbarRect = taskbars[current].TaskbarRect;
                                currentTrayRect.Top = currentTaskbarRect.Top;
                                currentTrayRect.Bottom = currentTaskbarRect.Bottom;
                                LocalPInvoke.RECT currentWidgetsRect = taskbars[current].TaskbarRect;
                                currentWidgetsRect.Right = Convert.ToInt32(currentWidgetsRect.Right - (currentWidgetsRect.Right - currentWidgetsRect.Left) + (168 * taskbars[current].ScaleFactor));

                                if (currentTrayRect.Left != 0)
                                {
                                    LocalPInvoke.GetCursorPos(out LocalPInvoke.POINT msPt);
                                    bool isHoveringOverTray = LocalPInvoke.PtInRect(ref currentTrayRect, msPt);
                                    bool isHoveringOverWidgets = LocalPInvoke.PtInRect(ref currentWidgetsRect, msPt);
                                    // [DEBUG] hover 诊断(仅预发布通道保留)
                                    if (ChannelInfo.VerboseLogging)
                                    {
                                        mw.interaction.AddLog($"hover: tray=({currentTrayRect.Left},{currentTrayRect.Top})-({currentTrayRect.Right},{currentTrayRect.Bottom}) mouse=({msPt.x},{msPt.y}) hoverTray={isHoveringOverTray} hoverShowTray={hoverShowTray} dyn={settings.IsDynamic}");
                                    }
                                    if (isHoveringOverTray && !hoverShowTray)
                                    {
                                        hoverShowTray = true;
                                        taskbars[current].Ignored = true;
                                    }
                                    else if (!isHoveringOverTray)
                                    {
                                        taskbars[current].Ignored = true;
                                        hoverShowTray = false;
                                    }

                                    if (isHoveringOverWidgets && !hoverShowWidgets)
                                    {
                                        hoverShowWidgets = true;
                                        taskbars[current].Ignored = true;
                                    }
                                    else if (!isHoveringOverWidgets)
                                    {
                                        taskbars[current].Ignored = true;
                                        hoverShowWidgets = false;
                                    }

                                }

                                effectiveSettings = settings.ShallowCopy();
                                effectiveSettings.ShowTray = hoverShowTray;
                                effectiveSettings.ShowWidgets = hoverShowWidgets;
                            }

                            if (settings.AutoHide > 0)
                            {
                                // AutoHide 由 OS 原生 ABM 自动隐藏(ABM_SETSTATE 在 MainWindow.AutoHide 设置)
                                // 全权处理任务栏滑出/滑回:鼠标移到停靠边缘,OS 自动把任务栏滑回。
                                // 此前在这里叠加 RTB 自己的 alpha 淡出 + WS_EX_TRANSPARENT,会与 OS 的
                                // 揭示机制冲突——桌面/非全屏下鼠标靠边任务栏唤不醒(只有触发 FillOnMaximise
                                // 的 ResetTaskbar 才恢复),现已移除,不再干预 OS 的自动隐藏。
                            }
                            else
                            {
                                int animSpeed = 15;
                                byte taskbarOpacity = 0;
                                LocalPInvoke.GetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, out _, out taskbarOpacity, out _);
                                if (taskbarOpacity < 255)
                                {
                                    int style = LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                                    if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
                                    {
                                        LocalPInvoke.SetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbars[current].TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
                                    }
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 63, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 127, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 191, LocalPInvoke.LWA_ALPHA);
                                    System.Threading.Thread.Sleep(animSpeed);
                                    LocalPInvoke.SetLayeredWindowAttributes(taskbars[current].TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
                                    taskbars[current].Ignored = true;
                                    taskbars[current].TaskbarHidden = false;
                                }
                            }


                            // If the taskbar's overall rect has changed, update it. If it's simple, just update. If it's dynamic, check it's a valid change, then update it.
                            if (Taskbar.TaskbarRefreshRequired(taskbars[current], newTaskbar, settings.IsDynamic) || taskbars[current].Ignored || redrawOverride)
                            {
                                // 动态模式:重放 region 前先同步刷新 UIA 内容边界。
                                // infrequent tick 每 ~1s 才更新一次缓存,新图标出现时若用旧缓存
                                // 重放会把它裁掉一半(鼠标移过触发重绘后才恢复)。
                                if (settings.IsDynamic)
                                {
                                    RefreshContentBounds(taskbars[current]);
                                }
                                Debug.WriteLine($"Refresh required on taskbar {current}");
                                taskbars[current].Ignored = false;
                                int isFullTest = newTaskbar.TrayRect.Left - newTaskbar.AppListRect.Right;
                                mw.interaction.AddLog($"Taskbar: {current} - AppList ends: {newTaskbar.AppListRect.Right} - Tray starts: {newTaskbar.TrayRect.Left} - Total gap: {isFullTest}");
                                if (!settings.IsDynamic || (isFullTest <= taskbars[current].ScaleFactor * 25 && isFullTest > 0 && newTaskbar.TrayRect.Left != 0))
                                {
                                    // Add the rect changes to the temporary list of taskbars
                                    taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                                    taskbars[current].AppListRect = newTaskbar.AppListRect;
                                    taskbars[current].TrayRect = newTaskbar.TrayRect;
                                    Taskbar.UpdateSimpleTaskbar(taskbars[current], effectiveSettings);
                                    mw.interaction.AddLog($"Updated taskbar {current} simply");
                                }
                                else
                                {
                                    if (Taskbar.CheckDynamicUpdateIsValid(taskbars[current], newTaskbar))
                                    {
                                        // Add the rect changes to the temporary list of taskbars
                                        taskbars[current].TaskbarRect = newTaskbar.TaskbarRect;
                                        taskbars[current].AppListRect = newTaskbar.AppListRect;
                                        taskbars[current].TrayRect = newTaskbar.TrayRect;
                                        Taskbar.UpdateDynamicTaskbar(taskbars[current], effectiveSettings);
                                        mw.interaction.AddLog($"Updated taskbar {current} dynamically");
                                    }
                                }
                            }
                        }
                        mw.taskbarDetails = taskbars;


                    System.Threading.Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    // Anything escaping here is swallowed by BackgroundWorker and handed to
                    // RunWorkerCompleted, which silently ends the loop - so log it and let
                    // MainWindow's RunWorkerCompleted handler restart us. (移植自 gniang Phase 1)
                    mw.interaction.AddLog(ex.Message);
                    if (ex.InnerException != null)
                    {
                        mw.interaction.AddLog(ex.InnerException.Message);
                    }
                    Debug.WriteLine($"Taskbar worker failed: {ex}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Checks whether every known taskbar has the handles the main loop needs. (移植自 gniang Phase 1)
        /// </summary>
        private static bool TaskbarHandlesAreValid(List<Types.Taskbar> taskbars)
        {
            if (taskbars == null || taskbars.Count == 0)
            {
                return false;
            }
            foreach (Types.Taskbar taskbar in taskbars)
            {
                if (taskbar.TaskbarHwnd == IntPtr.Zero || taskbar.AppListHwnd == IntPtr.Zero)
                {
                    return false;
                }
            }
            return true;
        }

        private void ResetRegenBackoff()
        {
            regenBackoffTicks = 0;
            regenCooldownTicks = 0;
            if (regenDegraded)
            {
                regenDegraded = false;
                mw.interaction.AddLog("Taskbar handles recovered.");
                Debug.WriteLine("Taskbar handles recovered");
                mw.SetTrayStatus(null);
            }
        }

        private void EscalateRegenBackoff()
        {
            if (regenBackoffTicks == 0)
            {
                regenBackoffTicks = RegenBackoffInitialTicks;
            }
            else if (regenBackoffTicks < RegenBackoffMaxTicks)
            {
                regenBackoffTicks = Math.Min(regenBackoffTicks * 2, RegenBackoffMaxTicks);
            }
            regenCooldownTicks = regenBackoffTicks;

            // Once we're retrying at the slowest rate, say so rather than degrading silently.
            if (regenBackoffTicks >= RegenBackoffMaxTicks && !regenDegraded)
            {
                regenDegraded = true;
                mw.interaction.AddLog("Taskbar handles unavailable - retrying slowly.");
                Debug.WriteLine("Taskbar handles unavailable - retrying slowly");
                mw.SetTrayStatus("RoundedTB - waiting for the taskbar...");
            }
        }

        /// <summary>
        /// 查询并更新任务栏的 UIA 内容边界缓存(带合理性检查),返回是否更新。
        /// 用于 infrequent tick 的周期性刷新,以及动态模式 region 重放前的同步刷新
        /// (新图标出现时若用旧缓存重放会把它裁掉一半)。
        /// </summary>
        private static bool RefreshContentBounds(Types.Taskbar tb)
        {
            if (Taskbar.GetTrueTaskbarContentBounds(tb, out int contentLeft, out int contentRight))
            {
                // 合理性检查:UIA 在任务栏重绘/悬停等瞬间偶发返回异常值,
                // 只在结果可信时才更新缓存,避免把左/右边界拉崩
                // (例如左侧多出一段空白任务栏)。
                bool sane =
                    contentLeft >= tb.TaskbarRect.Left &&
                    contentRight > contentLeft &&
                    contentRight <= tb.TaskbarRect.Right &&
                    (tb.TrayRect.Left <= tb.TaskbarRect.Left || contentRight <= tb.TrayRect.Left);
                if (sane && (contentLeft != tb.ContentLeft || contentRight != tb.ContentRight))
                {
                    tb.ContentLeft = contentLeft;
                    tb.ContentRight = contentRight;
                    return true;
                }
            }
            return false;
        }

    }
}
