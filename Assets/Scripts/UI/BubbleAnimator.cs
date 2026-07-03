using UnityEngine;

/// <summary>
/// Drives chat-bubble appearance animation: fade + scale-pop + slide-in + typewriter.
/// Read-only state (Alpha/Scale/SlideOffsetY) is applied by the renderer (GameStart.OnGUI)
/// via GUI.color / GUI.matrix / Rect offset. GetDisplayText() handles the typewriter
/// substring and keeps the last text visible during fade-out.
///
/// Streaming-aware: Show() starts the sequence; the renderer passes the live (growing)
/// text_answer each frame and the typewriter reveals it progressively. Hide() snapshots
/// the text so it stays visible while fading out. Chinese is unaffected (IMGUI skin font).
/// All timing is framerate-independent (SpringMath).
/// </summary>
public class BubbleAnimator : MonoBehaviour
{
    [Header("Fade")]
    public float fadeInDuration = 0.25f;
    public float fadeOutDuration = 0.15f;

    [Header("Pop (scale with overshoot)")]
    public float popStartScale = 0.85f;
    public float popSpringFrequency = 12f;
    [Range(0f, 1f)] public float popDamping = 0.45f;

    [Header("Slide-in")]
    public float slideDistance = 40f;
    public float slideSpeed = 12f;

    [Header("Typewriter")]
    public float typewriterCharsPerSec = 14f;
    public float typewriterStartDelay = 0.25f;

    public float Alpha { get; private set; }
    public float Scale { get; private set; } = 1f;
    public float SlideOffsetY { get; private set; }
    public int VisibleChars { get; private set; }
    public bool IsVisible => Alpha > 0.01f;

    private float _scaleVel;
    private float _showTime;
    private bool _shown;
    private bool _hiding;
    private string _fadeText = "";

    /// <summary>Start the show sequence (fade-in + pop + slide + typewriter reset).</summary>
    public void Show()
    {
        _showTime = 0f;
        _shown = true;
        _hiding = false;
        Scale = popStartScale;
        _scaleVel = 0f;
        SlideOffsetY = slideDistance;
        // VisibleChars keeps growing from 0; renderer truncates the live text to it
        VisibleChars = 0;
    }

    /// <summary>Begin fade-out, snapshotting text so it stays visible while fading.</summary>
    public void Hide(string fadeText)
    {
        _fadeText = fadeText ?? "";
        _hiding = true;
        _shown = false;
    }

    /// <summary>Text to render now: typewriter-revealed live text while active, full snapshot while fading out.</summary>
    public string GetDisplayText(string liveText, bool dialogueActive)
    {
        string src = dialogueActive ? (liveText ?? "") : _fadeText;
        if (string.IsNullOrEmpty(src)) return "";
        int n = dialogueActive ? VisibleChars : src.Length;
        if (n >= src.Length) return src;
        if (n <= 0) return "";
        return src.Substring(0, n);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_hiding)
        {
            Alpha = SpringMath.Damp(Alpha, 0f, 1f / Mathf.Max(0.01f, fadeOutDuration), dt);
            if (Alpha < 0.01f) { Alpha = 0f; _hiding = false; }
            return;
        }

        if (!_shown) { Alpha = 0f; return; }

        _showTime += dt;
        Alpha = SpringMath.Damp(Alpha, 1f, 1f / Mathf.Max(0.01f, fadeInDuration), dt);

        float s = Scale;
        SpringMath.Spring(ref s, ref _scaleVel, 1f, popSpringFrequency, popDamping, dt);
        Scale = s;

        SlideOffsetY = SpringMath.Damp(SlideOffsetY, 0f, slideSpeed, dt);

        if (_showTime > typewriterStartDelay)
            VisibleChars += Mathf.Max(1, Mathf.CeilToInt(typewriterCharsPerSec * dt));
    }
}
