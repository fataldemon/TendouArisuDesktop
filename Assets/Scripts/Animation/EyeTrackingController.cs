using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class EyeTrackingController : MonoBehaviour
{
    public FacialController facialController;
    public ActionController actionController;
    public AnimationLibrary animLibrary;

    public float lookStrength = 120f;
    public float headRotationAmount = 10f;

    private float _currentX;
    private float _currentY;
    private Quaternion _headDefaultRot = Quaternion.identity;
    private bool _wasInAction;
    private IntPtr _unityHwnd;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    void Start()
    {
        _unityHwnd = GetActiveWindow();
        if (facialController == null) facialController = GetComponent<FacialController>();
        if (actionController == null) actionController = GetComponent<ActionController>();
        StartCoroutine(SaveDefaultRot());
    }

    System.Collections.IEnumerator SaveDefaultRot()
    {
        yield return null;
        var head = actionController?.animator?.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) _headDefaultRot = head.localRotation;
    }

    void Update()
    {
        if (facialController == null || facialController.skinnedMeshRenderer == null) return;
        if (actionController?.animator == null || Camera.main == null) return;

        bool inAction = (animLibrary != null && animLibrary.IsPreviewing)
            || actionController.animator.GetInteger("action_param") >= 3
            || actionController.animator.GetBool("onAction");

        if (inAction)
        {
            _currentX = Mathf.Lerp(_currentX, 0f, Time.deltaTime * 12f);
            _currentY = Mathf.Lerp(_currentY, 0f, Time.deltaTime * 12f);
            _wasInAction = true;
        }
        else
        {
            if (_wasInAction) { _currentX = 0f; _currentY = 0f; _wasInAction = false; }

            var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            GetCursorPos(out POINT cursor);
            GetWindowRect(_unityHwnd, out RECT winRect);

            float cursorWinX = cursor.x - winRect.Left;
            float cursorWinY = cursor.y - winRect.Top;

            Vector3 headScreen = Camera.main.WorldToScreenPoint(head.position);
            float headWinX = headScreen.x;
            float headWinY = Screen.height - headScreen.y;

            float dx = cursorWinX - headWinX;
            float dy = headWinY - cursorWinY;

            float targetX = (dx / (Screen.width * 0.5f)) * lookStrength;
            float targetY = (dy / (Screen.height * 0.5f)) * lookStrength;

            _currentX = Mathf.Lerp(_currentX, targetX, Time.deltaTime * 8f);
            _currentY = Mathf.Lerp(_currentY, targetY, Time.deltaTime * 8f);
        }

        float left = Mathf.Max(0f, _currentX);
        float right = Mathf.Max(0f, -_currentX);
        float up = Mathf.Max(0f, _currentY);
        float down = Mathf.Max(0f, -_currentY);
        ApplyEyeWeights(left, right, up, down);
    }

    void LateUpdate()
    {
        if (facialController == null || actionController?.animator == null) return;

        bool headIdle = actionController.animator.GetInteger("action_param") == 0
            && actionController.animator.GetInteger("onWaiting") == 0
            && !actionController.animator.GetBool("onAction")
            && (animLibrary == null || !animLibrary.IsPreviewing);

        if (!headIdle) return;

        var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Vector3 targetRot = new Vector3(_currentY / lookStrength * headRotationAmount, -_currentX / lookStrength * headRotationAmount, 0f);
        Quaternion target = _headDefaultRot * Quaternion.Euler(targetRot);
        head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * 4f);
    }

    private void ApplyEyeWeights(float left, float right, float up, float down)
    {
        var smr = facialController.skinnedMeshRenderer;
        if (smr == null) return;
        smr.SetBlendShapeWeight(facialController.lookLeftBlendIndex, left);
        smr.SetBlendShapeWeight(facialController.lookRightBlendIndex, right);
        smr.SetBlendShapeWeight(facialController.lookUpBlendIndex, up);
        smr.SetBlendShapeWeight(facialController.lookDownBlendIndex, down);
    }
}
