using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ExpressionMappingManager : MonoBehaviour
{
    public ActionController actionController;

    private List<ExpressionMappingData> mappings = new List<ExpressionMappingData>();
    private static bool defaultsLoaded;

    private static readonly (string emotion, string facial, int action)[] Defaults =
    {
        ("微笑", "", 1),
        ("认真", "serious", 24),
        ("坚定", "serious", 11),
        ("承诺", "serious", 11),
        ("生气", "angry", 20),
        ("急切", "angry", 27),
        ("烦恼", "panic", 1),
        ("专注", "curious", 22),
        ("诚实", "curious", 1),
        ("期待", "fun", 19),
        ("回答", "curious", 24),
        ("回忆", "thinking", 17),
        ("发愣", "curious", 15),
        ("察觉", "curious", 1),
        ("建议", "fun", 24),
        ("好奇", "curious", 22),
        ("自信", "confident", 4),
        ("自豪", "confident", 4),
        ("解释", "fun", 24),
        ("失望", "disappointed", 25),
        ("委屈", "cry", 7),
        ("伤心", "cry", 28),
        ("高兴", "fun", 25),
        ("开心", "happy", 25),
        ("欢迎", "fun", 5),
        ("崇拜", "fun", 19),
        ("愉快", "fun", 1),
        ("贴心", "fun", 22),
        ("赞同", "fun", 16),
        ("邀请", "fun", 13),
        ("兴奋", "happy", 25),
        ("快乐", "happy", 25),
        ("难过", "disappointed", 1),
        ("为难", "disappointed", 24),
        ("尴尬", "disappointed", 24),
        ("紧张", "disappointed", 1),
        ("困惑", "disappointed", 24),
        ("困扰", "disappointed", 24),
        ("疑惑", "disappointed", 1),
        ("害怕", "sweating", 23),
        ("平和", "plain", 1),
        ("无聊", "plain", 1),
        ("冷漠", "plain", 1),
        ("慌张", "panic", 23),
        ("害羞", "shy", 28),
        ("羞涩", "shy", 7),
        ("惊喜", "fun", 25),
        ("理解", "fun", 16),
        ("喜悦", "fun", 25),
        ("担忧", "sweating", 24),
        ("流汗", "sweating", 24),
        ("犹豫", "disappointed", 24),
        ("震惊", "sweating", 23),
        ("惊讶", "sweating", 23),
        ("思考", "thinking", 26),
        ("沉思", "thinking", 17),
        ("否认", "thinking", 14),
        ("睡觉", "thinking", 18),
        ("陈述", "plain", 1),
        ("祈祷", "thinking", 1),
        ("拒绝", "serious", 10),
        ("感动", "touching", 25),
        ("感激", "touching", 25),
        ("道歉", "sweating", 29),
        ("可爱", "wink", 12),
        ("俏皮", "wink", 15),
        ("调皮", "wink", 15),
        ("卖萌", "wink", 3),
        ("眨眼", "wink", 12),
    };

    private void Awake()
    {
        LoadMerged();
    }

    public bool Apply(string emotion)
    {
        if (actionController == null) return false;
        var map = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (map == null) return false;

        if (!string.IsNullOrEmpty(map.facialExpression) && actionController.facialController != null)
            actionController.facialController.PerformExpression(map.facialExpression, null);

        actionController.animator.SetInteger("action_param", map.actionParam);
        return true;
    }

    public bool TryApplyFacial(string emotion)
    {
        if (actionController == null || string.IsNullOrEmpty(emotion)) return false;
        var map = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (map == null || string.IsNullOrEmpty(map.facialExpression)) return false;
        if (actionController.facialController == null) return true;
        actionController.facialController.PerformExpression(map.facialExpression, null);
        return true;
    }

    public bool TryApplyAction(string emotion)
    {
        if (actionController == null || string.IsNullOrEmpty(emotion)) return false;
        var map = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (map == null) return false;
        actionController.animator.SetInteger("action_param", map.actionParam);
        return true;
    }

    public List<ExpressionMappingData> GetAll() => mappings;

    public void SetMapping(string emotion, string facial, int action)
    {
        var existing = mappings.FirstOrDefault(m => m.emotion == emotion);
        if (existing != null)
        {
            existing.facialExpression = facial;
            existing.actionParam = action;
        }
        else
        {
            mappings.Add(new ExpressionMappingData { emotion = emotion, facialExpression = facial, actionParam = action });
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
        LoadMerged();
        Save();
    }

    private void LoadMerged()
    {
        if (!defaultsLoaded)
        {
            foreach (var d in Defaults)
                mappings.Add(new ExpressionMappingData { emotion = d.emotion, facialExpression = d.facial, actionParam = d.action });
            defaultsLoaded = true;
        }
        var custom = LoadCustom();
        if (custom != null)
        {
            foreach (var c in custom)
            {
                mappings.RemoveAll(m => m.emotion == c.emotion);
                mappings.Add(c);
            }
        }
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
