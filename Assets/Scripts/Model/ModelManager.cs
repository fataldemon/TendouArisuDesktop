using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UniVRM10;

public class ModelManager : MonoBehaviour
{
    public GameObject currentModel;
    public BodyEngine bodyEngine;
    public FacialEngine facialEngine;
    public Transform modelParent;
    public PipeServer pipeServer;
    public EyeTrackingController eyeTrackingController;
    public BlinkController blinkController;

    [SerializeField] private List<string> modelHistory = new List<string>();

    private GameObject defaultModel;
    private Quaternion defaultRotation;

    private void Awake()
    {
        defaultModel = currentModel;
        defaultRotation = defaultModel.transform.localRotation;
        modelParent = defaultModel.transform.parent;
        LoadHistory();
    }

    public void LoadModel(string vrmPath)
    {
        if (string.IsNullOrEmpty(vrmPath) || !File.Exists(vrmPath))
        {
            Debug.LogError("ModelManager: file not found: " + vrmPath);
            return;
        }

        string ext = Path.GetExtension(vrmPath).ToLower();
        if (ext != ".vrm")
        {
            Debug.LogError("ModelManager: not a .vrm file: " + vrmPath);
            return;
        }

        try
        {
            using (var fs = File.OpenRead(vrmPath))
            {
                byte[] magic = new byte[4];
                if (fs.Read(magic, 0, 4) < 4)
                {
                    Debug.LogError("ModelManager: file too small: " + vrmPath);
                    return;
                }
                uint magicU32 = BitConverter.ToUInt32(magic, 0);
                if (magicU32 != 0x46546C67)
                {
                    Debug.LogError("ModelManager: not a valid VRM/glTF file: " + vrmPath);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("ModelManager: failed to read file header: " + e.Message);
            return;
        }

        StartCoroutine(LoadVrmCoroutine(vrmPath));
    }

    private System.Collections.IEnumerator LoadVrmCoroutine(string path)
    {
        Debug.Log("[ModelManager] LoadVrmCoroutine START: path=" + path);
        var task = Vrm10.LoadPathAsync(path);
        while (!task.IsCompleted)
            yield return null;

        var instance = task.Result;
        Debug.Log("[ModelManager] LoadVrmCoroutine: task.Result=" + (instance != null ? instance.name : "NULL"));
        if (instance == null)
        {
            Debug.LogError("ModelManager: failed to load VRM");
            yield break;
        }

        instance.transform.SetParent(modelParent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = defaultRotation;

        _currentModelKey = ModelExpressionIO.ComputeModelKey(path);
        ReplaceModel(instance.gameObject);

        string modelName = Path.GetFileNameWithoutExtension(path);
        AddToHistory(path, modelName);
    }

    private string _currentModelKey;
    public string CurrentModelKey => _currentModelKey;
    public ModelEyeProfile CurrentEyeProfile { get; set; }

    private void ReplaceModel(GameObject newModel)
    {
        Debug.Log("[ModelManager] ReplaceModel START: new=" + newModel.name + " current=" + (currentModel != null ? currentModel.name : "NULL"));

        if (!string.IsNullOrEmpty(_currentModelKey))
            newModel.transform.localScale = Vector3.one * ModelScaleIO.GetScale(_currentModelKey);

        if (currentModel != null)
        {
            if (currentModel == defaultModel)
                defaultModel.SetActive(false);
            else
                Destroy(currentModel);
        }

        currentModel = newModel;

        if (bodyEngine != null)
        {
            var animator = currentModel.GetComponent<Animator>();
            if (animator == null)
                animator = currentModel.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            bodyEngine.animator = animator;
            bodyEngine.RebuildGraph();
        }

        if (facialEngine != null)
        {
            var renderer = FindBestBlendShapeRenderer(currentModel);
            if (renderer != null)
                facialEngine.meshRenderer = renderer;
            facialEngine.ResetInstant();

            if (!string.IsNullOrEmpty(_currentModelKey))
            {
                var profile = ModelExpressionIO.Load(_currentModelKey);
                if (profile == null)
                {
                    profile = BuildDefaultProfile(_currentModelKey, newModel);
                    ModelExpressionIO.Save(profile);
                    Debug.Log("[ModelManager] Created default expression profile for model " + _currentModelKey + " with " + profile.presets.Count + " presets");
                }
                else
                {
                    Debug.Log("[ModelManager] Loaded existing expression profile for model " + _currentModelKey + " (" + profile.presets.Count + " presets)");
                    EnsureRequiredPresets(profile);
                    ModelExpressionIO.Save(profile);
                }
                facialEngine.SetModelExpressionProfile(profile);
            }
        }

        // Eye profile — per-model eye tracking/blink BlendShape mapping
        if (!string.IsNullOrEmpty(_currentModelKey))
        {
            var eyeProfile = ModelEyeIO.Load(_currentModelKey);
            if (eyeProfile == null)
            {
                eyeProfile = BuildEyeProfileFromVrm(_currentModelKey, newModel);
                ModelEyeIO.Save(eyeProfile);
            }
            ApplyEyeProfile(eyeProfile);
        }

        var ep = bodyEngine != null ? bodyEngine.GetComponent<EmotionPlayer>() : null;
        if (ep == null) ep = FindObjectOfType<EmotionPlayer>();
        if (ep != null)
            ep.ForceIdle();

        pipeServer?.RefreshInitData();
        Debug.Log("[ModelManager] ReplaceModel DONE: current=" + currentModel.name);
    }

    public void RestoreDefault()
    {
        Debug.Log("[ModelManager] RestoreDefault: current=" + (currentModel != null ? currentModel.name : "NULL") + " → default=" + (defaultModel != null ? defaultModel.name : "NULL"));
        if (defaultModel == null) return;

        if (currentModel != null && currentModel != defaultModel)
            Destroy(currentModel);

        defaultModel.SetActive(true);
        currentModel = defaultModel;
        _currentModelKey = null;

        if (bodyEngine != null)
        {
            var a = defaultModel.GetComponent<Animator>();
            if (a != null) a.runtimeAnimatorController = null;
            bodyEngine.animator = a;
            bodyEngine.RebuildGraph();
        }

        if (facialEngine != null)
        {
            var renderer = FindBestBlendShapeRenderer(defaultModel);
            if (renderer != null)
                facialEngine.meshRenderer = renderer;
            facialEngine.ClearModelExpressionProfile();
            facialEngine.ResetInstant();
        }

        var ep = bodyEngine != null ? bodyEngine.GetComponent<EmotionPlayer>() : null;
        if (ep == null) ep = FindObjectOfType<EmotionPlayer>();
        if (ep != null)
            ep.ForceIdle();

        SaveHistory();
        pipeServer?.RefreshInitData();
    }

    private void AddToHistory(string path, string name)
    {
        modelHistory.RemoveAll(h => h.StartsWith(path + "|"));
        modelHistory.Insert(0, path + "|" + name);
        if (modelHistory.Count > 20)
            modelHistory.RemoveAt(modelHistory.Count - 1);
        SaveHistory();
    }

    public List<string> GetHistory()
    {
        return modelHistory.Select(h =>
        {
            var parts = h.Split('|');
            return parts.Length > 1 ? parts[1] : h;
        }).ToList();
    }

    public void LoadFromHistory(int index)
    {
        if (index < 0 || index >= modelHistory.Count) return;
        var parts = modelHistory[index].Split('|');
        if (parts.Length > 0)
            LoadModel(parts[0]);
    }

    private void SaveHistory()
    {
        string json = JsonUtility.ToJson(new HistoryWrapper { entries = modelHistory });
        File.WriteAllText(GetHistoryPath(), json);
    }

    private void LoadHistory()
    {
        string path = GetHistoryPath();
        if (File.Exists(path))
        {
            var wrapper = JsonUtility.FromJson<HistoryWrapper>(File.ReadAllText(path));
            if (wrapper != null)
                modelHistory = wrapper.entries ?? new List<string>();
        }
    }

    private string GetHistoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "model_history.json");
    }

    [Serializable]
    private class HistoryWrapper
    {
        public List<string> entries;
    }

    private ModelExpressionProfile BuildDefaultProfile(string modelKey, GameObject model)
    {
        var profile = new ModelExpressionProfile { modelKey = modelKey };

        var vrmInstance = model.GetComponent<Vrm10Instance>();
        if (vrmInstance != null && vrmInstance.Vrm != null)
        {
            var expression = vrmInstance.Vrm.Expression;
            var clips = expression?.Clips;
            if (clips != null)
            {
                var mappedNames = new HashSet<string>();
                foreach (var (preset, clip) in clips)
                {
                    if (clip == null || preset == ExpressionPreset.custom) continue;
                    string presetName = MapVrmPreset(preset);
                    if (string.IsNullOrEmpty(presetName) || mappedNames.Contains(presetName)) continue;
                    mappedNames.Add(presetName);

                    var config = new FacialPresetConfig { presetName = presetName };
                    var bindings = clip.MorphTargetBindings;
                    if (bindings != null)
                    {
                        foreach (var b in bindings)
                            config.targets.Add(new BlendShapeTarget { index = b.Index, weight = b.Weight * 100f });
                    }
                    profile.presets.Add(config);
                    Debug.Log("[ModelManager]   " + presetName + " ← VRM " + preset + " (" + config.targets.Count + " blends)");
                }

                Debug.Log("[ModelManager] BuildDefaultProfile: mapped " + profile.presets.Count + " VRM expression presets");
            }
        }

        if (profile.presets.Count == 0)
        {
            Debug.Log("[ModelManager] BuildDefaultProfile: no VRM presets mapped, creating empty preset shells");
        }

        // Always fill in any preset names referenced by emotion mappings or action groups
        EnsureRequiredPresets(profile);

        return profile;
    }

    private void EnsureRequiredPresets(ModelExpressionProfile profile)
    {
        var usedNames = new HashSet<string>();
        foreach (var m in ActionSystemRuntime.EmotionMappings)
            if (!string.IsNullOrEmpty(m.facialOverride))
                usedNames.Add(m.facialOverride);
        foreach (var g in ActionSystemRuntime.ActionGroups)
            if (!string.IsNullOrEmpty(g.facialPreset))
                usedNames.Add(g.facialPreset);

        foreach (var name in usedNames)
        {
            if (profile.Find(name) == null)
            {
                profile.presets.Add(new FacialPresetConfig { presetName = name });
                Debug.Log("[ModelManager] EnsureRequiredPresets: added shell preset '" + name + "' from references");
            }
        }
    }

    private static SkinnedMeshRenderer FindBestBlendShapeRenderer(GameObject model)
    {
        SkinnedMeshRenderer best = null;
        int maxBlends = 0;
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (r.sharedMesh != null && r.sharedMesh.blendShapeCount > maxBlends)
            {
                best = r;
                maxBlends = r.sharedMesh.blendShapeCount;
            }
        }
        if (best == null)
            best = model.GetComponentInChildren<SkinnedMeshRenderer>();
        Debug.Log("[ModelManager] FindBestBlendShapeRenderer: " + (best != null ? best.name + " (" + maxBlends + " blends)" : "none"));
        return best;
    }

