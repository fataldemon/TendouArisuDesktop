using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using UnityEngine.XR;



/// <summary>
/// 一共可选择三种样式
/// </summary>

public enum EnumWinStyle
{
    /// <summary>
    /// 置顶
    /// </summary>
    WinTop,
    /// <summary>
    /// 置顶并且透明
    /// </summary>
    WinTopApha,
    /// <summary>
    /// 置顶透明并且可以穿透
    /// </summary>
    WinTopAphaPenetrate
}

public class TransparentWindow : MonoBehaviour
{
    #region Win函数常量
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


    [DllImport("user32.dll")]
    static extern int SetLayeredWindowAttributes(IntPtr hwnd, int crKey, int bAlpha, int dwFlags);


    [DllImport("Dwmapi.dll")]
    static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private const int WS_POPUP = 0x800000;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int LWA_COLORKEY = 0x00000001;
    private const int LWA_ALPHA = 0x00000002;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    //
    private const int ULW_COLORKEY = 0x00000001;
    private const int ULW_ALPHA = 0x00000002;
    private const int ULW_OPAQUE = 0x00000004;
    private const int ULW_EX_NORESIZE = 0x00000008;
    #endregion

    private Vector3 mousePosition = Vector3.zero;

    //
    public string strProduct;//项目名称
    public EnumWinStyle WinStyle = EnumWinStyle.WinTop;//窗体样式
    //

    public int ResWidth = 800;//窗口宽度
    public int ResHeight = 800;//窗口高度

    //
    public int currentX = 1700;//窗口左上角坐标x
    public int currentY = 670;//窗口左上角坐标y

    //
    private bool isApha;//是否透明
    private bool isAphaPenetrate;//是否要穿透窗体

    //是否允许输入
    public bool configToken = false;

    private IntPtr hwnd = IntPtr.Zero;
    private bool _transparentEnabled;

    void Start()
    {
#if !UNITY_EDITOR
        SettingsData settings = SettingsData.Load();
        if (settings != null && settings.winX > 0) currentX = settings.winX;
        if (settings != null && settings.winY > 0) currentY = settings.winY;

        Application.runInBackground = true;
        Screen.fullScreen = false;

        switch (WinStyle)
        {
            case EnumWinStyle.WinTop:
                isApha = false;
                isAphaPenetrate = false;
                break;

            case EnumWinStyle.WinTopApha:
                isApha = true;
                isAphaPenetrate = false;
                break;

            case EnumWinStyle.WinTopAphaPenetrate:
                isApha = true;
                isAphaPenetrate = true;
                break;
        }

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

        if (isApha)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED);
            int intExTemp = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (isAphaPenetrate)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, intExTemp | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }

            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_BORDER & ~WS_CAPTION);
            SetWindowPos(hwnd, -1, currentX, currentY, ResWidth, ResHeight, SWP_SHOWWINDOW);

            yield return null;
            var margins = new MARGINS() { cxLeftWidth = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        else
        {
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);
            SetWindowPos(hwnd, -1, currentX, currentY, ResWidth, ResHeight, SWP_SHOWWINDOW);
        }

        _transparentEnabled = isAphaPenetrate;
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

    private void Update()
    {
#if !UNITY_EDITOR
        if (hwnd == IntPtr.Zero) return;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
        {
            SetTransparent(false);
        }

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetMouseButtonDown(0))
            {
                ReleaseCapture();
                SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl))
        {
            if (!configToken)
            {
                SetTransparent(true);
            }
        }
#endif

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!configToken)
            {
                configToken = true;
#if !UNITY_EDITOR
                SetTransparent(false);
#endif
            }
            else
            {
                configToken = false;
#if !UNITY_EDITOR
                SetTransparent(true);
#endif
            }
        }
    }

    public void EnableWindowPenetration()
    {
        configToken = false;
#if !UNITY_EDITOR
        SetTransparent(true);
#endif
    }

    public void GetWindowPosition(out int x, out int y)
    {
        RECT rect;
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out rect))
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
