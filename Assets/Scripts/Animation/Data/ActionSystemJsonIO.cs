using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ActionSystemJsonIO
{
    private static string BasePath => Application.persistentDataPath;

    private static string FacialPresetsPath => Path.Combine(BasePath, "facial_presets_v2.json");
    private static string ActionGroupsPath => Path.Combine(BasePath, "action_groups_v2.json");
    private static string EmotionMappingsPath => Path.Combine(BasePath, "emotion_mappings_v2.json");
    private static string ActionPresetsPath => Path.Combine(BasePath, "action_presets_v2.json");

    [Serializable]
    private class FacialPresetsWrapper { public List<FacialPresetConfig> presets; }

    [Serializable]
    private class ActionGroupsWrapper { public List<ActionGroupConfig> groups; }

    [Serializable]
    private class EmotionMappingsWrapper { public List<EmotionMappingEntry> mappings; }

    [Serializable]
    private class ActionPresetsWrapper { public List<ActionPresetConfigJson> presets; }

    [Serializable]
    public class ActionPresetConfigJson
    {
        public string presetName;
        public List<PartClipEntryJson> clips = new List<PartClipEntryJson>();
        public bool loop;
        public bool isDefault;
    }

    [Serializable]
    public class PartClipEntryJson
    {
        public string bodyPart;
        public string clipName;
    }

    public static List<FacialPresetConfig> LoadFacialPresets()
    {
        if (!File.Exists(FacialPresetsPath)) return null;
        try
        {
            var wrapper = JsonUtility.FromJson<FacialPresetsWrapper>(File.ReadAllText(FacialPresetsPath));
            return wrapper?.presets;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ActionSystemJsonIO] Failed to load facial presets: " + e.Message);
            return null;
        }
    }

    public static void SaveFacialPresets(List<FacialPresetConfig> presets)
    {
        var wrapper = new FacialPresetsWrapper { presets = presets };
        File.WriteAllText(FacialPresetsPath, JsonUtility.ToJson(wrapper, true));
    }

    public static List<ActionGroupConfig> LoadActionGroups()
    {
        if (!File.Exists(ActionGroupsPath)) return null;
        try
        {
            var wrapper = JsonUtility.FromJson<ActionGroupsWrapper>(File.ReadAllText(ActionGroupsPath));
            return wrapper?.groups;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ActionSystemJsonIO] Failed to load action groups: " + e.Message);
            return null;
        }
    }

    public static void SaveActionGroups(List<ActionGroupConfig> groups)
    {
        var wrapper = new ActionGroupsWrapper { groups = groups };
        File.WriteAllText(ActionGroupsPath, JsonUtility.ToJson(wrapper, true));
    }

    public static List<EmotionMappingEntry> LoadEmotionMappings()
    {
        if (!File.Exists(EmotionMappingsPath)) return null;
        try
        {
            var wrapper = JsonUtility.FromJson<EmotionMappingsWrapper>(File.ReadAllText(EmotionMappingsPath));
            return wrapper?.mappings;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ActionSystemJsonIO] Failed to load emotion mappings: " + e.Message);
            return null;
        }
    }

    public static void SaveEmotionMappings(List<EmotionMappingEntry> mappings)
    {
        var wrapper = new EmotionMappingsWrapper { mappings = mappings };
        File.WriteAllText(EmotionMappingsPath, JsonUtility.ToJson(wrapper, true));
    }

    public static List<ActionPresetConfigJson> LoadActionPresets()
    {
        if (!File.Exists(ActionPresetsPath)) return null;
        try
        {
            var wrapper = JsonUtility.FromJson<ActionPresetsWrapper>(File.ReadAllText(ActionPresetsPath));
            return wrapper?.presets;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ActionSystemJsonIO] Failed to load action presets: " + e.Message);
            return null;
        }
    }

    public static void SaveActionPresets(List<ActionPresetConfigJson> presets)
    {
        var wrapper = new ActionPresetsWrapper { presets = presets };
        File.WriteAllText(ActionPresetsPath, JsonUtility.ToJson(wrapper, true));
    }
}
