using System.IO;
using UnityEngine;

public static class ModelEyeIO
{
    private static string ProfilesDir =>
        Path.Combine(Application.persistentDataPath, "eyes");

    private static string GetFilePath(string modelKey)
        => Path.Combine(ProfilesDir, modelKey + ".json");

    public static ModelEyeProfile Load(string modelKey)
    {
        var path = GetFilePath(modelKey);
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<ModelEyeProfile>(File.ReadAllText(path)); }
        catch (System.Exception e) { Debug.LogWarning("[ModelEyeIO] Load error: " + e.Message); return null; }
    }

    public static void Save(ModelEyeProfile profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.modelKey)) return;
        try
        {
            Directory.CreateDirectory(ProfilesDir);
            File.WriteAllText(GetFilePath(profile.modelKey), JsonUtility.ToJson(profile, true));
        }
        catch (System.Exception e) { Debug.LogWarning("[ModelEyeIO] Save error: " + e.Message); }
    }

    public static void Delete(string modelKey)
    {
        var path = GetFilePath(modelKey);
        if (File.Exists(path)) File.Delete(path);
    }
}
