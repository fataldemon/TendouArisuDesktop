using System;
using System.Collections.Generic;
using UnityEngine;

public class FacialEngine : MonoBehaviour
{
    public SkinnedMeshRenderer meshRenderer;

    public GameObject[] effectObjects;
    public Material normalBlushMaterial;
    public Material shyBlushMaterial;
    public MeshRenderer[] blushRenderers;

    private Dictionary<int, BlendState> _activeBlends = new Dictionary<int, BlendState>();
    private HashSet<string> _activeEffects = new HashSet<string>();
    private string _currentBlushMode;
    private float _blendDuration;
    private bool _isBlending;
    private Action _onBlendComplete;
    private ModelExpressionProfile _modelProfile;

    public void SetModelExpressionProfile(ModelExpressionProfile profile)
    {
        _modelProfile = profile;
        Debug.Log("[FacialEngine] SetModelExpressionProfile: modelKey=" + (profile != null ? profile.modelKey : "null") + " presets=" + (profile != null ? profile.presets.Count : 0));
    }

    public void ClearModelExpressionProfile()
    {
        _modelProfile = null;
    }

    public ModelExpressionProfile GetModelExpressionProfile()
    {
        return _modelProfile;
    }

    public List<FacialPresetConfig> GetModelPresetsOrDefault()
    {
        if (_modelProfile != null && _modelProfile.presets.Count > 0)
            return _modelProfile.presets;
        return ActionSystemRuntime.FacialPresets;
    }

    private struct BlendState
    {
        public float currentWeight;
        public float fromWeight;
        public float targetWeight;
        public float elapsed;
        public float duration;
    }

    public FacialPresetConfig GetPreset(string presetName)
    {
        if (_modelProfile != null)
        {
            var p = _modelProfile.Find(presetName);
            if (p != null) return p;
        }
        var global = ActionSystemRuntime.GetFacialPreset(presetName);
        if (global == null && !string.IsNullOrEmpty(presetName))
            Debug.LogWarning("[FacialEngine] Preset not found: '" + presetName + "'");
        return global;
    }

    public void PlayExpression(string presetName, float weight = 1f, float duration = 0.15f, Action onComplete = null)
    {
        var preset = GetPreset(presetName);
        if (meshRenderer == null) { Debug.LogWarning("[FacialEngine] PlayExpression: meshRenderer is NULL! Cannot apply '" + presetName + "'"); return; }
        if (preset == null) { Debug.LogWarning("[FacialEngine] PlayExpression: preset '" + presetName + "' not found"); return; }
        Debug.Log("[FacialEngine] PlayExpression: '" + presetName + "' weight=" + weight + " targets=" + preset.targets.Count);

        var existingKeys = new List<int>(_activeBlends.Keys);
        for (int k = 0; k < existingKeys.Count; k++)
        {
            int idx = existingKeys[k];
            var state = _activeBlends[idx];
            state.fromWeight = state.currentWeight;
            state.targetWeight = 0f;
            state.elapsed = 0f;
            state.duration = duration;
            _activeBlends[idx] = state;
        }

        for (int i = 0; i < preset.targets.Count; i++)
        {
            var t = preset.targets[i];
            float target = t.weight * weight;
            if (_activeBlends.TryGetValue(t.index, out var existing))
            {
                existing.fromWeight = existing.currentWeight;
                existing.targetWeight = target;
                existing.elapsed = 0f;
                existing.duration = duration;
                _activeBlends[t.index] = existing;
            }
            else
            {
                _activeBlends[t.index] = new BlendState
                {
                    currentWeight = 0f,
                    fromWeight = 0f,
                    targetWeight = target,
                    elapsed = 0f,
                    duration = duration
                };
            }
        }

        DeactivateAllEffects();
        ActivateEffects(preset.activateObjects);
        ApplyBlush(preset.blushMode);

        _isBlending = true;
        _onBlendComplete = onComplete;
    }

    public void RestoreExpression(float duration = 0.2f, Action onComplete = null)
    {
        var keys = new List<int>(_activeBlends.Keys);
        for (int k = 0; k < keys.Count; k++)
        {
            int idx = keys[k];
            var state = _activeBlends[idx];
            state.fromWeight = state.currentWeight;
            state.targetWeight = 0f;
            state.elapsed = 0f;
            state.duration = duration;
            _activeBlends[idx] = state;
        }

        _isBlending = true;
        _onBlendComplete = () =>
        {
            DeactivateAllEffects();
            ApplyBlush(null);
            onComplete?.Invoke();
        };
    }

    public void CrossfadeTo(string presetName, float weight, float blendOutDuration, float blendInDuration, Action onComplete = null)
    {
        var preset = GetPreset(presetName);
        if (meshRenderer == null) { Debug.LogWarning("[FacialEngine] CrossfadeTo: meshRenderer is NULL!"); RestoreExpression(blendOutDuration, onComplete); return; }
        if (preset == null) { Debug.LogWarning("[FacialEngine] CrossfadeTo: preset '" + presetName + "' not found, restoring"); RestoreExpression(blendOutDuration, onComplete); return; }
        Debug.Log("[FacialEngine] CrossfadeTo: '" + presetName + "' weight=" + weight);

        var newTargets = new HashSet<int>();
        for (int i = 0; i < preset.targets.Count; i++)
            newTargets.Add(preset.targets[i].index);

        var blendKeys = new List<int>(_activeBlends.Keys);
        for (int k = 0; k < blendKeys.Count; k++)
        {
            int idx = blendKeys[k];
            if (newTargets.Contains(idx)) continue;
            var state = _activeBlends[idx];
            state.fromWeight = state.currentWeight;
            state.targetWeight = 0f;
            state.elapsed = 0f;
            state.duration = blendOutDuration;
            _activeBlends[idx] = state;
        }

        for (int i = 0; i < preset.targets.Count; i++)
        {
            var t = preset.targets[i];
            float target = t.weight * weight;
            if (_activeBlends.TryGetValue(t.index, out var existing))
            {
                existing.fromWeight = existing.currentWeight;
                existing.targetWeight = target;
                existing.elapsed = 0f;
                existing.duration = blendInDuration;
                _activeBlends[t.index] = existing;
            }
            else
            {
                _activeBlends[t.index] = new BlendState
                {
                    currentWeight = 0f,
                    fromWeight = 0f,
                    targetWeight = target,
                    elapsed = 0f,
                    duration = blendInDuration
                };
            }
        }

        DeactivateAllEffects();
        ActivateEffects(preset.activateObjects);
        ApplyBlush(preset.blushMode);

        _isBlending = true;
        _onBlendComplete = onComplete;
    }

