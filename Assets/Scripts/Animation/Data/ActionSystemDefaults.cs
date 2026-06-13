using System.Collections.Generic;

public static class ActionSystemDefaults
{
    public static List<FacialPresetConfig> BuildFacialPresets()
    {
        var list = new List<FacialPresetConfig>();

        list.Add(Facial("angry", new[] { B(33, 100), B(32, 100) }, null, null));
        list.Add(Facial("serious", new[] { B(33, 100) }, null, null));
        list.Add(Facial("happy", new[] { B(35, 100) }, null, null));
        list.Add(Facial("fun", new[] { B(34, 50) }, null, null));
        list.Add(Facial("panic", new[] { B(1, 50), B(4, 100), B(19, 100), B(26, 100), B(11, 92.8f) }, new[] { "Sweat1", "Sweat2" }, null));
        list.Add(Facial("curious", new[] { B(3, 50), B(9, 50) }, null, null));
        list.Add(Facial("thinking", new[] { B(2, 100), B(3, 30), B(11, 75), B(16, 50) }, null, null));
        list.Add(Facial("disappointed", new[] { B(6, 80), B(26, 100) }, new[] { "Sweat2" }, null));
        list.Add(Facial("sweating", new[] { B(1, 50), B(4, 100), B(19, 100), B(26, 100) }, new[] { "Sweat2" }, null));
        list.Add(Facial("confident", new[] { B(2, 50) }, null, null));
        list.Add(Facial("cry", new[] { B(2, 80), B(19, 100), B(26, 100), B(11, 10) }, new[] { "Tear1", "Tear2" }, null));
        list.Add(Facial("plain", new[] { B(6, 50) }, null, null));
        list.Add(Facial("shy", new[] { B(2, 50), B(6, 35), B(19, 100), B(26, 100), B(29, 100), B(30, 100) }, null, "shy"));
        list.Add(Facial("touching", new[] { B(35, 100), B(26, 100) }, new[] { "Tear1_Joy", "Tear2_Joy" }, null));
        list.Add(Facial("wink", new[] { B(34, 50), B(13, 50), B(18, 50) }, null, null));

        return list;
    }

