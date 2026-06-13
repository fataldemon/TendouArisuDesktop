using System;
using System.Collections.Generic;
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
        Debug.Log("[Preview] ExitPreview frame=" + Time.frameCount);

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

    public void PreviewMultiBody(string facialPreset, float facialWeight, List<(string bodyPart, AnimationClip clip)> clips, bool arm = false, bool et = false)
    {
        Debug.Log("[Preview] PreviewMultiBody: facial=" + (facialPreset ?? "null") + " w=" + facialWeight + " arm=" + arm + " et=" + et + " clips=" + (clips?.Count ?? 0));
        if (clips == null || clips.Count == 0)
        {
            if (!string.IsNullOrEmpty(facialPreset))
                PreviewFacial(facialPreset, facialWeight);
            return;
        }
        if (_isPreviewing) bodyEngine.EndPreviewLock();
        EnterPreview();

        var config = new ActionGroupConfig
        {
            groupName = "MultiPreview",
            facialPreset = facialPreset ?? "",
            facialWeight = facialWeight,
            loop = true,
            blendInBody = 0.1f,
            blendInFacial = 0.1f,
            blendOutBody = 0.2f,
            blendOutFacial = 0.15f,
            isIdle = false,
            allowRootMotion = arm,
            enableEyeTracking = et
        };
        for (int i = 0; i < clips.Count; i++)
        {
            var (bp, c) = clips[i];
            config.bodyClips.Add(new PartClipEntry { bodyPart = bp, clipName = c.name, clip = c });
        }

        Debug.Log("[Preview] config built: facialPreset='" + config.facialPreset + "' w=" + config.facialWeight + " ARM=" + config.allowRootMotion + " ET=" + config.enableEyeTracking);

        emotionPlayer.TransitionTo(config, true);
    }
}
