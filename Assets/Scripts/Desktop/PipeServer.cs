#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class PipeServer : MonoBehaviour
{
    public GameStart gameStart;
    public Configuration config;
    public LLMFormatter llmFormatter;
    public TTS ttsModule;
    public BaiduTranslator translator;
    public ModelManager modelManager;
    public AnimationLibrary animLibrary;
    public EmotionPlayer emotionPlayer;
    public PreviewController previewController;
    public ActionSystemDatabase database;

    private const int Port = 19876;
    private Thread? _serverThread;
    private Stream? _currentStream;
    private readonly object _streamLock = new();
    private volatile bool _running;
    private TcpListener? _listener;
    private string _logPath = "";

    private void Awake()
    {
        _logPath = Path.Combine(Application.persistentDataPath, "pipe_debug.log");
        if (database == null)
            database = Resources.Load<ActionSystemDatabase>("ActionSystemDatabase");
        if (database != null)
        {
            if (database.emotionMappings == null)
                database.emotionMappings = Resources.Load<EmotionMappingDatabase>("EmotionMappings");
            if (database.facialPresets == null)
                database.facialPresets = Resources.Load<FacialPresetDatabase>("FacialPresets");
            if (database.actionPresets == null)
                database.actionPresets = Resources.Load<ActionPresetDatabase>("ActionPresets");
        }
    }

    private void WriteLog(string msg)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            File.AppendAllText(_logPath,
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
        }
        catch { }
    }

    public void StartServer()
    {
        StopServer();
        _running = true;
        _serverThread = new Thread(ServerLoop);
        _serverThread.IsBackground = true;
        _serverThread.Start();
    }

    public void StopServer()
    {
        _running = false;
        try { _listener?.Server?.Close(); } catch { }
        try { _serverThread?.Join(500); } catch { }
    }

    private void ServerLoop()
    {
        WriteLog("ServerLoop started, port=" + Port);
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            WriteLog("TcpListener started");
        }
        catch (Exception ex)
        {
            WriteLog("TcpListener start error: " + ex.Message);
            return;
        }

        while (_running)
        {
            try
            {
                WriteLog("Waiting for connection...");
                using var client = _listener.AcceptTcpClient();
                WriteLog("Client connected");
                using var stream = client.GetStream();
                stream.ReadTimeout = 30000;
                lock (_streamLock) { _currentStream = stream; }

                var initJson = BuildInitJson();
                var initBytes = Encoding.UTF8.GetBytes(initJson + "\n");
                stream.Write(initBytes, 0, initBytes.Length);
                stream.Flush();
                WriteLog("Init sent (" + initBytes.Length + " bytes)");

                var reader = new StreamReader(stream, Encoding.UTF8);
                while (_running && client.Connected)
                {
                    try
                    {
                        var line = reader.ReadLine();
                        if (line == null) break;
                        WriteLog("Cmd: " + (line.Length > 80 ? line.Substring(0, 80) + "..." : line));
                        UnityMainThreadDispatcher.Enqueue(() => ProcessMessage(line));
                    }
                    catch (IOException) { break; }
                    catch (ObjectDisposedException) { break; }
                }
                WriteLog("Client disconnected");
                lock (_streamLock) { _currentStream = null; }
            }
            catch (Exception ex)
            {
                WriteLog("Session error: " + ex.GetType().Name + " - " + ex.Message);
                lock (_streamLock) { _currentStream = null; }
                if (_running) Thread.Sleep(500);
            }
        }
        WriteLog("ServerLoop ended");
    }

    private string BuildInitJson()
    {
        Debug.Log("[PipeServer] BuildInitJson: database=" + (database != null) +
            " emotionMappings=" + (database?.emotionMappings != null) +
            " mappingCount=" + (database?.emotionMappings?.mappings?.Count ?? -1) +
            " facialPresets=" + (database?.facialPresets != null) +
            " facialCount=" + (database?.facialPresets?.presets?.Count ?? -1) +
            " groups=" + (database?.actionGroups?.Count ?? -1));

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"init\",\"data\":{");

        AppendJsonProperty(sb, "websocketUrl", config.websocket_url);
        sb.Append(',');
        sb.Append("\"ttsMode\":").Append(config.tts).Append(',');
        AppendJsonProperty(sb, "gradioUrl", config.gradio_url);
        sb.Append(',');
        AppendJsonProperty(sb, "simpleVitsUrl", config.simpleVitsApi_url);
        sb.Append(',');
        AppendJsonProperty(sb, "translationUrl", config.translation_url ?? translator.Baidu_fanyi_url);
        sb.Append(',');
        AppendJsonProperty(sb, "translationAppId", config.translation_app_id ?? translator.App_id);
        sb.Append(',');
        AppendJsonProperty(sb, "translationKey", config.translation_key ?? translator.Private_key);
        sb.Append(',');
        AppendJsonProperty(sb, "translationSalt", config.translation_salt ?? translator.Salt);
        sb.Append(',');
        AppendJsonProperty(sb, "identity", llmFormatter.identity);
        sb.Append(',');
        AppendJsonProperty(sb, "preset", llmFormatter.preset_information);
        sb.Append(',');
        sb.Append("\"connected\":").Append(NetManager.M_Instance.GetNetStatus() ? "true" : "false").Append(',');
        sb.Append("\"modelHistory\":");
        AppendJsonArray(sb, modelManager != null ? modelManager.GetHistory() : new List<string>());
        sb.Append(',');
        sb.Append("\"animationList\":");
        AppendAnimationList(sb);
        sb.Append(',');
        sb.Append("\"expressionMappings\":");
        AppendExpressionMappings(sb);
        sb.Append(',');
        sb.Append("\"actionPresets\":");
        AppendActionPresets(sb);
        sb.Append(',');
        sb.Append("\"actionGroups\":");
        AppendActionGroups(sb);
        sb.Append(',');
        sb.Append("\"facialPresets\":");
        AppendFacialPresets(sb);
        sb.Append(',');
        AppendJsonProperty(sb, "dialogueHistory", llmFormatter.formatted_history);
        sb.Append(',');
        sb.Append("\"msgMaxWidth\":").Append(gameStart.msg_max_length).Append(',');
        sb.Append("\"msgHeight\":").Append(gameStart.msg_height);
        sb.Append("}}");
        return sb.ToString();
    }

    private static void AppendJsonProperty(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":\"");
        sb.Append(EscapeJson(value ?? ""));
        sb.Append('"');
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 20);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static void AppendJsonArray(StringBuilder sb, List<string> items)
    {
        sb.Append('[');
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(EscapeJson(items[i])).Append('"');
        }
        sb.Append(']');
    }

    private void AppendAnimationList(StringBuilder sb)
    {
        sb.Append('[');
        if (animLibrary != null)
        {
            bool first = true;
            foreach (var clip in animLibrary.registry)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":\"");
                sb.Append(EscapeJson(clip.name));
                sb.Append("\",\"category\":\"");
                sb.Append(EscapeJson(clip.category));
                sb.Append("\",\"duration\":");
                sb.Append(clip.duration.ToString("F1"));
                sb.Append(",\"actionParam\":");
                sb.Append(clip.actionParam);
                sb.Append('}');
            }
        }
        sb.Append(']');
    }

    private void AppendExpressionMappings(StringBuilder sb)
    {
        sb.Append('[');
        if (database != null && database.emotionMappings != null)
        {
            var mappings = database.emotionMappings.mappings;
            for (int i = 0; i < mappings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var m = mappings[i];
                var group = database.GetActionGroup(m.actionGroupName);
                string facial = !string.IsNullOrEmpty(m.facialOverride) ? m.facialOverride : (group != null ? group.facialPreset : "");
                float facialW = m.facialWeightOverride >= 0f ? m.facialWeightOverride : (group != null ? group.facialWeight : 1f);

                sb.Append("{\"emotion\":\"").Append(EscapeJson(m.emotion)).Append('"');
                sb.Append(",\"actionGroupName\":\"").Append(EscapeJson(m.actionGroupName)).Append('"');
                sb.Append(",\"facialOverride\":\"").Append(EscapeJson(m.facialOverride ?? "")).Append('"');
                sb.Append(",\"facialWeightOverride\":").Append(m.facialWeightOverride.ToString("F2"));
                sb.Append(",\"facialGroup\":{\"preset\":\"").Append(EscapeJson(facial ?? ""));
                sb.Append("\",\"weight\":").Append(facialW.ToString("F2")).Append('}');
                if (group != null)
                {
                    sb.Append(",\"actionGroup\":{\"animationName\":\"").Append(EscapeJson(group.groupName));
                    sb.Append("\",\"bodyPart\":\"fullBody\",\"weight\":1}");
                }
                sb.Append('}');
            }
        }
        sb.Append(']');
    }

    private void AppendActionGroups(StringBuilder sb)
    {
        sb.Append('[');
        if (database != null)
        {
            var groups = database.actionGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var g = groups[i];
                sb.Append("{\"groupName\":\"").Append(EscapeJson(g.groupName)).Append('"');
                sb.Append(",\"facialPreset\":\"").Append(EscapeJson(g.facialPreset ?? "")).Append('"');
                sb.Append(",\"facialWeight\":").Append(g.facialWeight.ToString("F2"));
                sb.Append(",\"loop\":").Append(g.loop ? "true" : "false");
                sb.Append(",\"blendInBody\":").Append(g.blendInBody.ToString("F2"));
                sb.Append(",\"blendInFacial\":").Append(g.blendInFacial.ToString("F2"));
                sb.Append(",\"blendOutBody\":").Append(g.blendOutBody.ToString("F2"));
                sb.Append(",\"blendOutFacial\":").Append(g.blendOutFacial.ToString("F2"));
                sb.Append(",\"holdAfterTTS\":").Append(g.holdAfterTTS.ToString("F1"));
                sb.Append(",\"holdNoTTS\":").Append(g.holdNoTTS.ToString("F1"));
                sb.Append(",\"isIdle\":").Append(g.isIdle ? "true" : "false");
                sb.Append(",\"bodyClips\":[");
                for (int j = 0; j < g.bodyClips.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append("{\"bodyPart\":\"").Append(EscapeJson(g.bodyClips[j].bodyPart));
                    sb.Append("\",\"clipName\":\"").Append(EscapeJson(g.bodyClips[j].clipName ?? "")).Append("\"}");
                }
                sb.Append("]}");
            }
        }
        sb.Append(']');
    }

    private void AppendFacialPresets(StringBuilder sb)
    {
        sb.Append('[');
        if (emotionPlayer != null && emotionPlayer.facialEngine != null && emotionPlayer.facialEngine.presetDatabase != null)
        {
            var presets = emotionPlayer.facialEngine.presetDatabase.presets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = presets[i];
                sb.Append("{\"presetName\":\"").Append(EscapeJson(p.presetName)).Append('"');
                sb.Append(",\"targets\":[");
                for (int j = 0; j < p.targets.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append("{\"index\":").Append(p.targets[j].index);
                    sb.Append(",\"weight\":").Append(p.targets[j].weight.ToString("F1")).Append('}');
                }
                sb.Append("],\"activateObjects\":[");
                for (int j = 0; j < p.activateObjects.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append('"').Append(EscapeJson(p.activateObjects[j])).Append('"');
                }
                sb.Append("],\"blushMode\":\"").Append(EscapeJson(p.blushMode ?? "")).Append("\"}");
            }
        }
        sb.Append(']');
    }

    private void AppendActionPresets(StringBuilder sb)
    {
        sb.Append('[');
        if (database != null)
        {
            var groups = database.actionGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var g = groups[i];
                sb.Append("{\"name\":\"").Append(EscapeJson(g.groupName));
                sb.Append("\",\"actionParam\":").Append(i + 1);
                sb.Append(",\"isDefault\":true}");
            }
        }
        sb.Append(']');
    }

    public void RefreshInitData()
    {
        Stream? s;
        lock (_streamLock) { s = _currentStream; }
        if (s == null) return;
        try
        {
            var json = BuildInitJson();
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            s.Write(bytes, 0, bytes.Length);
            s.Flush();
        }
        catch { lock (_streamLock) { _currentStream = null; } }
    }

    public void SendStatus(bool connected)
    {
        Stream? s;
        lock (_streamLock) { s = _currentStream; }
        if (s == null) return;
        try
        {
            var json = "{\"type\":\"status\",\"connected\":" + (connected ? "true" : "false") + "}\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            s.Write(bytes, 0, bytes.Length);
            s.Flush();
        }
        catch { lock (_streamLock) { _currentStream = null; } }
    }

    private void ProcessMessage(string line)
    {
        try
        {
            var cmd = JsonUtility.FromJson<PipeCommand>(line);
            if (cmd == null || string.IsNullOrEmpty(cmd.action)) return;

            switch (cmd.action)
            {
                case "connect":
                    gameStart.ConnectWebsocket();
                    break;
                case "disconnect":
                    gameStart.DisconnectWebsocket();
                    break;
                case "update_config":
                    ApplyConfig(cmd);
                    break;
                case "update_dialog":
                    ApplyDialogSettings(cmd);
                    break;
                case "load_model":
                    if (!string.IsNullOrEmpty(cmd.path))
                        modelManager?.LoadModel(cmd.path);
                    break;
                case "restore_default_model":
                    modelManager?.RestoreDefault();
                    break;
                case "load_model_from_history":
                    modelManager?.LoadFromHistory(cmd.index);
                    break;
                case "scan_animations":
                    animLibrary?.ScanAll();
                    break;
                case "import_animation":
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(cmd.path))
                        animLibrary?.ImportAnimation(cmd.path);
#endif
                    break;
                case "preview_animation":
                    if (!string.IsNullOrEmpty(cmd.name) && previewController != null)
                    {
                        var clip = FindClipByName(cmd.name);
                        if (clip != null)
                            previewController.PreviewBody(clip);
                    }
                    break;
                case "stop_preview":
                    previewController?.ExitPreview();
                    break;
                case "preview_facial":
                    if (!string.IsNullOrEmpty(cmd.facialX) && previewController != null)
                    {
                        if (!cmd.noZoom) gameStart.ZoomToHeadPublic();
                        previewController.PreviewFacial(cmd.facialX, cmd.facialW > 0 ? cmd.facialW : 1f);
                    }
                    break;
                case "preview_expression":
                    if (!string.IsNullOrEmpty(cmd.emotion) && previewController != null)
                    {
                        var group = database?.ResolveEmotion(cmd.emotion);
                        if (group != null && !string.IsNullOrEmpty(group.facialPreset))
                            previewController.PreviewFacial(group.facialPreset, group.facialWeight);
                    }
                    break;
                case "preview_action":
                    if (!string.IsNullOrEmpty(cmd.name) && previewController != null)
                    {
                        var clip = FindClipByName(cmd.name);
                        if (clip != null)
                            previewController.PreviewBody(clip);
                    }
                    break;
                case "reset_blendshapes":
                    previewController?.ExitPreview();
                    emotionPlayer?.ForceIdle();
                    break;
                case "restore_expression":
                    previewController?.ExitPreview();
                    emotionPlayer?.ForceIdle();
                    break;
                case "test_tts":
                    if (!string.IsNullOrEmpty(cmd.text))
                    {
                        gameStart.RefreshTtsModule();
                        var tts = gameStart.TTS_module;
                        if (tts != null)
                            tts.Speak(cmd.text, gameStart.PlayVoicePublic, gameStart.SetExceptionRestorePublic);
                    }
                    break;
                case "set_root_motion":
                    break;
                case "restore_default_mappings":
                    previewController?.ExitPreview();
                    emotionPlayer?.ForceIdle();
                    RefreshInitData();
                    break;
                case "update_expression_mapping":
                    UpdateExpressionMapping(cmd);
                    RefreshInitData();
                    break;
                case "delete_expression_mapping":
                    if (!string.IsNullOrEmpty(cmd.emotion) && database?.emotionMappings != null)
                    {
                        database.emotionMappings.Remove(cmd.emotion);
                        SaveMappings();
                    }
                    RefreshInitData();
                    break;
                case "restore_default_presets":
                    RefreshInitData();
                    break;
                case "save_action_preset":
                    RefreshInitData();
                    break;
                case "delete_action_preset":
                    RefreshInitData();
                    break;
                case "update_action_group":
                    UpdateActionGroup(cmd);
                    RefreshInitData();
                    break;
                case "clear_history":
                    llmFormatter.history.Clear();
                    llmFormatter.formatted_history = "";
                    RefreshInitData();
                    break;
            }
        }
        catch (Exception ex)
        {
            WriteLog("ProcessMessage error: " + ex.Message);
        }
    }

    private AnimationClip? FindClipByName(string name)
    {
        if (animLibrary == null || animLibrary.clipReferences == null) return null;
        foreach (var c in animLibrary.clipReferences)
            if (c != null && c.name == name) return c;
        return null;
    }

    private void ApplyConfig(PipeCommand cmd)
    {
        if (!string.IsNullOrEmpty(cmd.websocketUrl))
            config.websocket_url = cmd.websocketUrl;
        if (cmd.ttsMode >= 0)
            config.tts = cmd.ttsMode;
        if (!string.IsNullOrEmpty(cmd.ttsUrl))
        {
            if (config.tts == 0) config.gradio_url = cmd.ttsUrl;
            else if (config.tts == 1) config.simpleVitsApi_url = cmd.ttsUrl;
        }
        if (!string.IsNullOrEmpty(cmd.translationUrl))
            translator.Baidu_fanyi_url = cmd.translationUrl;
        if (!string.IsNullOrEmpty(cmd.translationAppId))
            translator.App_id = cmd.translationAppId;
        if (!string.IsNullOrEmpty(cmd.translationKey))
            translator.Private_key = cmd.translationKey;
        if (!string.IsNullOrEmpty(cmd.translationSalt))
            translator.Salt = cmd.translationSalt;
        config.translation_url = translator.Baidu_fanyi_url;
        config.translation_app_id = translator.App_id;
        config.translation_key = translator.Private_key;
        config.translation_salt = translator.Salt;
        gameStart.RefreshTtsModule();
        gameStart.SaveSettings();
    }

    private void ApplyDialogSettings(PipeCommand cmd)
    {
        if (cmd.msgWidth > 0)
        {
            gameStart.msg_max_length = cmd.msgWidth;
            gameStart.msg_length_receive = cmd.msgWidth;
        }
        if (cmd.msgHeight > 0)
            gameStart.msg_height = cmd.msgHeight;
        gameStart.SaveSettings();
    }

    private void UpdateExpressionMapping(PipeCommand cmd)
    {
        if (database == null || database.emotionMappings == null || string.IsNullOrEmpty(cmd.emotion)) return;
        string groupName = !string.IsNullOrEmpty(cmd.actionX) ? cmd.actionX : cmd.emotion;
        database.emotionMappings.Set(cmd.emotion, groupName);

        var entry = database.emotionMappings.GetEntry(cmd.emotion);
        if (entry != null)
        {
            entry.facialOverride = cmd.facialX ?? "";
            entry.facialWeightOverride = cmd.facialW > 0 ? cmd.facialW : -1f;
        }
        SaveMappings();
    }

    private void UpdateActionGroup(PipeCommand cmd)
    {
        if (database == null || string.IsNullOrEmpty(cmd.name)) return;
        var group = database.GetActionGroup(cmd.name);
        if (group == null) return;

        if (!string.IsNullOrEmpty(cmd.facialX))
            group.facialPreset = cmd.facialX;
        if (cmd.facialW > 0)
            group.facialWeight = cmd.facialW;

        ActionSystemJsonIO.SaveActionGroups(database.actionGroups);
    }

    private void SaveMappings()
    {
        if (database?.emotionMappings != null)
            ActionSystemJsonIO.SaveEmotionMappings(database.emotionMappings.mappings);
    }

    void OnDestroy() { StopServer(); }
    void OnApplicationQuit() { StopServer(); }
}

[Serializable]
public class PipeCommand
{
    public string type = "";
    public string action = "";
    public string websocketUrl = "";
    public int ttsMode = -1;
    public string ttsUrl = "";
    public string translationUrl = "";
    public string translationAppId = "";
    public string translationKey = "";
    public string translationSalt = "";
    public string identity = "";
    public string preset = "";
    public string path = "";
    public int index;
    public string name = "";
    public string text = "";
    public string emotion = "";
    public bool enable;
    public string facialX = "";
    public float facialW;
    public string actionX = "";
    public string actionP = "";
    public float actionY;
    public string facialGroupsJson = "";
    public string actionGroupsJson = "";
    public int actionParam = -1;
    public bool noZoom;
    public int msgWidth;
    public int msgHeight;
}
