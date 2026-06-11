using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ActionPreset
{
    public string name;
    public int actionParam;
    public bool isDefault;
}

[System.Serializable]
internal class ActionPresetListWrapper { public List<ActionPreset> presets; }

public class ActionPresetManager : MonoBehaviour
{
    public ActionController actionController;

    private List<ActionPreset> presets = new List<ActionPreset>();
    private static bool defaultsLoaded;

    private static readonly (int ap, string name)[] DefaultData = new (int, string)[] {
        (0,  "Idle"),            (1,  "Speak Normal"),    (2,  "Wave Hands"),
        (3,  "Cat"),             (4,  "Doya"),            (5,  "Welcome"),
        (6,  "Yay"),             (7,  "Shy"),             (8,  "Comfort"),
        (9,  "Highfive"),        (10, "Deny"),            (11, "Determine"),
        (12, "Cute"),            (13, "Invite Give"),     (14, "Disagree"),
        (15, "Confuse"),         (16, "Agree"),           (17, "Think"),
        (18, "Sleepy"),          (19, "Expectation"),     (20, "Angry"),
        (21, "Hurry"),           (22, "Focused"),         (23, "Afraid"),
        (24, "Speak Explain"),   (25, "Speak Excited"),   (26, "Speak Thinking"),
        (27, "Speak Chatty"),    (28, "Speak Shy"),       (29, "Apologize"),
    };

    private void Awake() { LoadMerged(); }

    public List<ActionPreset> GetAll() => presets.OrderBy(p => p.actionParam).ToList();
    public ActionPreset GetByParam(int ap) => presets.FirstOrDefault(p => p.actionParam == ap);
    public ActionPreset GetByName(string name) => presets.FirstOrDefault(p => p.name == name);

    public void AddOrUpdate(string name, int actionParam)
    {
        var existing = presets.FirstOrDefault(p => p.name == name);
        if (existing != null)
            existing.actionParam = actionParam;
        else
            presets.Add(new ActionPreset { name = name, actionParam = actionParam });
        Save();
    }

    public void Remove(string name)
    {
        presets.RemoveAll(p => p.name == name && !p.isDefault);
        Save();
    }

    public void RestoreDefaults()
    {
        presets.Clear();
        defaultsLoaded = false;
        if (File.Exists(GetPath())) File.Delete(GetPath());
        LoadMerged();
        Save();
    }

    public int NextActionParam()
    {
        return presets.Any() ? presets.Max(p => p.actionParam) + 1 : 30;
    }

    private void LoadMerged()
    {
        if (!defaultsLoaded)
        {
            foreach (var (ap, name) in DefaultData)
                presets.Add(new ActionPreset { name = name, actionParam = ap, isDefault = true });
            defaultsLoaded = true;
        }
        var custom = LoadCustom();
        if (custom != null)
        {
            foreach (var c in custom)
            {
                presets.RemoveAll(p => p.name == c.name);
                presets.Add(c);
            }
        }
    }

    private List<ActionPreset> LoadCustom()
    {
        string path = GetPath();
        if (File.Exists(path))
        {
            var w = JsonUtility.FromJson<ActionPresetListWrapper>(File.ReadAllText(path));
            return w?.presets;
        }
        return null;
    }

    private void Save()
    {
        var custom = presets.Where(p => !p.isDefault).ToList();
        string json = JsonUtility.ToJson(new ActionPresetListWrapper { presets = custom }, true);
        File.WriteAllText(GetPath(), json);
    }

    private string GetPath() => Path.Combine(Application.persistentDataPath, "action_presets.json");
}
