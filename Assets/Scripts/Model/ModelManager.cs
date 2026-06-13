using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UniVRM10;

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

        ReplaceModel(instance.gameObject);

        string modelName = Path.GetFileNameWithoutExtension(path);
        AddToHistory(path, modelName);
    }

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
}
