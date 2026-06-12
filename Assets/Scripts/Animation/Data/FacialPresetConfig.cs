using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BlendShapeTarget
{
    public int index;
    public float weight;
}

[Serializable]
public class FacialPresetConfig
{
    public string presetName;
    public List<BlendShapeTarget> targets = new List<BlendShapeTarget>();
    public List<string> activateObjects = new List<string>();
    public string blushMode;
}

[CreateAssetMenu(fileName = "FacialPresets", menuName = "AliceBot/Facial Preset Database")]
public class FacialPresetDatabase : ScriptableObject
{
    public List<FacialPresetConfig> presets = new List<FacialPresetConfig>();

    public FacialPresetConfig Get(string presetName)
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
