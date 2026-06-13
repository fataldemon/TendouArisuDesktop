using System;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    public FacialEngine facialEngine;
    public BodyEngine bodyEngine;
    public EmotionPlayer emotionPlayer;

    private bool _isPreviewing;
    private bool _facialPreviewing;

    public bool IsPreviewing => _isPreviewing;

    public event Action OnPreviewEnter;
    public event Action OnPreviewExit;

    public void EnterPreview()
    {
        if (_isPreviewing) return;
        _isPreviewing = true;
        bodyEngine.SetPreviewing(true);
        bodyEngine.StartPreviewLock();
        OnPreviewEnter?.Invoke();
    }

    public void ExitPreview()
    {
        if (!_isPreviewing) return;

        if (_facialPreviewing)
        {
            facialEngine.ResetInstant();
            _facialPreviewing = false;
        }

        _isPreviewing = false;
        bodyEngine.SetPreviewing(false);
        bodyEngine.EndPreviewLock();
        if (emotionPlayer != null)
            emotionPlayer.RestoreToIdle();
        OnPreviewExit?.Invoke();
    }

    public void PreviewFacial(string presetName, float weight = 1f)
    {
        EnterPreview();
        facialEngine.ResetInstant();
        facialEngine.PreviewInstant(presetName, weight);
        _facialPreviewing = true;
    }

    public void PreviewBody(AnimationClip clip, string bodyPart = "fullBody", bool loop = true)
    {
        if (clip == null) return;
        if (_isPreviewing) bodyEngine.EndPreviewLock();
        EnterPreview();
        if (emotionPlayer != null)
            emotionPlayer.PlayClipDirect(clip, loop, bodyPart);
    }

    public void PreviewActionGroup(string facialPreset, float facialWeight, AnimationClip bodyClip)
    {
        if (_isPreviewing) bodyEngine.EndPreviewLock();
        EnterPreview();

        if (!string.IsNullOrEmpty(facialPreset))
        {
            facialEngine.ResetInstant();
            facialEngine.PreviewInstant(facialPreset, facialWeight);
            _facialPreviewing = true;
        }

        if (bodyClip != null && emotionPlayer != null)
            emotionPlayer.PlayClipDirect(bodyClip, true);
    }
}
