using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MPlayerInput : MonoBehaviour
{
    // 单例
    public static MPlayerInput Single;

    /// <summary>
    /// 存储键盘按下时的响应事件
    /// 字典的键值为 响应事件对应的 按键
    /// 字典的元素为 响应事件
    /// 这里响应事件是 Action， 也可以通过 delegate 进行更丰富的定义。
    /// </summary>
    private Dictionary<KeyCode, Action> keyDownDic = new Dictionary<KeyCode, Action>();

    /// <summary>
    /// 存储按键抬起时的响应事件
    /// </summary>
    private Dictionary<KeyCode, Action> keyUpDic = new Dictionary<KeyCode, Action>();

    /// <summary>
    /// 存储鼠标移动时的回调
    /// 该回调需要一个 Vector3 参数， 该参数在 hook 调用时会传入 鼠标在一帧中的移动量
    /// </summary>
    private Action<Vector3> mouseMoveList = (movement) => { };

    /// <summary>
    /// 存储鼠标响应事件
    /// 这类事件是在鼠标 移动、点击左键、点击右键、点击中间等鼠标事件触发时都会被调用
    /// </summary>
    private Action mouseEventCall = () => { };

    /// <summary>
    /// 存储鼠标左键按下时的回调
    /// </summary>
    private Action mouseClickCall = () => { };

    /// <summary>
    /// 存储鼠标左键抬起时的回调
    /// </summary>
    private Action mouseReleaseCall = () => { };



    // 简单单例的构造
    private void Awake()
    {
        if (Single != null)
        {
            Destroy(Single.gameObject);
        }

        Single = this;
    }



    /// <summary>
    /// 注册鼠标移动时的回调
    /// </summary>
    /// <param name="callBack">回调</param>
    public void RegisterMouseMoveCallBack(Action<Vector3> callBack)
    {
        mouseMoveList += callBack;
    }

    /// <summary>
    /// 注册鼠标事件回调
    /// </summary>
    /// <param name="callBack">回调</param>
    public void RegisterMouseEventCallBack(Action callBack)
    {
        mouseEventCall += callBack;
    }

    /// <summary>
    /// 注册鼠标左键按下时的回调
    /// </summary>
    /// <param name="callBack">回调</param>
    public void RegisterMouseClickCallBack(Action callBack)
    {
        mouseClickCall += callBack;
    }

    /// <summary>
    /// 注册鼠标左键抬起时的回调
    /// </summary>
    /// <param name="callBack"></param>
    public void RegisterMouseRelaeaseCallBack(Action callBack)
    {
        mouseReleaseCall += callBack;
    }



    /// <summary>
    /// hook 用于调用鼠标移动回调的函数
    /// </summary>
    /// <param name="movement"></param>
    public void MouseMoveCallBack(Vector3 movement)
    {
        mouseMoveList.Invoke(movement);
    }

    /// <summary>
    /// hook 用于调用鼠标事件回调的函数
    /// </summary>
    public void MouseEventCallBack()
    {
        mouseEventCall.Invoke();
    }

    /// <summary>
    /// hook 用于调用鼠标左键按下时回调的函数
    /// </summary>
    public void MouseClickCallBack()
    {
        mouseClickCall.Invoke();
    }

    /// <summary>
    /// hook 用于调用鼠标左键抬起时回调的函数
    /// </summary>
    public void MouseReleaseCallBack()
    {
        mouseReleaseCall.Invoke();
    }



    /// <summary>
    /// 注册按键按下时的回调
    /// </summary>
    /// <param name="key">检测的按键</param>
    /// <param name="callBack">回调</param>
    public void RegisterKeyDownCallBack(KeyCode key, Action callBack)
    {
        if (!keyDownDic.ContainsKey(key)) keyDownDic[key] = callBack;
        else keyDownDic[key] += callBack;
    }

    /// <summary>
    /// 注册按键抬起时的回调
    /// </summary>
    /// <param name="key">检测的按键</param>
    /// <param name="callBack">回调</param>
    public void RegisterKeyUpCallBack(KeyCode key, Action callBack)
    {
        if (!keyUpDic.ContainsKey(key)) keyUpDic[key] = callBack;
        else keyUpDic[key] += callBack;
    }



    /// <summary>
    /// hook 用于调用按键按下时回调的函数
    /// </summary>
    /// <param name="key"></param>
    public void KeyDownCallBack(KeyCode key)
    {
        if (keyDownDic.ContainsKey(key)) keyDownDic[key].Invoke();
    }

    /// <summary>
    /// hook 用于调用按键抬起时回调的函数
    /// </summary>
    /// <param name="key"></param>
    public void KeyUpCallBack(KeyCode key)
    {
        if (keyUpDic.ContainsKey(key)) keyUpDic[key].Invoke();
    }
}
