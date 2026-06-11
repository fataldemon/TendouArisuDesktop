using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ExpressionMappingManager : MonoBehaviour
{
    public ActionController actionController;
    public AnimationLibrary animLibrary;

    private List<ExpressionMappingData> mappings = new List<ExpressionMappingData>();
    private static bool defaultsLoaded;

    private static List<ExpressionMappingData> BuildDefaults()
    {
        var list = new List<ExpressionMappingData>();
        void Add(string e, string f, int a)
        {
            var d = new ExpressionMappingData { emotion = e };
            if (!string.IsNullOrEmpty(f))
                d.facialGroup = new FacialGroup { preset = f, weight = 1f };
            d.actionGroup = new ActionGroup { animationName = a.ToString(), bodyPart = "fullBody", weight = 1f };
            d.actionParam = a;
            list.Add(d);
        }
        Add("待机", "", 0);
        Add("微笑", "", 1);  Add("认真", "serious", 24);  Add("坚定", "serious", 11);
        Add("承诺", "serious", 11);  Add("生气", "angry", 20);  Add("急切", "angry", 27);
        Add("烦恼", "panic", 1);  Add("专注", "curious", 22);  Add("诚实", "curious", 1);
        Add("期待", "fun", 19);  Add("回答", "curious", 24);  Add("回忆", "thinking", 17);
        Add("发愣", "curious", 15);  Add("察觉", "curious", 1);  Add("建议", "fun", 24);
        Add("好奇", "curious", 22);  Add("自信", "confident", 4);  Add("自豪", "confident", 4);
        Add("解释", "fun", 24);  Add("失望", "disappointed", 25);  Add("委屈", "cry", 7);
        Add("伤心", "cry", 28);  Add("高兴", "fun", 25);  Add("开心", "happy", 25);
        Add("欢迎", "fun", 5);  Add("崇拜", "fun", 19);  Add("愉快", "fun", 1);
        Add("贴心", "fun", 22);  Add("赞同", "fun", 16);  Add("邀请", "fun", 13);
        Add("兴奋", "happy", 25);  Add("快乐", "happy", 25);  Add("难过", "disappointed", 1);
        Add("为难", "disappointed", 24);  Add("尴尬", "disappointed", 24);  Add("紧张", "disappointed", 1);
        Add("困惑", "disappointed", 24);  Add("困扰", "disappointed", 24);  Add("疑惑", "disappointed", 1);
        Add("害怕", "sweating", 23);  Add("平和", "plain", 1);  Add("无聊", "plain", 1);
        Add("冷漠", "plain", 1);  Add("慌张", "panic", 23);  Add("害羞", "shy", 28);
        Add("羞涩", "shy", 7);  Add("惊喜", "fun", 25);  Add("理解", "fun", 16);
        Add("喜悦", "fun", 25);  Add("担忧", "sweating", 24);  Add("流汗", "sweating", 24);
        Add("犹豫", "disappointed", 24);  Add("震惊", "sweating", 23);  Add("惊讶", "sweating", 23);
        Add("思考", "thinking", 26);  Add("沉思", "thinking", 17);  Add("否认", "thinking", 14);
        Add("睡觉", "thinking", 18);  Add("陈述", "plain", 1);  Add("祈祷", "thinking", 1);
        Add("拒绝", "serious", 10);  Add("感动", "touching", 25);  Add("感激", "touching", 25);
        Add("道歉", "sweating", 29);  Add("可爱", "wink", 12);  Add("俏皮", "wink", 15);
        Add("调皮", "wink", 15);  Add("卖萌", "wink", 3);  Add("眨眼", "wink", 12);
        return list;
    }

    private void Awake()
    {
        LoadMerged();
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
        var clip = animLibrary != null ? animLibrary.registry.FirstOrDefault(r => r.name == ag.animationName) : null;
        if (clip != null)
            actionController.animator.SetInteger("action_param", clip.actionParam);
        else if (int.TryParse(ag.animationName, out int ap) && ap > 0)
            actionController.animator.SetInteger("action_param", ap);
        else
            return false;
        return true;
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
