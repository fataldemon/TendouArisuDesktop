using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Window snapping (stage 6): when the user drags the avatar's desktop window and
/// releases near another window's TOP edge, the avatar snaps to (sits on) that edge
/// and follows the target window as it moves. Dragging again unsnaps.
///
/// Reuses TransparentWindow (drag state, currentX/Y, OnDragStart/End). The avatar's
/// own hwnd is obtained via Process.GetCurrentProcess().MainWindowHandle (same source
/// TransparentWindow uses). On snap it triggers the "窗口吸附" emotion (configured via
/// WPF); on unsnap it restores to idle.
///
/// v1 scope: snap to normal-window top edges only (no taskbar / screen-bottom),
/// no occlusion rendering (avatar sits in front of the edge). Build-only (editor has
/// no OS window drag).
/// </summary>
public class WindowSnapController : MonoBehaviour
{
    [Header("References")]
    public TransparentWindow transparentWindow;
    public BodyEngine bodyEngine;
    public EmotionPlayer emotionPlayer;

    [Header("Snap Tuning")]
    [Tooltip("How close (px) the avatar probe must be to a window's top edge to snap.")]
    public float snapThresholdY = 48f;
    [Tooltip("Windows smaller than this (px) are ignored.")]
    public int minWindowSize = 60;
    [Tooltip("Snap emotion name (configure the clip via WPF).")]
    public string snapEmotion = "窗口吸附";
    [Tooltip("Action group whose body clips the avatar is locked to while snapped (keeps sitting during messages).")]
    public string bodyLockGroup = "WindowSit";
    [Tooltip("Smooth the window glide to the snap edge (seconds). 0 = instant.")]
    public float snapSmoothingTime = 0.15f;

    private IntPtr _avatarHwnd = IntPtr.Zero;
    private bool _snapped;
    private IntPtr _snappedHwnd = IntPtr.Zero;
    private float _relX;          // avatar probe X relative to target window left (cached at snap)
    private bool _wasDragging;
    private float _smoothX, _smoothY, _smoothVelX, _smoothVelY;
    private bool _smoothInit;
    private int _lastTargetL, _lastTargetT;

