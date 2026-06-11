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
    public ExpressionMappingManager mappingManager;
    public ActionController actionController;
    public ActionPresetManager presetManager;

    private const int Port = 19876;
    private Thread? _serverThread;
    private Stream? _currentStream;
    private readonly object _streamLock = new();
    private volatile bool _running;
    private string _logPath;

    private void WriteLog(string msg)
    {
        if (_logPath == null)
            _logPath = Path.Combine(Application.persistentDataPath, "pipe_debug.log");
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
        try { _serverThread?.Join(2000); } catch { }
    }

    private void ServerLoop()
    {
        WriteLog("ServerLoop started, port=" + Port);
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
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
                using var client = listener.AcceptTcpClient();
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
        AppendJsonProperty(sb, "dialogueHistory", llmFormatter.formatted_history);
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
        if (mappingManager != null)
        {
            var mappings = mappingManager.GetAll();
            for (int i = 0; i < mappings.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonUtility.ToJson(mappings[i]));
            }
        }
        sb.Append(']');
    }

    private void AppendActionPresets(StringBuilder sb)
    {
        sb.Append('[');
        if (presetManager != null)
        {
            var all = presetManager.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonUtility.ToJson(all[i]));
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
                    if (!string.IsNullOrEmpty(cmd.name))
                    {
                        WriteLog("Preview: looking for '" + cmd.name + "' refs=" + (animLibrary?.clipReferences?.Length ?? -1));
                        animLibrary?.StopPreview();
                        var clip = animLibrary?.registry.Find(r => r.name == cmd.name);
                        WriteLog("Preview: clip found=" + (clip != null) + " actionParam=" + (clip?.actionParam ?? -1));
                        if (clip != null) { animLibrary?.Preview(clip); WriteLog("Preview: called"); }
                        else WriteLog("Preview: clip not found in registry");
                    }
                    break;
                case "stop_preview":
                    animLibrary?.StopPreview();
                    break;
                case "preview_expression":
                    if (actionController?.facialController != null && !string.IsNullOrEmpty(cmd.emotion))
                    {
                        actionController.facialController.ResetBlendShapesInstant();
                        mappingManager?.TryApplyFacial(cmd.emotion);
                    }
                    break;
                case "preview_facial":
                    if (actionController?.facialController != null && !string.IsNullOrEmpty(cmd.facialX))
                    {
                        gameStart.ZoomToHeadPublic();
                        actionController.facialController.ResetBlendShapesInstant();
                        actionController.facialController.PreviewBlendShape(cmd.facialX, cmd.facialW > 0 ? cmd.facialW : 1f);
                    }
                    break;
                case "restore_expression":
                    gameStart.RestoreCharacterPublic();
                    mappingManager?.TryApplyFacial("待机");
                    break;
                case "preview_action":
                    if (!string.IsNullOrEmpty(cmd.name))
                    {
                        var p = presetManager?.GetByName(cmd.name);
                        if (p != null)
                            actionController.animator.SetInteger("action_param", p.actionParam);
                        else if (int.TryParse(cmd.name, out int ap2))
                            actionController.animator.SetInteger("action_param", ap2);
                    }
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
                    if (animLibrary != null)
                        animLibrary.allowRootMotion = cmd.enable;
                    break;
                case "restore_default_mappings":
                    mappingManager?.RestoreDefaults();
                    gameStart.RestoreCharacterPublic();
                    mappingManager?.TryApplyFacial("待机");
                    RefreshInitData();
                    break;
                case "update_expression_mapping":
                    UpdateExpressionMapping(cmd);
                    RefreshInitData();
                    break;
                case "delete_expression_mapping":
                    if (!string.IsNullOrEmpty(cmd.emotion))
                        mappingManager?.RemoveMapping(cmd.emotion);
                    RefreshInitData();
                    break;
                case "restore_default_presets":
                    presetManager?.RestoreDefaults();
                    RefreshInitData();
                    break;
                case "save_action_preset":
                    if (!string.IsNullOrEmpty(cmd.name) && cmd.actionParam >= 0)
                        presetManager?.AddOrUpdate(cmd.name, cmd.actionParam);
                    RefreshInitData();
                    break;
                case "delete_action_preset":
                    if (!string.IsNullOrEmpty(cmd.name))
                        presetManager?.Remove(cmd.name);
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
        if (!string.IsNullOrEmpty(cmd.identity))
            llmFormatter.identity = cmd.identity;
        if (!string.IsNullOrEmpty(cmd.preset))
            llmFormatter.preset_information = cmd.preset;
        config.identity = llmFormatter.identity;
        config.preset = llmFormatter.preset_information;
        gameStart.SaveSettings();
    }

    private void UpdateExpressionMapping(PipeCommand cmd)
    {
        if (mappingManager == null || string.IsNullOrEmpty(cmd.emotion)) return;
        var fg = new FacialGroup { preset = cmd.facialX, weight = cmd.facialW > 0 ? cmd.facialW : 1f };
        var ag = new ActionGroup { animationName = cmd.actionX, bodyPart = cmd.actionP ?? "fullBody", weight = cmd.actionY > 0 ? cmd.actionY : 1f };
        if (string.IsNullOrEmpty(fg.preset)) fg = null;
        if (string.IsNullOrEmpty(ag.animationName)) ag = null;
        mappingManager.AddOrUpdate(cmd.emotion, fg, ag);
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
}