    public ModelEyeProfile BuildEyeProfileFromVrm(string modelKey, GameObject model)
    {
        var profile = new ModelEyeProfile { modelKey = modelKey, blinkIndex = -1, lookLeftIndex = -1, lookRightIndex = -1, lookUpIndex = -1, lookDownIndex = -1 };
        var vrm = model.GetComponent<Vrm10Instance>();
        if (vrm == null || vrm.Vrm == null) return profile;

        var clips = vrm.Vrm.Expression?.Clips;
        if (clips != null)
        {
            foreach (var (preset, clip) in clips)
            {
                if (clip == null || clip.MorphTargetBindings == null || clip.MorphTargetBindings.Length == 0) continue;
                int idx = clip.MorphTargetBindings[0].Index;

                switch (preset)
                {
                    case ExpressionPreset.blink: profile.blinkIndex = idx; break;
                    case ExpressionPreset.lookLeft: profile.lookLeftIndex = idx; break;
                    case ExpressionPreset.lookRight: profile.lookRightIndex = idx; break;
                    case ExpressionPreset.lookUp: profile.lookUpIndex = idx; break;
                    case ExpressionPreset.lookDown: profile.lookDownIndex = idx; break;
                }

                if ((int)clip.OverrideBlink != 0)
                    foreach (var b in clip.MorphTargetBindings)
                        if (!profile.blinkConflictIndices.Contains(b.Index))
                            profile.blinkConflictIndices.Add(b.Index);
            }
        }
        Debug.Log("[ModelManager] BuildEyeProfileFromVrm: blink=" + profile.blinkIndex + " L=" + profile.lookLeftIndex + " R=" + profile.lookRightIndex + " U=" + profile.lookUpIndex + " D=" + profile.lookDownIndex);
        return profile;
    }

