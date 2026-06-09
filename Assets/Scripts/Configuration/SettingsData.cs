using System;
using UnityEngine;

[Serializable]
public class SettingsData
{
    public float posX;
    public float posY;
    public float posZ;
    public float rotX;
    public float rotY;
    public float rotZ;

    public string websocketUrl;
    public int ttsMode;
    public string gradioUrl;
    public string simpleVitsUrl;
    public string translationUrl;
    public string translationAppId;
    public string translationKey;
    public string translationSalt;
    public string identity;
    public string preset;

    public int msgMaxWidth;
    public int msgHeight;
    public int fontSize;

    public int winX;
    public int winY;

    public float scaleX = 1f;
    public float scaleY = 1f;
    public float scaleZ = 1f;

    public static string GetFilePath()
    {
        return System.IO.Path.Combine(Application.persistentDataPath, "settings.json");
    }

    public static SettingsData Load()
    {
        string path = GetFilePath();
        if (System.IO.File.Exists(path))
        {
            try
            {
                string json = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                return JsonUtility.FromJson<SettingsData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to load settings: " + e.Message);
            }
        }
        return new SettingsData();
    }

    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(this, true);
            System.IO.File.WriteAllText(GetFilePath(), json, System.Text.Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save settings: " + e.Message);
        }
    }
}
