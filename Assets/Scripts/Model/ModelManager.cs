using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UniVRM10;

public class ModelManager : MonoBehaviour
{
    public GameObject currentModel;
    public ActionController actionController;
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
        var oldController = actionController != null ? actionController.animator?.runtimeAnimatorController : null;

        if (currentModel != null)
        {
            if (currentModel == defaultModel)
                defaultModel.SetActive(false);
            else
                Destroy(currentModel);
        }

        currentModel = newModel;

        if (actionController != null)
        {
            var animator = currentModel.GetComponent<Animator>();
            if (animator == null)
                animator = currentModel.AddComponent<Animator>();
            if (oldController != null)
                animator.runtimeAnimatorController = oldController;
            actionController.animator = animator;

            var facial = actionController.facialController;
            if (facial != null)
            {
                var renderer = currentModel.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null)
                    facial.skinnedMeshRenderer = renderer;
            }
        }
    }

    public void RestoreDefault()
    {
        if (defaultModel == null) return;

        if (currentModel != null && currentModel != defaultModel)
            Destroy(currentModel);

        defaultModel.SetActive(true);
        currentModel = defaultModel;

        if (actionController != null)
        {
            actionController.animator = defaultModel.GetComponent<Animator>();
            var facial = actionController.facialController;
            if (facial != null)
            {
                var renderer = defaultModel.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null)
                    facial.skinnedMeshRenderer = renderer;
            }
        }

        SaveHistory();
    }

    public void ScaleModel(float delta)
    {
        if (currentModel == null) return;
        Vector3 s = currentModel.transform.localScale;
        s += Vector3.one * delta;
        s = new Vector3(
            Mathf.Clamp(s.x, 0.1f, 5f),
            Mathf.Clamp(s.y, 0.1f, 5f),
            Mathf.Clamp(s.z, 0.1f, 5f));
        currentModel.transform.localScale = s;
    }

    public Vector3 GetScale() => currentModel != null ? currentModel.transform.localScale : Vector3.one;

    public void SetScale(Vector3 s) { if (currentModel != null) currentModel.transform.localScale = s; }

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
