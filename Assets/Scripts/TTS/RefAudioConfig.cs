using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RefAudioEntry
{
    public string emotionKey;
    public string audioFileName;
    public string promptText;
    public string promptLang;
    public string audioFullPath;

    public RefAudioEntry Clone()
    {
        return new RefAudioEntry
        {
            emotionKey = this.emotionKey,
            audioFileName = this.audioFileName,
            promptText = this.promptText,
            promptLang = this.promptLang,
            audioFullPath = this.audioFullPath
        };
    }
}

public static class RefAudioConfig
{
    private static Dictionary<string, (string file, string prompt)> s_map;

    static RefAudioConfig()
    {
        s_map = new Dictionary<string, (string, string)>();

        AddGroup("1.wav", "アリス、知ってます。世の中には、メイドカフェというものがあるらしいです。",
            "平和", "消极", "冷漠", "无聊", "悠闲", "待机", "回忆", "思考", "沉思", "陈述", "睡觉", "祈祷");
        AddGroup("2.wav", "メイドには、ご主人様が必要です。アリスのご主人様は、もちろん先生です！",
            "欣喜", "高兴", "开心", "快乐", "欢迎", "愉快", "窃喜", "微笑", "惊喜", "喜悦", "感动", "感激");
        AddGroup("3.wav", "そっ、そんなにつっついても、何もドロップしませんよ？！アリスはモンスターではありません！",
            "困扰", "烦恼", "困惑", "疑惑", "惊诧", "发愣", "震惊", "惊讶");
        AddGroup("4.wav", "先生、急いでください！イベントですよ、イベント！",
            "兴奋", "期待", "雀跃", "崇拜", "好奇", "可爱", "俏皮", "调皮", "卖萌", "眨眼");
        AddGroup("5.wav", "アイテム倉庫の管理もメイドのお仕事です。アリス、今日も頑張ります！",
            "自信", "自豪", "专注", "诚实", "贴心", "察觉", "理解");
        AddGroup("6.wav", "ターゲット、ロックオン、光よ!",
            "坚定", "承诺", "认真", "生气", "急切", "否认", "拒绝");
        AddGroup("7.wav", "うぅ...この新スキン、まだ慣れません...",
            "紧张", "为难", "慌张", "流汗", "犹豫", "担忧");
        AddGroup("8.wav", "うわー......アリスは平常心を失ってしまいました...",
            "害羞", "羞涩", "尴尬");
        AddGroup("9.wav", "その通りです。もうメイドは怖くありません！",
            "回答", "解释", "建议", "赞同", "邀请");
        AddGroup("10.wav", "メイドレベルが足りませんでした...",
            "伤心", "难过", "委屈", "失望", "遗憾", "担心", "害怕", "道歉");
    }

    private static void AddGroup(string file, string prompt, params string[] emotions)
    {
        foreach (var e in emotions)
            s_map[e] = (file, prompt);
    }

    public static RefAudioEntry GetDefaultEntry(string emotion, string baseDir)
    {
        if (string.IsNullOrEmpty(emotion)) return null;
        if (!s_map.TryGetValue(emotion, out var info)) return null;
        if (!baseDir.EndsWith("/") && !baseDir.EndsWith("\\"))
            baseDir += "/";
        return new RefAudioEntry
        {
            emotionKey = emotion,
            audioFileName = info.file,
            promptText = info.prompt,
            promptLang = "ja",
            audioFullPath = baseDir + info.file
        };
    }

    public static RefAudioEntry GetDefaultZhEntry(string emotion, string baseDir)
    {
        return new RefAudioEntry
        {
            emotionKey = emotion,
            audioFileName = "",
            promptText = "",
            promptLang = "zh",
            audioFullPath = ""
        };
    }

    public static RefAudioEntry FindForEmotion(List<RefAudioEntry> entries, string emotion, string lang)
    {
        if (entries == null || entries.Count == 0 || string.IsNullOrEmpty(emotion))
            return null;

        var found = entries.Find(e => e.emotionKey == emotion && e.promptLang == lang);
        if (found != null && !string.IsNullOrEmpty(found.audioFullPath))
            return found;

        if (found == null)
            found = entries.Find(e => e.emotionKey == emotion && !string.IsNullOrEmpty(e.audioFullPath));

        if (found != null) return found;

        string other = lang == "ja" ? "zh" : "ja";
        var fallback = entries.Find(e => e.emotionKey == emotion && e.promptLang == other && !string.IsNullOrEmpty(e.audioFullPath));
        if (fallback != null) return fallback;

        return entries.Find(e => !string.IsNullOrEmpty(e.audioFullPath)) ?? entries[0];
    }

    public static RefAudioEntry GetFirst(List<RefAudioEntry> entries)
    {
        if (entries != null && entries.Count > 0)
            return entries[0];
        return null;
    }
}