    public static List<ActionGroupConfig> BuildActionGroups()
    {
        var groups = new List<ActionGroupConfig>();

        groups.Add(Group("Idle", "plain", 1f, "AGIA_Idle_generic_01", true, true));
        groups.Add(Group("Touch", "", 1f, "KA_Idle41_CuteShyPose", false, false));
        groups.Add(Group("Drag", "", 1f, "AGIA_Idle_generic_01", false, false));
        groups.Add(Group("Speak Normal", "", 1f, "KA_Speak01_Normal_Loop", true, false));
        groups.Add(Group("Speak Explain", "serious", 1f, "KA_Speak02_Explaining_Loop", true, false));
        groups.Add(Group("Speak Excited", "fun", 1f, "KA_Speak03_Excited_Loop", true, false));
        groups.Add(Group("Speak Thinking", "thinking", 1f, "KA_Speak04_Calm_Loop", true, false));
        groups.Add(Group("Speak Chatty", "angry", 1f, "KA_Speak07_Chatty_Loop", true, false));
        groups.Add(Group("Speak Shy", "shy", 1f, "KA_Speak10_Shy_Loop", true, false));
        groups.Add(Group("Wave Hands", "", 1f, "KA_Idle16_WaveHands", false, false));
        groups.Add(Group("Cat", "wink", 1f, "AGIA_Other_cat_01_emote_01", false, false));
        groups.Add(Group("Doya", "confident", 1f, "AGIA_Idle_angry_01_hands_on_waist", false, false));
        groups.Add(Group("Welcome", "fun", 1f, "AGIA_Other_wave_arm_01", false, false));
        groups.Add(Group("Yay", "happy", 1f, "KA_Idle36_Yay", false, false));
        groups.Add(Group("Shy", "shy", 1f, "KA_Idle41_CuteShyPose", false, false));
        groups.Add(Group("Comfort", "fun", 1f, "KA_Idle33_Hug1_1", false, false));
        groups.Add(Group("Highfive", "fun", 1f, "KA_Idle21_HighFive1_1", false, false));
        groups.Add(Group("Deny", "serious", 1f, "AGIA_Other_wave_hands_01", false, false));
        groups.Add(Group("Determine", "serious", 1f, "AGIA_Idle_energetic_03_flex", false, false));
        groups.Add(Group("Cute", "wink", 1f, "AGIA_Idle_energetic_02_right_hand_piece", false, false));
        groups.Add(Group("Invite Give", "fun", 1f, "AGIA_Other_cute_02_emote_01", false, false));
        groups.Add(Group("Disagree", "thinking", 1f, "AGIA_Layer_shake_head_01", false, false));
        groups.Add(Group("Confuse", "curious", 1f, "AGIA_Layer_tilt_neck_01", false, false));
        groups.Add(Group("Agree", "fun", 1f, "AGIA_Layer_nod_twice_01", false, false));
        groups.Add(Group("Think", "thinking", 1f, "AGIA_Idle_think_01", false, false));
        groups.Add(Group("Sleepy", "thinking", 1f, "KA_Idle05_Stretch", false, false));
        groups.Add(Group("Expectation", "fun", 1f, "KA_Idle18_Shy", false, false));
        groups.Add(Group("Angry", "angry", 1f, "KA_Idle27_Angry", false, false));
        groups.Add(Group("Hurry", "angry", 1f, "KA_Idle27_Angry", false, false));
        groups.Add(Group("Focused", "curious", 1f, "AGIA_Idle_cute_03_leaning_forward", false, false));
        groups.Add(Group("Afraid", "sweating", 1f, "KA_Idle29_Surprised", false, false));
        groups.Add(Group("Apologize", "sweating", 1f, "KA_Idle44_GreetingBow", false, false));

        return groups;
    }

