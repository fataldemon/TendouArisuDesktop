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
    private Coroutine _previewRoutine;
    private Vector3 _savedPos;
    private Quaternion _savedRot;
    private Vector3 _savedRootLocalPos;
    private Quaternion _savedRootLocalRot;

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

    public bool IsPreviewing => _isPreviewing;
    public bool IsGraphActive => _graphActive;

    private void Awake()
    {
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
        var data = _layers[layer];

        if (layer > 0)
            _layerMixer.SetInputWeight(layer, 1f);

        var inactiveSlot = data.activeSlotIsA ? data.clipB : data.clipA;
        int inactiveIndex = data.activeSlotIsA ? 1 : 0;

        inactiveSlot.GetGraph().Disconnect(data.mixer, inactiveIndex);
        var newClip = AnimationClipPlayable.Create(_graph, clip);
        newClip.SetDuration(clip.length);
        if (loop)
            newClip.GetAnimationClip(); // Playable respects clip's WrapMode
        data.mixer.ConnectInput(inactiveIndex, newClip, 0);

        if (data.activeSlotIsA)
            data.clipB = newClip;
        else
            data.clipA = newClip;

        inactiveSlot.Destroy();

        data.crossfadeElapsed = 0f;
        data.crossfadeDuration = blendDuration;
        data.isCrossfading = true;
        data.activeSlotIsA = !data.activeSlotIsA;
        data.isPlaying = true;
        data.isLoop = loop;
        data.currentClip = clip;

        _layers[layer] = data;
    }

    private IEnumerator FadeOutLayer(int layer, float duration)
    {
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
                data.isCrossfading = false;

            _layers[i] = data;
        }
    }

    public void Pause()
    {
        if (_graph.IsValid())
            _graph.Stop();
    }

    public void Resume()
    {
        if (_graph.IsValid())
            _graph.Play();
        _isPreviewing = false;
    }

    public void PreviewSample(AnimationClip clip, bool loop = true)
    {
        if (clip == null || animator == null) return;

        if (_previewRoutine != null)
        {
            StopCoroutine(_previewRoutine);
            _previewRoutine = null;
        }

        if (!_isPreviewing)
        {
            _savedPos = animator.transform.position;
            _savedRot = animator.transform.rotation;
            var root = animator.transform.Find("root");
            _savedRootLocalPos = root != null ? root.localPosition : Vector3.zero;
            _savedRootLocalRot = root != null ? root.localRotation : Quaternion.identity;
            Pause();
            _isPreviewing = true;
        }

        _previewRoutine = StartCoroutine(PreviewCoroutine(clip, loop));
    }

    public void StopPreview()
    {
        if (!_isPreviewing) return;

        if (_previewRoutine != null)
        {
            StopCoroutine(_previewRoutine);
            _previewRoutine = null;
        }

        animator.transform.position = _savedPos;
        animator.transform.rotation = _savedRot;
        var root = animator.transform.Find("root");
        if (root != null)
        {
            root.localPosition = _savedRootLocalPos;
            root.localRotation = _savedRootLocalRot;
        }

        _isPreviewing = false;
        Resume();
    }

    private IEnumerator PreviewCoroutine(AnimationClip clip, bool loop)
    {
        float elapsed = 0f;
        while (_isPreviewing)
        {
            float sampleTime = loop ? (elapsed % clip.length) : Mathf.Min(elapsed, clip.length);
            clip.SampleAnimation(animator.gameObject, sampleTime);

            if (!allowRootMotion)
            {
                animator.transform.position = _savedPos;
                animator.transform.rotation = _savedRot;
                var root = animator.transform.Find("root");
                if (root != null)
                {
                    root.localPosition = _savedRootLocalPos;
                    root.localRotation = _savedRootLocalRot;
                }
            }

            elapsed += Time.deltaTime;
            if (!loop && elapsed >= clip.length) break;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (_graph.IsValid())
            _graph.Destroy();
    }
}
