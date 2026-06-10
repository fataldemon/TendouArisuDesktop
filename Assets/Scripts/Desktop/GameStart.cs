using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    private string msg;
    private string reply;
    private string thought;
    private string answer;
    private string text_answer;
    private string finish_reason;
    private string streamBuffer = "";

    [SerializeField] private string websocket_url;
    [SerializeField] private bool withExpression;
    [SerializeField] private bool onRestore;
    [SerializeField] public float facialRestoreTime = 3f;
    [SerializeField] private float facialRestoreTimer;
    private bool exceptionRestore;
    private bool expressionApplied;

    [SerializeField] private bool onVoice;
    [SerializeField] private AudioSource m_AudioSource;
    [SerializeField] public int msg_position_x = 300;
    [SerializeField] public int msg_position_y = 150;
    [SerializeField] private int msg_max_length = 580;
    private int msg_length_receive;
    private int msg_length_send;
    [SerializeField] public int msg_height = 1600;

    public Configuration config;
    public TransparentWindow windowController;
    public ActionController actionController;
    public LLMFormatter llmFormatter;
    public TTS TTS_module;
    public BaiduTranslator translator;
    public ModelManager modelManager;
    public AnimationLibrary animLibrary;
    public ExpressionMappingManager mappingManager;
    public PipeServer pipeServer;
    public SystemTrayManager trayManager;

    public float dialogueInterval = 0.5f;
    private float dialogueTimer;
    private float messageTimer;

    public Transform targetTransform;

    [SerializeField] private bool onDialogue;
    [SerializeField] private int fontSize = 40;

    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 scrollPosition2 = Vector2.zero;
    private float scrollSpeed = 7f;
    private float pauseDuration = 1f;
    private bool isScrolling = true;
    private float pauseTimer;

    private bool isCameraZoomed;
    private Vector3 savedCameraPos;
    private Quaternion savedCameraRot;
    private Coroutine cameraZoomRoutine;

    [SerializeField] private float waitingTimer;
    [SerializeField] private float waitingInterval = 10f;
    private System.Random rand = new System.Random();

    private bool isResizingDialog;
    private Vector2 resizeStartMouse;
    private int resizeStartWidth;
    private int resizeStartHeight;

    private Vector3 screenPos;
    private Vector2 guiOffset = new Vector2(-450f, -200f);

    private GUIStyle TextAreaStyle;
    private GUIStyle roundedBoxStyle;

    private Texture2D cachedRoundedBg;
    private int cachedBgWidth;
    private int cachedBgHeight;

    private bool skinReady;
    private bool _ctrlDown;

    private void SetupSkin()
    {
        if (skinReady) return;
        fontSize = 30;

        roundedBoxStyle = new GUIStyle(GUI.skin.box);
        roundedBoxStyle.normal.background = CreateRoundedBg(600, 1600, 24, new Color(0.298f, 0.788f, 0.941f, 0.88f));

        skinReady = true;
    }

    private Texture2D CreateRoundedBg(int width, int height, int radius, Color color)
    {
        var tex = new Texture2D(width, height);
        var pixels = new Color[width * height];
        float r2 = radius * radius;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool inside = true;
                if (x < radius && y < radius && (x - radius) * (x - radius) + (y - radius) * (y - radius) > r2) inside = false;
                if (x >= width - radius && y < radius && (x - (width - radius - 1)) * (x - (width - radius - 1)) + (y - radius) * (y - radius) > r2) inside = false;
                if (x < radius && y >= height - radius && (x - radius) * (x - radius) + (y - (height - radius - 1)) * (y - (height - radius - 1)) > r2) inside = false;
                if (x >= width - radius && y >= height - radius && (x - (width - radius - 1)) * (x - (width - radius - 1)) + (y - (height - radius - 1)) * (y - (height - radius - 1)) > r2) inside = false;
                pixels[y * width + x] = inside ? color : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void SetExceptionRestore(bool value)
    {
        Debug.Log("捕捉到错误信息。");
        exceptionRestore = value;
        if (actionController != null)
            actionController.RestoreAnimator();
    }

    public void SetExceptionRestorePublic(bool value) { SetExceptionRestore(value); }

    private void ApplyIdleState(bool animated = true)
    {
        if (actionController == null) return;
        RestoreCamera();
        actionController.RestoreAnimator();
        if (animated)
            actionController.RestoreFacialExpression(SetRestoreEndToken);
        else
        {
            actionController.facialController.ResetBlendShapesInstant();
            actionController.mappingManager?.TryApplyFacial("待机");
        }
    }

    private void SetRestoreEndToken(bool value)
    {
        withExpression = value;
        onVoice = value;
        onRestore = value;
        if (actionController != null)
            actionController.RestoreAnimator();
        if (actionController != null && actionController.mappingManager != null)
            StartCoroutine(DelayedIdleApply(0.35f));
    }

    private IEnumerator DelayedIdleApply(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (actionController != null && actionController.mappingManager != null)
            actionController.mappingManager.TryApplyFacial("待机");
    }

    private void ZoomToHead()
    {
        if (isCameraZoomed) return;
        if (actionController?.animator == null) return;
        var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;
        if (cameraZoomRoutine != null) StopCoroutine(cameraZoomRoutine);
        cameraZoomRoutine = StartCoroutine(ZoomCoroutine(head));
    }

    private IEnumerator ZoomCoroutine(Transform head)
    {
        savedCameraPos = Camera.main.transform.position;
        savedCameraRot = Camera.main.transform.rotation;
        isCameraZoomed = true;
        Vector3 target = head.position - (Camera.main.transform.forward * 1.0f) + (Vector3.up * 0.05f);
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            Camera.main.transform.position = Vector3.Lerp(savedCameraPos, target, elapsed / 0.3f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Camera.main.transform.position = target;
    }

    private void RestoreCamera()
    {
        if (!isCameraZoomed) return;
        if (cameraZoomRoutine != null) StopCoroutine(cameraZoomRoutine);
        cameraZoomRoutine = StartCoroutine(RestoreCoroutine());
    }

    private IEnumerator RestoreCoroutine()
    {
        Vector3 from = Camera.main.transform.position;
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            Camera.main.transform.position = Vector3.Lerp(from, savedCameraPos, elapsed / 0.3f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Camera.main.transform.position = savedCameraPos;
        Camera.main.transform.rotation = savedCameraRot;
        isCameraZoomed = false;
    }

    void Start()
    {
        SettingsData settings = SettingsData.Load();
        if (settings != null)
        {
            if (settings.posX != 0f || settings.posY != 0f || settings.posZ != 0f)
            {
                targetTransform.position = new Vector3(settings.posX, settings.posY, settings.posZ);
                targetTransform.rotation = Quaternion.Euler(settings.rotX, settings.rotY, settings.rotZ);
            }
            if (!string.IsNullOrEmpty(settings.websocketUrl))
                websocket_url = settings.websocketUrl;
            if (settings.msgMaxWidth > 0)
                msg_max_length = settings.msgMaxWidth;
            if (settings.fontSize > 0)
                fontSize = settings.fontSize;
            config.ApplyFrom(settings);
            // Restore camera
            if (settings.camZ != 0f || settings.camX != 0f || settings.camY != 0f)
            {
                Camera.main.transform.position = new Vector3(settings.camX, settings.camY, settings.camZ);
                Camera.main.transform.rotation = new Quaternion(settings.camRotX, settings.camRotY, settings.camRotZ, settings.camRotW);
            }
        }

        int ttsMode = config.tts;
        config.initConfiguration(websocket_url, ttsMode, translator.Baidu_fanyi_url, translator.App_id, translator.Private_key, translator.Salt, llmFormatter.identity, llmFormatter.preset_information);
        TTS_module = config.getTTS(ttsMode);
        screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
        NetManager.M_Instance.Connect(websocket_url);
        actionController.animator.SetInteger("action_param", 2);
        withExpression = true;

        // Start pipe server for WPF settings communication
        if (pipeServer != null)
        {
            pipeServer.StartServer();
            Debug.Log("[GameStart] PipeServer started");
        }
        else
        {
            Debug.LogError("[GameStart] PipeServer is null - WPF settings will not work");
        }

        // Setup system tray
        if (trayManager != null)
        {
            trayManager.OnOpenPanel += OpenSettingsPanel;
            trayManager.OnToggleWindow += ToggleWindow;
            trayManager.OnExit += () =>
            {
#if !UNITY_EDITOR
                try { System.Diagnostics.Process.Start("taskkill", "/f /im AliceBotSettings.exe").WaitForExit(500); } catch { }
#endif
                Application.Quit();
            };
            Debug.Log("[GameStart] TrayManager events subscribed");
        }
        else
        {
            Debug.LogError("[GameStart] TrayManager is null - system tray will not work");
        }

        // Pre-scan animations so the WPF library is populated
        animLibrary?.ScanAll();
    }

    private void OpenSettingsPanel(int tabIndex)
    {
#if !UNITY_EDITOR
        string exePath = System.IO.Path.Combine(Application.streamingAssetsPath, "AliceBotSettings.exe");
        Debug.Log("[GameStart] Looking for settings exe: " + exePath + "  exists=" + System.IO.File.Exists(exePath));
        if (System.IO.File.Exists(exePath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = tabIndex.ToString(),
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                Debug.Log("[GameStart] Settings exe launched, tab=" + tabIndex);
                // Give WPF time to start and connect
                if (pipeServer != null)
                    StartCoroutine(DelayedRefreshPipe(1.5f));
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to launch settings: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[GameStart] Settings EXE not found at: " + exePath);
        }
#else
        Debug.Log("[GameStart] Open settings panel (editor), tab: " + tabIndex);
        if (pipeServer != null)
            pipeServer.RefreshInitData();
#endif
    }

    private IEnumerator DelayedRefreshPipe(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pipeServer != null)
            pipeServer.RefreshInitData();
        // Send status update
        if (pipeServer != null)
            pipeServer.SendStatus(NetManager.M_Instance.GetNetStatus());
    }

    private bool _windowVisible = true;

    private void ToggleWindow()
    {
#if !UNITY_EDITOR
        if (windowController != null)
        {
            _windowVisible = !_windowVisible;
            if (_windowVisible)
                windowController.ShowAppWindow();
            else
                windowController.HideAppWindow();
        }
#endif
    }

    public void ConnectWebsocket()
    {
        websocket_url = config.websocket_url;
        NetManager.M_Instance.Connect(websocket_url);
        if (pipeServer != null) pipeServer.SendStatus(NetManager.M_Instance.GetNetStatus());
    }

    public void DisconnectWebsocket()
    {
        NetManager.M_Instance.CloseClientWebSocket();
        if (pipeServer != null) pipeServer.SendStatus(false);
    }

    public void RefreshTtsModule()
    {
        TTS_module = config.getTTS(config.tts);
        if (TTS_module != null)
        {
            if (config.tts == 0)
                TTS_module.PostURL = config.gradio_url;
            else if (config.tts == 1)
                TTS_module.PostURL = config.simpleVitsApi_url;
        }
    }

    public void ZoomToHeadPublic()
    {
        ZoomToHead();
    }

    public void RestoreCharacterPublic()
    {
        RestoreCamera();
        actionController?.RestoreAnimator();
        actionController?.facialController?.ResetBlendShapesInstant();
    }

    public void PlayVoicePublic(AudioClip _clip, string _response)
    {
        PlayVoice(_clip, _response);
    }

    void Update()
    {
        bool ctrlDown = (GetAsyncKeyState(0x11) & 0x8000) != 0;

        if (ctrlDown)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 headPos = Camera.main.transform.position + Camera.main.transform.forward;
                if (actionController?.animator != null)
                {
                    var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) headPos = head.position;
                }
                Vector3 dir = (headPos - Camera.main.transform.position).normalized;
                Camera.main.transform.position += dir * scroll * 2f;
                float d = Vector3.Distance(Camera.main.transform.position, headPos);
                if (d < 0.7f) Camera.main.transform.position = headPos - dir * 0.7f;
                if (d > 3.0f) Camera.main.transform.position = headPos - dir * 3.0f;
            }

            if (Input.GetMouseButton(1))
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                Vector3 orbitCenter = targetTransform != null ? targetTransform.position : Camera.main.transform.position + Camera.main.transform.forward * 1f;
                if (actionController?.animator != null)
                {
                    var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) orbitCenter = head.position;
                }
                Camera.main.transform.RotateAround(orbitCenter, Vector3.up, mx * 3f);
                Camera.main.transform.RotateAround(orbitCenter, Camera.main.transform.right, -my * 3f);
            }
        }

        _ctrlDown = ctrlDown;

        if (onDialogue)
        {
            dialogueTimer += Time.deltaTime;
            if (dialogueTimer <= dialogueInterval)
            {
                int num = (int)Math.Round((float)msg_max_length * Time.deltaTime / dialogueInterval);
                msg_length_receive += num;
            }
            if (actionController.getWaitingStatus())
            {
                onDialogue = false;
                dialogueTimer = 0f;
                msg_length_receive = 0;
            }
        }

        if (withExpression)
        {
            facialRestoreTimer += Time.deltaTime;
            if (actionController.getIdleStatus() && !onRestore)
            {
                onRestore = true;
                actionController.RestoreFacialExpression(SetRestoreEndToken);
                facialRestoreTimer = 0f;
            }
            else if (facialRestoreTimer > facialRestoreTime)
            {
                if (((!m_AudioSource.isPlaying && onVoice) || exceptionRestore) && !onRestore)
                {
                    onRestore = true;
                    actionController.RestoreAnimator();
                    actionController.RestoreFacialExpression(SetRestoreEndToken);
                    facialRestoreTimer = 0f;
                    exceptionRestore = false;
                }
            }
            else
            {
                waitingTimer = 0f;
            }
        }
        else if (NetManager.M_Instance.response_queue.Count > 0 && !m_AudioSource.isPlaying && !onVoice && actionController.getIdleStatus())
        {
            reply = NetManager.M_Instance.response_queue.Dequeue();

            LLMFormatter.LLMResponseWrapper wrapper = null;
            try { wrapper = JsonUtility.FromJson<LLMFormatter.LLMResponseWrapper>(reply); } catch { }

            if (wrapper != null && wrapper.choices != null && wrapper.choices.Count > 0)
            {
                var choice = wrapper.choices[0];

                if (choice.delta != null)
                {
                    if (!string.IsNullOrEmpty(choice.delta.content))
                        streamBuffer += choice.delta.content;

                    if (!expressionApplied)
                    {
                        int searchStart = 0;
                        int thinkEndTag = streamBuffer.IndexOf("</think>");
                        if (thinkEndTag >= 0)
                            searchStart = thinkEndTag + "</think>".Length;

                        int tagStart = streamBuffer.IndexOf("【{'expression':", searchStart);
                        if (tagStart >= 0)
                        {
                            int tagEnd = streamBuffer.IndexOf("}】", tagStart);
                            if (tagEnd >= 0)
                            {
                                string exprText = streamBuffer.Substring(tagStart, tagEnd + 2 - tagStart);
                                actionController.SetFacialExpression(exprText);
                                actionController.AnimatorControl(exprText);
                                expressionApplied = true;
                                withExpression = true;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(choice.finish_reason))
                    {
                        if (choice.finish_reason == "abort" || choice.finish_reason == "overthink")
                        {
                            streamBuffer = "";
                            expressionApplied = false;
                            text_answer = "";
                            onDialogue = false;
                        }
                        else
                        {
                            ProcessResponse(streamBuffer, choice.finish_reason, choice.index);
                        }
                        streamBuffer = "";
                        expressionApplied = false;
                    }
                    else if (!string.IsNullOrEmpty(choice.delta.content))
                    {
                        int thinkEnd = streamBuffer.IndexOf("</think>");
                        if (thinkEnd >= 0)
                        {
                            string displayText = streamBuffer.Substring(thinkEnd + "</think>".Length);
                            if (!string.IsNullOrEmpty(displayText))
                            {
                                text_answer = "爱丽丝：" + LLMFormatter.RemoveEmotion(displayText);
                                onDialogue = true;
                            }
                        }
                    }
                    return;
                }
                else if (choice.message != null && !string.IsNullOrEmpty(choice.message.content))
                {
                    if (choice.finish_reason == "abort" || choice.finish_reason == "overthink")
                        return;
                    answer = choice.message.content;
                    finish_reason = choice.finish_reason;
                    ProcessResponse(answer, finish_reason, choice.index);
                }
            }
            else
            {
                LLMFormatter.LLMResponse lLMResponse = null;
                try { lLMResponse = JsonUtility.FromJson<LLMFormatter.LLMResponse>(reply); } catch { }
                if (lLMResponse != null && lLMResponse.message != null)
                {
                    answer = lLMResponse.message.content;
                    finish_reason = lLMResponse.finish_reason;
                    ProcessResponse(answer, finish_reason, lLMResponse.index);
                }
            }
        }
        else if (waitingTimer > waitingInterval && !isCameraZoomed)
        {
            int num3 = rand.Next(1, 4);
            actionController.animator.SetInteger("onWaiting", num3);
            switch (num3)
            {
                case 2:
                    withExpression = true;
                    actionController.facialController.PerformExpression("curious", null);
                    break;
                case 3:
                    withExpression = true;
                    actionController.facialController.PerformExpression("wink", null);
                    break;
            }
            waitingTimer = 0f;
            waitingInterval = rand.Next(30, 50);
        }
        else if (actionController.getIdleStatus())
        {
            waitingTimer += Time.deltaTime;
        }
    }

    private void ProcessResponse(string _answer, string _finish_reason, int _index)
    {
        if (string.IsNullOrEmpty(_answer) && _finish_reason == "function_call")
            return;
        if (_answer != null && _answer.Trim() == "[SILENCE]")
            return;

        onDialogue = true;
        finish_reason = _finish_reason;
        Debug.Log("想法：；回答：" + _answer + "；终止原因：" + finish_reason);

        string answerPure = _answer;
        if (_answer != null && _answer.Contains("</think>"))
        {
            int idx = _answer.IndexOf("</think>");
            answerPure = _answer.Substring(idx + "</think>".Length).TrimStart();
            if (string.IsNullOrEmpty(answerPure))
                answerPure = _answer;
        }

        if (_index == 1)
        {
            var toSave = new LLMFormatter.LLMResponse
            {
                index = _index,
                message = new LLMFormatter.MessageData { content = _answer },
                finish_reason = _finish_reason,
            };
            llmFormatter.SaveResponse(toSave);
        }
        string text = LLMFormatter.RemoveAction(LLMFormatter.RemoveEmotion(answerPure));
        text_answer = "爱丽丝：" + text;
        if (string.IsNullOrEmpty(text))
        {
            text_answer += "唔...";
        }
        Debug.Log(text_answer);
        waitingTimer = 0f;
        withExpression = true;
        actionController.SetFacialExpression(answerPure);
        actionController.AnimatorControl(answerPure);
        translator.translate(text, "jp", GenerateVoice, SetExceptionRestore);
        llmFormatter.pending = false;
    }

    void OnGUI()
    {
        SetupSkin();
        GUI.skin.textArea.fontSize = fontSize;
        GUI.skin.textArea.normal.textColor = Color.white;
        GUI.skin.textArea.normal.background = null;
        GUI.skin.textArea.hover.background = null;
        GUI.skin.textArea.focused.background = null;
        GUI.skin.textArea.active.background = null;
        GUI.skin.textArea.padding = new RectOffset(8, 8, 6, 6);

        // Dialogue bubble rendering
        if (targetTransform != null && onDialogue)
        {
            TextAreaStyle = new GUIStyle(GUI.skin.textArea);
            TextAreaStyle.fontSize = fontSize;
            TextAreaStyle.wordWrap = true;
            TextAreaStyle.normal.textColor = Color.white;
            TextAreaStyle.normal.background = null;
            TextAreaStyle.hover.background = null;
            TextAreaStyle.focused.background = null;
            TextAreaStyle.active.background = null;
            TextAreaStyle.padding = new RectOffset(8, 8, 6, 6);

            float height = TextAreaStyle.CalcHeight(new GUIContent(text_answer), msg_length_receive - 20);

            // Clamp: keep dialogue within screen bounds
            float bubbleX = Mathf.Clamp(screenPos.x + guiOffset.x - (float)(msg_length_receive / 2) + (float)(msg_max_length / 2),
                -msg_length_receive + 80f, Screen.width - 80f);
            float bubbleY = Mathf.Clamp(Screen.height - msg_height - 160f,
                0f, Screen.height - msg_height);

            Rect position = new Rect(bubbleX, bubbleY, msg_length_receive, msg_height);
            Rect rect = new Rect(bubbleX - 10f, (float)Screen.height - screenPos.y + guiOffset.y - 10f,
                msg_length_receive - 20, height);

            Rect boxRect = new Rect(position.x - 12f, position.y - 12f, position.width + 24f, position.height + 24f);
            int bw = (int)boxRect.width, bh = (int)boxRect.height;
            if (cachedRoundedBg == null || cachedBgWidth != bw || cachedBgHeight != bh)
            {
                if (cachedRoundedBg != null) Destroy(cachedRoundedBg);
                cachedRoundedBg = CreateRoundedBg(bw, bh, 28, new Color(0.298f, 0.788f, 0.941f, 0.88f));
                cachedBgWidth = bw; cachedBgHeight = bh;
                roundedBoxStyle.normal.background = cachedRoundedBg;
            }
            GUI.Box(boxRect, "", roundedBoxStyle);

            scrollPosition = GUI.BeginScrollView(position, scrollPosition, rect);
            GUI.TextArea(rect, text_answer, TextAreaStyle);
            GUI.EndScrollView();

            if (isScrolling)
            {
                scrollPosition.y += scrollSpeed * Time.deltaTime;
                if (scrollPosition.y >= rect.height - position.height)
                {
                    isScrolling = false;
                    pauseTimer = pauseDuration;
                }
            }
            else
            {
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    scrollPosition.y = 0f;
                    isScrolling = true;
                }
            }
        }

        // Ctrl interaction: resize grip + font size
        if (_ctrlDown && targetTransform != null)
        {
            float gripSize = 24f;
            float bubbleX = Mathf.Clamp(screenPos.x + guiOffset.x - (float)(msg_max_length / 2) + (float)(msg_max_length / 2),
                -msg_max_length + 80f, Screen.width - 80f);
            float bubbleY = Mathf.Clamp(Screen.height - msg_height - 160f,
                0f, Screen.height - msg_height);

            float dialogRight = bubbleX + msg_max_length;
            float dialogBottom = bubbleY + msg_height;
            Rect gripRect = new Rect(dialogRight - gripSize, dialogBottom - gripSize, gripSize, gripSize);

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Box(gripRect, "");
            GUI.Label(gripRect, "╋");
            GUI.color = prevColor;

            Event e = Event.current;
            if (e.type == EventType.MouseDown && gripRect.Contains(e.mousePosition))
            {
                isResizingDialog = true;
                resizeStartMouse = e.mousePosition;
                resizeStartWidth = msg_max_length;
                resizeStartHeight = msg_height;
                e.Use();
            }
            if (e.type == EventType.MouseDrag && isResizingDialog)
            {
                Vector2 delta = e.mousePosition - resizeStartMouse;
                msg_max_length = Mathf.Clamp(resizeStartWidth + (int)delta.x, 200, 1400);
                msg_height = Mathf.Clamp(resizeStartHeight + (int)delta.y, 60, 2000);
                e.Use();
            }
            if (e.type == EventType.MouseUp && isResizingDialog)
            {
                isResizingDialog = false;
                SaveSettings();
                e.Use();
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                fontSize = Mathf.Clamp(fontSize + (int)(scroll * 4f), 10, 60);
            }
        }
    }

    void OnApplicationQuit()
    {
        SaveSettings();
        if (NetManager.M_Instance.GetNetStatus())
        {
            Debug.Log("向服务器请求断开连接......");
            NetManager.M_Instance.CloseClientWebSocket();
        }
    }

    public void SaveSettings()
    {
        SettingsData settings = new SettingsData();
        if (targetTransform != null)
        {
            settings.posX = targetTransform.position.x;
            settings.posY = targetTransform.position.y;
            settings.posZ = targetTransform.position.z;
            Vector3 euler = targetTransform.rotation.eulerAngles;
            settings.rotX = euler.x;
            settings.rotY = euler.y;
            settings.rotZ = euler.z;
        }
        settings.websocketUrl = websocket_url;
        settings.msgMaxWidth = msg_max_length;
        settings.msgHeight = msg_height;
        settings.fontSize = fontSize;
        int wx, wy;
        windowController.GetWindowPosition(out wx, out wy);
        settings.winX = wx;
        settings.winY = wy;
        settings.winWidth = windowController.ResWidth;
        settings.winHeight = windowController.ResHeight;
        var cam = Camera.main.transform;
        settings.camX = cam.position.x;
        settings.camY = cam.position.y;
        settings.camZ = cam.position.z;
        settings.camRotX = cam.rotation.x;
        settings.camRotY = cam.rotation.y;
        settings.camRotZ = cam.rotation.z;
        settings.camRotW = cam.rotation.w;
        config.PopulateTo(settings);
        settings.Save();
    }

    private void GenerateVoice(string _text)
    {
        text_answer = text_answer + "\n" + _text;
        if (TTS_module != null)
        {
            Debug.Log("发送语音合成请求......");
            TTS_module.Speak(_text, PlayVoice, SetExceptionRestore);
        }
        else
        {
            Debug.Log("未配置语音模块");
            SetExceptionRestore(value: true);
        }
    }

    private void PlayVoice(AudioClip _clip, string _response)
    {
        m_AudioSource.clip = _clip;
        m_AudioSource.Play();
        onVoice = true;
        Debug.Log("音频时长：" + _clip.length);
    }
}
