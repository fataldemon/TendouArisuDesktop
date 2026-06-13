using System;
using System.Collections.Generic;
using UnityEngine;

public class EmotionPlayer : MonoBehaviour
{
    public FacialEngine facialEngine;
    public BodyEngine bodyEngine;
    public PreviewController previewController;
    public BlinkController blinkController;
    public EyeTrackingController eyeTrackingController;
    public AnimationLibrary animLibrary;

    private ActionGroupInstance _current;
    private bool _isCrossfadingToNext;
    private float _crossfadeTimer;
    private float _crossfadeDuration;
    private ActionGroupConfig _pendingConfig;
    private string _facialOverride;
    private float _facialWeightOverride = -1f;

    public bool IsPlaying => _current != null && !_current.config.isIdle;
    public bool IsTTSPlaying { get; set; }
    public ActionGroupConfig CurrentConfig => _current?.config;

    public event Action OnActionGroupStart;
    public event Action OnActionGroupEnd;

    public void PlayClipDirect(AnimationClip clip, bool loop = true)
    {
        _facialOverride = null;
        _facialWeightOverride = -1f;
        var config = new ActionGroupConfig
        {
            groupName = "Preview",
            facialPreset = "",
            loop = loop,
            blendInBody = 0.1f,
            blendInFacial = 0.1f,
            blendOutBody = 0.2f,
            blendOutFacial = 0.15f,
            isIdle = false
        };
        config.bodyClips.Add(new PartClipEntry { bodyPart = "fullBody", clipName = clip.name, clip = clip });
        config.allowRootMotion = bodyEngine != null && bodyEngine.allowRootMotion;
        _current = null;
        TransitionTo(config, true);
    }

    private void Start()
    {
        ActionSystemRuntime.EnsureInit();
        Debug.Log("[EmotionPlayer] Start: facialEngine=" + (facialEngine != null) +
            " bodyEngine=" + (bodyEngine != null) +
            " animLibrary=" + (animLibrary != null) +
            " clipRefs=" + (animLibrary != null && animLibrary.clipReferences != null ? animLibrary.clipReferences.Length : -1));
        if (facialEngine != null)
            Debug.Log("[EmotionPlayer] facialEngine.meshRenderer=" + (facialEngine.meshRenderer != null));
        if (ActionSystemRuntime.IdleGroup != null)
            TransitionTo(ActionSystemRuntime.IdleGroup, true);
        else
            Debug.LogWarning("[EmotionPlayer] IdleGroup is null!");
    }

    public void PlayEmotion(string emotion, float weight = 1f)
    {
        if (previewController != null && previewController.IsPreviewing) return;

        _facialOverride = null;
        _facialWeightOverride = -1f;

        var entry = ActionSystemRuntime.GetMappingEntry(emotion);
        if (entry != null)
        {
            if (!string.IsNullOrEmpty(entry.facialOverride))
                _facialOverride = entry.facialOverride;
            if (entry.facialWeightOverride >= 0f)
                _facialWeightOverride = entry.facialWeightOverride;
        }

        var config = ActionSystemRuntime.ResolveEmotion(emotion);
        if (config == null)
        {
            Debug.LogWarning("[EmotionPlayer] PlayEmotion: no config for '" + emotion + "', falling back to idle");
            config = ActionSystemRuntime.IdleGroup;
            if (config == null) return;
        }

        Debug.Log("[EmotionPlayer] PlayEmotion: " + emotion + " → group=" + config.groupName +
            " facial=" + (_facialOverride ?? config.facialPreset) + " w=" + (_facialWeightOverride >= 0 ? _facialWeightOverride : config.facialWeight) +
            " clip=" + (config.bodyClips.Count > 0 ? config.bodyClips[0].clipName : "none"));

        TransitionTo(config, false);
    }

    public void RestoreToIdle()
    {
        PlayEmotion("待机");
    }

    public void NotifyTTSStart()
    {
        IsTTSPlaying = true;
        if (_current != null)
            _current.ttsStarted = true;
    }

    public void NotifyTTSEnd()
    {
        IsTTSPlaying = false;
        if (_current != null)
        {
            _current.ttsEnded = true;
            _current.holdTimer = 0f;
        }
    }

    public void NotifyTTSError()
    {
        IsTTSPlaying = false;
        if (_current != null)
        {
            _current.ttsEnded = true;
            _current.holdTimer = 0f;
        }
    }

    public ActionGroupConfig ResolveEmotion(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return null;
        return ActionSystemRuntime.ResolveEmotion(emotion);
    }

    public ActionGroupConfig ResolveConfig(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return null;
        return ActionSystemRuntime.GetActionGroup(groupName);
    }

    private void TransitionTo(ActionGroupConfig config, bool instant)
    {
        var clips = ResolveAllBodyClips(config);
        var instance = new ActionGroupInstance(config, clips);

        if (instant || _current == null)
        {
            ApplyImmediate(instance);
        }
        else
        {
            StartCrossfade(instance);
        }
    }

