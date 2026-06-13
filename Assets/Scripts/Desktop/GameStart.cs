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

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private struct POINT
    {
        public int X;
        public int Y;
    }

    private string msg;
    private string reply;
    private string thought;
    private string answer;
    private string text_answer;
    private string finish_reason;
    private string streamBuffer = "";

    [SerializeField] private string websocket_url;
    [SerializeField] private bool onVoice;
    [SerializeField] private AudioSource m_AudioSource;
    [SerializeField] public int msg_position_x = 300;
    [SerializeField] public int msg_position_y = 150;
    [SerializeField] public int msg_max_length = 580;
    public int msg_length_receive;
    private int msg_length_send;
    [SerializeField] public int msg_height = 1600;

    public Configuration config;
    public TransparentWindow windowController;
    public EmotionPlayer emotionPlayer;
    public PreviewController previewController;
    public LLMFormatter llmFormatter;
    public TTS TTS_module;
    public BaiduTranslator translator;
    public ModelManager modelManager;
    public AnimationLibrary animLibrary;
    public PipeServer pipeServer;
    public SystemTrayManager trayManager;

    public float dialogueInterval = 0.5f;
    private float dialogueTimer;
    private float dialogueClearTimer;
    private float _dialogueHoldDuration = 10f;
    private float _audioLength;
    private bool expressionApplied;

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

    private Vector3 screenPos;
    private Vector2 guiOffset = new Vector2(-450f, -200f);

    private GUIStyle TextAreaStyle;
    private GUIStyle roundedBoxStyle;

    private Texture2D cachedRoundedBg;
    private int cachedBgWidth;
    private int cachedBgHeight;

    private bool skinReady;
    private bool _ctrlDown;

    private bool _isTouching;
    private bool _isDragging;
    private const float TouchScreenRadius = 150f;
    private int _touchLogFrame;
    private ActionGroupConfig _touchConfig;
    private bool _isOverGrip;
    private Rect _gripRect;
    public bool IsOverGrip => _isOverGrip;
    private bool _gripDragActive;
    public bool GripDragActive => _gripDragActive;
    private bool _gripDragTracking;
    private Vector2 _gripDragStartMouse;
    private Vector2 _gripDragStartOffset;

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

    public void SetExceptionRestorePublic(bool value)
    {
        Debug.Log("捕捉到错误信息。");
        if (value && emotionPlayer != null)
        {
            emotionPlayer.NotifyTTSError();
        }
    }

    public void ZoomToHeadPublic()
    {
        ZoomToHead();
    }

    public void RestoreCharacterPublic()
    {
        RestoreCamera();
        if (emotionPlayer != null)
            emotionPlayer.ForceIdle();
    }

    public void PlayVoicePublic(AudioClip _clip, string _response)
    {
        PlayVoice(_clip, _response);
    }

    private void ZoomToHead()
    {
        if (isCameraZoomed) return;
        if (emotionPlayer == null || emotionPlayer.bodyEngine == null || emotionPlayer.bodyEngine.animator == null) return;
        var head = emotionPlayer.bodyEngine.animator.GetBoneTransform(HumanBodyBones.Head);
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
        cameraZoomRoutine = StartCoroutine(RestoreCameraCoroutine());
    }

    private IEnumerator RestoreCameraCoroutine()
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

    private Animator GetAnimator()
    {
        return emotionPlayer != null && emotionPlayer.bodyEngine != null ? emotionPlayer.bodyEngine.animator : null;
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
            if (settings.camZ != 0f || settings.camX != 0f || settings.camY != 0f)
            {
                Camera.main.transform.position = new Vector3(settings.camX, settings.camY, settings.camZ);
                Camera.main.transform.rotation = new Quaternion(settings.camRotX, settings.camRotY, settings.camRotZ, settings.camRotW);
            }
            if (settings.guiOffsetX != 0f || settings.guiOffsetY != 0f)
                guiOffset = new Vector2(settings.guiOffsetX, settings.guiOffsetY);
            if (settings.dialogMinHoldTime > 0f)
                _dialogueHoldDuration = settings.dialogMinHoldTime;
        }

        int ttsMode = config.tts;
        config.initConfiguration(websocket_url, ttsMode, translator.Baidu_fanyi_url, translator.App_id, translator.Private_key, translator.Salt, llmFormatter.identity, llmFormatter.preset_information);
        TTS_module = config.getTTS(ttsMode);
        screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
        NetManager.M_Instance.Connect(websocket_url);

        if (pipeServer != null)
        {
            pipeServer.StartServer();
            Debug.Log("[GameStart] PipeServer started");
        }

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
        }

        if (windowController != null)
        {
            windowController.gameStart = this;
            windowController.OnDragStart += () =>
            {
                if (!emotionPlayer.IsPlaying && !_isTouching)
                {
                    emotionPlayer.PlayEmotion("拖拽");
                    _isDragging = true;
                }
            };
            windowController.OnDragEnd += () =>
            {
                if (_isDragging)
                {
                    emotionPlayer.RestoreToIdle();
                    _isDragging = false;
                }
            };
        }

        animLibrary?.ScanAll();
    }

    private void OpenSettingsPanel(int tabIndex)
    {
#if !UNITY_EDITOR
        string exePath = System.IO.Path.Combine(Application.streamingAssetsPath, "AliceBotSettings.exe");
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
                if (pipeServer != null)
                    StartCoroutine(DelayedRefreshPipe(1.5f));
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to launch settings: " + e.Message);
            }
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
        {
            pipeServer.RefreshInitData();
            pipeServer.SendStatus(NetManager.M_Instance.GetNetStatus());
        }
    }

    private bool _windowVisible = true;

    public float DialogMinHoldTime => _dialogueHoldDuration;

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

    void Update()
    {
        bool ctrlDown = (GetAsyncKeyState(0x11) & 0x8000) != 0;

        if (ctrlDown)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var anim = GetAnimator();
                Vector3 headPos = Camera.main.transform.position + Camera.main.transform.forward;
                if (anim != null)
                {
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
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
                var anim = GetAnimator();
                Vector3 orbitCenter = targetTransform != null ? targetTransform.position : Camera.main.transform.position + Camera.main.transform.forward * 1f;
                if (anim != null)
                {
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) orbitCenter = head.position;
                }
                Camera.main.transform.RotateAround(orbitCenter, Vector3.up, mx * 3f);
                Camera.main.transform.RotateAround(orbitCenter, Camera.main.transform.right, -my * 3f);
            }
        }

        _ctrlDown = ctrlDown;

        bool shiftDown = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        if (shiftDown && Input.GetMouseButton(0))
        {
            float panSpeed = 0.006f;
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            Camera.main.transform.Translate(-mx * panSpeed, -my * panSpeed, 0, Space.Self);
        }

        // Grip resize logic (Input-driven, before OnGUI for TransparentWindow LateUpdate)
        {
            bool leftDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            _gripDragActive = false;
            _isOverGrip = false;
            GetCursorPos(out POINT cursor);
            Vector2 cursorScreen = new Vector2(cursor.X, cursor.Y);
            Vector2 cursorWindow = new Vector2(
                cursor.X - windowController.currentX,
                cursor.Y - windowController.currentY);
            if (targetTransform != null)
            {
                screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
                float gx = Mathf.Clamp(screenPos.x + guiOffset.x, -msg_max_length + 80f, Screen.width - 80f);
                float gy = Mathf.Clamp(Screen.height - msg_height - 160f + guiOffset.y, 0f, Screen.height - msg_height);
                float gripSize = 36f;
                _gripRect = new Rect(gx + msg_max_length / 2f - gripSize / 2f,
                    gy + msg_height / 2f - gripSize / 2f, gripSize, gripSize);
                _isOverGrip = _ctrlDown && _gripRect.Contains(cursorWindow);
            }

            if (_ctrlDown && leftDown && _isOverGrip && !_gripDragTracking)
            {
                _gripDragTracking = true;
                _gripDragStartMouse = cursorWindow;
                _gripDragStartOffset = guiOffset;
            }

            if (_gripDragTracking)
            {
                if (leftDown && _ctrlDown)
                {
                    _gripDragActive = true;
                    guiOffset = _gripDragStartOffset + (cursorWindow - _gripDragStartMouse);
                }
                else
                {
                    _gripDragTracking = false;
                    SaveSettings();
                }
            }

            if (_ctrlDown)
            {
                Debug.Log($"[GRIP] f={Time.frameCount} ctrl=T left={leftDown} over={_isOverGrip} track={_gripDragTracking} active={_gripDragActive} rect=({_gripRect.x:F0},{_gripRect.y:F0} {_gripRect.width:F0}x{_gripRect.height:F0}) cursorScr=({cursor.X},{cursor.Y}) cursorWin=({cursorWindow.x:F0},{cursorWindow.y:F0}) winXY=({windowController.currentX},{windowController.currentY}) target={(targetTransform != null)}");
            }
        }

        if (onDialogue)
        {
            dialogueTimer += Time.deltaTime;
            if (dialogueTimer <= dialogueInterval)
            {
                int num = (int)Math.Round((float)msg_max_length * Time.deltaTime / dialogueInterval);
                msg_length_receive += num;
            }
            if (m_AudioSource.isPlaying)
            {
                dialogueClearTimer = 0f;
            }
            else
            {
                dialogueClearTimer += Time.deltaTime;
                if (dialogueClearTimer > _dialogueHoldDuration)
                {
                    Debug.Log("[Dialog] Clearing after " + dialogueClearTimer.ToString("F1") + "s (hold=" + _dialogueHoldDuration.ToString("F1") + ")");
                    onDialogue = false;
                    dialogueTimer = 0f;
                    msg_length_receive = 0;
                    dialogueClearTimer = 0f;
                }
            }
        }

        if (onVoice && !m_AudioSource.isPlaying)
        {
            onVoice = false;
            emotionPlayer.NotifyTTSEnd();
        }

        if (!emotionPlayer.IsPlaying && !m_AudioSource.isPlaying && !onVoice
            && NetManager.M_Instance.response_queue.TryDequeue(out reply))
        {

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
                                string emotion = EmotionParser.Extract(exprText);
                                if (!string.IsNullOrEmpty(emotion))
                                    emotionPlayer.PlayEmotion(emotion);
                                expressionApplied = true;
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
        else if (!emotionPlayer.IsPlaying && waitingTimer > waitingInterval && !isCameraZoomed)
        {
            var randomEvents = ActionSystemRuntime.EmotionMappings.Where(m => m.isRandomEvent).ToList();
            if (randomEvents.Count > 0)
            {
                int idx = rand.Next(randomEvents.Count);
                emotionPlayer.PlayEmotion(randomEvents[idx].emotion);
            }
            waitingTimer = 0f;
            waitingInterval = rand.Next(30, 50);
        }
        else if (!emotionPlayer.IsPlaying)
        {
            waitingTimer += Time.deltaTime;
        }

        // Touch detection
        {
            bool leftDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool touching = false;
            var anim = GetAnimator();

            if (leftDown && anim != null && !_ctrlDown && !shiftDown)
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    Vector3 headScreen = Camera.main.WorldToScreenPoint(head.position);
                    float dist = Vector2.Distance(new Vector2(headScreen.x, headScreen.y),
                        new Vector2(Input.mousePosition.x, Input.mousePosition.y));
                    touching = dist < TouchScreenRadius;
                }
            }

            if (touching && !_isTouching)
            {
                if (!emotionPlayer.IsPlaying && !_isDragging)
                {
                    if (_touchConfig != null && !_touchConfig.loop)
                        emotionPlayer.RestoreToIdle();
                    emotionPlayer.PlayEmotion("触摸");
                    _touchConfig = emotionPlayer.CurrentConfig;
                    _isTouching = true;
                }
            }
            else if (!touching && _isTouching)
            {
                if (_touchConfig == null || _touchConfig.loop)
                    emotionPlayer.RestoreToIdle();
                _isTouching = false;
                _touchConfig = null;
            }
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
        Debug.Log("回答：" + _answer + "；终止原因：" + finish_reason);

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

        string text = EmotionParser.RemoveActionTag(EmotionParser.RemoveEmotionTag(answerPure));
        text_answer = "爱丽丝：" + text;
        if (string.IsNullOrEmpty(text))
            text_answer += "唔...";

        Debug.Log(text_answer);
        waitingTimer = 0f;

        _dialogueHoldDuration = 10f;
        if (emotionPlayer.CurrentConfig != null)
        {
            float clipLen = emotionPlayer.bodyEngine != null ? emotionPlayer.bodyEngine.GetCurrentClipLength("fullBody") : 0f;
            if (emotionPlayer.CurrentConfig.loop)
                _dialogueHoldDuration = 10f;
            else
                _dialogueHoldDuration = Mathf.Max(10f, clipLen + 3f);
        }

        string emotion = EmotionParser.Extract(answerPure);
        if (!string.IsNullOrEmpty(emotion))
            emotionPlayer.PlayEmotion(emotion);

        translator.translate(text, "jp", GenerateVoice, SetExceptionRestorePublic);
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

        float gripBubbleX = 0f, gripBubbleY = 0f;
        if (targetTransform != null)
        {
            gripBubbleX = Mathf.Clamp(screenPos.x + guiOffset.x, -msg_max_length + 80f, Screen.width - 80f);
            gripBubbleY = Mathf.Clamp(Screen.height - msg_height - 160f + guiOffset.y, 0f, Screen.height - msg_height);
        }

        if (_ctrlDown && targetTransform != null)
        {
            float gripSize = 36f;
            Rect gripRect = new Rect(gripBubbleX + msg_max_length / 2f - gripSize / 2f,
                gripBubbleY + msg_height / 2f - gripSize / 2f, gripSize, gripSize);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                fontSize = Mathf.Clamp(fontSize + (int)(scroll * 4f), 10, 60);
        }

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

            float bubbleX = Mathf.Clamp(screenPos.x + guiOffset.x - (float)(msg_length_receive / 2) + (float)(msg_max_length / 2),
                -msg_length_receive + 80f, Screen.width - 80f);
            float bubbleY = Mathf.Clamp(Screen.height - msg_height - 160f + guiOffset.y,
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

        if (_ctrlDown && targetTransform != null)
        {
            float gripSize = 36f;
            Rect gripRect = new Rect(gripBubbleX + msg_max_length / 2f - gripSize / 2f,
                gripBubbleY + msg_height / 2f - gripSize / 2f, gripSize, gripSize);

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Box(gripRect, "");
            GUI.Label(gripRect, "╋");
            GUI.color = prevColor;
            Debug.Log($"[GUI-GRIP] f={Time.frameCount} rect=({gripRect.x:F0},{gripRect.y:F0} {gripRect.width:F0}x{gripRect.height:F0}) screenWH=({Screen.width},{Screen.height}) bubbleXY=({gripBubbleX:F0},{gripBubbleY:F0}) guiOffset=({guiOffset.x:F0},{guiOffset.y:F0}) screenPos=({screenPos.x:F0},{screenPos.y:F0})");
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
        settings.guiOffsetX = guiOffset.x;
        settings.guiOffsetY = guiOffset.y;
        settings.dialogMinHoldTime = _dialogueHoldDuration > 0 ? _dialogueHoldDuration : 10f;
        config.PopulateTo(settings);
        settings.Save();
    }

    private void GenerateVoice(string _text)
    {
        text_answer = text_answer + "\n" + _text;
        if (TTS_module != null)
        {
            Debug.Log("发送语音合成请求......");
            emotionPlayer.NotifyTTSStart();
            TTS_module.Speak(_text, PlayVoice, SetExceptionRestorePublic);
        }
        else
        {
            Debug.Log("未配置语音模块");
            emotionPlayer.NotifyTTSError();
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
