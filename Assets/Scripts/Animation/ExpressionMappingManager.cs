using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ExpressionMappingManager : MonoBehaviour
{
    public ActionController actionController;
    public AnimationLibrary animLibrary;
    public ActionPresetManager presetManager;

    private List<ExpressionMappingData> mappings = new List<ExpressionMappingData>();
    private static bool defaultsLoaded;

    private static List<ExpressionMappingData> BuildDefaults()
    {
        var list = new List<ExpressionMappingData>();
        void Add(string e, string f, string a)
        {
            var d = new ExpressionMappingData { emotion = e };
            if (!string.IsNullOrEmpty(f))
                d.facialGroup = new FacialGroup { preset = f, weight = 1f };
            d.actionGroup = new ActionGroup { animationName = a, bodyPart = "fullBody", weight = 1f };
            list.Add(d);
        }
        Add("待机", "plain", "Idle");
        Add("触摸", "", "Touch");      Add("拖拽", "", "Drag");
        Add("微笑", "", "Speak Normal");  Add("认真", "serious", "Speak Explain");  Add("坚定", "serious", "Determine");
        Add("承诺", "serious", "Determine");  Add("生气", "angry", "Angry");  Add("急切", "angry", "Speak Chatty");
        Add("烦恼", "panic", "Speak Normal");  Add("专注", "curious", "Focused");  Add("诚实", "curious", "Speak Normal");
        Add("期待", "fun", "Expectation");  Add("回答", "curious", "Speak Explain");  Add("回忆", "thinking", "Think");
        Add("发愣", "curious", "Confuse");  Add("察觉", "curious", "Speak Normal");  Add("建议", "fun", "Speak Explain");
        Add("好奇", "curious", "Focused");  Add("自信", "confident", "Doya");  Add("自豪", "confident", "Doya");
        Add("解释", "fun", "Speak Explain");  Add("失望", "disappointed", "Speak Excited");  Add("委屈", "cry", "Shy");
        Add("伤心", "cry", "Speak Shy");  Add("高兴", "fun", "Speak Excited");  Add("开心", "happy", "Speak Excited");
        Add("欢迎", "fun", "Welcome");  Add("崇拜", "fun", "Expectation");  Add("愉快", "fun", "Speak Normal");
        Add("贴心", "fun", "Focused");  Add("赞同", "fun", "Agree");  Add("邀请", "fun", "Invite Give");
        Add("兴奋", "happy", "Speak Excited");  Add("快乐", "happy", "Speak Excited");  Add("难过", "disappointed", "Speak Normal");
        Add("为难", "disappointed", "Speak Explain");  Add("尴尬", "disappointed", "Speak Explain");  Add("紧张", "disappointed", "Speak Normal");
        Add("困惑", "disappointed", "Speak Explain");  Add("困扰", "disappointed", "Speak Explain");  Add("疑惑", "disappointed", "Speak Normal");
        Add("害怕", "sweating", "Afraid");  Add("平和", "plain", "Speak Normal");  Add("无聊", "plain", "Speak Normal");
        Add("冷漠", "plain", "Speak Normal");  Add("慌张", "panic", "Afraid");  Add("害羞", "shy", "Speak Shy");
        Add("羞涩", "shy", "Shy");  Add("惊喜", "fun", "Speak Excited");  Add("理解", "fun", "Agree");
        Add("喜悦", "fun", "Speak Excited");  Add("担忧", "sweating", "Speak Explain");  Add("流汗", "sweating", "Speak Explain");
        Add("犹豫", "disappointed", "Speak Explain");  Add("震惊", "sweating", "Afraid");  Add("惊讶", "sweating", "Afraid");
        Add("思考", "thinking", "Speak Thinking");  Add("沉思", "thinking", "Think");  Add("否认", "thinking", "Disagree");
        Add("睡觉", "thinking", "Sleepy");  Add("陈述", "plain", "Speak Normal");  Add("祈祷", "thinking", "Speak Normal");
        Add("拒绝", "serious", "Deny");  Add("感动", "touching", "Speak Excited");  Add("感激", "touching", "Speak Excited");
        Add("道歉", "sweating", "Apologize");  Add("可爱", "wink", "Cute");  Add("俏皮", "wink", "Confuse");
        Add("调皮", "wink", "Confuse");  Add("卖萌", "wink", "Cat");  Add("眨眼", "wink", "Cute");
        return list;
    }

    private void Awake()
    {
        LoadMerged();
    }

    private void Start()
    {
        MigrateActionNames();
    }

    private void MigrateActionNames()
    {
        if (presetManager == null) return;
        bool changed = false;
        foreach (var m in mappings)
        {
            if (m.actionGroup == null || string.IsNullOrEmpty(m.actionGroup.animationName)) continue;
            if (m.actionGroup.animationName.All(char.IsDigit))
            {
                int ap = int.Parse(m.actionGroup.animationName);
                if (ap == 0) { m.actionGroup.animationName = "Idle"; changed = true; }
                else
                {
                    var preset = presetManager.GetByParam(ap);
                    if (preset != null) { m.actionGroup.animationName = preset.name; changed = true; }
                }
            }
        }
        if (changed) Save();
    }

    public bool TryApplyFacial(string emotion)
    {
        if (actionController == null || string.IsNullOrEmpty(emotion)) return false;
        var map = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (map == null || map.facialGroup == null || string.IsNullOrEmpty(map.facialGroup.preset)) return false;
        if (actionController.facialController == null) return true;
        actionController.facialController.PerformExpression(map.facialGroup.preset, null, map.facialGroup.weight);
        return true;
    }

    public bool TryApplyAction(string emotion)
    {
        if (actionController == null || string.IsNullOrEmpty(emotion)) return false;
        var map = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (map == null || map.actionGroup == null) return false;
        var ag = map.actionGroup;

        var preset = presetManager != null ? presetManager.GetByName(ag.animationName) : null;
        if (preset != null)
        {
            actionController.animator.SetInteger("action_param", preset.actionParam);
            return true;
        }

        var clip = animLibrary != null ? animLibrary.registry.FirstOrDefault(r => r.name == ag.animationName) : null;
        if (clip != null)
        {
            actionController.animator.SetInteger("action_param", clip.actionParam);
            return true;
        }

        if (int.TryParse(ag.animationName, out int ap) && ap > 0)
        {
            actionController.animator.SetInteger("action_param", ap);
            return true;
        }
        return false;
    }

    public List<ExpressionMappingData> GetAll()
    {
        return mappings.OrderByDescending(m => m.emotion == "待机").ThenBy(m => m.emotion).ToList();
    }

    public void AddOrUpdate(string emotion, FacialGroup fg, ActionGroup ag)
    {
        var existing = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (existing != null)
        {
            existing.facialGroup = fg ?? existing.facialGroup;
            existing.actionGroup = ag ?? existing.actionGroup;
        }
        else
        {
            mappings.Add(new ExpressionMappingData { emotion = emotion, facialGroup = fg, actionGroup = ag });
        }
        Save();
    }

    public void RemoveMapping(string emotion)
    {
        mappings.RemoveAll(m => m.emotion == emotion);
        Save();
    }

    public void RestoreDefaults()
    {
        mappings.Clear();
        defaultsLoaded = false;
        if (File.Exists(GetPath()))
            File.Delete(GetPath());
        LoadMerged();
        Save();
    }

    private void LoadMerged()
    {
        if (!defaultsLoaded)
        {
            mappings.AddRange(BuildDefaults());
            defaultsLoaded = true;
        }
        var custom = LoadCustom();
        if (custom != null)
        {
            foreach (var c in custom)
            {
                MigrateLegacy(c);
                mappings.RemoveAll(m => m.emotion == c.emotion);
                mappings.Add(c);
            }
        }
    }

    private void MigrateLegacy(ExpressionMappingData d)
    {
        if (d.facialGroup == null && d.facialGroups != null && d.facialGroups.Count > 0)
            d.facialGroup = d.facialGroups[0];
        if (d.facialGroup == null && !string.IsNullOrEmpty(d.facialExpression))
            d.facialGroup = new FacialGroup { preset = d.facialExpression, weight = 1f };
        if (d.actionGroup == null && d.actionGroups != null && d.actionGroups.Count > 0)
            d.actionGroup = d.actionGroups[0];
        if (d.actionGroup == null && d.actionParam > 0)
            d.actionGroup = new ActionGroup { animationName = d.actionParam.ToString(), bodyPart = "fullBody", weight = 1f };
    }

    private List<ExpressionMappingData> LoadCustom()
    {
        string path = GetPath();
        if (File.Exists(path))
        {
            var w = JsonUtility.FromJson<MappingListWrapper>(File.ReadAllText(path));
            return w?.mappings;
        }
        return null;
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(new MappingListWrapper { mappings = mappings }, true);
        File.WriteAllText(GetPath(), json);
    }

    private string GetPath() => Path.Combine(Application.persistentDataPath, "expression_mappings.json");
}