    private void ApplyImmediate(ActionGroupInstance instance)
    {
        _current = instance;
        _current.state = ActionGroupState.Active;
        _current.stateTimer = 0f;

        string facialPreset = _facialOverride ?? instance.config.facialPreset;
        float facialWeight = _facialWeightOverride >= 0f ? _facialWeightOverride : instance.config.facialWeight;
        Debug.Log("[EmotionPlayer] ApplyImmediate: override='" + _facialOverride + "' default='" + instance.config.facialPreset + "' → use='" + facialPreset + "' w=" + facialWeight);

        if (!string.IsNullOrEmpty(facialPreset))
            facialEngine.PlayExpression(facialPreset, facialWeight, instance.config.blendInFacial);
        else
            facialEngine.RestoreExpression(instance.config.blendInFacial);

        for (int i = 0; i < instance.resolvedClips.Count; i++)
        {
            var rc = instance.resolvedClips[i];
            if (rc.clip != null)
            {
                Debug.Log("[EmotionPlayer] ApplyImmediate: play " + rc.clip.name + " on " + rc.bodyPart +
                    " blend=" + instance.config.blendInBody + " loop=" + instance.config.loop);
                bodyEngine.Play(rc.clip, rc.bodyPart, instance.config.blendInBody, instance.config.loop);
            }
        }

        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = instance.config.allowRootMotion;

        UpdateAuxiliary(instance.config);
        OnActionGroupStart?.Invoke();
    }

    private void StartCrossfade(ActionGroupInstance newInstance)
    {
        float blendOutFacial = _current.config.blendOutFacial;
        float blendOutBody = _current.config.blendOutBody;
        float blendInFacial = newInstance.config.blendInFacial;
        float blendInBody = newInstance.config.blendInBody;

        string facialPreset = _facialOverride ?? newInstance.config.facialPreset;
        float facialWeight = _facialWeightOverride >= 0f ? _facialWeightOverride : newInstance.config.facialWeight;
        Debug.Log("[EmotionPlayer] StartCrossfade: override='" + _facialOverride + "' default='" + newInstance.config.facialPreset + "' → use='" + facialPreset + "' w=" + facialWeight);

        if (!string.IsNullOrEmpty(facialPreset))
            facialEngine.CrossfadeTo(facialPreset, facialWeight, blendOutFacial, blendInFacial);
        else
            facialEngine.RestoreExpression(blendOutFacial);

        for (int i = 0; i < newInstance.resolvedClips.Count; i++)
        {
            var rc = newInstance.resolvedClips[i];
            if (rc.clip != null)
                bodyEngine.Play(rc.clip, rc.bodyPart, Mathf.Max(blendOutBody, blendInBody), newInstance.config.loop);
        }
        if (newInstance.resolvedClips.Count == 0 && bodyEngine.idleClip != null)
            bodyEngine.Play(bodyEngine.idleClip, "fullBody", blendOutBody, true);

        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = newInstance.config.allowRootMotion;

        _current.state = ActionGroupState.BlendingOut;
        newInstance.state = ActionGroupState.BlendingIn;

        _crossfadeTimer = 0f;
        _crossfadeDuration = Mathf.Max(blendOutBody, blendInBody);
        _isCrossfadingToNext = true;
        _pendingConfig = newInstance.config;

        _current = newInstance;
        _current.state = ActionGroupState.Active;
        _current.stateTimer = 0f;

        UpdateAuxiliary(newInstance.config);
        OnActionGroupStart?.Invoke();
    }

    private void UpdateAuxiliary(ActionGroupConfig config)
    {
        bool suppress = !config.isIdle;
        if (blinkController != null)
            blinkController.suppressed = suppress;
        if (eyeTrackingController != null)
            eyeTrackingController.expressionActive = suppress;
    }

    private void Update()
    {
        if (_current == null) return;
        if (previewController != null && previewController.IsPreviewing) return;

        _current.stateTimer += Time.deltaTime;

        if (_isCrossfadingToNext)
        {
            _crossfadeTimer += Time.deltaTime;
            if (_crossfadeTimer >= _crossfadeDuration)
                _isCrossfadingToNext = false;
        }

        if (_current.state == ActionGroupState.Active && !_current.config.isIdle)
        {
            if (!_current.config.loop)
            {
                _current.clipFinished = bodyEngine.HasClipFinished("fullBody");
            }

            if (_current.ttsEnded || (!_current.ttsStarted && _current.stateTimer > 1f))
                _current.holdTimer += Time.deltaTime;

            if (_current.ShouldEnd())
            {
                OnActionGroupEnd?.Invoke();
                RestoreToIdle();
            }
        }
    }

