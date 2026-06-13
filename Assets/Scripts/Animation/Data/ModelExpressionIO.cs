using System.IO;
using UnityEngine;

public static class ModelExpressionIO
{
    private static string ProfilesDir =>
        Path.Combine(Application.persistentDataPath, "expressions");

    public static string GetFilePath(string modelKey)
    {
        return Path.Combine(ProfilesDir, modelKey + ".json");
    }

    public static ModelExpressionProfile Load(string modelKey)
    {
        var path = GetFilePath(modelKey);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<ModelExpressionProfile>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ModelExpressionIO] Failed to load profile for " + modelKey + ": " + e.Message);
            return null;
        }
    }

    public static void Save(ModelExpressionProfile profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.modelKey)) return;
        try
        {
            Directory.CreateDirectory(ProfilesDir);
            var json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(GetFilePath(profile.modelKey), json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ModelExpressionIO] Failed to save profile: " + e.Message);
        }
    }

    public static string ComputeModelKey(string vrmPath)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(vrmPath);
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var hash = sha.ComputeHash(bytes);
            return System.BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }
    }
}
