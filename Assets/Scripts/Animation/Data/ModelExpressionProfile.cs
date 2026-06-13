using System;
using System.Collections.Generic;

[Serializable]
public class ModelExpressionProfile
{
    public string modelKey;
    public List<FacialPresetConfig> presets = new List<FacialPresetConfig>();

    public FacialPresetConfig Find(string presetName)
    {
        for (int i = 0; i < presets.Count; i++)
            if (presets[i].presetName == presetName) return presets[i];
        return null;
    }
}
