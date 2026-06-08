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

    private IntPtr hwnd;

    // Use this for initialization
    void Awake()
    {
        #if !UNITY_EDITOR
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

        //获取当前窗口
        hwnd = GetActiveWindow();

        //
        if (isApha)
        {

            //去边框并且透明
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED);
            int intExTemp = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (isAphaPenetrate)//是否透明穿透窗体
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, intExTemp | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                //SetWindowLong(hwnd, GWL_EXSTYLE, intExTemp | WS_EX_LAYERED);
                //SetLayeredWindowAttributes(hwnd, 0, 0, LWA_COLORKEY);
            }

            //
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_BORDER & ~WS_CAPTION);
            SetWindowPos(hwnd, -1, currentX, currentY, ResWidth, ResHeight, SWP_SHOWWINDOW);
            var margins = new MARGINS() { cxLeftWidth = -1 };
            //
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        else
        {
            //单纯去边框
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);
            SetWindowPos(hwnd, -1, currentX, currentY, ResWidth, ResHeight, SWP_SHOWWINDOW);
        }
        #endif
    }

    private void Update()
    {
#if !UNITY_EDITOR
        //检查键盘是否按下
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
        {
            // 临时禁用透明穿透，以允许拖动窗口
            SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_TRANSPARENT));
        }

        //检查键盘是否按下
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            // 检查鼠标左键是否按下
            if (Input.GetMouseButtonDown(0))
            {
                // 判断鼠标是否在模型上
                // 释放鼠标捕获，确保鼠标事件可以传递给窗口管理器
                ReleaseCapture();

                // 向窗口发送消息，模拟在标题栏上按下鼠标左键的操作，从而实现拖动窗口
                SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);      
            }
        }

        //检查键盘是否弹起
        if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl))
        {
            if (!configToken)
            {
                // 重新启用透明穿透
                SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
            }
        }

#endif

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (!configToken)
            {
                configToken = true;
#if !UNITY_EDITOR
                // 临时禁用透明穿透，以允许拖动窗口
                SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_TRANSPARENT));
#endif
            }
            else 
            {
                configToken = false;
#if !UNITY_EDITOR
                // 重新启用透明穿透
                SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
#endif
            }
        }
    }

    public void EnableWindowPenetration()
    {
        configToken = false;
#if !UNITY_EDITOR
        // 重新启用透明穿透
        SetWindowLong(hwnd, GWL_EXSTYLE, (uint)(GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT));
#endif
    }
}
