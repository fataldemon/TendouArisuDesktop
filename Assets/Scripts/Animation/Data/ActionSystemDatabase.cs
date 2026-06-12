using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActionSystemDatabase", menuName = "AliceBot/Action System Database")]
public class ActionSystemDatabase : ScriptableObject
{
    public FacialPresetDatabase facialPresets;
    public ActionPresetDatabase actionPresets;
    public EmotionMappingDatabase emotionMappings;
    public List<ActionGroupConfig> actionGroups = new List<ActionGroupConfig>();
    public ActionGroupConfig idleGroup;

    public ActionGroupConfig GetActionGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return null;
        if (idleGroup != null && idleGroup.groupName == groupName) return idleGroup;
        for (int i = 0; i < actionGroups.Count; i++)
            if (actionGroups[i].groupName == groupName) return actionGroups[i];
        return null;
    }

    public ActionGroupConfig ResolveEmotion(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return idleGroup;
        string groupName = emotionMappings != null ? emotionMappings.Resolve(emotion) : null;
        if (string.IsNullOrEmpty(groupName)) return null;
        return GetActionGroup(groupName);
    }

    public List<string> GetAllGroupNames()
    {
        var names = new List<string>();
        if (idleGroup != null) names.Add(idleGroup.groupName);
        for (int i = 0; i < actionGroups.Count; i++)
            names.Add(actionGroups[i].groupName);
        return names;
    }
}
