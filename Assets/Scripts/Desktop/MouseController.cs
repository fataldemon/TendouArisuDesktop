using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

// 用于表示鼠标位置的结构体
[StructLayout(LayoutKind.Sequential)]
public class POINT
{
    public int x;
    public int y;
}


// 用于表示鼠标移动信息的结构体
[StructLayout(LayoutKind.Sequential)]
public class MouseHookStruct
{
    public POINT pt;
    public int hwnd;
    public int wHitTestCode;
    public int dwExtraInfo;
}



public class MouseController : MonoBehaviour
{
    //截获按钮
    // 鼠标事件吗
    private const int WH_KEYBOARD_LL = 14;

    // 鼠标移动事件码
    private const int WM_MOUSEMOVE = 0x0200;

    // 鼠标右键按下事件码
    private const int WM_RBUTTONDOWN = 0x0204;

    // 鼠标左键抬起事件码
    private const int WM_RBUTTONUP = 0x0205;

    // 鼠标左键按下事件码
    private const int WM_LBUTTONDOWN = 0x0201;

    // 鼠标左键抬起事件码
    private const int WM_LBUTTONUP = 0x0202;



    private static LowLevelMouseProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;



    // 存储上一帧鼠标的位置
    private static Vector3 lastMousePos = Vector3.zero;

    // 上一帧到当前帧鼠标的移动量
    public static Vector3 mouseDeltMove = Vector3.zero;

    // 用于判断当前帧是否是游戏启动后的第一帧
    private static bool first = true;



    // Use this for initialization

    void Start()
    {
        // 注册 hook
        _hookID = SetHook(_proc);

    }



    void OnApplicationQuit()
    {
        // 注销 hook
        UnhookWindowsHookEx(_hookID);
    }

    private static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // hook 回调
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 只要有鼠标事件被触发，该 hook 回调方法就会被系统调用
        // 所以直接在该方法中 通过 MPlayerInput 调用 游戏注册的鼠标事件回调
        MPlayerInput.Single.MouseEventCallBack();

        // 如果鼠标左键被按下
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            // 通过 MPlayerInput 调用鼠标左键被按下时的回调
            MPlayerInput.Single.MouseClickCallBack();
        }

        // 如果鼠标右键被按下
        if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            MPlayerInput.Single.MouseClickCallBack();
        }



        // 如果鼠标左键被抬起
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONUP)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            MPlayerInput.Single.MouseReleaseCallBack();
        }

        // 如果鼠标右键被抬起
        if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONUP)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            MPlayerInput.Single.MouseReleaseCallBack();
        }



        // 输入鼠标发生了移动
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            // 获取鼠标移动信息
            MouseHookStruct M_MouseHookStruct = (MouseHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseHookStruct));
            // 根据鼠标移动信息获取 当前帧 鼠标位置
            Vector3 curMousePos = new Vector3(-M_MouseHookStruct.pt.x, M_MouseHookStruct.pt.y, 0f);

            // 如果当前是游戏启动后的第一帧
            if (first)
            {
                // 则 lastMousePos = curMousePos
                // 即 认为当前鼠标的移动量为 0
                lastMousePos = curMousePos;
                first = false;
            }

            // 计算上一帧到当前帧鼠标的移动量
            mouseDeltMove = lastMousePos - curMousePos;

            // 通过 MPlayerInput 调用游戏的鼠标移动回调
            MPlayerInput.Single.MouseMoveCallBack(mouseDeltMove);

            // 更新 lastMousePos
            lastMousePos = curMousePos;
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }





    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

}