    // ---- Win32 ----
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int pvAttribute, int cbAttribute);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);
    static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    private const int DWMWA_CLOAKED = 14;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_SHOWWINDOW = 0x0040;
    private const int HWND_TOPMOST = -1;
    private const int HWND_NOTOPMOST = -2;
    private const int SWP_NOACTIVATE = 0x0010;
    private const uint GA_ROOT = 2;
    private const uint GW_HWNDPREV = 3;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 2;

    private static readonly HashSet<string> _ignoredClasses = new HashSet<string>
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",   // taskbar
        "Progman", "WorkerW",                          // desktop
        "DV2ControlHost", "MsgrIMEWindowClass",        // start menu / IME
    };

    private void OnEnable()
    {
        if (transparentWindow != null)
        {
            transparentWindow.OnDragStart += HandleDragStart;
            transparentWindow.OnDragEnd += HandleDragEnd;
        }
    }

    private void OnDisable()
    {
        if (transparentWindow != null)
        {
            transparentWindow.OnDragStart -= HandleDragStart;
            transparentWindow.OnDragEnd -= HandleDragEnd;
        }
        Unsnap();
    }

    private void HandleDragStart()
    {
        // starting a drag while snapped => user wants to detach
        if (_snapped) Unsnap();
    }

    private void HandleDragEnd()
    {
        TrySnap();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (_snapped)
        {
            if (_avatarHwnd == IntPtr.Zero) CacheAvatarHwnd();
            if (_avatarHwnd == IntPtr.Zero) return;
            FollowTarget();
            // safety net: if the sit emotion ended for any reason (returned to idle), re-trigger
            // it so the avatar stays sitting while snapped. Other emotions (e.g. chat replies)
            // play out first since IsPlaying is true while they're active.
            if (emotionPlayer != null && !emotionPlayer.IsPlaying)
                PlaySnapEmotion();
        }
#endif
    }

    private void TrySnap()
    {
#if !UNITY_EDITOR
        if (_avatarHwnd == IntPtr.Zero) CacheAvatarHwnd();
        if (_avatarHwnd == IntPtr.Zero) return;
        if (transparentWindow == null) return;

        if (!ComputeProbe(out Vector2 probe)) return;

        IntPtr best = IntPtr.Zero;
        RECT bestRect = default;
        int bestDist = int.MaxValue;

        EnumWindows((hWnd, lp) =>
        {
            if (hWnd == _avatarHwnd) return true;
            if (!IsWindowVisible(hWnd) || IsCloaked(hWnd)) return true;
            if (!GetWindowRect(hWnd, out RECT r)) return true;
            var cls = new System.Text.StringBuilder(256);
            GetClassName(hWnd, cls, cls.Capacity);
            if (!IsSitEligibleWindow(hWnd, r, cls.ToString())) return true;

            // probe must be horizontally over the window and near its top edge
            if (probe.x < r.Left || probe.x > r.Right) return true;
            int dist = (int)Mathf.Abs(probe.y - r.Top);
            if (dist > snapThresholdY) return true;

            // only snap if the target edge is actually visible (not covered by a higher window)
            if (IsOccludedByHigherWindowsAtPoint(hWnd, (int)probe.x, (int)probe.y)) return true;

            if (dist < bestDist) { bestDist = dist; best = hWnd; bestRect = r; }
            return true;
        }, IntPtr.Zero);

        if (best != IntPtr.Zero)
        {
            _snappedHwnd = best;
            _relX = probe.x - bestRect.Left;   // remember horizontal placement
            _snapped = true;
            FollowTarget();                     // jump to the edge immediately
            PlaySnapEmotion();
        }
#endif
    }

    private void FollowTarget()
    {
        if (_snappedHwnd == IntPtr.Zero || !IsWindow(_snappedHwnd) || !GetWindowRect(_snappedHwnd, out RECT r))
        {
            Unsnap();   // target window closed
            return;
        }
        if (!ComputeProbe(out Vector2 probe)) return;

        // first frame after snap: seed smoothing + target tracking so the avatar glides to the edge
        if (!_smoothInit)
        {
            _smoothX = transparentWindow.currentX;
            _smoothY = transparentWindow.currentY;
            _smoothVelX = _smoothVelY = 0f;
            _lastTargetL = r.Left;
            _lastTargetT = r.Top;
            _smoothInit = true;
        }

        // if the TARGET window moved, follow tightly (no smoothing lag); if it's stationary,
        // keep gliding smoothly toward the edge (initial settle)
        bool targetMoved = (r.Left != _lastTargetL || r.Top != _lastTargetT);
        _lastTargetL = r.Left;
        _lastTargetT = r.Top;

        float spX = probe.x - transparentWindow.currentX;
        float spYFromBottom = Screen.height - (probe.y - transparentWindow.currentY);
        int targetX = (int)(r.Left + _relX - spX);
        int targetY = (int)(r.Top - spYFromBottom);

        int newX, newY;
        if (targetMoved || snapSmoothingTime <= 0f)
        {
            _smoothX = targetX; _smoothY = targetY;
            _smoothVelX = _smoothVelY = 0f;
            newX = targetX; newY = targetY;
        }
        else
        {
            float dt = Time.unscaledDeltaTime;
            _smoothX = Mathf.SmoothDamp(_smoothX, targetX, ref _smoothVelX, snapSmoothingTime, Mathf.Infinity, dt);
            _smoothY = Mathf.SmoothDamp(_smoothY, targetY, ref _smoothVelY, snapSmoothingTime, Mathf.Infinity, dt);
            newX = Mathf.RoundToInt(_smoothX);
            newY = Mathf.RoundToInt(_smoothY);
        }

        // topmost: the avatar stays above the target window (and everything) while snapped
        SetWindowPos(_avatarHwnd, HWND_TOPMOST, newX, newY, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW);
        transparentWindow.currentX = newX;
        transparentWindow.currentY = newY;
    }



    private void Unsnap()
    {
        if (!_snapped) return;
        _snapped = false;
        _snappedHwnd = IntPtr.Zero;
        _smoothInit = false;
        if (emotionPlayer != null)
        {
            emotionPlayer.BodyLockActionGroup = null;  // release body lock
            emotionPlayer.RestoreToIdle();
        }
    }

    private void PlaySnapEmotion()
    {
        if (emotionPlayer == null) return;
        emotionPlayer.BodyLockActionGroup = bodyLockGroup;  // keep sitting body during any later emotion
        emotionPlayer.PlayEmotion(snapEmotion);
        // hold the sit pose while snapped. Unconditional: do NOT depend on the group's loop
        // flag (the user may have turned loop off in WPF, which would otherwise let the
        // emotion auto-end and the sit pose disappear immediately).
        if (emotionPlayer.CurrentInstance != null)
            emotionPlayer.CurrentInstance.suppressAutoEnd = true;
    }

    // avatar seat (mid-thigh root = anatomical sit point) in DESKTOP pixel coords (top-left, y-down).
    // Using the upper-leg midpoint (not Hips) makes the butt rest on the window edge instead of
    // sitting too low (Hips are above the actual seat).
    private bool ComputeProbe(out Vector2 probe)
    {
        probe = default;
        if (bodyEngine == null || bodyEngine.animator == null || Camera.main == null) return false;
        var anim = bodyEngine.animator;
        Transform lul = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform rul = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Vector3 worldPos;
        if (lul != null && rul != null) worldPos = (lul.position + rul.position) * 0.5f;
        else
        {
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            worldPos = hips != null ? hips.position : anim.transform.position;
        }
        Vector3 sp = Camera.main.WorldToScreenPoint(worldPos);
        probe = new Vector2(
            transparentWindow.currentX + sp.x,
            transparentWindow.currentY + (Screen.height - sp.y));
        return true;
    }

    private void CacheAvatarHwnd()
    {
        try { _avatarHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; }
        catch { _avatarHwnd = IntPtr.Zero; }
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return cloaked != 0;
    }

    private static bool IsSameProcess(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        return pid == System.Diagnostics.Process.GetCurrentProcess().Id;
    }

    // Ported from Mate-Engine: only top-level, non-minimized, titled, reasonably sized windows
    // that aren't the desktop/taskbar/IME are eligible to be sat on.
    private bool IsSitEligibleWindow(IntPtr hWnd, RECT r, string className)
    {
        if (GetParent(hWnd) != IntPtr.Zero || GetAncestor(hWnd, GA_ROOT) != hWnd) return false; // top-level only
        if (IsIconic(hWnd)) return false;                       // not minimized
        if (GetWindowTextLength(hWnd) == 0) return false;       // must have a title
        if (IsSameProcess(hWnd)) return false;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w < 200 || h < 60) return false;
        if (_ignoredClasses.Contains(className)) return false;
        if (className.StartsWith("#") || className.Contains("Desktop")) return false;
        return true;
    }

    // Ported from Mate-Engine: walk windows above `hwnd` in z-order; if any opaque, visible,
    // higher window covers the point (x,y), the target's edge is not actually visible -> skip.
    private bool IsOccludedByHigherWindowsAtPoint(IntPtr hwnd, int x, int y)
    {
        var cls = new System.Text.StringBuilder(256);
        IntPtr h = GetWindow(hwnd, GW_HWNDPREV);
        while (h != IntPtr.Zero)
        {
            if (h == _avatarHwnd || IsSameProcess(h)) { h = GetWindow(h, GW_HWNDPREV); continue; }
            if (!IsWindowVisible(h) || IsCloaked(h) || !GetWindowRect(h, out RECT r)) { h = GetWindow(h, GW_HWNDPREV); continue; }

            bool hit = x >= r.Left && x <= r.Right && y >= r.Top && y <= r.Bottom;
            if (!hit) { h = GetWindow(h, GW_HWNDPREV); continue; }

            cls.Clear(); GetClassName(h, cls, cls.Capacity);
            string cn = cls.ToString();
            if (_ignoredClasses.Contains(cn) || cn.StartsWith("#") || cn.Contains("Desktop")) { h = GetWindow(h, GW_HWNDPREV); continue; }

            long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            if ((ex & WS_EX_TRANSPARENT) != 0) { h = GetWindow(h, GW_HWNDPREV); continue; }              // click-through overlay
            if ((ex & WS_EX_LAYERED) != 0 && GetLayeredWindowAttributes(h, out _, out byte alpha, out uint flags))
            {
                if ((flags & LWA_ALPHA) != 0 && alpha <= 8) { h = GetWindow(h, GW_HWNDPREV); continue; } // nearly invisible
            }
            return true; // a higher opaque window covers the point -> occluded
        }
        return false;
    }
}
