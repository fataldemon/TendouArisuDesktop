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
        ActionSystemRuntime.EnsureInit();
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
        Debug.Log("[PipeServer] BuildInitJson: mappings=" + ActionSystemRuntime.EmotionMappings.Count +
            " facials=" + ActionSystemRuntime.FacialPresets.Count +
            " groups=" + ActionSystemRuntime.ActionGroups.Count);

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"init\",\"data\":{");

        AppendJsonProperty(sb, "websocketUrl", config.websocket_url);
        sb.Append(',');
        sb.Append("\"ttsMode\":").Append(config.tts).Append(',');
        AppendJsonProperty(sb, "gptSovitsUrl", config.gptSovitsUrl);
        sb.Append(',');
        AppendJsonProperty(sb, "gradioUrl", config.gradio_url);
        sb.Append(',');
        AppendJsonProperty(sb, "simpleVitsUrl", config.simpleVitsApi_url);
        sb.Append(',');
        sb.Append("\"translationEnabled\":").Append(config.translationEnabled ? "true" : "false").Append(',');
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
        sb.Append("\"blendShapeNames\":");
        AppendBlendShapeNames(sb);
        sb.Append(',');
        AppendJsonProperty(sb, "dialogueHistory", llmFormatter.formatted_history);
        sb.Append(',');
        sb.Append("\"msgMaxWidth\":").Append(gameStart.msg_max_length).Append(',');
        sb.Append("\"msgHeight\":").Append(gameStart.msg_height).Append(',');
        sb.Append("\"dialogMinHoldTime\":").Append(gameStart.DialogMinHoldTime.ToString("F1")).Append(',');
        sb.Append("\"bubbleColor\":[").Append(gameStart.BubbleBgColor.r.ToString("F3")).Append(',').Append(gameStart.BubbleBgColor.g.ToString("F3")).Append(',').Append(gameStart.BubbleBgColor.b.ToString("F3")).Append(',').Append(gameStart.BubbleBgColor.a.ToString("F2")).Append("],");
        sb.Append("\"bubbleTextColor\":[").Append(gameStart.BubbleTextColor.r.ToString("F2")).Append(',').Append(gameStart.BubbleTextColor.g.ToString("F2")).Append(',').Append(gameStart.BubbleTextColor.b.ToString("F2")).Append(',').Append(gameStart.BubbleTextColor.a.ToString("F2")).Append("],");
        sb.Append("\"modelScale\":").Append((modelManager != null && modelManager.currentModel != null ? modelManager.currentModel.transform.localScale.x : 1f).ToString("F2")).Append(',');
        sb.Append("\"eyeProfile\":");
        AppendEyeProfile(sb);
        sb.Append(',');
        sb.Append("\"allowRootMotion\":").Append(
            (emotionPlayer != null && emotionPlayer.bodyEngine != null && emotionPlayer.bodyEngine.allowRootMotion) ? "true" : "false");
        sb.Append(',');
        AppendJsonProperty(sb, "refAudioBaseDir", config.gptSovitsRefAudioBaseDir);
        sb.Append(',');
        AppendJsonProperty(sb, "bangbangkabangWavPath", SettingsData.Load()?.bangbangkabangWavPath ?? "");
        sb.Append(',');
        sb.Append("\"refAudioConfigs\":");
        AppendRefAudioConfigs(sb);
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
        var mappings = ActionSystemRuntime.EmotionMappings;
        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var m = mappings[i];
            var group = ActionSystemRuntime.GetActionGroup(m.actionGroupName);
            string facial = !string.IsNullOrEmpty(m.facialOverride) ? m.facialOverride : (group != null ? group.facialPreset : "");
            float facialW = m.facialWeightOverride >= 0f ? m.facialWeightOverride : (group != null ? group.facialWeight : 1f);

            sb.Append("{\"emotion\":\"").Append(EscapeJson(m.emotion)).Append('"');
            sb.Append(",\"actionGroupName\":\"").Append(EscapeJson(m.actionGroupName)).Append('"');
            sb.Append(",\"facialOverride\":\"").Append(EscapeJson(m.facialOverride ?? "")).Append('"');
                sb.Append(",\"facialWeightOverride\":").Append(m.facialWeightOverride.ToString("F2"));
                sb.Append(",\"isRandomEvent\":").Append(m.isRandomEvent ? "true" : "false");
            sb.Append(",\"facialGroup\":{\"preset\":\"").Append(EscapeJson(facial ?? ""));
            sb.Append("\",\"weight\":").Append(facialW.ToString("F2")).Append('}');
            if (group != null)
            {
                sb.Append(",\"actionGroup\":{\"animationName\":\"").Append(EscapeJson(group.groupName));
                sb.Append("\",\"bodyPart\":\"fullBody\",\"weight\":1}");
            }
            sb.Append('}');
        }
        sb.Append(']');
    }

    private void AppendActionGroups(StringBuilder sb)
    {
        sb.Append('[');
        {
            var groups = ActionSystemRuntime.ActionGroups;
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
                sb.Append(",\"allowRootMotion\":").Append(g.allowRootMotion ? "true" : "false");
                sb.Append(",\"enableEyeTracking\":").Append(g.enableEyeTracking ? "true" : "false");
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
        List<FacialPresetConfig> presets;
        if (emotionPlayer != null && emotionPlayer.facialEngine != null)
            presets = emotionPlayer.facialEngine.GetModelPresetsOrDefault();
        else
            presets = ActionSystemRuntime.FacialPresets;
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
        sb.Append(']');
    }

    private void AppendBlendShapeNames(StringBuilder sb)
    {
        sb.Append('[');
        var anim = emotionPlayer?.bodyEngine?.animator;
        var renderers = anim?.GetComponentsInChildren<SkinnedMeshRenderer>();
        bool first = true;
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                var mesh = r.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;
                Debug.Log("[PipeServer] AppendBlendShapeNames: mesh=" + mesh.name + " count=" + mesh.blendShapeCount);
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(EscapeJson(mesh.GetBlendShapeName(i))).Append('"');
                }
            }
        }
        if (first)
            Debug.LogWarning("[PipeServer] AppendBlendShapeNames: no blend shapes found on any mesh, renderers=" + (renderers?.Length ?? 0));
        sb.Append(']');
    }

    private void AppendEyeProfile(StringBuilder sb)
    {
        var p = modelManager?.CurrentEyeProfile;
        if (p == null)
        {
            var etc = emotionPlayer?.eyeTrackingController;
            var bc = emotionPlayer?.blinkController;
            p = new ModelEyeProfile();
            if (etc != null)
            {
                p.lookLeftIndex = etc.lookLeftBlendIndex;
                p.lookRightIndex = etc.lookRightBlendIndex;
                p.lookUpIndex = etc.lookUpBlendIndex;
                p.lookDownIndex = etc.lookDownBlendIndex;
                p.lookStrength = etc.lookStrength;
                p.headRotationAmount = etc.headRotationAmount;
            }
            if (bc != null)
            {
                p.blinkIndex = bc.blinkBlendIndex;
                p.blinkConflictIndices = bc.blinkConflictIndices ?? new List<int>();
            }
        }
        sb.Append('{');
        sb.Append("\"blinkIndex\":").Append(p.blinkIndex).Append(',');
        sb.Append("\"lookLeftIndex\":").Append(p.lookLeftIndex).Append(',');
        sb.Append("\"lookRightIndex\":").Append(p.lookRightIndex).Append(',');
        sb.Append("\"lookUpIndex\":").Append(p.lookUpIndex).Append(',');
        sb.Append("\"lookDownIndex\":").Append(p.lookDownIndex).Append(',');
        sb.Append("\"lookStrength\":").Append(p.lookStrength.ToString("F1")).Append(',');
        sb.Append("\"headRotationAmount\":").Append(p.headRotationAmount.ToString("F1")).Append(',');
        sb.Append("\"blinkConflictIndices\":[");
        if (p.blinkConflictIndices != null)
        {
            for (int i = 0; i < p.blinkConflictIndices.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(p.blinkConflictIndices[i]);
            }
        }
        sb.Append("]}");
    }

    private void AppendActionPresets(StringBuilder sb)
    {
        sb.Append('[');
        var groups = ActionSystemRuntime.ActionGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var g = groups[i];
            sb.Append("{\"name\":\"").Append(EscapeJson(g.groupName));
            sb.Append("\",\"actionParam\":").Append(i + 1);
            sb.Append(",\"isDefault\":true}");
        }
        sb.Append(']');
    }

    private void AppendRefAudioConfigs(StringBuilder sb)
    {
        ActionSystemRuntime.EnsureInit();
        var emotionKeys = new System.Collections.Generic.List<string>();
        foreach (var m in ActionSystemRuntime.EmotionMappings)
        {
            if (m.emotion == "触摸" || m.emotion == "拖拽" || m.emotion.StartsWith("随机-"))
                continue;
            if (!emotionKeys.Contains(m.emotion))
                emotionKeys.Add(m.emotion);
        }

        var settings = SettingsData.Load();
        var saved = settings?.refAudioConfigs;
        var savedDict = new System.Collections.Generic.Dictionary<string, RefAudioDataEntry>();
        if (saved != null)
            foreach (var se in saved)
                if (!string.IsNullOrEmpty(se.emotionKey))
                    savedDict[se.emotionKey + "|" + se.promptLang] = se;

        string baseDir = config.gptSovitsRefAudioBaseDir;
        if (string.IsNullOrEmpty(baseDir))
            baseDir = System.IO.Path.Combine(Application.streamingAssetsPath, "RefAudio");

        var items = new System.Collections.Generic.List<string>();
        foreach (var key in emotionKeys)
        {
            foreach (var lang in new[] { "ja", "zh" })
            {
                string dictKey = key + "|" + lang;
                if (savedDict.TryGetValue(dictKey, out var savedEntry))
                {
                    items.Add("{\"emotionKey\":\"" + EscapeJson(savedEntry.emotionKey) + '"' +
                        ",\"audioFileName\":\"" + EscapeJson(savedEntry.audioFileName) + '"' +
                        ",\"promptText\":\"" + EscapeJson(savedEntry.promptText) + '"' +
                        ",\"promptLang\":\"" + EscapeJson(savedEntry.promptLang) + '"' +
                        ",\"audioFullPath\":\"" + EscapeJson(savedEntry.audioFullPath) + "\"}");
                }
                else
                {
                    RefAudioEntry defaultEntry;
                    if (lang == "ja")
                        defaultEntry = RefAudioConfig.GetDefaultEntry(key, baseDir);
                    else
                        defaultEntry = RefAudioConfig.GetDefaultZhEntry(key, baseDir);

                    if (defaultEntry != null)
                    {
                        items.Add("{\"emotionKey\":\"" + EscapeJson(defaultEntry.emotionKey) + '"' +
                            ",\"audioFileName\":\"" + EscapeJson(defaultEntry.audioFileName) + '"' +
                            ",\"promptText\":\"" + EscapeJson(defaultEntry.promptText) + '"' +
                            ",\"promptLang\":\"" + EscapeJson(defaultEntry.promptLang) + '"' +
                            ",\"audioFullPath\":\"" + EscapeJson(defaultEntry.audioFullPath) + "\"}");
                    }
                }
            }
        }
        sb.Append('[');
        sb.Append(string.Join(",", items));
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

    public void SendToWPF(string json)
    {
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            Stream? s;
            lock (_streamLock) { s = _currentStream; }
            if (s == null) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json + "\n");
                s.Write(bytes, 0, bytes.Length);
                s.Flush();
            }
            catch { lock (_streamLock) { _currentStream = null; } }
        });
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
                case "update_bubble_color":
                    gameStart.SetBubbleColor(
                        new Color(cmd.bubbleR, cmd.bubbleG, cmd.bubbleB, cmd.bubbleA),
                        new Color(cmd.bubbleTextR, cmd.bubbleTextG, cmd.bubbleTextB, cmd.bubbleTextA));
                    gameStart.SaveSettings();
                    RefreshInitData();
                    break;
                case "load_model":
                    if (!string.IsNullOrEmpty(cmd.path))
                    {
                        Debug.Log("[PipeServer] load_model: path=" + cmd.path + " modelManager=" + (modelManager != null));
                        modelManager?.LoadModel(cmd.path);
                        gameStart?.SetCurrentModelPath(cmd.path);
                    }
                    break;
                case "restore_default_model":
                    Debug.Log("[PipeServer] restore_default_model: modelManager=" + (modelManager != null));
                    modelManager?.RestoreDefault();
                    gameStart?.ClearCurrentModelPath();
                    break;
                case "load_model_from_history":
                    modelManager?.LoadFromHistory(cmd.index);
                    if (modelManager != null)
                    {
                        var path = modelManager.GetHistoryPath(cmd.index);
                        if (!string.IsNullOrEmpty(path)) gameStart?.SetCurrentModelPath(path);
                    }
                    break;
                case "remove_model_from_history":
                    modelManager?.RemoveFromHistory(cmd.index);
                    break;
                case "update_model_scale":
                    if (modelManager != null && !string.IsNullOrEmpty(modelManager.CurrentModelKey) && cmd.modelScale > 0.1f)
                    {
                        ModelScaleIO.SetScale(modelManager.CurrentModelKey, cmd.modelScale);
                        if (modelManager.currentModel != null)
                            modelManager.currentModel.transform.localScale = Vector3.one * cmd.modelScale;
                        RefreshInitData();
                    }
                    break;
                case "preview_eye":
                    gameStart.ZoomToHeadPublic();
                    {
                        var etc = emotionPlayer?.eyeTrackingController;
                        var bc = emotionPlayer?.blinkController;
                        if (etc != null) etc.enabled = false;
                        if (bc != null) bc.enabled = false;

                        var renderer = etc?.meshRenderer ?? bc?.skinnedMeshRenderer;
                        if (renderer != null)
                        {
                            // Reset all eye BlendShapes to 0
                            if (etc != null)
                            {
                                renderer.SetBlendShapeWeight(etc.lookLeftBlendIndex, 0);
                                renderer.SetBlendShapeWeight(etc.lookRightBlendIndex, 0);
                                renderer.SetBlendShapeWeight(etc.lookUpBlendIndex, 0);
                                renderer.SetBlendShapeWeight(etc.lookDownBlendIndex, 0);
                            }
                            if (bc != null) renderer.SetBlendShapeWeight(bc.blinkBlendIndex, 0);

                            // Apply selected BlendShapes at full weight
                            ApplyPreviewEyeWeight(renderer, cmd.eyeLookL, 100f);
                            ApplyPreviewEyeWeight(renderer, cmd.eyeLookR, 100f);
                            ApplyPreviewEyeWeight(renderer, cmd.eyeLookU, 100f);
                            ApplyPreviewEyeWeight(renderer, cmd.eyeLookD, 100f);
                            ApplyPreviewEyeWeight(renderer, cmd.eyeBlinkIdx, 100f);
                        }
                        if (etc != null)
                        {
                            if (cmd.eyeLookL >= 0) etc.lookLeftBlendIndex = cmd.eyeLookL;
                            if (cmd.eyeLookR >= 0) etc.lookRightBlendIndex = cmd.eyeLookR;
                            if (cmd.eyeLookU >= 0) etc.lookUpBlendIndex = cmd.eyeLookU;
                            if (cmd.eyeLookD >= 0) etc.lookDownBlendIndex = cmd.eyeLookD;
                            if (cmd.eyeStrength >= 0) etc.lookStrength = cmd.eyeStrength;
                            if (cmd.eyeHeadRot >= 0) etc.headRotationAmount = cmd.eyeHeadRot;
                        }
                        if (bc != null && cmd.eyeBlinkIdx >= 0)
                            bc.blinkBlendIndex = cmd.eyeBlinkIdx;
                    }
                    break;
                case "update_eye_profile":
                    if (modelManager != null)
                    {
                        var ep = new ModelEyeProfile { modelKey = modelManager.CurrentModelKey };
                        ep.blinkIndex = cmd.eyeBlinkIdx;
                        ep.lookLeftIndex = cmd.eyeLookL;
                        ep.lookRightIndex = cmd.eyeLookR;
                        ep.lookUpIndex = cmd.eyeLookU;
                        ep.lookDownIndex = cmd.eyeLookD;
                        ep.lookStrength = cmd.eyeStrength;
                        ep.headRotationAmount = cmd.eyeHeadRot;
                        if (!string.IsNullOrEmpty(cmd.targetsJson))
                        {
                            var dtos = JsonUtility.FromJson<TargetsWrapper>("{\"items\":" + cmd.targetsJson + "}");
                            if (dtos?.items != null)
                                foreach (var d in dtos.items) ep.blinkConflictIndices.Add(d.index);
                        }
                        if (!string.IsNullOrEmpty(modelManager.CurrentModelKey))
                            ModelEyeIO.Save(ep);
                        modelManager.ApplyEyeProfile(ep);
                        RefreshInitData();
                    }
                    break;
                case "auto_detect_eyes":
                    if (modelManager != null)
                    {
                        if (!string.IsNullOrEmpty(modelManager.CurrentModelKey) && modelManager.currentModel != null)
                        {
                            ModelEyeIO.Delete(modelManager.CurrentModelKey);
                            var ep = modelManager.BuildEyeProfileFromVrm(modelManager.CurrentModelKey, modelManager.currentModel);
                            ModelEyeIO.Save(ep);
                            modelManager.ApplyEyeProfile(ep);
                        }
                        else
                        {
                            modelManager.ApplyEyeProfile(null);
                        }
                        RefreshInitData();
                    }
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
                        string bp = !string.IsNullOrEmpty(cmd.bodyPart) ? cmd.bodyPart : "fullBody";
                        if (clip != null)
                            previewController.PreviewBody(clip, bp);
                    }
                    break;
                case "stop_preview":
                    previewController?.ExitPreview();
                    gameStart.RestoreCharacterPublic();
                    RestoreEyeControllers();
                    break;
                case "preview_facial":
                    if (!string.IsNullOrEmpty(cmd.facialX) && previewController != null)
                    {
                        if (!cmd.noZoom) gameStart.ZoomToHeadPublic();
                        if (!string.IsNullOrEmpty(cmd.targetsJson))
                        {
                            var stored = emotionPlayer?.facialEngine?.GetPreset(cmd.facialX);
                            if (stored == null) stored = new FacialPresetConfig { presetName = cmd.facialX };
                            var overrides = BuildPresetFromJson(cmd.facialX, cmd.targetsJson);
                            stored.targets = overrides.targets;
                            emotionPlayer?.facialEngine?.ApplyPresetDirect(stored, cmd.facialW > 0 ? cmd.facialW : 1f);
                        }
                        else
                            previewController.PreviewFacial(cmd.facialX, cmd.facialW > 0 ? cmd.facialW : 1f);
                    }
                    break;
                case "preview_expression":
                    if (!string.IsNullOrEmpty(cmd.emotion) && previewController != null)
                    {
                        var group = ActionSystemRuntime.ResolveEmotion(cmd.emotion);
                        if (group != null && !string.IsNullOrEmpty(group.facialPreset))
                            previewController.PreviewFacial(group.facialPreset, group.facialWeight);
                    }
                    break;
                case "preview_action":
                    if (!string.IsNullOrEmpty(cmd.name) && previewController != null)
                    {
                        var actionGroup = ActionSystemRuntime.GetActionGroup(cmd.name);
                        string clipName = (actionGroup != null && actionGroup.bodyClips.Count > 0) ? actionGroup.bodyClips[0].clipName : cmd.name;
                        var clip = FindClipByName(clipName);
                        if (clip != null)
                            previewController.PreviewBody(clip);
                    }
                    break;
                case "preview_group_action":
                    Debug.Log("[Pipe] preview_group_action: facialX=" + (cmd.facialX ?? "null") + " facialW=" + cmd.facialW + " actionY=" + cmd.actionY + " actionW=" + cmd.actionW + " actionX=" + (cmd.actionX ?? "null"));
                    if (previewController != null)
                    {
                        var multiClips = new System.Collections.Generic.List<(string, AnimationClip)>();
                        string clipsStr = cmd.actionX ?? "";
                        if (!string.IsNullOrEmpty(clipsStr))
                        {
                            foreach (var partStr in clipsStr.Split('|'))
                            {
                                var kv = partStr.Split('=');
                                if (kv.Length == 2)
                                {
                                    var c = FindClipByName(kv[1]);
                                    if (c != null) multiClips.Add((kv[0], c));
                                }
                            }
                        }
                        Debug.Log("[Pipe] preview_group_action: " + multiClips.Count + " clips parsed, calling PreviewMultiBody(facial=" + (cmd.facialX ?? "") + " w=" + cmd.facialW + " arm=" + (cmd.actionY > 0f) + " et=" + (cmd.actionW > 0f) + ")");
                        previewController.PreviewMultiBody(cmd.facialX ?? "", cmd.facialW > 0 ? cmd.facialW : 1f, multiClips, cmd.actionY > 0f, cmd.actionW > 0f);
                    }
                    break;
                case "reset_blendshapes":
                    previewController?.ExitPreview();
                    gameStart.RestoreCharacterPublic();
                    RestoreEyeControllers();
                    break;
                case "restore_expression":
                    previewController?.ExitPreview();
                    gameStart.RestoreCharacterPublic();
                    RestoreEyeControllers();
                    break;
                case "test_tts":
                    if (!string.IsNullOrEmpty(cmd.text))
                    {
                        SendToWPF("{\"type\":\"tts_test_start\"}");
                        gameStart.RefreshTtsModule();
                        if (config.tts == 0 && config.ttsCoordinator != null)
                        {
                            config.ttsCoordinator.TestTts(cmd.text,
                                () => SendToWPF("{\"type\":\"tts_test_result\",\"success\":true}"),
                                (err) => SendToWPF("{\"type\":\"tts_test_result\",\"success\":false,\"error\":\"" + EscapeJson(err) + "\"}"));
                        }
                        else
                        {
                            var tts = gameStart.TTS_module;
                            if (tts != null)
                            {
                                try
                                {
                                    tts.Speak(cmd.text, (clip, txt) =>
                                    {
                                        UnityMainThreadDispatcher.Enqueue(() =>
                                        {
                                            gameStart.PlayVoicePublic(clip, txt);
                                            SendToWPF("{\"type\":\"tts_test_result\",\"success\":true}");
                                        });
                                    }, (err) =>
                                    {
                                        gameStart.SetExceptionRestorePublic(err);
                                        SendToWPF("{\"type\":\"tts_test_result\",\"success\":false,\"error\":\"TTS engine error\"}");
                                    });
                                }
                                catch (System.Exception ex)
                                {
                                    SendToWPF("{\"type\":\"tts_test_result\",\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
                                }
                            }
                            else
                            {
                                SendToWPF("{\"type\":\"tts_test_result\",\"success\":false,\"error\":\"No TTS module configured\"}");
                            }
                        }
                    }
                    break;
                case "set_root_motion":
                    if (emotionPlayer != null && emotionPlayer.bodyEngine != null)
                    {
                        emotionPlayer.bodyEngine.allowRootMotion = cmd.enable;
                        if (emotionPlayer.bodyEngine.animator != null)
                            emotionPlayer.bodyEngine.animator.applyRootMotion = cmd.enable;
                    }
                    break;
                case "restore_default_mappings":
                    previewController?.ExitPreview();
                    gameStart.RestoreCharacterPublic();
                    RefreshInitData();
                    break;
                case "update_expression_mapping":
                    UpdateExpressionMapping(cmd);
                    RefreshInitData();
                    break;
                case "delete_expression_mapping":
                    if (!string.IsNullOrEmpty(cmd.emotion))
                        ActionSystemRuntime.RemoveMapping(cmd.emotion);
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
                case "delete_action_group":
                    if (!string.IsNullOrEmpty(cmd.name))
                    {
                        ActionSystemRuntime.RemoveActionGroup(cmd.name);
                        RefreshInitData();
                    }
                    break;
                case "update_action_group":
                    UpdateActionGroup(cmd);
                    RefreshInitData();
                    break;
                case "update_facial_preset":
                    UpdateFacialPreset(cmd);
                    RefreshInitData();
                    break;
                case "clear_history":
                    llmFormatter.history.Clear();
                    llmFormatter.formatted_history = "";
                    RefreshInitData();
                    break;
                case "update_translation_toggle":
                    config.translationEnabled = cmd.translationEnabled;
                    gameStart.SaveSettings();
                    RefreshInitData();
                    break;
                case "update_ref_audio_base_dir":
                    if (!string.IsNullOrEmpty(cmd.refAudioBaseDir))
                    {
                        config.gptSovitsRefAudioBaseDir = cmd.refAudioBaseDir;
                        var settings = SettingsData.Load();
                        settings.gptSovitsRefAudioBaseDir = cmd.refAudioBaseDir;
                        settings.Save();
                        if (config.ttsCoordinator != null)
                            config.ttsCoordinator.ReloadRefAudio();
                    }
                    break;
                case "update_ref_audio_entry":
                    if (!string.IsNullOrEmpty(cmd.refAudioEmotion))
                    {
                        var settings = SettingsData.Load();
                        if (settings.refAudioConfigs == null)
                            settings.refAudioConfigs = new System.Collections.Generic.List<RefAudioDataEntry>();
                        var existing = settings.refAudioConfigs.Find(e => e.emotionKey == cmd.refAudioEmotion);
                        if (existing != null)
                        {
                            existing.audioFileName = cmd.refAudioPath ?? existing.audioFileName;
                            existing.promptText = cmd.refAudioPrompt ?? existing.promptText;
                            existing.promptLang = cmd.refAudioLang ?? existing.promptLang;
                            existing.audioFullPath = System.IO.Path.Combine(settings.gptSovitsRefAudioBaseDir ?? config.gptSovitsRefAudioBaseDir, existing.audioFileName);
                        }
                        settings.Save();
                        if (config.ttsCoordinator != null)
                            config.ttsCoordinator.ReloadRefAudio();
                        RefreshInitData();
                    }
                    break;
                case "import_ref_audio":
                    if (!string.IsNullOrEmpty(cmd.refAudioSourcePath) && System.IO.File.Exists(cmd.refAudioSourcePath))
                    {
                        string baseDir = config.gptSovitsRefAudioBaseDir;
                        if (string.IsNullOrEmpty(baseDir))
                            baseDir = System.IO.Path.Combine(Application.streamingAssetsPath, "RefAudio");
                        if (!System.IO.Directory.Exists(baseDir))
                            System.IO.Directory.CreateDirectory(baseDir);
                        string fileName = System.IO.Path.GetFileName(cmd.refAudioSourcePath);
                        string destPath = System.IO.Path.Combine(baseDir, fileName);
                        try
                        {
                            System.IO.File.Copy(cmd.refAudioSourcePath, destPath, true);
#if UNITY_EDITOR
                            UnityEditor.AssetDatabase.Refresh();
#endif
                            SendToWPF("{\"type\":\"ref_audio_imported\",\"emotionKey\":\"" + EscapeJson(cmd.refAudioEmotion ?? "") + "\",\"fileName\":\"" + EscapeJson(fileName) + "\",\"promptLang\":\"" + EscapeJson(cmd.refAudioLang ?? "ja") + "\"}");
                        }
                        catch (System.Exception ex)
                        {
                            SendToWPF("{\"type\":\"ref_audio_imported\",\"error\":\"" + EscapeJson(ex.Message) + "\"}");
                        }
                    }
                    break;
                case "update_bangbangkabang_wav":
                    if (!string.IsNullOrEmpty(cmd.bangbangkabangWavPath))
                    {
                        var settings = SettingsData.Load();
                        settings.bangbangkabangWavPath = cmd.bangbangkabangWavPath;
                        settings.Save();
                        if (config.ttsCoordinator != null)
                            config.ttsCoordinator.ReloadRefAudio();
                    }
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
        if (!string.IsNullOrEmpty(cmd.gptSovitsUrl))
            config.gptSovitsUrl = cmd.gptSovitsUrl;
        if (!string.IsNullOrEmpty(cmd.gradioUrl))
            config.gradio_url = cmd.gradioUrl;
        if (!string.IsNullOrEmpty(cmd.simpleVitsUrl))
            config.simpleVitsApi_url = cmd.simpleVitsUrl;
        if (!string.IsNullOrEmpty(cmd.ttsUrl))
        {
            if (config.tts == 0) config.gptSovitsUrl = cmd.ttsUrl;
            else if (config.tts == 1) config.gradio_url = cmd.ttsUrl;
            else if (config.tts == 2) config.simpleVitsApi_url = cmd.ttsUrl;
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
        if (cmd.dialogHold > 0)
            gameStart.GetType().GetField("_dialogueHoldDuration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(gameStart, cmd.dialogHold);
        gameStart.SaveSettings();
    }

    private void UpdateExpressionMapping(PipeCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.emotion)) return;
        string groupName = !string.IsNullOrEmpty(cmd.actionX) ? cmd.actionX : cmd.emotion;
        string facial = cmd.facialX ?? "";
        bool random = cmd.isRandom;
        ActionSystemRuntime.SetMapping(cmd.emotion, groupName, facial, random);
    }

    private void UpdateActionGroup(PipeCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.name)) return;
        bool arm = cmd.actionY > 0f;
        bool et = cmd.actionW > 0f;
        ActionSystemRuntime.UpdateActionGroup(cmd.name, cmd.facialX ?? "", cmd.facialW > 0 ? cmd.facialW : 1f, cmd.actionX ?? "", arm, et, cmd.loop);
        if (emotionPlayer != null)
            emotionPlayer.RefreshCurrentGroup(cmd.name);
    }

    [Serializable]
    private class BlendShapeTargetDto
    {
        public int index;
        public float weight;
    }

    private void UpdateFacialPreset(PipeCommand cmd)
    {
        if (string.IsNullOrEmpty(cmd.name)) return;
        string presetName = cmd.name;

        var profile = emotionPlayer?.facialEngine?.GetModelExpressionProfile();
        if (profile == null)
        {
            Debug.LogWarning("[PipeServer] UpdateFacialPreset: no model profile loaded, skipping save");
            return;
        }

        FacialPresetConfig preset = profile.Find(presetName);
        if (preset == null)
        {
            preset = new FacialPresetConfig { presetName = presetName };
            profile.presets.Add(preset);
        }

        if (!string.IsNullOrEmpty(cmd.targetsJson))
        {
            var dtos = JsonUtility.FromJson<TargetsWrapper>("{\"items\":" + cmd.targetsJson + "}");
            if (dtos?.items != null)
            {
                preset.targets.Clear();
                for (int i = 0; i < dtos.items.Length; i++)
                    preset.targets.Add(new BlendShapeTarget { index = dtos.items[i].index, weight = dtos.items[i].weight });
            }
        }

        if (!string.IsNullOrEmpty(cmd.blushMode))
            preset.blushMode = cmd.blushMode;

        ModelExpressionIO.Save(profile);
        Debug.Log("[PipeServer] UpdateFacialPreset: saved '" + presetName + "' with " + preset.targets.Count + " targets");
    }

    [Serializable]
    private class TargetsWrapper { public BlendShapeTargetDto[] items; }

    private FacialPresetConfig BuildPresetFromJson(string name, string targetsJson)
    {
        var preset = new FacialPresetConfig { presetName = name };
        var dtos = JsonUtility.FromJson<TargetsWrapper>("{\"items\":" + targetsJson + "}");
        if (dtos?.items != null)
        {
            foreach (var d in dtos.items)
                preset.targets.Add(new BlendShapeTarget { index = d.index, weight = d.weight });
        }
        return preset;
    }

    private static void ApplyPreviewEyeWeight(SkinnedMeshRenderer renderer, int index, float weight)
    {
        if (index >= 0 && index < renderer.sharedMesh.blendShapeCount)
            renderer.SetBlendShapeWeight(index, weight);
    }

    private void RestoreEyeControllers()
    {
        var etc = emotionPlayer?.eyeTrackingController;
        var bc = emotionPlayer?.blinkController;
        var renderer = etc?.meshRenderer ?? bc?.skinnedMeshRenderer;
        if (renderer != null)
        {
            if (etc != null)
            {
                renderer.SetBlendShapeWeight(etc.lookLeftBlendIndex, 0);
                renderer.SetBlendShapeWeight(etc.lookRightBlendIndex, 0);
                renderer.SetBlendShapeWeight(etc.lookUpBlendIndex, 0);
                renderer.SetBlendShapeWeight(etc.lookDownBlendIndex, 0);
            }
            if (bc != null) renderer.SetBlendShapeWeight(bc.blinkBlendIndex, 0);
        }
        if (etc != null) { etc.enabled = true; etc.expressionActive = false; }
        if (bc != null) { bc.enabled = true; bc.suppressed = false; }
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
    public string gptSovitsUrl = "";
    public string gradioUrl = "";
    public string simpleVitsUrl = "";
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
    public float actionW;
    public bool isRandom;
    public bool loop;
    public string facialGroupsJson = "";
    public string actionGroupsJson = "";
    public string targetsJson = "";
    public string blushMode = "";
    public int actionParam = -1;
    public bool noZoom;
    public int msgWidth;
    public int msgHeight;
    public float dialogHold;
    public string bodyPart = "fullBody";
    public float bubbleR, bubbleG, bubbleB, bubbleA = 0.88f;
    public float bubbleTextR = 1f, bubbleTextG = 1f, bubbleTextB = 1f, bubbleTextA = 1f;
    public float modelScale = 1f;
    public int eyeBlinkIdx = -1, eyeLookL = -1, eyeLookR = -1, eyeLookU = -1, eyeLookD = -1;
    public float eyeStrength = 120f, eyeHeadRot = 10f;
    public string refAudioBaseDir = "";
    public string refAudioEmotion = "";
    public string refAudioPath = "";
    public string refAudioPrompt = "";
    public string refAudioLang = "";
    public string refAudioSourcePath = "";
    public string bangbangkabangWavPath = "";
    public bool translationEnabled = false;
}
