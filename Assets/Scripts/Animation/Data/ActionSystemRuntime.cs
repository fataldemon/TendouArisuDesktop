using System.Collections.Generic;
using UnityEngine;

public static class ActionSystemRuntime
{
    private static bool _initialized;
    private static List<FacialPresetConfig> _facialPresets;
    private static List<ActionGroupConfig> _actionGroups;
    private static List<EmotionMappingEntry> _emotionMappings;
    private static ActionGroupConfig _idleGroup;

    public static List<FacialPresetConfig> FacialPresets { get { EnsureInit(); return _facialPresets; } }
    public static List<ActionGroupConfig> ActionGroups { get { EnsureInit(); return _actionGroups; } }
    public static List<EmotionMappingEntry> EmotionMappings { get { EnsureInit(); return _emotionMappings; } }
    public static ActionGroupConfig IdleGroup { get { EnsureInit(); return _idleGroup; } }

    public static void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        _facialPresets = ActionSystemDefaults.BuildFacialPresets();
        _actionGroups = ActionSystemDefaults.BuildActionGroups();
        _emotionMappings = ActionSystemDefaults.BuildEmotionMappings();

        var jsonFacial = ActionSystemJsonIO.LoadFacialPresets();
        if (jsonFacial != null && jsonFacial.Count > 0)
        {
            _facialPresets = jsonFacial;
            Debug.Log("[Runtime] Loaded facial presets from JSON: " + jsonFacial.Count);
        }

        var jsonGroups = ActionSystemJsonIO.LoadActionGroups();
        if (jsonGroups != null && jsonGroups.Count > 0)
        {
            MergeActionGroups(jsonGroups);
            Debug.Log("[Runtime] Merged action groups from JSON: " + jsonGroups.Count);
        }

        var jsonMappings = ActionSystemJsonIO.LoadEmotionMappings();
        if (jsonMappings != null && jsonMappings.Count > 0)
        {
            _emotionMappings = jsonMappings;
            Debug.Log("[Runtime] Loaded emotion mappings from JSON: " + jsonMappings.Count);
        }

        for (int i = 0; i < _actionGroups.Count; i++)
        {
            if (_actionGroups[i].isIdle)
            {
                _idleGroup = _actionGroups[i];
                break;
            }
        }

        Debug.Log("[Runtime] Init complete: " + _emotionMappings.Count + " mappings, " +
            _facialPresets.Count + " facials, " + _actionGroups.Count + " groups, idle=" + (_idleGroup != null));
    }

    private static void MergeActionGroups(List<ActionGroupConfig> jsonGroups)
    {
        foreach (var jg in jsonGroups)
        {
            bool found = false;
            for (int i = 0; i < _actionGroups.Count; i++)
            {
                if (_actionGroups[i].groupName == jg.groupName)
                {
                    _actionGroups[i] = jg;
                    found = true;
                    break;
                }
            }
            if (!found) _actionGroups.Add(jg);
        }
    }

    public static ActionGroupConfig GetActionGroup(string groupName)
    {
        EnsureInit();
        if (string.IsNullOrEmpty(groupName)) return null;
        for (int i = 0; i < _actionGroups.Count; i++)
            if (_actionGroups[i].groupName == groupName) return _actionGroups[i];
        return null;
    }

    public static ActionGroupConfig ResolveEmotion(string emotion)
    {
        EnsureInit();
        if (string.IsNullOrEmpty(emotion)) return _idleGroup;
        for (int i = 0; i < _emotionMappings.Count; i++)
        {
            if (_emotionMappings[i].emotion == emotion)
                return GetActionGroup(_emotionMappings[i].actionGroupName);
        }
        return null;
    }

    public static EmotionMappingEntry GetMappingEntry(string emotion)
    {
        EnsureInit();
        for (int i = 0; i < _emotionMappings.Count; i++)
            if (_emotionMappings[i].emotion == emotion) return _emotionMappings[i];
        return null;
    }

    public static FacialPresetConfig GetFacialPreset(string presetName)
    {
        EnsureInit();
        if (string.IsNullOrEmpty(presetName)) return null;
        for (int i = 0; i < _facialPresets.Count; i++)
            if (_facialPresets[i].presetName == presetName) return _facialPresets[i];
        return null;
    }

    public static void SetMapping(string emotion, string actionGroupName, string facialOverride)
    {
        EnsureInit();
        Debug.Log("[Runtime] SetMapping: " + emotion + " → group=" + actionGroupName + " facial=" + facialOverride);
        for (int i = 0; i < _emotionMappings.Count; i++)
        {
            if (_emotionMappings[i].emotion == emotion)
            {
                _emotionMappings[i].actionGroupName = actionGroupName;
                _emotionMappings[i].facialOverride = facialOverride;
                ActionSystemJsonIO.SaveEmotionMappings(_emotionMappings);
                return;
            }
        }
        _emotionMappings.Add(new EmotionMappingEntry { emotion = emotion, actionGroupName = actionGroupName, facialOverride = facialOverride });
        ActionSystemJsonIO.SaveEmotionMappings(_emotionMappings);
    }

    public static void RemoveMapping(string emotion)
    {
        EnsureInit();
        _emotionMappings.RemoveAll(m => m.emotion == emotion);
        ActionSystemJsonIO.SaveEmotionMappings(_emotionMappings);
    }

    public static void UpdateActionGroup(string groupName, string facialPreset, float facialWeight, string clipName = null)
    {
        var group = GetActionGroup(groupName);
        if (group == null) { Debug.LogWarning("[Runtime] UpdateActionGroup: group '" + groupName + "' not found!"); return; }
        Debug.Log("[Runtime] UpdateActionGroup: " + groupName + " facial=" + facialPreset + " w=" + facialWeight + " clip=" + (clipName ?? "(unchanged)"));
        if (!string.IsNullOrEmpty(facialPreset))
            group.facialPreset = facialPreset;
        if (facialWeight > 0f)
            group.facialWeight = facialWeight;
        if (clipName != null && group.bodyClips.Count > 0)
            group.bodyClips[0].clipName = clipName;
        ActionSystemJsonIO.SaveActionGroups(_actionGroups);
    }

    public static void SaveFacialPresets()
    {
        ActionSystemJsonIO.SaveFacialPresets(_facialPresets);
    }

    public static List<string> GetAllGroupNames()
    {
        EnsureInit();
        var names = new List<string>();
        for (int i = 0; i < _actionGroups.Count; i++)
            names.Add(_actionGroups[i].groupName);
        return names;
    }
}
