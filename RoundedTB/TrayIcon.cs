using System;
using System.Runtime.InteropServices;

namespace RoundedTB
{
    /// <summary>
    /// 基于标准 Win32 Shell_NotifyIcon 的托盘图标(参考 TranslucentTB 的 tray/trayicon.cpp 实现)。
    /// 不依赖 WPFUI 的内置 NotifyIcon——后者在 Win11 26H1 等新任务栏上图标不显示/显示错误。
    /// 通过宿主窗口的回调消息接收鼠标事件(左键点击 / 右键弹出菜单)。
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private const uint NIM_ADD = 0;
        private const uint NIM_MODIFY = 1;
        private const uint NIM_DELETE = 2;

        private const uint NIF_MESSAGE = 0x01;
        private const uint NIF_ICON = 0x02;
        private const uint NIF_TIP = 0x04;

        // 回调消息(WM_APP + 5);lParam 为具体鼠标消息
        private const uint WM_TRAYICON = 0x8005;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_CONTEXTMENU = 0x007B;

        private readonly IntPtr _hwnd;
        private readonly uint _id;
        private NOTIFYICONDATA _nid;

        /// <summary>Explorer 重启(TaskbarCreated)后 Shell 要求重新添加托盘图标。</summary>
        private static readonly uint WM_TASKBARCREATED = (uint)LocalPInvoke.RegisterWindowMessage("TaskbarCreated");

        /// <summary>左键单击托盘图标(显示设置窗口)。</summary>
        public event Action LeftClick;

        /// <summary>右键单击托盘图标(弹出菜单)。</summary>
        public event Action RightClick;

        public TrayIcon(IntPtr hwnd)
        {
            _hwnd = hwnd;
            // 用窗口句柄生成一个稳定的唯一 id(低 16 位,保留高位给系统)。
            _id = ((uint)hwnd & 0xFFFF) | 0x8000;

            _nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = _id,
                uFlags = NIF_MESSAGE | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                szTip = "RoundedTB Revived",
            };
        }

        public void Show()
        {
            Shell_NotifyIcon(NIM_ADD, ref _nid);
            // 使用较新的 NOTIFYICONDATA 版本,获得右键/新版行为。
            _nid.uVersion = 4;
            Shell_NotifyIcon(0x00000006 /*NIM_SETVERSION*/, ref _nid);
        }

        /// <summary>设置图标与提示文字(带主题切换时调用)。</summary>
        public void SetIcon(IntPtr hIcon, string tip)
        {
            _nid.hIcon = hIcon;
            _nid.uFlags |= NIF_ICON | NIF_TIP;
            _nid.szTip = tip ?? "RoundedTB Revived";
            Shell_NotifyIcon(NIM_MODIFY, ref _nid);
        }

        public void SetTip(string tip)
        {
            _nid.uFlags |= NIF_TIP;
            _nid.szTip = tip ?? "RoundedTB Revived";
            Shell_NotifyIcon(NIM_MODIFY, ref _nid);
        }

        /// <summary>删除托盘图标。</summary>
        public void Delete()
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
        }

        /// <summary>
        /// 处理宿主窗口的消息。返回 true 表示已消费。
        /// 在窗口 HwndSource hook 里调用。
        /// </summary>
        public bool HandleWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == (int)WM_TASKBARCREATED)
            {
                // Explorer 重启后重新添加图标(数据仍在 _nid 里)。
                Shell_NotifyIcon(NIM_ADD, ref _nid);
                return false;
            }

            if (msg == (int)WM_TRAYICON && (uint)wParam == _id)
            {
                uint mouse = (uint)lParam;
                switch (mouse)
                {
                    case WM_LBUTTONUP:
                        LeftClick?.Invoke();
                        return true;
                    case WM_RBUTTONUP:
                    case WM_CONTEXTMENU:
                        RightClick?.Invoke();
                        return true;
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            Delete();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion; // 与 uTimeout 共用
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);
    }
}
