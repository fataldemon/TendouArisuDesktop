using System;
using System.Collections.Generic;

[Serializable]
public class FacialGroup
{
    public string preset;
    public float weight = 1f;
}

[Serializable]
public class ActionGroup
{
    public string animationName;
    public string bodyPart = "fullBody";
    public float weight = 1f;
}

[Serializable]
public class ExpressionMappingData
{
    public string emotion;
    public List<FacialGroup> facialGroups = new List<FacialGroup>();
    public List<ActionGroup> actionGroups = new List<ActionGroup>();

    // legacy migration
    public string facialExpression;
    public int actionParam;
}

[Serializable]
public class MappingListWrapper
{
    public List<ExpressionMappingData> mappings;
}

public static class FacialPresets
{
    public static readonly string[] All = { "angry", "serious", "happy", "fun", "panic", "curious", "thinking", "disappointed", "sweating", "confident", "cry", "plain", "shy", "touching", "wink" };
}

public static class BodyParts
{
    public static readonly string[] All = { "fullBody", "upperBody", "lowerBody", "head", "leftArm", "rightArm", "leftLeg", "rightLeg", "hands" };
}
