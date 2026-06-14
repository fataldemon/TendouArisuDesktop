using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

[ComImport, Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITaskbarList { void HrInit(); void AddTab(IntPtr hwnd); void DeleteTab(IntPtr hwnd); void ActivateTab(IntPtr hwnd); void SetActiveAlt(IntPtr hwnd); }

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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int WS_POPUP = 0x800000;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_NOSIZE = 0x0001;
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
    public bool IsDraggingWindow => _isDraggingWindow;

    public SystemTrayManager trayManager;
    public GameStart gameStart;

    public event Action? OnDragStart;
    public event Action? OnDragEnd;

    private int _realWidth, _realHeight, _realX, _realY;
    private IntPtr hwnd = IntPtr.Zero;
    private bool _transparentEnabled;
    private bool _ctrlWasDown;
    private bool _shiftWasDown;
    private bool _wasLeftDown;
    private bool _isDraggingWindow;
    private bool _dragPending;
    private POINT _dragStartCursor;
    private POINT _lastDragCursor;
    private int _dragStartWindowX, _dragStartWindowY;

    void Start()
    {
#if !UNITY_EDITOR
        SettingsData settings = SettingsData.Load();

        int displayW = Display.main.systemWidth;
        int displayH = Display.main.systemHeight;

        _realWidth = (settings.winWidth > 0) ? settings.winWidth : displayW;
        _realHeight = (settings.winHeight > 0) ? settings.winHeight : displayH;
        _realX = (settings.winX > 0) ? settings.winX : 0;
        _realY = (settings.winY > 0) ? settings.winY : 0;
        _realHeight += 400;

        Application.runInBackground = true;
        Screen.fullScreen = false;

        hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        StartCoroutine(ApplyWindowStyleDelayed());
#endif
    }

    IEnumerator ApplyWindowStyleDelayed()
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        // Bring to foreground briefly so style changes apply reliably
        SetForegroundWindow(hwnd);
        yield return null;

        Screen.SetResolution(_realWidth, _realHeight, FullScreenMode.Windowed);
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_BORDER & ~WS_CAPTION);
        SetWindowPos(hwnd, -1, _realX, _realY, _realWidth, _realHeight, SWP_SHOWWINDOW);
        currentX = _realX;
        currentY = _realY;

        yield return null;
        var margins = new MARGINS() { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        try
        {
            var tbl = (ITaskbarList)new TaskbarList();
            tbl.HrInit();
            tbl.DeleteTab(hwnd);
            Marshal.ReleaseComObject(tbl);
        }
        catch { }

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

        bool ctrlDown = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool shiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        bool anyMod = ctrlDown || shiftDown;
        bool anyModWas = _ctrlWasDown || _shiftWasDown;
        bool leftDownNow = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool mouseDownNow = leftDownNow && !_wasLeftDown;
        bool mouseUpNow = !leftDownNow && _wasLeftDown;

        if (anyMod && !anyModWas && !_isDraggingWindow)
            SetTransparent(false);

        if (!anyMod && anyModWas && !_isDraggingWindow)
            SetTransparent(true);

        // Defer actual drag start to LateUpdate (after GameStart.Update updates IsOverGrip)
        if (ctrlDown && mouseDownNow && !_isDraggingWindow)
        {
            _dragPending = true;
        }

        if (_isDraggingWindow)
        {
            GetCursorPos(out POINT cur);
            if (cur.X != _lastDragCursor.X || cur.Y != _lastDragCursor.Y)
            {
                int newX = _dragStartWindowX + (cur.X - _dragStartCursor.X);
                int newY = _dragStartWindowY + (cur.Y - _dragStartCursor.Y);
                SetWindowPos(hwnd, 0, newX, newY, 0, 0, SWP_SHOWWINDOW | SWP_NOZORDER | SWP_NOSIZE);
                currentX = newX;
                currentY = newY;
                _lastDragCursor = cur;
            }
        }

        if (_isDraggingWindow && (mouseUpNow || (!ctrlDown && _ctrlWasDown)))
        {
            _isDraggingWindow = false;
            if (!(ctrlDown || shiftDown))
                SetTransparent(true);
            OnDragEnd?.Invoke();
        }

        _ctrlWasDown = ctrlDown;
        _shiftWasDown = shiftDown;
        _wasLeftDown = leftDownNow;
#endif
    }

    void LateUpdate()
    {
#if !UNITY_EDITOR
        if (!_dragPending) return;
        _dragPending = false;
        bool skip = gameStart != null && gameStart.GripDragActive;
        if (skip) return;

        GetCursorPos(out _dragStartCursor);
        if (gameStart != null && gameStart.targetTransform != null)
        {
            var ms = Camera.main.WorldToScreenPoint(gameStart.targetTransform.position + Vector3.up * 0.8f);
            Vector2 modelAbs = new Vector2(
                currentX + ms.x,
                currentY + (Screen.height - ms.y));
            float dist = Vector2.Distance(
                new Vector2(_dragStartCursor.X, _dragStartCursor.Y),
                modelAbs);
            if (dist > 400f) return;
        }

        SetTransparent(false);
        _lastDragCursor = _dragStartCursor;
        if (GetWindowRect(hwnd, out RECT rect))
        {
            _dragStartWindowX = rect.Left;
            _dragStartWindowY = rect.Top;
        }
        _isDraggingWindow = true;
        OnDragStart?.Invoke();
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
            return (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0;
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

    void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        if (hwnd != IntPtr.Zero)
        {
            try
            {
                var tbl = (ITaskbarList)new TaskbarList();
                tbl.HrInit();
                tbl.AddTab(hwnd);
                Marshal.ReleaseComObject(tbl);
            }
            catch { }
        }
#endif
    }
}

[ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
class TaskbarList { }
