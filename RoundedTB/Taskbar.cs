using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;



namespace RoundedTB
{
    class Taskbar
    {
        /// <summary>
        /// Checks if the taskbar is centred.
        /// </summary>
        /// <returns>
        /// A bool indicating if the taskbar is centred.
        /// </returns>
        public static bool CheckIfCentred()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"))
                {
                    if (key != null)
                    {
                        object raw = key.GetValue("TaskbarAl");
                        if (raw != null)
                        {
                            return Convert.ToInt32(raw) == 1;
                        }
                    }
                }
                // TaskbarAl is absent (seen on some Windows 11 builds/images): fall back to the OS
                // default. Windows 11 defaults to a centred taskbar, Windows 10 to left-aligned.
                return Environment.OSVersion.Version.Build >= 21996;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Compares two taskbars' rects to see if they've changed
        /// </summary>
        /// <returns>
        /// a bool indicating if the taskbar's, applist's, and tray's rects rects have changed.
        /// </returns>
        public static bool TaskbarRefreshRequired(Types.Taskbar currentTB, Types.Taskbar newTB, bool isDynamic)
        {
            // REMINDER: newTB will only have rect & hwnd info. Everything else will be null.


            bool taskbarRectChanged = true;
            bool appListRectChanged = true;
            bool trayRectChanged = true;

            if (
                currentTB.TaskbarRect.Left == newTB.TaskbarRect.Left &&
                currentTB.TaskbarRect.Top == newTB.TaskbarRect.Top &&
                currentTB.TaskbarRect.Right == newTB.TaskbarRect.Right &&
                currentTB.TaskbarRect.Bottom == newTB.TaskbarRect.Bottom)
            {
                taskbarRectChanged = false;
            }
            if (
                currentTB.AppListRect.Left == newTB.AppListRect.Left &&
                currentTB.AppListRect.Top == newTB.AppListRect.Top &&
                currentTB.AppListRect.Right == newTB.AppListRect.Right &&
                currentTB.AppListRect.Bottom == newTB.AppListRect.Bottom)
            {
                appListRectChanged = false;
            }
            if (
                (currentTB.TrayRect.Left + 5 >= newTB.TrayRect.Left && currentTB.TrayRect.Left - 5 <= newTB.TrayRect.Left) &&
                currentTB.TrayRect.Top == newTB.TrayRect.Top &&
                currentTB.TrayRect.Right == newTB.TrayRect.Right &&
                currentTB.TrayRect.Bottom == newTB.TrayRect.Bottom)
            {
                trayRectChanged = false;
            }

            if (isDynamic && (taskbarRectChanged || appListRectChanged || trayRectChanged))
            {
                return true;
            }
            else if (!isDynamic && taskbarRectChanged)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the rects of the three components of the taskbar from their respective handles.
        /// </summary>
        /// <returns>
        /// a partial Taskbar containing just rects and handles.
        /// </returns>
        public static Types.Taskbar GetQuickTaskbarRects(IntPtr taskbarHwnd, IntPtr trayHwnd, IntPtr appListHwnd)
        {
            LocalPInvoke.GetWindowRect(taskbarHwnd, out LocalPInvoke.RECT taskbarRectCheck);
            LocalPInvoke.GetWindowRect(trayHwnd, out LocalPInvoke.RECT trayRectCheck);
            LocalPInvoke.GetWindowRect(appListHwnd, out LocalPInvoke.RECT appListRectCheck);

            return new Types.Taskbar()
            {
                TaskbarHwnd = taskbarHwnd,
                TrayHwnd = trayHwnd,
                AppListHwnd = appListHwnd,
                TaskbarRect = taskbarRectCheck,
                TrayRect = trayRectCheck,
                AppListRect = appListRectCheck
            };
        }

        /// <summary>
        /// Resets the specified taskbar.
        /// </summary>
        public static void ResetTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, IntPtr.Zero, true);
            LocalPInvoke.SetLayeredWindowAttributes(taskbar.TaskbarHwnd, 0, 255, LocalPInvoke.LWA_ALPHA);
            int style = LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_LAYERED) == LocalPInvoke.WS_EX_LAYERED)
            {
                LocalPInvoke.SetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
            }
            style = LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_TRANSPARENT) == LocalPInvoke.WS_EX_TRANSPARENT)
            {
                LocalPInvoke.SetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(taskbar.TaskbarHwnd, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_TRANSPARENT);
            }

            if (settings.CompositionCompat)
            {
                Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
            }
        }

        /// <summary>
        /// Creates a basic region for a specific taskbar and applies it.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool UpdateSimpleTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            try
            {
                // Create an effective region to be applied to the taskbar
                Types.EffectiveRegion taskbarEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.SimpleTaskbarLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.SimpleTaskbarLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.SimpleTaskbarLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.SimpleTaskbarLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.SimpleTaskbarLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                IntPtr region = LocalPInvoke.CreateRoundRectRgn(taskbarEffectiveRegion.Left, taskbarEffectiveRegion.Top, taskbarEffectiveRegion.Width, taskbarEffectiveRegion.Height, taskbarEffectiveRegion.CornerRadius, taskbarEffectiveRegion.CornerRadius);
                LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, region, true);
                if (settings.CompositionCompat)
                {
                    Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a dynamic region for a specific taskbar and applies it.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool UpdateDynamicTaskbar(Types.Taskbar taskbar, Types.Settings settings)
        {
            try
            {
                IntPtr mainRegion;
                IntPtr workingRegion = LocalPInvoke.CreateRoundRectRgn(1, 1, 1, 1, 0, 0);

                int cornerRadius = Convert.ToInt32(settings.DynamicAppListLayout.CornerRadius * taskbar.ScaleFactor);
                int marginTop = Convert.ToInt32(settings.DynamicAppListLayout.MarginTop * taskbar.ScaleFactor);
                int marginLeft = Convert.ToInt32(settings.DynamicAppListLayout.MarginLeft * taskbar.ScaleFactor);
                int marginRight = Convert.ToInt32(settings.DynamicAppListLayout.MarginRight * taskbar.ScaleFactor);
                int marginBottom = Convert.ToInt32(settings.DynamicAppListLayout.MarginBottom * taskbar.ScaleFactor);
                int taskbarWidth = taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left;
                int taskbarHeight = taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top;

                // Windows 11 22H2+ renders the taskbar content in a XAML island, so the legacy
                // app-list window rect (MSTaskSwWClass) no longer reflects where the icons actually
                // are. That produced two symptoms on modern Windows:
                //   - the rect started too far right, leaving the empty strip to the left of a
                //     centred Start button visible, and
                //   - it ended too far left, clipping every running app that appears to the right
                //     of the pinned ones.
                // Derive the true horizontal span of the content via UI Automation instead.
                int contentLeft, contentRight;
                if (taskbar.ContentLeft >= 0 && taskbar.ContentRight >= taskbar.ContentLeft)
                {
                    contentLeft = taskbar.ContentLeft;
                    contentRight = taskbar.ContentRight;
                }
                else if (GetTrueTaskbarContentBounds(taskbar, out contentLeft, out contentRight))
                {
                    taskbar.ContentLeft = contentLeft;
                    taskbar.ContentRight = contentRight;
                }
                else
                {
                    // Fall back to the legacy window rect when UIA is unavailable (previous behaviour).
                    contentLeft = taskbar.AppListRect.Left;
                    contentRight = taskbar.AppListRect.Right;
                }

                // 结构性约束:应用列表的右边界不应越过托盘左边界(防止溢出/异常的 UIA 值
                // 导致任务栏右侧偶发多出一段)。注意:只压缩右边界,绝不反向拉小左边界,
                // 否则会在左侧露出一大段空白任务栏(悬停托盘重绘时偶发触发)。
                if (taskbar.TrayRect.Left > taskbar.TaskbarRect.Left && taskbar.TrayRect.Left > contentLeft)
                {
                    int maxContentRight = taskbar.TrayRect.Left - Convert.ToInt32(1 * taskbar.ScaleFactor);
                    if (contentRight > maxContentRight)
                    {
                        contentRight = maxContentRight;
                    }
                }
                // 内容左缘不可能在任务栏左缘之外。
                if (contentLeft < taskbar.TaskbarRect.Left)
                {
                    contentLeft = taskbar.TaskbarRect.Left;
                }
                // 兜底:内容边界失效(右 ≤ 左)时退回 legacy 窗口矩形,而不是把左边界拉崩。
                if (contentRight <= contentLeft)
                {
                    contentLeft = taskbar.AppListRect.Left;
                    contentRight = taskbar.AppListRect.Right;
                }

                // Convert to coordinates relative to the taskbar's own top-left corner (SetWindowRgn space).
                int cx1 = Math.Max(contentLeft - taskbar.TaskbarRect.Left, 0);
                int cx2 = Math.Min(contentRight - taskbar.TaskbarRect.Left, taskbarWidth);

                int x1, x2;
                if (settings.IsCentred)
                {
                    // Centred taskbar: the segment hugs the actual content (Start button + apps),
                    // clipping the empty strips on both sides.
                    x1 = cx1 - marginLeft;
                    x2 = cx2 + marginRight;
                }
                else
                {
                    // Left-aligned taskbar: keep the old left edge (an absolute margin, so negative
                    // margins still "attach" the taskbar to the screen edge), but derive the right
                    // edge from the real content so running apps are no longer clipped.
                    x1 = marginLeft;
                    x2 = cx2 + marginRight;
                    if (!settings.IsWindows11)
                    {
                        // Extra space for the Windows 10 grab-handle.
                        x2 += Convert.ToInt32(20 * taskbar.ScaleFactor);
                    }
                }
                if (x1 < 0) x1 = 0;
                if (x2 > taskbarWidth) x2 = taskbarWidth;
                if (x2 <= x1)
                {
                    // Degenerate bounds (no detectable content) - fall back to a plain rounded taskbar.
                    x1 = 0;
                    x2 = taskbarWidth;
                }

                mainRegion = LocalPInvoke.CreateRoundRectRgn(
                    x1,
                    marginTop,
                    x2 + 1,
                    taskbarHeight - marginBottom + 1,
                    cornerRadius,
                    cornerRadius
                    );

                // Create an effective region to be applied to the taskbar for the tray
                Types.EffectiveRegion trayEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicTrayLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicTrayLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32((settings.DynamicTrayLayout.MarginLeft * taskbar.ScaleFactor) - (3 * taskbar.ScaleFactor)), // Add extra margin for taskbar left as there's no "padding" provided by Windows and always looks weird as soon as you trim it otherwise.
                    Width = Convert.ToInt32(taskbar.TaskbarRect.Right - taskbar.TaskbarRect.Left - (settings.DynamicTrayLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicTrayLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                Types.EffectiveRegion widgetsEffectiveRegion = new Types.EffectiveRegion
                {
                    CornerRadius = Convert.ToInt32(settings.DynamicWidgetsLayout.CornerRadius * taskbar.ScaleFactor),
                    Top = Convert.ToInt32(settings.DynamicWidgetsLayout.MarginTop * taskbar.ScaleFactor),
                    Left = Convert.ToInt32(settings.DynamicWidgetsLayout.MarginLeft * taskbar.ScaleFactor),
                    Width = Convert.ToInt32(168 * taskbar.ScaleFactor - (settings.DynamicWidgetsLayout.MarginRight * taskbar.ScaleFactor)) + 1,
                    Height = Convert.ToInt32(taskbar.TaskbarRect.Bottom - taskbar.TaskbarRect.Top - (settings.DynamicWidgetsLayout.MarginBottom * taskbar.ScaleFactor)) + 1
                };

                // If the user has it enabled and the tray handle isn't null, create a region for the system tray and merge it with the taskbar region
                if (settings.ShowTray && taskbar.TrayHwnd != IntPtr.Zero)
                {
                    IntPtr trayRegion = LocalPInvoke.CreateRoundRectRgn(
                        (taskbar.TrayRect.Left - taskbar.TaskbarRect.Left) - trayEffectiveRegion.Left,
                        trayEffectiveRegion.Top,
                        trayEffectiveRegion.Width,
                        trayEffectiveRegion.Height,
                        trayEffectiveRegion.CornerRadius,
                        trayEffectiveRegion.CornerRadius
                        );

                    LocalPInvoke.CombineRgn(workingRegion, trayRegion, mainRegion, 2);
                    mainRegion = workingRegion;
                }

                if (settings.ShowWidgets)
                {
                    IntPtr widgetsRegion = LocalPInvoke.CreateRoundRectRgn(
                        widgetsEffectiveRegion.Left,
                        widgetsEffectiveRegion.Top,
                        widgetsEffectiveRegion.Width,
                        widgetsEffectiveRegion.Height,
                        widgetsEffectiveRegion.CornerRadius,
                        widgetsEffectiveRegion.CornerRadius
                        );

                    LocalPInvoke.CombineRgn(workingRegion, widgetsRegion, mainRegion, 2);
                    mainRegion = workingRegion;
                }

                // Apply the final region to the taskbar
                LocalPInvoke.SetWindowRgn(taskbar.TaskbarHwnd, mainRegion, true);
                if (settings.CompositionCompat)
                {
                    Interaction.UpdateTranslucentTB(taskbar.TaskbarHwnd);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        /// <summary>
        /// Checks if there are any new taskbars, or if any taskbars are no longer present.
        /// </summary>
        /// <returns>
        /// a bool indicating success.
        /// </returns>
        public static bool TaskbarCountOrHandleChanged(int taskbarCount, IntPtr mainTaskbarHandle)
        {
            List<IntPtr> currentTaskbars = new List<IntPtr>();
            bool otherTaskbarsExist = true;
            IntPtr hwndPrevious = IntPtr.Zero;
            currentTaskbars.Add(LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_TrayWnd", null));

            if (currentTaskbars[0] == IntPtr.Zero)
            {
                return false;
            }

            if (currentTaskbars[0] != mainTaskbarHandle)
            {
                return true;
            }

            while (otherTaskbarsExist)
            {
                IntPtr hwndCurrent = LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_SecondaryTrayWnd", null);
                hwndPrevious = hwndCurrent;

                if (hwndCurrent == IntPtr.Zero)
                {
                    otherTaskbarsExist = false;
                }
                else
                {
                    currentTaskbars.Add(hwndCurrent);
                }
            }
            if (currentTaskbars.Count != taskbarCount)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the provided update is valid.
        /// </summary>
        /// <returns>
        /// A bool indicating if the update is valid.
        /// </returns>
        public static bool CheckDynamicUpdateIsValid(Types.Taskbar currentTB, Types.Taskbar newTB)
        {
            // REMINDER: newTB will only have rect & hwnd info. Everything else will be null.

            // Check if either of the supplied taskbars are null
            if (currentTB == null || newTB == null)
            {
                return false;
            }

            // Check if the taskbar handles are different
            if (currentTB.TaskbarHwnd != newTB.TaskbarHwnd)
            {
                return false;
            }

            // Get width of app list. Not strictly necessary as the applist is always measured from the left but doing so just in case
            int newAppListWidth = newTB.AppListRect.Right - newTB.AppListRect.Left;
            int currentAppListWidth = currentTB.AppListRect.Right - currentTB.AppListRect.Left;

            if (newTB.AppListRect.Right >= newTB.TrayRect.Left && newTB.TrayRect.Left != 0)
            {
                return false;
            }

            if (newAppListWidth == newTB.TrayRect.Left && newTB.TrayRect.Left != 0)
            {
                return false;
            }

            if (newAppListWidth <= 20 * currentTB.ScaleFactor && newAppListWidth != 0)
            {
                return false;
            }

            if (newAppListWidth >= newTB.TaskbarRect.Right - newTB.TaskbarRect.Left && newAppListWidth != 0)
            {
                return false;
            }

            Debug.WriteLine($"Old width: {currentAppListWidth}\nNew width: {newAppListWidth}");
            return true;
        }

        /// <summary>
        /// Collects information on any currently-present taskbars.
        /// </summary>
        /// <returns>
        /// A list of taskbars populated with information about their size, handles etc.
        /// </returns>
        public static List<Types.Taskbar> GenerateTaskbarInfo()
        {
            List<Types.Taskbar> retVal = new List<Types.Taskbar>();

            IntPtr hwndMain = LocalPInvoke.FindWindowExA(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null); // Find main taskbar
            LocalPInvoke.GetWindowRect(hwndMain, out LocalPInvoke.RECT rectMain); // Get the RECT of the main taskbar
            IntPtr hrgnMain = IntPtr.Zero; // Set recovery region to IntPtr.Zero
            IntPtr hwndTray = LocalPInvoke.FindWindowExA(hwndMain, IntPtr.Zero, "TrayNotifyWnd", null); // Get handle to the main taskbar's tray
            LocalPInvoke.GetWindowRect(hwndTray, out LocalPInvoke.RECT rectTray); // Get the RECT for the main taskbar's tray
            IntPtr hwndAppList = LocalPInvoke.FindWindowExA(LocalPInvoke.FindWindowExA(hwndMain, IntPtr.Zero, "ReBarWindow32", null), IntPtr.Zero, "MSTaskSwWClass", null); // Get the handle to the main taskbar's app list
            LocalPInvoke.GetWindowRect(hwndAppList, out LocalPInvoke.RECT rectAppList);// Get the RECT for the main taskbar's app list

            retVal.Add(new Types.Taskbar
            {
                TaskbarHwnd = hwndMain,
                TrayHwnd = hwndTray,
                AppListHwnd = hwndAppList,
                TaskbarRect = rectMain,
                TrayRect = rectTray,
                AppListRect = rectAppList,
                RecoveryHrgn = hrgnMain,
                ScaleFactor = Convert.ToDouble(LocalPInvoke.GetDpiForWindow(hwndMain)) / 96.00,
                TaskbarRes = $"{rectMain.Right - rectMain.Left} x {rectMain.Bottom - rectMain.Top}",
                Ignored = false
            });
            int style = LocalPInvoke.GetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE).ToInt32();
            if ((style & LocalPInvoke.WS_EX_LAYERED) != LocalPInvoke.WS_EX_LAYERED)
            {
                LocalPInvoke.SetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(hwndMain, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
                LocalPInvoke.SetLayeredWindowAttributes(hwndMain, 0, 255, LocalPInvoke.LWA_ALPHA);
            }




            bool i = true;
            IntPtr hwndPrevious = IntPtr.Zero;
            while (i)
            {
                IntPtr hwndCurrent = LocalPInvoke.FindWindowExA(IntPtr.Zero, hwndPrevious, "Shell_SecondaryTrayWnd", null);
                hwndPrevious = hwndCurrent;

                if (hwndCurrent == IntPtr.Zero)
                {
                    i = false;
                }
                else
                {
                    LocalPInvoke.GetWindowRect(hwndCurrent, out LocalPInvoke.RECT rectCurrent);
                    LocalPInvoke.GetWindowRgn(hwndCurrent, out IntPtr hrgnCurrent);
                    Interaction interaction = new Interaction();
                    IntPtr hwndSecTray = IntPtr.Zero;
                    if (interaction.IsWindows11())
                    {
                        IntPtr imd = LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                        hwndSecTray = LocalPInvoke.FindWindowExA(hwndCurrent, imd, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                    }
                    else
                    {
                        hwndSecTray = LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "TrayNotifyWnd", null); // Get handle to this secondary taskbar's tray
                    }
                    LocalPInvoke.GetWindowRect(hwndTray, out LocalPInvoke.RECT rectSecTray); // Get the RECT for this secondary taskbar's tray
                    IntPtr hwndSecAppList = LocalPInvoke.FindWindowExA(LocalPInvoke.FindWindowExA(hwndCurrent, IntPtr.Zero, "WorkerW", null), IntPtr.Zero, "MSTaskListWClass", null); // Get the handle to the main taskbar's app list
                    LocalPInvoke.GetWindowRect(hwndSecAppList, out LocalPInvoke.RECT rectSecAppList);// Get the RECT for this secondary taskbar's app list
                    retVal.Add(new Types.Taskbar
                    {
                        TaskbarHwnd = hwndCurrent,
                        TrayHwnd = hwndSecTray,
                        AppListHwnd = hwndSecAppList,
                        TaskbarRect = rectCurrent,
                        TrayRect = rectSecTray,
                        AppListRect = rectSecAppList,
                        RecoveryHrgn = hrgnCurrent,
                        ScaleFactor = Convert.ToDouble(LocalPInvoke.GetDpiForWindow(hwndCurrent)) / 96.00,
                        TaskbarRes = $"{rectCurrent.Right - rectCurrent.Left} x {rectCurrent.Bottom - rectCurrent.Top}",
                        Ignored = false
                    });
                    style = LocalPInvoke.GetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE).ToInt32();
                    if ((style & LocalPInvoke.WS_EX_LAYERED) != LocalPInvoke.WS_EX_LAYERED)
                    {
                        LocalPInvoke.SetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE, LocalPInvoke.GetWindowLong(hwndCurrent, LocalPInvoke.GWL_EXSTYLE).ToInt32() ^ LocalPInvoke.WS_EX_LAYERED);
                        LocalPInvoke.SetLayeredWindowAttributes(hwndCurrent, 0, 255, LocalPInvoke.LWA_ALPHA);
                    }
                }
            }

            //foreach (var tb in retVal)
            //{
            //    TaskbarShouldBeFilled(tb.TaskbarHwnd);
            //}
            return retVal;
        }

        /// <summary>
        /// Checks if the given taskbar should be filled to the edge of the screen.
        /// </summary>
        /// <returns>
        /// A bool indicating whether or not the taskbar needs to be filled.
        /// </returns>
        public static bool TaskbarShouldBeFilled(IntPtr taskbarHwnd, Types.Settings settings)
        {
            bool retVal = false;

            if (settings.FillOnMaximise)
            {
                // Attempt to check for if alt+tab/task switcher is open (Windows 11 only)
                IntPtr topHwnd = LocalPInvoke.WindowFromPoint(new LocalPInvoke.POINT() { x = 0, y = 0 });
                StringBuilder windowClass = new StringBuilder(1024);
                try
                {
                    LocalPInvoke.GetClassName(topHwnd, windowClass, 1024);

                    if (windowClass.ToString() == "XamlExplorerHostIslandWindow" && settings.FillOnTaskSwitch)
                    {
                        return true;
                    }
                }
                catch (Exception) { }

                List<IntPtr> windowList = Interaction.GetTopLevelWindows();
                foreach (IntPtr windowHwnd in windowList)
                {
                    if (LocalPInvoke.IsWindowVisible(windowHwnd))
                    {
                        if (LocalPInvoke.MonitorFromWindow(taskbarHwnd, 2) == LocalPInvoke.MonitorFromWindow(windowHwnd, 2))
                        {
                            LocalPInvoke.DwmGetWindowAttribute(windowHwnd, LocalPInvoke.DWMWINDOWATTRIBUTE.Cloaked, out bool isCloaked, 0x4);
                            if (!isCloaked)
                            {
                                LocalPInvoke.WINDOWPLACEMENT lpwndpl = new LocalPInvoke.WINDOWPLACEMENT();
                                LocalPInvoke.GetWindowPlacement(windowHwnd, ref lpwndpl);
                                if (lpwndpl.ShowCmd == LocalPInvoke.ShowWindowCommands.ShowMaximized)
                                {
                                    retVal = true;
                                }
                            }
                        }
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the real horizontal span of the taskbar's app-list content (Start button, search,
        /// task view and app buttons) via UI Automation. On Windows 11 22H2+ the content is rendered
        /// by the taskbar's XAML island and the legacy window rects no longer track it.
        /// </summary>
        /// <returns>
        /// true and the content bounds (screen coordinates) if buttons could be enumerated;
        /// false if UIA is unavailable or no buttons were found.
        /// </returns>
        public static bool GetTrueTaskbarContentBounds(Types.Taskbar taskbar, out int contentLeft, out int contentRight)
        {
            contentLeft = -1;
            contentRight = -1;
            try
            {
                AutomationElement taskbarElement = AutomationElement.FromHandle(taskbar.TaskbarHwnd);
                if (taskbarElement == null)
                {
                    return false;
                }

                int minLeft = int.MaxValue;
                int maxRight = int.MinValue;
                bool found = false;
                foreach (AutomationElement element in taskbarElement.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
                {
                    string automationId;
                    try { automationId = element.Current.AutomationId; }
                    catch (Exception) { continue; }

                    // Only the app-list content is relevant; the tray / notify icons are excluded by id.
                    if (string.IsNullOrEmpty(automationId)) continue;
                    if (automationId != "StartButton" && automationId != "SearchButton" &&
                        automationId != "TaskViewButton" && !automationId.StartsWith("Appid:"))
                    {
                        continue;
                    }

                    System.Windows.Rect bounds;
                    bool offscreen;
                    try { bounds = element.Current.BoundingRectangle; offscreen = element.Current.IsOffscreen; }
                    catch (Exception) { continue; }
                    // 跳过隐藏/溢出(进入溢出菜单)的按钮,避免其位置把内容右边界撑大。
                    if (offscreen || bounds.Width <= 0 || bounds.Height <= 0) continue;

                    if (bounds.Left < minLeft) minLeft = (int)bounds.Left;
                    if (bounds.Right > maxRight) maxRight = (int)bounds.Right;
                    found = true;
                }

                if (!found) return false;
                contentLeft = minLeft;
                contentRight = maxRight;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sets the appbar properties of the taskbar.
        /// </summary>
        public static void SetTaskbarState(LocalPInvoke.AppBarStates option, IntPtr hwnd)
        {
            LocalPInvoke.APPBARDATA msgData = new LocalPInvoke.APPBARDATA();
            msgData.cbSize = (uint)Marshal.SizeOf(msgData);
            msgData.hWnd = hwnd;
            msgData.lParam = (int)option;
            LocalPInvoke.SHAppBarMessage(LocalPInvoke.ABM.SetState, ref msgData);
        }


    }
}
