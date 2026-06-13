using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class BodyEngine : MonoBehaviour
{
    public Animator animator;
    public AnimationClip idleClip;
    public bool allowRootMotion;
    public AvatarMask upperBodyMask;
    public AvatarMask headMask;
    public AvatarMask leftArmMask;
    public AvatarMask rightArmMask;
    public AvatarMask legsMask;

    private PlayableGraph _graph;
    private AnimationLayerMixerPlayable _layerMixer;
    private LayerData[] _layers;
    private bool _graphActive;
    private bool _isPreviewing;
    private bool _previewLock;
    private Vector3 _previewLockedPos;
    private Quaternion _previewLockedRot;

    private static readonly string[] BodyPartNames = { "fullBody", "upperBody", "head", "leftArm", "rightArm", "lowerBody" };

    private struct LayerData
    {
        public AnimationMixerPlayable mixer;
        public AnimationClipPlayable clipA;
        public AnimationClipPlayable clipB;
        public bool activeSlotIsA;
        public float crossfadeElapsed;
        public float crossfadeDuration;
        public bool isCrossfading;
        public bool isPlaying;
        public bool isLoop;
        public AnimationClip currentClip;
    }

    public bool IsGraphActive => _graphActive;

    public void SetPreviewing(bool value) { _isPreviewing = value; }

    public void StartPreviewLock()
    {
        if (animator == null) return;
        _previewLockedPos = animator.transform.position;
        _previewLockedRot = animator.transform.rotation;
        _previewLock = true;
    }

    public void EndPreviewLock()
    {
        if (animator == null) return;
        Debug.Log("[BodyEngine] EndPreviewLock: restore pos=" + _previewLockedPos.ToString("F2") + " lock=" + _previewLock + " frame=" + Time.frameCount);
        _previewLock = false;
        animator.transform.position = _previewLockedPos;
        animator.transform.rotation = _previewLockedRot;
    }

    void LateUpdate()
    {
        if (_previewLock && !animator.applyRootMotion && animator != null)
        {
            Debug.Log("[BodyEngine] LateUpdate LOCK: pos=" + _previewLockedPos.ToString("F2") + " frame=" + Time.frameCount);
            animator.transform.position = _previewLockedPos;
            animator.transform.rotation = _previewLockedRot;
        }
    }

    private void Awake()
    {
        if (animator != null)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = allowRootMotion;
        }
        BuildGraph();
    }

    private void BuildGraph()
    {
        if (animator == null) return;

        _graph = PlayableGraph.Create("BodyEngine");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        int layerCount = BodyPartNames.Length;
        _layerMixer = AnimationLayerMixerPlayable.Create(_graph, layerCount);
        _layers = new LayerData[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            var mixer = AnimationMixerPlayable.Create(_graph, 2);
            _layerMixer.ConnectInput(i, mixer, 0);
            _layerMixer.SetInputWeight(i, i == 0 ? 1f : 0f);

            AvatarMask mask = GetMaskForLayer(i);
            if (mask != null && i > 0)
            {
                _layerMixer.SetLayerMaskFromAvatarMask((uint)i, mask);
                _layerMixer.SetLayerAdditive((uint)i, false);
            }

            var clipA = AnimationClipPlayable.Create(_graph, idleClip != null ? idleClip : null);
            var clipB = AnimationClipPlayable.Create(_graph, idleClip != null ? idleClip : null);
            mixer.ConnectInput(0, clipA, 0);
            mixer.ConnectInput(1, clipB, 0);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);

            _layers[i] = new LayerData
            {
                mixer = mixer,
                clipA = clipA,
                clipB = clipB,
                activeSlotIsA = true,
                isPlaying = i == 0,
                isLoop = i == 0,
                currentClip = idleClip
            };
        }

        var output = AnimationPlayableOutput.Create(_graph, "BodyOutput", animator);
        output.SetSourcePlayable(_layerMixer);

        _graph.Play();
        _graphActive = true;
    }

    private AvatarMask GetMaskForLayer(int layer)
    {
        switch (layer)
        {
            case 1: return upperBodyMask;
            case 2: return headMask;
            case 3: return leftArmMask;
            case 4: return rightArmMask;
            case 5: return legsMask;
            default: return null;
        }
    }

    public int GetLayerIndex(string bodyPart)
    {
        for (int i = 0; i < BodyPartNames.Length; i++)
            if (BodyPartNames[i] == bodyPart) return i;
        return 0;
    }

    public void Play(AnimationClip clip, string bodyPart = "fullBody", float blendDuration = 0.35f, bool loop = false)
    {
        if (clip == null) { Debug.LogWarning("[BodyEngine] Play: clip is null!"); return; }
        if (!_graphActive) { Debug.LogWarning("[BodyEngine] Play: graph not active!"); return; }
        Debug.Log("[BodyEngine] Play: " + clip.name + " part=" + bodyPart + " blend=" + blendDuration + " loop=" + loop);
        int layer = GetLayerIndex(bodyPart);
        PlayOnLayer(layer, clip, blendDuration, loop);
    }

    public void Stop(string bodyPart = "fullBody", float blendDuration = 0.35f)
    {
        int layer = GetLayerIndex(bodyPart);
        if (layer == 0)
        {
            if (idleClip != null)
                PlayOnLayer(0, idleClip, blendDuration, true);
        }
        else
        {
            StartCoroutine(FadeOutLayer(layer, blendDuration));
        }
    }

    public void StopAll(float blendDuration = 0.35f)
    {
        for (int i = 1; i < _layers.Length; i++)
        {
            if (_layers[i].isPlaying)
                StartCoroutine(FadeOutLayer(i, blendDuration));
        }
        if (idleClip != null)
            PlayOnLayer(0, idleClip, blendDuration, true);
    }

    public bool IsPlaying(string bodyPart = "fullBody")
    {
        int layer = GetLayerIndex(bodyPart);
        if (layer == 0)
            return _layers[0].isPlaying && _layers[0].currentClip != idleClip;
        return _layers[layer].isPlaying;
    }

    public bool IsAnyNonIdlePlaying()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            if (i == 0 && _layers[i].currentClip != idleClip && _layers[i].isPlaying) return true;
            if (i > 0 && _layers[i].isPlaying) return true;
        }
        return false;
    }

    public float GetCurrentClipLength(string bodyPart = "fullBody")
    {
        int layer = GetLayerIndex(bodyPart);
        return _layers[layer].currentClip != null ? _layers[layer].currentClip.length : 0f;
    }

    public float GetCurrentClipTime(string bodyPart = "fullBody")
    {
        int layer = GetLayerIndex(bodyPart);
        var data = _layers[layer];
        var active = data.activeSlotIsA ? data.clipA : data.clipB;
        return (float)active.GetTime();
    }

    public bool HasClipFinished(string bodyPart = "fullBody")
    {
        int layer = GetLayerIndex(bodyPart);
        var data = _layers[layer];
        if (data.isLoop || data.currentClip == null) return false;
        var active = data.activeSlotIsA ? data.clipA : data.clipB;
        return (float)active.GetTime() >= data.currentClip.length;
    }

    private void PlayOnLayer(int layer, AnimationClip clip, float blendDuration, bool loop)
    {
        Debug.Log("[BodyEngine] PlayOnLayer: layer=" + layer + "(" + BodyPartNames[layer] + ") clip=" + clip.name + " blend=" + blendDuration.ToString("F2") + " frame=" + Time.frameCount);
        var data = _layers[layer];

        if (layer > 0)
        {
            float current = _layerMixer.GetInputWeight(layer);
            if (current < 0.99f)
                StartCoroutine(FadeInLayer(layer, blendDuration));
            else
                _layerMixer.SetInputWeight(layer, 1f);
        }

        int inactiveIndex = data.activeSlotIsA ? 1 : 0;
        int activeIndex = data.activeSlotIsA ? 0 : 1;

        data.mixer.DisconnectInput(inactiveIndex);

        var newClip = AnimationClipPlayable.Create(_graph, clip);
        newClip.SetDuration(clip.length);
        data.mixer.ConnectInput(inactiveIndex, newClip, 0);

        if (data.activeSlotIsA)
            data.clipB = newClip;
        else
            data.clipA = newClip;

        data.crossfadeElapsed = 0f;
        data.crossfadeDuration = blendDuration;
        data.isCrossfading = true;
        data.activeSlotIsA = !data.activeSlotIsA;
        data.isPlaying = true;
        data.isLoop = loop;
        data.currentClip = clip;

        _layers[layer] = data;
    }

    private IEnumerator FadeInLayer(int layer, float duration)
    {
        Debug.Log("[BodyEngine] FadeInLayer START: layer=" + layer + "(" + BodyPartNames[layer] + ") duration=" + duration.ToString("F2") + " frame=" + Time.frameCount);
        float startWeight = _layerMixer.GetInputWeight(layer);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _layerMixer.SetInputWeight(layer, Mathf.Lerp(startWeight, 1f, elapsed / duration));
            yield return null;
        }
        _layerMixer.SetInputWeight(layer, 1f);
        Debug.Log("[BodyEngine] FadeInLayer DONE: layer=" + layer + "(" + BodyPartNames[layer] + ") frame=" + Time.frameCount);
    }

    private IEnumerator FadeOutLayer(int layer, float duration)
    {
        Debug.Log("[BodyEngine] FadeOutLayer START: layer=" + layer + "(" + BodyPartNames[layer] + ") duration=" + duration.ToString("F2") + " frame=" + Time.frameCount);
        float elapsed = 0f;
        float startWeight = _layerMixer.GetInputWeight(layer);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _layerMixer.SetInputWeight(layer, Mathf.Lerp(startWeight, 0f, t));
            yield return null;
        }
        _layerMixer.SetInputWeight(layer, 0f);
        var data = _layers[layer];
        data.isPlaying = false;
        _layers[layer] = data;
        Debug.Log("[BodyEngine] FadeOutLayer DONE: layer=" + layer + "(" + BodyPartNames[layer] + ") frame=" + Time.frameCount);
    }

    private void Update()
    {
        if (!_graphActive || _layers == null) return;

        for (int i = 0; i < _layers.Length; i++)
        {
            if (!_layers[i].isCrossfading) continue;

            var data = _layers[i];
            data.crossfadeElapsed += Time.deltaTime;
            float t = data.crossfadeDuration > 0 ? Mathf.Clamp01(data.crossfadeElapsed / data.crossfadeDuration) : 1f;

            int activeIndex = data.activeSlotIsA ? 0 : 1;
            int inactiveIndex = data.activeSlotIsA ? 1 : 0;
            data.mixer.SetInputWeight(activeIndex, t);
            data.mixer.SetInputWeight(inactiveIndex, 1f - t);

            if (t >= 1f)
            {
                data.isCrossfading = false;
                Debug.Log("[BodyEngine] Crossfade DONE: layer=" + i + "(" + BodyPartNames[i] + ") frame=" + Time.frameCount);
            }

            _layers[i] = data;
        }

        for (int i = 0; i < _layers.Length; i++)
        {
            if (!_layers[i].isLoop || !_layers[i].isPlaying || _layers[i].isCrossfading || _layers[i].currentClip == null) continue;
            var active = _layers[i].activeSlotIsA ? _layers[i].clipA : _layers[i].clipB;
            if ((float)active.GetTime() >= _layers[i].currentClip.length - 0.05f)
            {
                active.SetTime(0f);
                Debug.Log("[BodyEngine] Loop reset: layer=" + i + "(" + BodyPartNames[i] + ") clip=" + _layers[i].currentClip.name + " frame=" + Time.frameCount);
            }
        }
    }

    private void OnDestroy()
    {
        if (_graph.IsValid())
            _graph.Destroy();
    }
}
