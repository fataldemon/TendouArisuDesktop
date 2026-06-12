using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AliceBotSettings;

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

public class PipeMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; set; }
}

public class InitData
{
    public string WebsocketUrl { get; set; } = "";
    public int TtsMode { get; set; }
    public string GradioUrl { get; set; } = "";
    public string SimpleVitsUrl { get; set; } = "";
    public string TranslationUrl { get; set; } = "";
    public string TranslationAppId { get; set; } = "";
    public string TranslationKey { get; set; } = "";
    public string TranslationSalt { get; set; } = "";
    public string Identity { get; set; } = "";
    public string Preset { get; set; } = "";
    public bool Connected { get; set; }
    public List<string> ModelHistory { get; set; } = new();
    public List<AnimationEntry> AnimationList { get; set; } = new();
    public List<ExpressionMappingEntry> ExpressionMappings { get; set; } = new();
    public List<ActionGroupFullEntry> ActionGroups { get; set; } = new();
    public List<FacialPresetEntry> FacialPresets { get; set; } = new();
    public string DialogueHistory { get; set; } = "";
    public int MsgMaxWidth { get; set; }
    public int MsgHeight { get; set; }
    public bool AllowRootMotion { get; set; }

    // Legacy compatibility
    public List<ActionPresetEntry> ActionPresets { get; set; } = new();
}

public class AnimationEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public float Duration { get; set; }
    public int ActionParam { get; set; }
}

public class ExpressionMappingEntry
{
    public string Emotion { get; set; } = "";
    public string ActionGroupName { get; set; } = "";
    public string FacialOverride { get; set; } = "";
    public float FacialWeightOverride { get; set; } = -1f;

    // Legacy compatibility
    public FacialGroupEntry? FacialGroup { get; set; }
    public ActionGroupEntry? ActionGroup { get; set; }
}

public class FacialGroupEntry
{
    public string Preset { get; set; } = "";
    public float Weight { get; set; } = 1f;
}

public class ActionGroupEntry
{
    public string AnimationName { get; set; } = "";
    public string BodyPart { get; set; } = "fullBody";
    public float Weight { get; set; } = 1f;
}

public class ActionGroupFullEntry
{
    public string GroupName { get; set; } = "";
    public string FacialPreset { get; set; } = "";
    public float FacialWeight { get; set; } = 1f;
    public List<PartClipEntryDto> BodyClips { get; set; } = new();
    public bool Loop { get; set; }
    public float BlendInBody { get; set; } = 0.35f;
    public float BlendInFacial { get; set; } = 0.15f;
    public float BlendOutBody { get; set; } = 0.35f;
    public float BlendOutFacial { get; set; } = 0.2f;
    public float HoldAfterTTS { get; set; } = 3f;
    public float HoldNoTTS { get; set; } = 4f;
    public bool IsIdle { get; set; }
}

public class PartClipEntryDto
{
    public string BodyPart { get; set; } = "fullBody";
    public string ClipName { get; set; } = "";
}

public class FacialPresetEntry
{
    public string PresetName { get; set; } = "";
    public List<BlendShapeTargetEntry> Targets { get; set; } = new();
    public List<string> ActivateObjects { get; set; } = new();
    public string BlushMode { get; set; } = "";
}

public class BlendShapeTargetEntry
{
    public int Index { get; set; }
    public float Weight { get; set; }
}

public class StatusUpdate
{
    public bool Connected { get; set; }
    public string? CurrentModel { get; set; }
}

public class ActionPresetEntry
{
    public string Name { get; set; } = "";
    public int ActionParam { get; set; }
    public bool IsDefault { get; set; }
}

public static class FacialPresetNames
{
    public static readonly string[] All = { "angry", "serious", "happy", "fun", "panic", "curious", "thinking", "disappointed", "sweating", "confident", "cry", "plain", "shy", "touching", "wink" };
}

public static class BodyPartNames
{
    public static readonly string[] All = { "fullBody", "upperBody", "head", "leftArm", "rightArm", "lowerBody" };
}
