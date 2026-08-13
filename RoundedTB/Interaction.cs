using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RoundedTB
{
    public class Interaction
    {
        public MainWindow mw;

        public Interaction(MainWindow mw = null)
        {
            this.mw = mw;
        }

        /// <summary>
        /// Value MarginBasic carried in pre-per-segment configs to mean "the four margins below are
        /// set individually". Any other value applied to all four sides. (移植自 gniang Phase 1)
        /// </summary>
        private const int LegacyAdvancedMarginSentinel = -384;

        /// <summary>
        /// Populating over the defaults rather than deserialising fresh: a key the file doesn't
        /// mention keeps the value it would have had on a first launch instead of dropping to
        /// 0/false, and an explicit null doesn't wipe a layout out. (移植自 gniang Phase 1)
        /// </summary>
        private static readonly JsonSerializerSettings PopulateOverDefaults = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
        };

        public Types.Settings ReadJSON()
        {
            Types.Settings settings = CreateDefaultSettings(mw.isWindows11);
            JObject rawSettings = null;
            try
            {
                string jsonSettings = File.ReadAllText(mw.configPath);
                rawSettings = JObject.Parse(jsonSettings);
                JsonConvert.PopulateObject(jsonSettings, settings, PopulateOverDefaults);
            }
            catch (Exception ex)
            {
                // An empty or corrupt config file must not prevent the app from starting. Start over
                // from the defaults, since population may have stopped part-way through.
                Debug.WriteLine($"Failed to read settings, falling back to defaults: {ex.Message}");
                AddLog($"Failed to read settings, falling back to defaults: {ex.Message}");
                settings = CreateDefaultSettings(mw.isWindows11);
                rawSettings = null;
            }

            MigrateLegacySettings(settings, rawSettings, mw.isWindows11);
            return settings;
        }

        /// <summary>
        /// Carries settings written by older versions of RoundedTB over to the current schema, for
        /// the cases a straight populate can't express: fields that were renamed or restructured.
        /// Anything the file has no value for at all is already sitting at its first-launch default.
        /// (移植自 gniang Phase 1)
        /// </summary>
        private void MigrateLegacySettings(Types.Settings settings, JObject raw, bool isWindows11)
        {
            if (raw == null)
            {
                return;
            }

            Types.Settings defaults = CreateDefaultSettings(isWindows11);

            // Before the per-segment layouts, one corner radius and one margin set covered the whole
            // taskbar. Spread those across every segment so the look is preserved.
            if (raw["SimpleTaskbarLayout"] == null && raw["CornerRadius"] != null)
            {
                int cornerRadius = raw.Value<int?>("CornerRadius") ?? defaults.SimpleTaskbarLayout.CornerRadius;
                int marginBasic = raw.Value<int?>("MarginBasic") ?? LegacyAdvancedMarginSentinel;

                int marginTop, marginLeft, marginRight, marginBottom;
                if (marginBasic != LegacyAdvancedMarginSentinel)
                {
                    marginTop = marginLeft = marginRight = marginBottom = marginBasic;
                }
                else
                {
                    marginTop = raw.Value<int?>("MarginTop") ?? defaults.SimpleTaskbarLayout.MarginTop;
                    marginLeft = raw.Value<int?>("MarginLeft") ?? defaults.SimpleTaskbarLayout.MarginLeft;
                    marginRight = raw.Value<int?>("MarginRight") ?? defaults.SimpleTaskbarLayout.MarginRight;
                    marginBottom = raw.Value<int?>("MarginBottom") ?? defaults.SimpleTaskbarLayout.MarginBottom;
                }

                settings.SimpleTaskbarLayout = LegacyLayout(cornerRadius, marginTop, marginLeft, marginRight, marginBottom);
                settings.DynamicAppListLayout = LegacyLayout(cornerRadius, marginTop, marginLeft, marginRight, marginBottom);
                settings.DynamicTrayLayout = LegacyLayout(cornerRadius, marginTop, marginLeft, marginRight, marginBottom);
                settings.DynamicWidgetsLayout = LegacyLayout(cornerRadius, marginTop, marginLeft, marginRight, marginBottom);

                AddLog("Migrated pre-3.0 layout settings.");
                Debug.WriteLine("Migrated pre-3.0 layout settings");
            }

            // ShowTrayOnHover was renamed to ShowSegmentsOnHover when widgets gained the same behaviour.
            if (raw["ShowSegmentsOnHover"] == null && raw["ShowTrayOnHover"] != null)
            {
                settings.ShowSegmentsOnHover = raw.Value<bool?>("ShowTrayOnHover") ?? defaults.ShowSegmentsOnHover;
            }
        }

        private static Types.SegmentSettings LegacyLayout(int cornerRadius, int top, int left, int right, int bottom)
        {
            return new Types.SegmentSettings
            {
                CornerRadius = cornerRadius,
                MarginTop = top,
                MarginLeft = left,
                MarginRight = right,
                MarginBottom = bottom
            };
        }

        public static bool IsWindows11()
        {
            try
            {
                // .NET Core + 无 supportedOS manifest 时 Environment.OSVersion 返回兼容版本
                // (如 9600),必须用注册表真实构建号判断(与 MainWindow 的 isWindows11 一致)。
                using (RegistryKey ver = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    object b = ver?.GetValue("CurrentBuild");
                    return b != null && Convert.ToInt32(b) >= 21996;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void WriteJSON()
        {
            // Write to a temporary file first, then swap it in atomically. Truncating the real config
            // up front (File.Create) meant a crash mid-write left the user with an empty settings file.
            // (移植自 gniang Phase 1)
            string tempPath = mw.configPath + ".tmp";
            string json = JsonConvert.SerializeObject(mw.activeSettings, Formatting.Indented);

            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(mw.configPath))
                {
                    File.Replace(tempPath, mw.configPath, null);
                }
                else
                {
                    File.Move(tempPath, mw.configPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write settings: {ex.Message}");
                AddLog($"Failed to write settings: {ex.Message}");
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Builds the default settings for the given OS. Used on first launch and when the
        /// config file turns out to be missing or unreadable. (移植自 gniang Phase 1)
        /// </summary>
        public static Types.Settings CreateDefaultSettings(bool isWindows11)
        {
            if (isWindows11)
            {
                return new Types.Settings()
                {
                    SimpleTaskbarLayout = new Types.SegmentSettings { CornerRadius = 8, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                    DynamicAppListLayout = new Types.SegmentSettings { CornerRadius = 8, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                    DynamicTrayLayout = new Types.SegmentSettings { CornerRadius = 8, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
                    DynamicWidgetsLayout = new Types.SegmentSettings { CornerRadius = 8, MarginLeft = 3, MarginTop = 3, MarginRight = 3, MarginBottom = 3 },
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

            return new Types.Settings()
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

        public void FileSystem()
        {
            // 注:未移植 gniang 的 %LOCALAPPDATA%\RoundedTB\ 配置路径迁移(见 PROGRESS.md TODO),
            // rtb.json 仍保留在 %LOCALAPPDATA% 根目录,以兼容老版本/降级。等配置 schema 与老版本
            // 差异变大时再搬。

            try
            {
                File.Create(mw.logPath).Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create log file: {ex.Message}");
            }

            bool configUsable = false;
            try
            {
                configUsable = File.Exists(mw.configPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(mw.configPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to inspect settings file: {ex.Message}");
            }

            if (!configUsable)
            {
                mw.activeSettings = CreateDefaultSettings(mw.isWindows11);
                WriteJSON(); // butts - Missy Quarry, 2020
            }
        }

        public static bool SetWorkspace(LocalPInvoke.RECT rect)
        {
            bool result = LocalPInvoke.SystemParametersInfo(LocalPInvoke.SPI_SETWORKAREA, 0, ref rect, LocalPInvoke.SPIF_change);
            if (!result)
            {
                // Get error
                Debug.WriteLine("Error setting work area: " + Marshal.GetLastWin32Error().ToString());
            }

            return result;
        }

        public void AddLog(string message)
        {
            try
            {
                if (mw != null && !string.IsNullOrEmpty(mw.logPath))
                {
                    File.AppendAllText(mw.logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch (Exception)
            {
                // 日志失败不影响主功能
            }
        }

        public static bool IsTranslucentTBRunning()
        {
            Mutex mutex = null;
            try
            {
                return Mutex.TryOpenExisting("344635E9-9AE4-4E60-B128-D53E25AB70A7", out mutex);
            }
            finally
            {
                mutex?.Dispose();
            }
        }

        /// <summary>
        /// 由 Background 在 AutoHide 淡入/淡出动画期间置位,抑制对 TranslucentTB 的 force-refresh。
        /// </summary>
        public static bool SuppressTranslucentRefresh;

        /// <summary>
        /// 请求 TranslucentTB 重刷任务栏外观。基于 TTB v4 源码(taskbarattributeworker.cpp:262-276)
        /// 加了保护:
        /// 1) TTB 未运行:TTB_WorkerWindow 不存在,发送无意义。
        /// 2) Win11:TTB 的 TTB_ForceRefreshTaskbar 在有 m_TaskbarService(XAML 任务栏)时是 no-op。
        /// 3) SuppressTranslucentRefresh(AutoHide 淡入/淡出动画中):Win10 上 force-refresh 会触发
        ///    WM_DWMCOMPOSITIONCHANGED ×2 让 Explorer 重组任务栏回默认外观(alpha→255),把淡出拉回,
        ///    淡出期间反复发送正是闪烁放大器。
        /// </summary>
        public static IntPtr UpdateTranslucentTB(IntPtr taskbarHwnd)
        {
            if (SuppressTranslucentRefresh) return IntPtr.Zero;
            if (!IsTranslucentTBRunning()) return IntPtr.Zero;
            if (IsWindows11()) return IntPtr.Zero;
            return LocalPInvoke.SendMessage(LocalPInvoke.FindWindow("TTB_WorkerWindow", "TTB_WorkerWindow"), LocalPInvoke.RegisterWindowMessage("TTB_ForceRefreshTaskbar"), 0, taskbarHwnd);
        }
        public IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            switch (msg)
            {
                case WM_HOTKEY:
                    Debug.WriteLine(msg);
                    switch (wParam.ToInt32())
                    {
                        case 9000:
                            int vkey = ((int)lParam >> 16) & 0xFFFF;
                            Debug.WriteLine(vkey);
                            if (vkey == 0x71)
                            {
                                if (mw.showTrayCheckBox.IsChecked == true)
                                {
                                    mw.showTrayCheckBox.IsChecked = false;
                                }
                                else
                                {
                                    mw.showTrayCheckBox.IsChecked = true;
                                }
                                mw.ApplyButton_Click(null, null);
                            }
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        public static bool IsAutoHideEnabled()
        {
            return Math.Abs(SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height) > 0;
        }

        public bool IsTaskbarVisibleOnMonitor(LocalPInvoke.RECT tbRectP, LocalPInvoke.RECT monitorRectP)
        {
            Rectangle tbRect = new Rectangle(tbRectP.Left + 3, tbRectP.Top + 3, tbRectP.Right - tbRectP.Left - 3, tbRectP.Bottom - tbRectP.Top - 3);
            Rectangle monitorRect = new Rectangle(monitorRectP.Left, monitorRectP.Top, monitorRectP.Right - monitorRectP.Left, monitorRectP.Bottom - monitorRectP.Top);
            return tbRect.IntersectsWith(monitorRect);
        }

        public delegate bool CallBack(int hwnd, int lParam);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        public static List<IntPtr> GetTopLevelWindows()
        {
            List<IntPtr> AllActiveHandles = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(AllActiveHandles);
            try
            {
                EnumWindowsProc tlProc = new EnumWindowsProc(EnumWindow);
                LocalPInvoke.EnumWindows(tlProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                {
                    listHandle.Free();
                }
            }
            return AllActiveHandles;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            if (!(gch.Target is List<IntPtr> list))
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }
            list.Add(handle);
            return true;
        }

        public static bool TaskbarOnMonitorWithMaximisedWindow(IntPtr taskbarHwnd)
        {
            return true;
        }

        public enum TaskbarPosition
        {
            Unknown = -1,
            Left,
            Top,
            Right,
            Bottom,
        }

        public sealed class Taskbar
        {
            public Rectangle Bounds
            {
                get;
                private set;
            }
            public TaskbarPosition Position
            {
                get;
                private set;
            }
            public System.Drawing.Point Location
            {
                get
                {
                    return Bounds.Location;
                }
            }
            public System.Drawing.Size Size
            {
                get
                {
                    return Bounds.Size;
                }
            }

            //Always returns false under Windows 7
            public bool AlwaysOnTop
            {
                get;
                private set;
            }
            public bool AutoHide
            {
                get;
                private set;
            }

            public Taskbar(IntPtr taskbarHandle)
            {

                LocalPInvoke.APPBARDATA data = new LocalPInvoke.APPBARDATA();
                data.cbSize = (uint)Marshal.SizeOf(typeof(LocalPInvoke.APPBARDATA));
                data.hWnd = taskbarHandle;
                IntPtr result = LocalPInvoke.SHAppBarMessage(LocalPInvoke.ABM.GetTaskbarPos, ref data);
                Position = (TaskbarPosition)data.uEdge;
                Bounds = Rectangle.FromLTRB(data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);

                data.cbSize = (uint)Marshal.SizeOf(typeof(LocalPInvoke.APPBARDATA));
                result = LocalPInvoke.SHAppBarMessage(LocalPInvoke.ABM.GetState, ref data);
                int state = result.ToInt32();
                AlwaysOnTop = (state & LocalPInvoke.ABS.AlwaysOnTop) == LocalPInvoke.ABS.AlwaysOnTop;
                AutoHide = (state & LocalPInvoke.ABS.Autohide) == LocalPInvoke.ABS.Autohide;
            }
        }
    }
}
