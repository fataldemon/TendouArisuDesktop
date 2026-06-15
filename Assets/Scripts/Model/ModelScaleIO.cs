using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ModelScaleIO
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "model_scales.json");

    [Serializable]
    private class ScaleData { public List<ScaleEntry> entries = new List<ScaleEntry>(); }

    [Serializable]
    private class ScaleEntry { public string key; public float scale = 1f; }

    private static Dictionary<string, float> _cache;
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _cache = new Dictionary<string, float>();
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonUtility.FromJson<ScaleData>(File.ReadAllText(FilePath));
                if (data?.entries != null)
                    foreach (var e in data.entries)
                        _cache[e.key] = e.scale;
            }
        }
        catch (Exception ex) { Debug.LogWarning("[ModelScaleIO] Load error: " + ex.Message); }
    }

    public static float GetScale(string modelKey, float defaultScale = 1f)
    {
        EnsureLoaded();
        return _cache.TryGetValue(modelKey, out float s) && s > 0f ? s : defaultScale;
    }

    public static void SetScale(string modelKey, float scale)
    {
        EnsureLoaded();
        scale = Mathf.Clamp(scale, 0.1f, 3f);
        _cache[modelKey] = scale;
        var data = new ScaleData();
        foreach (var kv in _cache)
            data.entries.Add(new ScaleEntry { key = kv.Key, scale = kv.Value });
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(data, true)); }
        catch (Exception ex) { Debug.LogWarning("[ModelScaleIO] Save error: " + ex.Message); }
    }
}