    public static List<EmotionMappingEntry> BuildEmotionMappings()
    {
        var list = new List<EmotionMappingEntry>();

        M(list, "待机", "Idle", "plain");
        M(list, "触摸", "Touch", "");
        M(list, "拖拽", "Drag", "");
        M(list, "微笑", "Speak Normal", "");
        M(list, "认真", "Speak Explain", "serious");
        M(list, "坚定", "Determine", "serious");
        M(list, "承诺", "Determine", "serious");
        M(list, "生气", "Angry", "angry");
        M(list, "急切", "Speak Chatty", "angry");
        M(list, "烦恼", "Speak Normal", "panic");
        M(list, "专注", "Focused", "curious");
        M(list, "诚实", "Speak Normal", "curious");
        M(list, "期待", "Expectation", "fun");
        M(list, "回答", "Speak Explain", "curious");
        M(list, "回忆", "Think", "thinking");
        M(list, "发愣", "Confuse", "curious");
        M(list, "察觉", "Speak Normal", "curious");
        M(list, "建议", "Speak Explain", "fun");
        M(list, "好奇", "Focused", "curious");
        M(list, "自信", "Doya", "confident");
        M(list, "自豪", "Doya", "confident");
        M(list, "解释", "Speak Explain", "fun");
        M(list, "失望", "Speak Excited", "disappointed");
        M(list, "委屈", "Shy", "cry");
        M(list, "伤心", "Speak Shy", "cry");
        M(list, "高兴", "Speak Excited", "fun");
        M(list, "开心", "Speak Excited", "happy");
        M(list, "欢迎", "Welcome", "fun");
        M(list, "崇拜", "Expectation", "fun");
        M(list, "愉快", "Speak Normal", "fun");
        M(list, "贴心", "Focused", "fun");
        M(list, "赞同", "Agree", "fun");
        M(list, "邀请", "Invite Give", "fun");
        M(list, "兴奋", "Speak Excited", "happy");
        M(list, "快乐", "Speak Excited", "happy");
        M(list, "难过", "Speak Normal", "disappointed");
        M(list, "为难", "Speak Explain", "disappointed");
        M(list, "尴尬", "Speak Explain", "disappointed");
        M(list, "紧张", "Speak Normal", "disappointed");
        M(list, "困惑", "Speak Explain", "disappointed");
        M(list, "困扰", "Speak Explain", "disappointed");
        M(list, "疑惑", "Speak Normal", "disappointed");
        M(list, "害怕", "Afraid", "sweating");
        M(list, "平和", "Speak Normal", "plain");
        M(list, "无聊", "Speak Normal", "plain");
        M(list, "冷漠", "Speak Normal", "plain");
        M(list, "慌张", "Afraid", "panic");
        M(list, "害羞", "Speak Shy", "shy");
        M(list, "羞涩", "Shy", "shy");
        M(list, "惊喜", "Speak Excited", "fun");
        M(list, "理解", "Agree", "fun");
        M(list, "喜悦", "Speak Excited", "fun");
        M(list, "担忧", "Speak Explain", "sweating");
        M(list, "流汗", "Speak Explain", "sweating");
        M(list, "犹豫", "Speak Explain", "disappointed");
        M(list, "震惊", "Afraid", "sweating");
        M(list, "惊讶", "Afraid", "sweating");
        M(list, "思考", "Speak Thinking", "thinking");
        M(list, "沉思", "Think", "thinking");
        M(list, "否认", "Disagree", "thinking");
        M(list, "睡觉", "Sleepy", "thinking");
        M(list, "陈述", "Speak Normal", "plain");
        M(list, "祈祷", "Speak Normal", "thinking");
        M(list, "拒绝", "Deny", "serious");
        M(list, "感动", "Speak Excited", "touching");
        M(list, "感激", "Speak Excited", "touching");
        M(list, "道歉", "Apologize", "sweating");
        M(list, "可爱", "Cute", "wink");
        M(list, "俏皮", "Confuse", "wink");
        M(list, "调皮", "Confuse", "wink");
        M(list, "卖萌", "Cat", "wink");
        M(list, "眨眼", "Cute", "wink");

        // Random events
        var events = new List<EmotionMappingEntry>
        {
            new EmotionMappingEntry { emotion = "随机-好奇", actionGroupName = "Focused", facialOverride = "curious", isRandomEvent = true },
            new EmotionMappingEntry { emotion = "随机-眨眼", actionGroupName = "Cute", facialOverride = "wink", isRandomEvent = true }
        };
        list.AddRange(events);

        return list;
    }

    private static BlendShapeTarget B(int index, float weight)
    {
        return new BlendShapeTarget { index = index, weight = weight };
    }

    private static FacialPresetConfig Facial(string name, BlendShapeTarget[] targets, string[] objects, string blush)
    {
        var config = new FacialPresetConfig
        {
            presetName = name,
            targets = new List<BlendShapeTarget>(targets),
            blushMode = blush
        };
        if (objects != null)
            config.activateObjects = new List<string>(objects);
        return config;
    }

    private static ActionGroupConfig Group(string name, string facial, float facialW, string actionPreset, bool loop, bool idle)
    {
        var config = new ActionGroupConfig
        {
            groupName = name,
            facialPreset = facial,
            facialWeight = facialW,
            loop = loop,
            isIdle = idle,
            blendInBody = 0.35f,
            blendInFacial = 0.15f,
            blendOutBody = 0.35f,
            blendOutFacial = 0.2f,
            holdAfterTTS = 3f,
            holdNoTTS = 4f,
            enableEyeTracking = idle
        };
        config.bodyClips.Add(new PartClipEntry { bodyPart = "fullBody", clipName = actionPreset });
        return config;
    }

    private static void M(List<EmotionMappingEntry> list, string emotion, string group, string facial = "")
    {
        list.Add(new EmotionMappingEntry { emotion = emotion, actionGroupName = group, facialOverride = facial });
    }
}
