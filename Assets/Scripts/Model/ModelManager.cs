using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UniVRM10;
using VrmLib;  // for ExpressionPreset enum

public class ModelManager : MonoBehaviour
{
    public GameObject currentModel;
    public BodyEngine bodyEngine;
    public FacialEngine facialEngine;
    public Transform modelParent;

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
        var task = Vrm10.LoadPathAsync(path);
        while (!task.IsCompleted)
            yield return null;

        var instance = task.Result;
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

    private void ReplaceModel(GameObject newModel)
    {
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
            var renderer = currentModel.GetComponentInChildren<SkinnedMeshRenderer>();
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
                }
                facialEngine.SetModelExpressionProfile(profile);
            }
        }

        var ep = bodyEngine != null ? bodyEngine.GetComponent<EmotionPlayer>() : null;
        if (ep == null) ep = FindObjectOfType<EmotionPlayer>();
        if (ep != null)
            ep.ForceIdle();
    }

    public void RestoreDefault()
    {
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
            var renderer = defaultModel.GetComponentInChildren<SkinnedMeshRenderer>();
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
            if (clips != null && clips.Count > 0)
            {
                Debug.Log("[ModelManager] BuildDefaultProfile: found " + clips.Count + " VRM expression clips");

                var mappedNames = new HashSet<string>();
                foreach (var clip in clips)
                {
                    if (clip == null || clip.Clip == null || clip.Preset == ExpressionPreset.custom) continue;
                    string presetName = MapVrmPreset(clip.Preset);
                    if (string.IsNullOrEmpty(presetName) || mappedNames.Contains(presetName)) continue;
                    mappedNames.Add(presetName);

                    var config = new FacialPresetConfig { presetName = presetName };
                    var bindings = clip.Clip.BlendShapeBindings;
                    if (bindings != null)
                    {
                        foreach (var b in bindings)
                            config.targets.Add(new BlendShapeTarget { index = b.Index, weight = b.Weight });
                    }
                    profile.presets.Add(config);
                    Debug.Log("[ModelManager]   " + presetName + " ← VRM " + clip.Preset + " (" + config.targets.Count + " blends)");
                }
            }
        }

        if (profile.presets.Count == 0)
        {
            Debug.Log("[ModelManager] BuildDefaultProfile: no VRM presets found, copying global defaults");
            var globals = ActionSystemRuntime.FacialPresets;
            foreach (var g in globals)
            {
                var copy = new FacialPresetConfig
                {
                    presetName = g.presetName,
                    blushMode = g.blushMode
                };
                for (int i = 0; i < g.targets.Count; i++)
                    copy.targets.Add(new BlendShapeTarget { index = g.targets[i].index, weight = g.targets[i].weight });
                for (int i = 0; i < g.activateObjects.Count; i++)
                    copy.activateObjects.Add(g.activateObjects[i]);
                profile.presets.Add(copy);
            }
        }

        return profile;
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
