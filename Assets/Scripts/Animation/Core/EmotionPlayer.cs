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
    private EmotionSequenceInstance _sequence;
    private bool _isCrossfadingToNext;
    private float _crossfadeTimer;
    private float _crossfadeDuration;
    private ActionGroupConfig _pendingConfig;
    private string _facialOverride;
    private float _facialWeightOverride = -1f;

    public bool IsPlaying => _current != null && !_current.config.isIdle;
    public bool IsSequencePlaying => _sequence != null && !_sequence.IsFinished;
    public bool IsTTSPlaying { get; set; }
    public ActionGroupConfig CurrentConfig => _current?.config;
    public ActionGroupInstance CurrentInstance => _current;

    public event Action OnActionGroupStart;
    public event Action OnActionGroupEnd;

    public void PlayClipDirect(AnimationClip clip, bool loop = true, string bodyPart = "fullBody")
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
        config.bodyClips.Add(new PartClipEntry { bodyPart = bodyPart, clipName = clip.name, clip = clip });
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

        var entry = ActionSystemRuntime.GetMappingEntry(emotion);

        if (entry != null && entry.steps != null && entry.steps.Count > 0)
        {
            StartSequence(entry);
            return;
        }
        _sequence = null;

        _facialOverride = null;
        _facialWeightOverride = -1f;

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
        if (_sequence != null) _sequence.ttsStarted = true;
        if (_current != null)
        {
            _current.ttsStarted = true;
            _current.ttsEnded = false;
        }
    }

    public void NotifyTTSEnd()
    {
        IsTTSPlaying = false;
        if (_sequence != null) _sequence.ttsEnded = true;
        if (_current != null)
        {
            _current.ttsEnded = true;
            _current.holdTimer = 0f;
        }
    }

    public void NotifyTTSError()
    {
        IsTTSPlaying = false;
        if (_sequence != null) _sequence.ttsEnded = true;
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

    /// <summary>
    /// When non-empty, every transition plays THIS group's body clips while keeping the
    /// requested emotion's facial expression + lifecycle. Used by window-snap so the avatar
    /// stays seated (body) while messages only change the face. Null/empty = normal behavior.
    /// </summary>
    public string BodyLockActionGroup;

    public void TransitionTo(ActionGroupConfig config, bool instant)
    {
        if (_current != null && (_current.state == ActionGroupState.BlendingOut || _current.state == ActionGroupState.BlendingIn))
        {
            Debug.Log("[EmotionPlayer] TransitionTo: previous crossfade still in progress (state=" + _current.state + "), forcing finish");
            ForceIdle();
        }

        var clips = ResolveAllBodyClips(config);
        if (!string.IsNullOrEmpty(BodyLockActionGroup))
        {
            var lockConfig = ResolveConfig(BodyLockActionGroup);
            if (lockConfig != null) clips = ResolveAllBodyClips(lockConfig);
        }
        var instance = new ActionGroupInstance(config, clips);

        if (instant || _current == null)
        {
            _facialOverride = null;
            _facialWeightOverride = -1f;
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
        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = instance.config.allowRootMotion;

        Debug.Log("[EmotionPlayer] ApplyImmediate: override='" + _facialOverride + "' default='" + instance.config.facialPreset + "' → use='" + facialPreset + "' w=" + facialWeight + " arm=" + instance.config.allowRootMotion + " et=" + instance.config.enableEyeTracking);
        Debug.Log("[EmotionPlayer] ApplyImmediate: override='" + _facialOverride + "' default='" + instance.config.facialPreset + "' → use='" + facialPreset + "' w=" + facialWeight);

        if (!string.IsNullOrEmpty(facialPreset))
            facialEngine.PlayExpression(facialPreset, facialWeight, instance.config.blendInFacial);
        else
            facialEngine.RestoreExpression(instance.config.blendInFacial);

        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = instance.config.allowRootMotion;

        Debug.Log("[EmotionPlayer] ApplyImmediate: pos=" + (bodyEngine.animator != null ? bodyEngine.animator.transform.position.ToString("F2") : "?") +
            " frame=" + Time.frameCount);
        float blend = instance.config.blendInBody;
        string[] allParts = { "fullBody", "upperBody", "head", "leftArm", "rightArm", "lowerBody" };
        for (int p = 0; p < allParts.Length; p++)
        {
            string part = allParts[p];
            var rc = instance.GetClip(part);
            if (rc != null && rc.Value.clip != null)
            {
                Debug.Log("[EmotionPlayer] ApplyImmediate: " + part + " PLAY " + rc.Value.clip.name + " blend=" + blend);
                bodyEngine.Play(rc.Value.clip, part, blend, instance.config.loop);
            }
            else if (part != "fullBody")
            {
                Debug.Log("[EmotionPlayer] ApplyImmediate: " + part + " STOP blend=" + blend);
                bodyEngine.Stop(part, blend);
            }
        }

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

        if (bodyEngine != null && bodyEngine.animator != null)
            bodyEngine.animator.applyRootMotion = newInstance.config.allowRootMotion;

        Debug.Log("[EmotionPlayer] StartCrossfade: pos=" + (bodyEngine != null && bodyEngine.animator != null ? bodyEngine.animator.transform.position.ToString("F2") : "?") +
            " frame=" + Time.frameCount);
        float blend = Mathf.Max(blendOutBody, blendInBody);
        string[] allParts = { "fullBody", "upperBody", "head", "leftArm", "rightArm", "lowerBody" };
        for (int p = 0; p < allParts.Length; p++)
        {
            string part = allParts[p];
            var rc = newInstance.GetClip(part);
            if (rc != null && rc.Value.clip != null)
            {
                Debug.Log("[EmotionPlayer] StartCrossfade: " + part + " PLAY " + rc.Value.clip.name + " blend=" + blend);
                bodyEngine.Play(rc.Value.clip, part, blend, newInstance.config.loop);
            }
            else if (part != "fullBody")
            {
                Debug.Log("[EmotionPlayer] StartCrossfade: " + part + " STOP blend=" + blend);
                bodyEngine.Stop(part, blend);
            }
        }

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
        bool suppress = !config.isIdle && !config.enableEyeTracking;
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

        if (_sequence != null && !_sequence.IsFinished && _current != null && _current.state == ActionGroupState.Active)
        {
            if (!_current.config.loop)
            {
                if (bodyEngine.HasClipFinished("fullBody"))
                    AdvanceSequence();
            }
            else if (_sequence.ttsEnded)
            {
                AdvanceSequence();
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

    private void StartSequence(EmotionMappingEntry mapping)
    {
        _sequence = new EmotionSequenceInstance
        {
            steps = mapping.steps,
            currentStepIndex = -1,
            allOneShot = true
        };
        for (int i = 0; i < mapping.steps.Count; i++)
        {
            var g = ActionSystemRuntime.GetActionGroup(mapping.steps[i].actionGroupName);
            if (g != null && g.loop) { _sequence.allOneShot = false; break; }
        }
        Debug.Log("[EmotionPlayer] StartSequence: " + mapping.emotion + " steps=" + mapping.steps.Count + " allOneShot=" + _sequence.allOneShot);
        PlaySequenceStep(0, mapping.steps[0].blendDuration);
        OnActionGroupStart?.Invoke();
    }

    public void PreviewSequence(List<EmotionStepEntry> steps)
    {
        if (steps == null || steps.Count == 0) return;
        _sequence = new EmotionSequenceInstance
        {
            steps = steps,
            currentStepIndex = -1,
            allOneShot = true
        };
        for (int i = 0; i < steps.Count; i++)
        {
            var g = ActionSystemRuntime.GetActionGroup(steps[i].actionGroupName);
            if (g != null && g.loop) { _sequence.allOneShot = false; break; }
        }
        Debug.Log("[EmotionPlayer] PreviewSequence: steps=" + steps.Count + " allOneShot=" + _sequence.allOneShot);
        PlaySequenceStep(0, steps[0].blendDuration);
    }

    private void PlaySequenceStep(int index, float blendDuration)
    {
        if (_sequence == null || index >= _sequence.steps.Count) return;
        _sequence.currentStepIndex = index;
        var step = _sequence.steps[index];

        var config = ActionSystemRuntime.GetActionGroup(step.actionGroupName);
        if (config == null)
        {
            Debug.LogWarning("[EmotionPlayer] PlaySequenceStep: group '" + step.actionGroupName + "' not found, advancing");
            AdvanceSequence();
            return;
        }

        _facialOverride = !string.IsNullOrEmpty(step.facialOverride) ? step.facialOverride : null;
        _facialWeightOverride = step.facialWeightOverride;

        var playConfig = new ActionGroupConfig
        {
            groupName = config.groupName,
            facialPreset = config.facialPreset,
            facialWeight = config.facialWeight,
            bodyClips = config.bodyClips,
            loop = config.loop,
            blendInBody = blendDuration,
            blendInFacial = 0.01f,
            blendOutBody = config.blendOutBody,
            blendOutFacial = 0.01f,
            holdAfterTTS = config.holdAfterTTS,
            holdNoTTS = config.holdNoTTS,
            isIdle = false,
            allowRootMotion = config.allowRootMotion,
            enableEyeTracking = config.enableEyeTracking
        };

        Debug.Log("[EmotionPlayer] PlaySequenceStep: [" + index + "] " + step.actionGroupName +
            " blend=" + blendDuration + " loop=" + config.loop +
            " facial=" + (_facialOverride ?? config.facialPreset));
        TransitionTo(playConfig, blendDuration <= 0);
        if (_current != null) _current.suppressAutoEnd = true;
    }

    private void AdvanceSequence()
    {
        if (_sequence == null) return;
        int next = _sequence.currentStepIndex + 1;

        if (_sequence.ttsEnded)
        {
            while (next < _sequence.steps.Count)
            {
                var g = ActionSystemRuntime.GetActionGroup(_sequence.steps[next].actionGroupName);
                if (g != null && g.loop) { next++; continue; }
                break;
            }
        }

        if (next >= _sequence.steps.Count)
        {
            Debug.Log("[EmotionPlayer] AdvanceSequence: sequence finished");
            _sequence = null;
            OnActionGroupEnd?.Invoke();
            if (previewController != null && previewController.IsPreviewing)
                return;
            RestoreToIdle();
            return;
        }

        PlaySequenceStep(next, _sequence.steps[next].blendDuration);
    }

    public void ForceIdle()
    {
        _sequence = null;
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
            _facialOverride = null;
            _facialWeightOverride = -1f;
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
