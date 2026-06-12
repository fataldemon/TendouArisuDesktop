using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PartClipEntry
{
    public string bodyPart = "fullBody";
    public string clipName;
    public AnimationClip clip;
}

[Serializable]
public class ActionPresetConfig
{
    public string presetName;
    public List<PartClipEntry> clips = new List<PartClipEntry>();
    public bool loop;
    public bool isDefault;

    public AnimationClip GetClip(string bodyPart)
    {
        for (int i = 0; i < clips.Count; i++)
            if (clips[i].bodyPart == bodyPart) return clips[i].clip;
        return null;
    }

    public string GetClipName(string bodyPart)
    {
        for (int i = 0; i < clips.Count; i++)
            if (clips[i].bodyPart == bodyPart) return clips[i].clipName;
        return null;
    }
}

[CreateAssetMenu(fileName = "ActionPresets", menuName = "AliceBot/Action Preset Database")]
public class ActionPresetDatabase : ScriptableObject
{
    public List<ActionPresetConfig> presets = new List<ActionPresetConfig>();

    public ActionPresetConfig Get(string presetName)
    {
        for (int i = 0; i < presets.Count; i++)
            if (presets[i].presetName == presetName) return presets[i];
        return null;
    }

    public List<string> GetAllNames()
    {
        var names = new List<string>();
        for (int i = 0; i < presets.Count; i++)
            names.Add(presets[i].presetName);
        return names;
    }
}
