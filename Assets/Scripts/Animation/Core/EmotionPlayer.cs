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
    public ActionSystemDatabase database;
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
        if (database == null)
            database = Resources.Load<ActionSystemDatabase>("ActionSystemDatabase");
        if (database != null)
        {
            if (database.emotionMappings == null)
                database.emotionMappings = Resources.Load<EmotionMappingDatabase>("EmotionMappings");
            if (database.facialPresets == null)
                database.facialPresets = Resources.Load<FacialPresetDatabase>("FacialPresets");
            if (database.actionPresets == null)
                database.actionPresets = Resources.Load<ActionPresetDatabase>("ActionPresets");
        }
        if (database != null && database.idleGroup != null)
            TransitionTo(database.idleGroup, true);
    }

    public void PlayEmotion(string emotion, float weight = 1f)
    {
        if (previewController != null && previewController.IsPreviewing) return;

        _facialOverride = null;
        _facialWeightOverride = -1f;

        if (database != null && database.emotionMappings != null)
        {
            var entry = database.emotionMappings.GetEntry(emotion);
            if (entry != null)
            {
                if (!string.IsNullOrEmpty(entry.facialOverride))
                    _facialOverride = entry.facialOverride;
                if (entry.facialWeightOverride >= 0f)
                    _facialWeightOverride = entry.facialWeightOverride;
            }
        }

        var config = ResolveEmotion(emotion);
        if (config == null)
        {
            config = database != null ? database.idleGroup : null;
            if (config == null) return;
        }

        TransitionTo(config, false);
    }

    public void RestoreToIdle()
    {
        if (database == null || database.idleGroup == null) return;
        TransitionTo(database.idleGroup, false);
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
        if (database == null || string.IsNullOrEmpty(emotion)) return null;
        return database.ResolveEmotion(emotion);
    }

    public ActionGroupConfig ResolveConfig(string groupName)
    {
        if (database == null || string.IsNullOrEmpty(groupName)) return null;
        return database.GetActionGroup(groupName);
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
        if (string.IsNullOrEmpty(clipName)) return null;

        if (clipName == "Idle") return bodyEngine.idleClip;

        if (animLibrary != null && animLibrary.clipReferences != null)
        {
            for (int i = 0; i < animLibrary.clipReferences.Length; i++)
            {
                if (animLibrary.clipReferences[i] != null && animLibrary.clipReferences[i].name == clipName)
                    return animLibrary.clipReferences[i];
            }
        }

        if (database != null && database.actionPresets != null)
        {
            var preset = database.actionPresets.Get(clipName);
            if (preset != null)
            {
                var clip = preset.GetClip("fullBody");
                if (clip != null) return clip;
            }
        }

        return null;
    }

    public void ForceIdle()
    {
        if (database == null || database.idleGroup == null) return;
        var clip = ResolveBodyClip(database.idleGroup);
        _current = new ActionGroupInstance(database.idleGroup, clip);
        _current.state = ActionGroupState.Active;

        facialEngine.ResetInstant();
        if (!string.IsNullOrEmpty(database.idleGroup.facialPreset))
            facialEngine.PreviewInstant(database.idleGroup.facialPreset, database.idleGroup.facialWeight);

        if (clip != null)
            bodyEngine.Play(clip, "fullBody", 0.1f, true);

        UpdateAuxiliary(database.idleGroup);
    }
}
