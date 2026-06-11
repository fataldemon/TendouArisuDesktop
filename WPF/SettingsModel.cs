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
    public string DialogueHistory { get; set; } = "";
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

public class StatusUpdate
{
    public bool Connected { get; set; }
    public string? CurrentModel { get; set; }
}