    private List<ResolvedClip> ResolveAllBodyClips(ActionGroupConfig config)
    {
        var results = new List<ResolvedClip>();
        if (config?.bodyClips != null)
        {
            for (int i = 0; i < config.bodyClips.Count; i++)
            {
                var entry = config.bodyClips[i];
                if (entry.clip != null)
                {
                    results.Add(new ResolvedClip { clip = entry.clip, bodyPart = entry.bodyPart });
                }
                else if (!string.IsNullOrEmpty(entry.clipName))
                {
                    var resolved = ResolveClipByName(entry.clipName);
                    if (resolved != null)
                        results.Add(new ResolvedClip { clip = resolved, bodyPart = entry.bodyPart });
                }
            }
        }
        return results;
    }

    private AnimationClip ResolveBodyClip(ActionGroupConfig config)
    {
        var clips = ResolveAllBodyClips(config);
        for (int i = 0; i < clips.Count; i++)
            if (clips[i].bodyPart == "fullBody")
                return clips[i].clip;
        return null;
    }

    private AnimationClip ResolveClipByName(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) { Debug.LogWarning("[EmotionPlayer] ResolveClipByName: clipName is empty"); return null; }

        if (clipName == "Idle") return bodyEngine.idleClip;

        if (animLibrary == null) { Debug.LogWarning("[EmotionPlayer] ResolveClipByName: animLibrary is null!"); }
        else if (animLibrary.clipReferences == null) { Debug.LogWarning("[EmotionPlayer] ResolveClipByName: clipReferences is null!"); }
        else
        {
            for (int i = 0; i < animLibrary.clipReferences.Length; i++)
            {
                if (animLibrary.clipReferences[i] != null && animLibrary.clipReferences[i].name == clipName)
                {
                    Debug.Log("[EmotionPlayer] ResolveClipByName: '" + clipName + "' found in clipRefs[" + i + "]");
                    return animLibrary.clipReferences[i];
                }
            }
        }

        var group = ActionSystemRuntime.GetActionGroup(clipName);
        if (group != null && group.bodyClips.Count > 0 && group.bodyClips[0].clip != null)
            return group.bodyClips[0].clip;

        Debug.LogWarning("[EmotionPlayer] ResolveClipByName: '" + clipName + "' NOT FOUND (clipRefs=" + (animLibrary?.clipReferences?.Length ?? -1) + ")");
        return null;
    }

    public void ForceIdle()
    {
        var idle = ActionSystemRuntime.ResolveEmotion("待机") ?? ActionSystemRuntime.IdleGroup;
        if (idle == null) return;
        var clips = ResolveAllBodyClips(idle);
        for (int i = 0; i < clips.Count; i++)
            if (clips[i].bodyPart == "fullBody" && clips[i].clip != null)
                bodyEngine.idleClip = clips[i].clip;
        _current = new ActionGroupInstance(idle, clips);
        _current.state = ActionGroupState.Active;

        facialEngine.ResetInstant();
        var entry = ActionSystemRuntime.GetMappingEntry("待机");
        string facial = (entry != null && !string.IsNullOrEmpty(entry.facialOverride)) ? entry.facialOverride : idle.facialPreset;
        float w = (entry != null && entry.facialWeightOverride >= 0f) ? entry.facialWeightOverride : idle.facialWeight;
        if (!string.IsNullOrEmpty(facial))
            facialEngine.PreviewInstant(facial, w);

        for (int i = 0; i < clips.Count; i++)
        {
            var rc = clips[i];
            if (rc.clip != null)
                bodyEngine.Play(rc.clip, rc.bodyPart, 0.1f, true);
        }

        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = idle.allowRootMotion;

        UpdateAuxiliary(idle);
    }

    public void RefreshCurrentGroup(string groupName)
    {
        var group = ActionSystemRuntime.GetActionGroup(groupName);
        if (group == null) { Debug.LogWarning("[EmotionPlayer] RefreshCurrentGroup: group '" + groupName + "' not found"); return; }
        Debug.Log("[EmotionPlayer] RefreshCurrentGroup: " + groupName + " isIdle=" + group.isIdle +
            " currentIsIdle=" + (_current != null && _current.config.isIdle) +
            " currentGroup=" + (_current?.config?.groupName ?? "null"));

        if (group.isIdle)
        {
            var clips = ResolveAllBodyClips(group);
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].bodyPart == "fullBody" && clips[i].clip != null)
                    bodyEngine.idleClip = clips[i].clip;
            if (_current == null || _current.config.isIdle)
                ForceIdle();
        }
        else if (_current != null && _current.config.groupName == groupName)
        {
            var clips = ResolveAllBodyClips(group);
            _current = new ActionGroupInstance(group, clips);
            _current.state = ActionGroupState.Active;

            facialEngine.ResetInstant();
            string facial = _facialOverride ?? group.facialPreset;
            float weight = _facialWeightOverride >= 0f ? _facialWeightOverride : group.facialWeight;
            if (!string.IsNullOrEmpty(facial))
                facialEngine.PlayExpression(facial, weight, group.blendInFacial);

            for (int i = 0; i < clips.Count; i++)
            {
                var rc = clips[i];
                if (rc.clip != null)
                    bodyEngine.Play(rc.clip, rc.bodyPart, group.blendInBody, group.loop);
            }
        }
    }
}