    public void PreviewInstant(string presetName, float weight = 1f)
    {
        var preset = GetPreset(presetName);
        if (preset == null || meshRenderer == null) return;

        for (int i = 0; i < preset.targets.Count; i++)
        {
            var t = preset.targets[i];
            float w = t.weight * weight;
            meshRenderer.SetBlendShapeWeight(t.index, w);
            _activeBlends[t.index] = new BlendState
            {
                currentWeight = w,
                fromWeight = w,
                targetWeight = w,
                elapsed = 1f,
                duration = 1f
            };
        }

        DeactivateAllEffects();
        ActivateEffects(preset.activateObjects);
        ApplyBlush(preset.blushMode);
        _isBlending = false;
    }

    public void ApplyPresetDirect(FacialPresetConfig preset, float weight = 1f)
    {
        if (meshRenderer == null || preset == null) return;
        ResetInstant();
        for (int i = 0; i < preset.targets.Count; i++)
        {
            var t = preset.targets[i];
            float w = t.weight * weight;
            meshRenderer.SetBlendShapeWeight(t.index, w);
            _activeBlends[t.index] = new BlendState
            {
                currentWeight = w,
                fromWeight = w,
                targetWeight = w,
                elapsed = 1f,
                duration = 1f
            };
        }
        ActivateEffects(preset.activateObjects);
        ApplyBlush(preset.blushMode);
        _isBlending = false;
    }

    public void ResetInstant()
    {
        if (meshRenderer != null)
        {
            foreach (var kv in _activeBlends)
                meshRenderer.SetBlendShapeWeight(kv.Key, 0f);
        }
        _activeBlends.Clear();
        DeactivateAllEffects();
        ApplyBlush(null);
        _isBlending = false;
        _onBlendComplete = null;
    }

    public bool IsBlending => _isBlending;

    private void Update()
    {
        if (!_isBlending || meshRenderer == null) return;

        bool allDone = true;
        var keys = new List<int>(_activeBlends.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            int idx = keys[i];
            var state = _activeBlends[idx];
            state.elapsed += Time.deltaTime;
            float t = state.duration > 0f ? Mathf.Clamp01(state.elapsed / state.duration) : 1f;
            state.currentWeight = Mathf.Lerp(state.fromWeight, state.targetWeight, t);
            meshRenderer.SetBlendShapeWeight(idx, state.currentWeight);
            _activeBlends[idx] = state;

            if (t < 1f) allDone = false;
        }

        if (allDone)
        {
            var toRemove = new List<int>();
            foreach (var kv in _activeBlends)
            {
                if (Mathf.Approximately(kv.Value.targetWeight, 0f) && Mathf.Approximately(kv.Value.currentWeight, 0f))
                    toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
                _activeBlends.Remove(toRemove[i]);

            _isBlending = false;
            var cb = _onBlendComplete;
            _onBlendComplete = null;
            cb?.Invoke();
        }
    }

    private void ActivateEffects(List<string> objectNames)
    {
        if (objectNames == null || effectObjects == null) return;
        for (int i = 0; i < objectNames.Count; i++)
        {
            var obj = FindEffect(objectNames[i]);
            if (obj != null)
            {
                obj.SetActive(true);
                _activeEffects.Add(objectNames[i]);
            }
        }
    }

    private void DeactivateAllEffects()
    {
        if (effectObjects == null) return;
        for (int i = 0; i < effectObjects.Length; i++)
        {
            if (effectObjects[i] != null)
                effectObjects[i].SetActive(false);
        }
        _activeEffects.Clear();
    }

    private void ApplyBlush(string mode)
    {
        if (blushRenderers == null || blushRenderers.Length == 0) return;
        _currentBlushMode = mode;
        if (string.IsNullOrEmpty(mode))
        {
            for (int i = 0; i < blushRenderers.Length; i++)
                if (blushRenderers[i] != null) blushRenderers[i].enabled = false;
            return;
        }
        Material mat = (mode == "shy") ? shyBlushMaterial : normalBlushMaterial;
        if (mat == null) return;
        for (int i = 0; i < blushRenderers.Length; i++)
        {
            if (blushRenderers[i] != null)
            {
                blushRenderers[i].enabled = true;
                blushRenderers[i].material = mat;
            }
        }
    }

    private GameObject FindEffect(string name)
    {
        if (effectObjects == null) return null;
        for (int i = 0; i < effectObjects.Length; i++)
        {
            if (effectObjects[i] != null && effectObjects[i].name == name)
                return effectObjects[i];
        }
        return null;
    }

    public List<string> GetAllPresetNames()
    {
        var presets = ActionSystemRuntime.FacialPresets;
        var names = new List<string>();
        for (int i = 0; i < presets.Count; i++)
            names.Add(presets[i].presetName);
        return names;
    }
}
