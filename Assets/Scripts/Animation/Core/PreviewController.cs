using System;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    public FacialEngine facialEngine;
    public BodyEngine bodyEngine;

    private bool _isPreviewing;
    private bool _facialPreviewing;
    private bool _bodyPreviewing;

    public bool IsPreviewing => _isPreviewing;

    public event Action OnPreviewEnter;
    public event Action OnPreviewExit;

    public void EnterPreview()
    {
        if (_isPreviewing) return;
        _isPreviewing = true;
        bodyEngine.Pause();
        OnPreviewEnter?.Invoke();
    }

    public void ExitPreview()
    {
        if (!_isPreviewing) return;

        if (_bodyPreviewing)
        {
            bodyEngine.StopPreview();
            _bodyPreviewing = false;
        }

        if (_facialPreviewing)
        {
            facialEngine.ResetInstant();
            _facialPreviewing = false;
        }

        _isPreviewing = false;
        bodyEngine.Resume();
        OnPreviewExit?.Invoke();
    }

    public void PreviewFacial(string presetName, float weight = 1f)
    {
        EnterPreview();
        facialEngine.ResetInstant();
        facialEngine.PreviewInstant(presetName, weight);
        _facialPreviewing = true;
    }

    public void PreviewBody(AnimationClip clip, bool loop = true)
    {
        if (clip == null) return;
        EnterPreview();
        bodyEngine.PreviewSample(clip, loop);
        _bodyPreviewing = true;
    }

    public void PreviewActionGroup(string facialPreset, float facialWeight, AnimationClip bodyClip)
    {
        EnterPreview();

        if (_facialPreviewing)
            facialEngine.ResetInstant();
        if (!string.IsNullOrEmpty(facialPreset))
        {
            facialEngine.PreviewInstant(facialPreset, facialWeight);
            _facialPreviewing = true;
        }

        if (bodyClip != null)
        {
            bodyEngine.PreviewSample(bodyClip, true);
            _bodyPreviewing = true;
        }
    }

    public void StopFacialPreview()
    {
        if (!_facialPreviewing) return;
        facialEngine.ResetInstant();
        _facialPreviewing = false;
        if (!_bodyPreviewing)
            ExitPreview();
    }

    public void StopBodyPreview()
    {
        if (!_bodyPreviewing) return;
        bodyEngine.StopPreview();
        _bodyPreviewing = false;
        if (!_facialPreviewing)
            ExitPreview();
    }
}
