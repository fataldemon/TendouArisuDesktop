using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class KeyCodeController : MonoBehaviour
{
    //截获按钮
    // 键盘事件码
    private const int WH_KEYBOARD_LL = 13;

    // 键盘按下 事件码
    private const int WM_KEYDOWN = 0x0100;

    // 键盘抬起 事件码
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    /// <summary>
    /// 信号存储字典
    /// 因为在键盘处于被按下的状态，系统会一直调用 键盘按下的 hook
    /// 而我们希望只在键盘被按下的一瞬间，调用一次回调
    /// 所以在键盘被按下时 存储一个为 true 的信号
    /// 在调用回调前判断对应键的信号，如果为 true 则不调用，为 false 则调用，调用完 信号转为 true
    /// 在按键被抬起时，把信号设置回 false
    /// </summary>
    private static Dictionary<KeyCode, bool> keyDownDic = new Dictionary<KeyCode, bool>();

    // Use this for initialization
    void Start()
    {
        // 向键盘事件 hook 注册回调
        _hookID = SetHook(_proc);
    }

    void OnApplicationQuit()
    {
        // 注销 hook 回调
        UnhookWindowsHookEx(_hookID);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // 键盘事件 hook 回调
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 如果当前有按键被按下
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam); // 按下的按键的键盘码
            KeyCode key = KeyCode.None;
            try
            {
                // 大部分按键的键盘码 与 Unity 的 KeyCokde 转换可以用 KeyCode key = (vkCode + 32)
                // 但是有些则不能，例如 回车键、TAB键等，因此为了防止程序崩溃，需要用 try catch 让程序稳定
                key = (KeyCode)(vkCode + 32);
            }
            catch { }

            // 判断是否是按下按键的瞬间
            if (!keyDownDic.ContainsKey(key) || keyDownDic[key] == false)
            {
                // 通过 MPlayerInput 调用游戏注册的按键回调
                MPlayerInput.Single.KeyDownCallBack(key);
                // 字典这样写，如果字典没有存储 key 这个键值，则会默认调用 dic.Add() 方法
                // 将按键信号设为 true
                keyDownDic[key] = true;
            }
        }

        // 如果按键有被抬起
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYUP)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            KeyCode key = KeyCode.None;
            try
            {
                key = (KeyCode)(vkCode + 32);
            }
            catch { }
            // 将按键信号恢复为 false
            keyDownDic[key] = false;
            // 通过 MPlayerInput 调用游戏注册的按键回调
            MPlayerInput.Single.KeyUpCallBack(key);
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

}