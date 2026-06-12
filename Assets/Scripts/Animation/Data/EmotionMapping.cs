using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EmotionMappingEntry
{
    public string emotion;
    public string actionGroupName;
    public string facialOverride;
    public float facialWeightOverride = -1f;
}

[CreateAssetMenu(fileName = "EmotionMappings", menuName = "AliceBot/Emotion Mapping Database")]
public class EmotionMappingDatabase : ScriptableObject
{
    public List<EmotionMappingEntry> mappings = new List<EmotionMappingEntry>();

    public string Resolve(string emotion)
    {
        for (int i = 0; i < mappings.Count; i++)
            if (mappings[i].emotion == emotion) return mappings[i].actionGroupName;
        return null;
    }

    public EmotionMappingEntry GetEntry(string emotion)
    {
        for (int i = 0; i < mappings.Count; i++)
            if (mappings[i].emotion == emotion) return mappings[i];
        return null;
    }

    public void Set(string emotion, string actionGroupName)
    {
        for (int i = 0; i < mappings.Count; i++)
        {
            if (mappings[i].emotion == emotion)
            {
                mappings[i].actionGroupName = actionGroupName;
                return;
            }
        }
        mappings.Add(new EmotionMappingEntry { emotion = emotion, actionGroupName = actionGroupName });
    }

    public void Remove(string emotion)
    {
        mappings.RemoveAll(m => m.emotion == emotion);
    }

    public List<string> GetAllEmotions()
    {
        var list = new List<string>();
        for (int i = 0; i < mappings.Count; i++)
            list.Add(mappings[i].emotion);
        return list;
    }
}
