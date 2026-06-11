#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class SystemTrayManager : MonoBehaviour
{
    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int ExtractIconExW(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, [MarshalAs(UnmanagedType.LPWStr)] string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    private const uint PM_REMOVE = 0x0001;
    private const uint WM_QUIT = 0x0012;

    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_TIP = 0x00000004;
    private const int IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const int WM_USER_TRAY = 0x8001;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint CW_USEDEFAULT = 0x80000000;
    private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

    private static readonly IntPtr IDI_APPLICATION = new IntPtr(32512);

    private const int IDM_SETTINGS = 1;
    private const int IDM_MODEL = 2;
    private const int IDM_ANIMATION = 3;
    private const int IDM_EXPRESSION = 4;
    private const int IDM_HISTORY = 5;
    private const int IDM_EXIT = 7;

    #endregion

    public TransparentWindow? windowController;

    public event Action<int>? OnOpenPanel;
    public event Action? OnToggleWindow;
    public event Action? OnExit;

    private IntPtr _trayHwnd;
    private IntPtr _hIcon;
    private IntPtr _mainHwnd;
    private IntPtr _handleHwnd;
    private Thread? _messageThread;
    private volatile bool _running;
    private GCHandle _wndProcHandle;

    void Start()
    {
#if !UNITY_EDITOR
        _mainHwnd = GetActiveWindow();
        _running = true;
        _messageThread = new Thread(MessageLoop);
        _messageThread.IsBackground = true;
        _messageThread.Start();

        for (int i = 0; i < 50 && _trayHwnd == IntPtr.Zero; i++)
            Thread.Sleep(20);
        Debug.Log("[Tray] Start completed, hwnd=" + (_trayHwnd != IntPtr.Zero ? "ok" : "FAIL"));
#endif
    }

    void OnDestroy()
    {
#if !UNITY_EDITOR
        _running = false;
        if (_trayHwnd != IntPtr.Zero)
        {
            PostMessageW(_trayHwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            RemoveTrayIcon();
        }
        if (_handleHwnd != IntPtr.Zero) { DestroyWindow(_handleHwnd); _handleHwnd = IntPtr.Zero; }
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        if (_wndProcHandle.IsAllocated) _wndProcHandle.Free();
        try { _messageThread?.Join(1000); } catch { }
#endif
    }

    void OnApplicationQuit()
    {
        RemoveTrayIcon();
    }

    private void RemoveTrayIcon()
    {
        if (_trayHwnd == IntPtr.Zero) return;
        var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _trayHwnd, uID = 1 };
        Shell_NotifyIcon(NIM_DELETE, ref nid);
    }

    private void MessageLoop()
    {
        IntPtr hInstance = GetModuleHandleW(null);

        var wndProc = new WndProcDelegate(WndProc);
        _wndProcHandle = GCHandle.Alloc(wndProc);

        var wcx = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = hInstance,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszClassName = "AliceBotTrayClass"
        };

        ushort atom = RegisterClassExW(ref wcx);
        if (atom == 0)
        {
            Debug.LogError("[Tray] RegisterClassEx failed: " + Marshal.GetLastWin32Error());
            _wndProcHandle.Free();
            return;
        }
        Debug.Log("[Tray] RegisterClassEx ok, atom=" + atom);

        _trayHwnd = CreateWindowExW(0, "AliceBotTrayClass", "AliceBotTray", 0,
            unchecked((int)CW_USEDEFAULT), unchecked((int)CW_USEDEFAULT), unchecked((int)CW_USEDEFAULT), unchecked((int)CW_USEDEFAULT),
            HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_trayHwnd == IntPtr.Zero)
        {
            Debug.LogError("[Tray] CreateWindowEx failed: " + Marshal.GetLastWin32Error());
            _wndProcHandle.Free();
            return;
        }
        Debug.Log("[Tray] CreateWindowEx ok");

        // Create 1x1 popup window for menu dismiss
        _handleHwnd = CreateWindowExW(0, "AliceBotTrayClass", "AliceBotHandle", 0u,
            0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        CreateTrayIcon();

        while (_running)
        {
            if (PeekMessageW(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                if (msg.message == WM_QUIT) break;
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        DestroyWindow(_trayHwnd);
        _trayHwnd = IntPtr.Zero;
        if (_handleHwnd != IntPtr.Zero) { DestroyWindow(_handleHwnd); _handleHwnd = IntPtr.Zero; }
    }

    private void CreateTrayIcon()
    {
        _hIcon = LoadTrayIcon();
        if (_hIcon == IntPtr.Zero)
        {
            Debug.LogError("[Tray] Failed to load any icon, tray will be invisible");
        }
        else
        {
            Debug.Log("[Tray] Icon loaded, hIcon=" + _hIcon);
        }

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _trayHwnd,
            uID = 1,
            uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
            uCallbackMessage = WM_USER_TRAY,
            hIcon = _hIcon,
            szTip = "AliceBot"
        };

        bool ok = Shell_NotifyIcon(NIM_ADD, ref nid);
        if (!ok)
            Debug.LogError("[Tray] Shell_NotifyIcon NIM_ADD failed: " + Marshal.GetLastWin32Error());
        else
            Debug.Log("[Tray] System tray icon created");
    }

    private IntPtr LoadTrayIcon()
    {
        string iconPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.streamingAssetsPath, "app.ico"));
        Debug.Log("[Tray] Loading icon: " + iconPath + "  exists=" + System.IO.File.Exists(iconPath));

        // Use SHGetFileInfo - the Windows shell icon handler supports all ICO formats
        var shInfo = new SHFILEINFO();
        IntPtr result = SHGetFileInfoW(iconPath, 0, ref shInfo, (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_SMALLICON);
        if (result != IntPtr.Zero && shInfo.hIcon != IntPtr.Zero)
        {
            Debug.Log("[Tray] SHGetFileInfo success, hIcon=" + shInfo.hIcon);
            return shInfo.hIcon;
        }

        Debug.LogWarning("[Tray] SHGetFileInfo failed. Trying IDI_APPLICATION.");
        return LoadIconW(IntPtr.Zero, IDI_APPLICATION);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_USER_TRAY)
        {
            int eventType = (int)lParam;
            if (eventType == WM_LBUTTONUP)
            {
                UnityMainThreadDispatcher.Enqueue(() => OnToggleWindow?.Invoke());
            }
            else if (eventType == WM_RBUTTONUP)
            {
                ShowContextMenu();
            }
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_SETTINGS), "设置");
        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_MODEL), "模型管理");
        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_ANIMATION), "动画库");
        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_EXPRESSION), "情绪映射");
        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_HISTORY), "对话记录");
        AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, "");
        AppendMenuW(hMenu, MF_STRING, new IntPtr(IDM_EXIT), "退出");

        if (_handleHwnd != IntPtr.Zero)
            SetForegroundWindow(_handleHwnd);

        GetCursorPos(out POINT pt);
        int cmdId = TrackPopupMenu(hMenu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | TPM_RETURNCMD, pt.x, pt.y, 0, _handleHwnd, IntPtr.Zero);
        PostMessageW(_handleHwnd, 0 /*WM_NULL*/, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(hMenu);

        if (cmdId > 0)
        {
            int id = cmdId;
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                switch (id)
                {
                    case IDM_SETTINGS: OnOpenPanel?.Invoke(0); break;
                    case IDM_MODEL: OnOpenPanel?.Invoke(2); break;
                    case IDM_ANIMATION: OnOpenPanel?.Invoke(3); break;
                    case IDM_EXPRESSION: OnOpenPanel?.Invoke(4); break;
                    case IDM_HISTORY: OnOpenPanel?.Invoke(5); break;
                    case IDM_EXIT: OnExit?.Invoke(); break;
                }
            });
        }
    }
}
