using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

public enum EnumWinStyle
{
    WinTop,
    WinTopApha,
    WinTopAphaPenetrate
}

public class TransparentWindow : MonoBehaviour
{
    #region Win32 Interop

    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern int SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

    [DllImport("Dwmapi.dll")]
    static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int WS_POPUP = 0x800000;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    #endregion

    public EnumWinStyle WinStyle = EnumWinStyle.WinTopAphaPenetrate;

    public int ResWidth = 800;
    public int ResHeight = 800;

    public int currentX = 0;
    public int currentY = 0;

    private IntPtr hwnd = IntPtr.Zero;
    private bool _transparentEnabled;
    private bool _ctrlWasDown;

    void Start()
    {
#if !UNITY_EDITOR
        SettingsData settings = SettingsData.Load();

        // Match primary display resolution on startup
        int displayW = Display.main.systemWidth;
        int displayH = Display.main.systemHeight;

        ResWidth = (settings.winWidth > 0) ? settings.winWidth : displayW;
        ResHeight = (settings.winHeight > 0) ? settings.winHeight : displayH;
        currentX = (settings.winX > 0) ? settings.winX : 0;
        currentY = (settings.winY > 0) ? settings.winY : 0;

        Application.runInBackground = true;
        Screen.fullScreen = false;

        hwnd = GetActiveWindow();
        StartCoroutine(ApplyWindowStyleDelayed());
#endif
    }

    IEnumerator ApplyWindowStyleDelayed()
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        Screen.SetResolution(ResWidth, ResHeight, FullScreenMode.Windowed);
        yield return null;

        // Transparent borderless window
        SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED);
        int intExTemp = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, intExTemp | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_BORDER & ~WS_CAPTION);
        SetWindowPos(hwnd, -1, currentX, currentY, ResWidth, ResHeight, SWP_SHOWWINDOW);

        yield return null;
        var margins = new MARGINS() { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        _transparentEnabled = true;
    }

    private void SetTransparent(bool enable)
    {
        if (hwnd == IntPtr.Zero || _transparentEnabled == enable) return;
        _transparentEnabled = enable;
        if (enable)
            SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
        else
            SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_TRANSPARENT));
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (hwnd == IntPtr.Zero) return;

        bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        // Ctrl pressed → temporarily disable penetration for interaction
        if (ctrlDown && !_ctrlWasDown)
        {
            SetTransparent(false);
        }

        // Ctrl+drag to move the window
        if (ctrlDown && Input.GetMouseButtonDown(0))
        {
            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        // Ctrl released → restore penetration
        if (!ctrlDown && _ctrlWasDown)
        {
            SetTransparent(true);
        }

        _ctrlWasDown = ctrlDown;
#endif
    }

    public void ShowAppWindow()
    {
#if !UNITY_EDITOR
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_SHOW);
#endif
    }

    public void HideAppWindow()
    {
#if !UNITY_EDITOR
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, SW_HIDE);
#endif
    }

    public bool IsWindowVisible()
    {
#if !UNITY_EDITOR
        if (hwnd != IntPtr.Zero)
        {
            GetWindowRect(hwnd, out RECT r);
            return (GetWindowLong(hwnd, GWL_STYLE) & 0x10000000) == 0; // WS_VISIBLE check (approx)
        }
#endif
        return true;
    }

    public void SetWindowSize(int w, int h)
    {
        ResWidth = w;
        ResHeight = h;
#if !UNITY_EDITOR
        if (hwnd != IntPtr.Zero)
        {
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            SetWindowPos(hwnd, -1, currentX, currentY, w, h, SWP_SHOWWINDOW);
        }
#endif
    }

    public void GetWindowPosition(out int x, out int y)
    {
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
        {
            x = rect.Left;
            y = rect.Top;
        }
        else
        {
            x = currentX;
            y = currentY;
        }
    }
}
