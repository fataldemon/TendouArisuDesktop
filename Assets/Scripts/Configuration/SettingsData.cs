using System;
using System.Collections.Generic;
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
    public string gptSovitsUrl;
    public string gradioUrl;
    public string simpleVitsUrl;
    public string gptSovitsRefAudioBaseDir;
    public bool translationEnabled;
    public string translationUrl;
    public string translationAppId;
    public string translationKey;
    public string translationSalt;
    public string identity;
    public string preset;
    public string bangbangkabangWavPath;

    public List<RefAudioDataEntry> refAudioConfigs;

    public int msgMaxWidth;
    public int msgHeight;
    public int fontSize;

    public int winX;
    public int winY;
    public int winWidth;
    public int winHeight;

    public float camX;
    public float camY;
    public float camZ;
    public float camRotX;
    public float camRotY;
    public float camRotZ;
    public float camRotW;

    public float guiOffsetX;
    public float guiOffsetY;
    public float dialogMinHoldTime = 10f;
    public string currentModelPath = "";

    public float bubbleR = 0.298f, bubbleG = 0.788f, bubbleB = 0.941f, bubbleA = 0.88f;
    public float bubbleTextR = 1f, bubbleTextG = 1f, bubbleTextB = 1f, bubbleTextA = 1f;

    public bool allowRootMotion;

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

[Serializable]
public class RefAudioDataEntry
{
    public string emotionKey;
    public string audioFileName;
    public string promptText;
    public string promptLang;
    public string audioFullPath;
}
