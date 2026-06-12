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
        var clip = ResolveBodyClip(config);
        var instance = new ActionGroupInstance(config, clip);

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

        if (instance.resolvedClip != null)
            bodyEngine.Play(instance.resolvedClip, "fullBody", instance.config.blendInBody, instance.config.loop);

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

        if (newInstance.resolvedClip != null)
            bodyEngine.Play(newInstance.resolvedClip, "fullBody", Mathf.Max(blendOutBody, blendInBody), newInstance.config.loop);
        else if (bodyEngine.idleClip != null)
            bodyEngine.Play(bodyEngine.idleClip, "fullBody", blendOutBody, true);

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

    private AnimationClip ResolveBodyClip(ActionGroupConfig config)
    {
        if (config == null) return null;

        if (config.bodyClips != null && config.bodyClips.Count > 0)
        {
            var entry = config.bodyClips[0];
            if (entry.clip != null) return entry.clip;

            if (!string.IsNullOrEmpty(entry.clipName))
                return ResolveClipByName(entry.clipName);
        }

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
        var clip = ResolveBodyClip(idle);
        if (clip != null) bodyEngine.idleClip = clip;
        _current = new ActionGroupInstance(idle, clip);
        _current.state = ActionGroupState.Active;

        facialEngine.ResetInstant();
        var entry = ActionSystemRuntime.GetMappingEntry("待机");
        string facial = (entry != null && !string.IsNullOrEmpty(entry.facialOverride)) ? entry.facialOverride : idle.facialPreset;
        float w = (entry != null && entry.facialWeightOverride >= 0f) ? entry.facialWeightOverride : idle.facialWeight;
        if (!string.IsNullOrEmpty(facial))
            facialEngine.PreviewInstant(facial, w);

        if (clip != null)
            bodyEngine.Play(clip, "fullBody", 0.1f, true);

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
            var clip = ResolveBodyClip(group);
            if (clip != null) bodyEngine.idleClip = clip;
            if (_current == null || _current.config.isIdle)
                ForceIdle();
        }
        else if (_current != null && _current.config.groupName == groupName)
        {
            var clip = ResolveBodyClip(group);
            _current = new ActionGroupInstance(group, clip);
            _current.state = ActionGroupState.Active;

            facialEngine.ResetInstant();
            string facial = _facialOverride ?? group.facialPreset;
            float weight = _facialWeightOverride >= 0f ? _facialWeightOverride : group.facialWeight;
            if (!string.IsNullOrEmpty(facial))
                facialEngine.PlayExpression(facial, weight, group.blendInFacial);

            if (clip != null)
                bodyEngine.Play(clip, "fullBody", group.blendInBody, group.loop);
        }
    }
}