    public void ApplyEyeProfile(ModelEyeProfile profile)
    {
        CurrentEyeProfile = profile;
        if (profile == null)
        {
            if (eyeTrackingController != null)
                eyeTrackingController.ApplyEyeProfile(null);
            if (blinkController != null)
                blinkController.ApplyEyeProfile(null);
            return;
        }
        if (eyeTrackingController != null)
        {
            var renderer = FindBestBlendShapeRenderer(currentModel);
            if (renderer != null) eyeTrackingController.meshRenderer = renderer;
            eyeTrackingController.ApplyEyeProfile(profile);
        }
        if (blinkController != null)
        {
            var renderer = FindBestBlendShapeRenderer(currentModel);
            if (renderer != null) blinkController.skinnedMeshRenderer = renderer;
            blinkController.ApplyEyeProfile(profile);
        }
    }

    private static string MapVrmPreset(ExpressionPreset preset)
    {
        switch (preset)
        {
            case ExpressionPreset.happy: return "happy";
            case ExpressionPreset.angry: return "angry";
            case ExpressionPreset.sad: return "cry";
            case ExpressionPreset.relaxed: return "plain";
            case ExpressionPreset.surprised: return "fun";
            case ExpressionPreset.aa: return null;
            case ExpressionPreset.ih: return null;
            case ExpressionPreset.ou: return null;
            case ExpressionPreset.ee: return null;
            case ExpressionPreset.oh: return null;
            case ExpressionPreset.blink: return null;
            case ExpressionPreset.blinkLeft: return null;
            case ExpressionPreset.blinkRight: return null;
            case ExpressionPreset.lookUp: return null;
            case ExpressionPreset.lookDown: return null;
            case ExpressionPreset.lookLeft: return null;
            case ExpressionPreset.lookRight: return null;
            case ExpressionPreset.neutral: return "plain";
            default: return null;
        }
    }
}
