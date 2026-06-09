// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// GameStart
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameStart : MonoBehaviour
{
	private string msg;

	private string reply;

	private string thought;

	private string answer;

	private string text_answer;

	private string finish_reason;
	// 流式输出累积缓冲区
	private string streamBuffer = "";

	[SerializeField]
	private string websocket_url;

	[SerializeField]
	private bool withExpression;

	[SerializeField]
	private bool onRestore;

	[SerializeField]
	public float facialRestoreTime = 3f;

	[SerializeField]
	private float facialRestoreTimer;

	private bool exceptionRestore;

	private bool expressionApplied;

	[SerializeField]
	private bool onVoice;

	[SerializeField]
	private AudioSource m_AudioSource;

	[SerializeField]
	public int msg_position_x = 300;

	[SerializeField]
	public int msg_position_y = 150;

	[SerializeField]
	private int msg_max_length = 580;

	private int msg_length_receive;

	private int msg_length_send;

	[SerializeField]
	public int msg_height = 800;

	public Configuration config;

	public TransparentWindow windowController;

	public ActionController actionController;

	public LLMFormatter llmFormatter;

	public TTS TTS_module;

	public BaiduTranslator translator;

	public ModelManager modelManager;

	public AnimationLibrary animLibrary;

	public ExpressionMappingManager mappingManager;

	public float dialogueInterval = 0.5f;

	private float dialogueTimer;

	private float messageTimer;

	public Texture2D chatButton;

	public Texture2D configButton;

	public Texture2D historyButton;

	public Texture2D yesButton;

	public Texture2D closeButton;

	public Texture2D deleteButton;

	public Transform targetTransform;

	[SerializeField]
	private bool onDialogue;

	[SerializeField]
	private bool onConfig;

	[SerializeField]
	private bool onHistory;

	[SerializeField]
	private bool onTestVoice;

	private bool onModel;

	private bool onAnim;

	private bool onExpr;

	private bool onBottom = true;

	private int config_page;

	private string[] config_page_list = new string[2] { "连接设置", "对话设置" };

	private int tts_page;

	private string[] tts_list = new string[3] { "Gradio", "Simple-Vits-API", "None" };

	private string voice_test_line = "";

	[SerializeField]
	private Vector3 screenPos;

	private Vector2 guiOffset = new Vector2(-450f, -200f);

	private GUIStyle TextAreaStyle;

	private GUIStyle TextFieldStyle;

	private GUIStyle TextLabelStyle;

	private GUIStyle InformationLabelStyle;

	private GUIStyle windowStyle;
	private GUIStyle buttonStyle;
	private GUIStyle selButtonStyle;
	private GUIStyle labelStyle;
	private GUIStyle toolbarStyle;

	private bool skinReady;

	private void SetupSkin()
	{
		if (skinReady) return;
		fontSize = 30;

		// 窗口背景：深海军蓝
		var winBg = new Texture2D(1, 1);
		winBg.SetPixel(0, 0, new Color(0.039f, 0.086f, 0.157f, 0.95f));
		winBg.Apply();

		windowStyle = new GUIStyle(GUI.skin.window);
		windowStyle.normal.background = winBg;
		windowStyle.focused.background = winBg;
		windowStyle.active.background = winBg;
		windowStyle.hover.background = winBg;
		windowStyle.onNormal.background = winBg;
		windowStyle.onHover.background = winBg;
		windowStyle.onActive.background = winBg;
		windowStyle.onFocused.background = winBg;
		windowStyle.normal.textColor = new Color(0.557f, 0.808f, 0.902f);
		windowStyle.fontSize = 16;
		windowStyle.border = new RectOffset(4, 4, 4, 4);

		// 按钮：天蓝色底 + 白字
		buttonStyle = new GUIStyle(GUI.skin.button);
		buttonStyle.normal.textColor = Color.white;
		buttonStyle.fontSize = 16;

		// 标签：淡蓝灰
		labelStyle = new GUIStyle(GUI.skin.label);
		labelStyle.normal.textColor = new Color(0.8f, 0.9f, 1f, 1f);
		labelStyle.fontSize = 16;

		// Toolbar
		toolbarStyle = new GUIStyle(GUI.skin.button);
		toolbarStyle.normal.textColor = Color.white;
		toolbarStyle.fontSize = 16;
		toolbarStyle.fixedHeight = 30;

		TextLabelStyle = new GUIStyle(GUI.skin.label);
		TextLabelStyle.fontSize = 18;
		TextLabelStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
		TextLabelStyle.alignment = TextAnchor.UpperLeft;

		selButtonStyle = new GUIStyle(GUI.skin.button);
		selButtonStyle.normal.textColor = Color.white;
		selButtonStyle.fontSize = 16;
		var selBg = new Texture2D(1, 1);
		selBg.SetPixel(0, 0, new Color(0.298f, 0.788f, 0.941f, 1f));
		selBg.Apply();
		selButtonStyle.normal.background = selBg;
		selButtonStyle.hover.background = selBg;
		selButtonStyle.active.background = selBg;
		selButtonStyle.focused.background = selBg;

		skinReady = true;
	}


	private Rect configWindowRect;

	private Rect historyWindowRect;

	private Rect testWindowRect;

	private Rect modelWindowRect;

	private Rect animWindowRect;

	private Rect exprWindowRect;
	
	private string vrmPath = "";
	private string animSearch = "";
	private string animCatFilter = "All";

	private bool onExprEdit;
	private Rect exprEditWindowRect;
	private ExpressionMappingData exprEditTarget;

	[SerializeField]
	private int fontSize = 40;

	private Vector2 scrollPosition = Vector2.zero;

	private Vector2 scrollPosition2 = Vector2.zero;

	private Vector2 animScrollPosition = Vector2.zero;

	private Vector2 exprScrollPosition = Vector2.zero;

	private float scrollSpeed = 5f;

	private float pauseDuration = 1f;

	private bool isScrolling = true;

	private float pauseTimer;

	[SerializeField]
	private float waitingTimer;

	[SerializeField]
	private float waitingInterval = 10f;

	private System.Random rand = new System.Random();

	private bool isResizingDialog;
	private Vector2 resizeStartMouse;
	private int resizeStartWidth;
	private int resizeStartHeight;

	private void SetExceptionRestore(bool value)
	{
		Debug.Log("捕捉到错误信息。");
		exceptionRestore = value;
	}

	private void SetRestoreEndToken(bool value)
	{
		withExpression = value;
		onVoice = value;
		onRestore = value;
	}

	private void Start()
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
			if (settings.ttsMode >= 0)
				tts_page = settings.ttsMode;
			if (settings.msgMaxWidth > 0)
				msg_max_length = settings.msgMaxWidth;
			if (settings.msgHeight > 0)
				msg_height = settings.msgHeight;
			if (settings.fontSize > 0)
				fontSize = settings.fontSize;
			config.ApplyFrom(settings);
			if (modelManager != null && modelManager.currentModel != null)
				modelManager.currentModel.transform.localScale = new Vector3(settings.scaleX, settings.scaleY, settings.scaleZ);
		}

		config.initConfiguration(websocket_url, tts_page, translator.Baidu_fanyi_url, translator.App_id, translator.Private_key, translator.Salt, llmFormatter.identity, llmFormatter.preset_information);
		TTS_module = config.getTTS(tts_page);
		screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
		configWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, Screen.height * 0.05f, msg_max_length + 100, Screen.height * 0.78f);
		historyWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, Screen.height * 0.05f, msg_max_length + 100, Screen.height * 0.78f);
		testWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, Screen.height * 0.05f, msg_max_length + 100, 150f);
		modelWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, Screen.height * 0.05f, msg_max_length + 100, Screen.height * 0.78f);
		animWindowRect = new Rect(screenPos.x + guiOffset.x - 250f, Screen.height * 0.05f, msg_max_length + 100, Screen.height * 0.78f);
		exprWindowRect = new Rect(screenPos.x + guiOffset.x - 250f, Screen.height * 0.05f, msg_max_length + 100, Screen.height * 0.78f);
		exprEditWindowRect = new Rect(Screen.width * 0.5f - 375f, Screen.height * 0.05f, 750f, Screen.height * 0.75f);
		NetManager.M_Instance.Connect(websocket_url);
		actionController.animator.SetInteger("action_param", 2);
	}

	private void Update()
	{
		if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
		    && modelManager != null && modelManager.currentModel != null)
		{
			float scroll = Input.GetAxis("Mouse ScrollWheel");
			if (Mathf.Abs(scroll) > 0.01f)
				modelManager.ScaleModel(scroll * 0.1f);
		}

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
		if (windowController.configToken)
		{
			messageTimer += Time.deltaTime;
			if (messageTimer <= dialogueInterval)
			{
				int num2 = (int)Math.Round((float)msg_max_length * Time.deltaTime / dialogueInterval);
				msg_length_send += num2;
			}
		}
		else
		{
			messageTimer = 0f;
			msg_length_send = 0;
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

			// 尝试用新格式的 ChatCompletionResponse 包装类解析
			LLMFormatter.LLMResponseWrapper wrapper = null;
			try { wrapper = JsonUtility.FromJson<LLMFormatter.LLMResponseWrapper>(reply); } catch { }

			if (wrapper != null && wrapper.choices != null && wrapper.choices.Count > 0)
			{
				var choice = wrapper.choices[0];

		// 流式 chunk（有 delta）
		if (choice.delta != null)
		{
			if (!string.IsNullOrEmpty(choice.delta.content))
				streamBuffer += choice.delta.content;

			// 实时检测表情标签，仅在 </think> 之后识别
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

			// finish_reason 到达 → 流结束，无论 content 是否为空都要处理
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
				// think 结束之前不显示任何内容，结束后只显示 answer
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
			// 非流式新格式（有 message 无 delta）
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
				// 旧格式：ChatCompletionResponseChoice 直接解析
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
		else if (waitingTimer > waitingInterval)
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
		// 跳过中间的工具调用回复（MCP 中间轮次，没有实际内容）
		if (string.IsNullOrEmpty(_answer) && _finish_reason == "function_call")
			return;
		// 跳过沉默回复
		if (_answer != null && _answer.Trim() == "[SILENCE]")
			return;

		onDialogue = true;
		finish_reason = _finish_reason;
		Debug.Log("想法：；回答：" + _answer + "；终止原因：" + finish_reason);

		// 剥离 <think>...</think>，只保留纯回答用于表情/动作/翻译
		string answerPure = _answer;
		if (_answer != null && _answer.Contains("</think>"))
		{
			int idx = _answer.IndexOf("</think>");
			answerPure = _answer.Substring(idx + "</think>".Length).TrimStart();
			if (string.IsNullOrEmpty(answerPure))
				answerPure = _answer;
		}

		// 保存到对话历史（仅WebSocket直连回复，index=1）
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

	private void OnApplicationQuit()
	{
		SaveSettings();
		if (NetManager.M_Instance.GetNetStatus())
		{
			Debug.Log("向服务器请求断开连接......");
			NetManager.M_Instance.CloseClientWebSocket();
		}
	}

	private void SaveSettings()
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
		settings.ttsMode = tts_page;
		settings.msgMaxWidth = msg_max_length;
		settings.msgHeight = msg_height;
		settings.fontSize = fontSize;
		if (modelManager != null && modelManager.currentModel != null)
		{
			var s = modelManager.currentModel.transform.localScale;
			settings.scaleX = s.x; settings.scaleY = s.y; settings.scaleZ = s.z;
		}
		int wx, wy;
		windowController.GetWindowPosition(out wx, out wy);
		settings.winX = wx;
		settings.winY = wy;
		config.PopulateTo(settings);
		settings.Save();
	}

	private void OnGUI()
	{
		SetupSkin();
		GUI.skin.button.fontSize = 16;
		GUI.skin.button.normal.textColor = Color.white;
		GUI.skin.label.fontSize = 16;
		GUI.skin.label.normal.textColor = new Color(0.8f, 0.9f, 1f);
		GUI.skin.textArea.fontSize = fontSize;
		GUI.skin.textArea.normal.textColor = Color.white;
		GUI.skin.textField.fontSize = 18;
		GUI.skin.textField.normal.textColor = Color.white;
		GUI.skin.toggle.fontSize = 16;

		if (targetTransform != null && onDialogue)
		{
			screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
			TextAreaStyle = new GUIStyle(GUI.skin.textArea);
			TextAreaStyle.fontSize = fontSize;
			TextAreaStyle.wordWrap = true;
			TextAreaStyle.normal.textColor = Color.white;
			var taBg = new Texture2D(1, 1);
			taBg.SetPixel(0, 0, new Color(0.024f, 0.059f, 0.122f, 0.85f));
			taBg.Apply();
			TextAreaStyle.normal.background = taBg;
			TextAreaStyle.padding = new RectOffset(8, 8, 6, 6);
			float height = TextAreaStyle.CalcHeight(new GUIContent(text_answer), msg_length_receive - 20);
			Rect position = new Rect(screenPos.x + guiOffset.x - (float)(msg_length_receive / 2) + (float)(msg_max_length / 2), (float)Screen.height - screenPos.y + guiOffset.y, msg_length_receive, msg_height);
			Rect rect = new Rect(screenPos.x + guiOffset.x - (float)(msg_length_receive / 2) + (float)(msg_max_length / 2) - 10f, (float)Screen.height - screenPos.y + guiOffset.y - 10f, msg_length_receive - 20, height);
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

		if (windowController.configToken && targetTransform != null)
		{
			screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
			float gripSize = 24f;
			float dialogRight = screenPos.x + guiOffset.x + msg_max_length;
			float dialogBottom = (float)Screen.height - screenPos.y + guiOffset.y + msg_height;
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
				msg_height = Mathf.Clamp(resizeStartHeight + (int)delta.y, 60, 600);
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

		if (!windowController.configToken)
		{
			return;
		}
		Vector3 vector = Camera.main.WorldToScreenPoint(targetTransform.position);
		float btnX = vector.x + guiOffset.x + (float)msg_max_length + 20f;
		float btnBaseY = Screen.height - 10f;
		if (GUI.Button(new Rect(btnX, btnBaseY - 40f, 90f, 40f), "表情", buttonStyle))
		{
			onExpr = !onExpr;
		}
		if (GUI.Button(new Rect(btnX, btnBaseY - 90f, 90f, 40f), "动画", buttonStyle))
		{
			if (onAnim && animLibrary != null) animLibrary.StopPreview();
			else if (animLibrary != null) animLibrary.ScanAll();
			onAnim = !onAnim;
		}
		if (GUI.Button(new Rect(btnX, btnBaseY - 140f, 90f, 40f), "模型", buttonStyle))
		{
			onModel = !onModel;
		}
		if (GUI.Button(new Rect(btnX, btnBaseY - 190f, 90f, 40f), "配置", buttonStyle))
		{
			if (onConfig)
			{
				onConfig = false;
			}
			else
			{
				config.initConfiguration(websocket_url, tts_page, translator.Baidu_fanyi_url, translator.App_id, translator.Private_key, translator.Salt, llmFormatter.identity, llmFormatter.preset_information);
				onConfig = true;
			}
		}
		if (GUI.Button(new Rect(btnX, btnBaseY - 240f, 90f, 40f), "历史", buttonStyle))
		{
			if (onHistory)
			{
				onHistory = false;
			}
			else
			{
				onBottom = true;
				onHistory = true;
			}
		}
		if (GUI.Button(new Rect(btnX, btnBaseY - 290f, 90f, 40f), new GUIContent(closeButton), buttonStyle))
		{
			windowController.EnableWindowPenetration();
		}
		if (onModel)
		{
			modelWindowRect = GUI.Window(3, modelWindowRect, modelFunc, "模型管理", windowStyle);
		}
		if (onAnim && animLibrary != null)
		{
			animWindowRect = GUI.Window(4, animWindowRect, animFunc, "动画库", windowStyle);
		}
		if (onExpr && mappingManager != null)
		{
			exprWindowRect = GUI.Window(5, exprWindowRect, exprFunc, "表情映射", windowStyle);
		}
		if (onExprEdit && mappingManager != null)
		{
			exprEditWindowRect = GUI.Window(6, exprEditWindowRect, exprEditFunc, "编辑映射", windowStyle);
		}
		if (onHistory)
		{
			historyWindowRect = GUI.Window(0, historyWindowRect, historyFunc, "对话记录", windowStyle);
		}
		if (onConfig)
		{
			InformationLabelStyle = new GUIStyle(GUI.skin.label);
			InformationLabelStyle.fontSize = 16;
			configWindowRect = GUI.Window(1, configWindowRect, configurationFunc, "配置", windowStyle);
		}
		if (onTestVoice)
		{
			config.setTTSUrl(config.tts);
			testWindowRect = GUI.Window(2, testWindowRect, TestVoiceFunc, "Test Voice Module", windowStyle);
		}
	}

	public void configurationFunc(int window_id)
	{
		config_page = GUI.Toolbar(new Rect(20f, 30f, 750f, 30f), config_page, config_page_list, toolbarStyle);
		if (config_page == 0)
		{
			GUI.Label(new Rect(20f, 70f, 200f, 30f), "Websocket连接地址：");
			config.websocket_url = GUI.TextField(new Rect(220f, 70f, 550f, 30f), config.websocket_url);
			if (NetManager.M_Instance.GetNetStatus())
			{
				InformationLabelStyle.normal.textColor = new Color(0.298f, 0.788f, 0.941f);
				InformationLabelStyle.hover.textColor = new Color(0.298f, 0.788f, 0.941f);
				GUI.Label(new Rect(40f, 110f, 140f, 30f), "<连接中>", InformationLabelStyle);
				GUI.enabled = false;
			}
			if (GUI.Button(new Rect(220f, 110f, 90f, 30f), "发起连接"))
			{
				websocket_url = config.websocket_url;
				NetManager.M_Instance.Connect(websocket_url);
			}
			GUI.enabled = true;
			if (!NetManager.M_Instance.GetNetStatus())
			{
				InformationLabelStyle.normal.textColor = new Color(0.976f, 0.384f, 0.49f);
				InformationLabelStyle.hover.textColor = new Color(0.976f, 0.384f, 0.49f);
				GUI.Label(new Rect(40f, 110f, 140f, 30f), "<未连接>", InformationLabelStyle);
				GUI.enabled = false;
			}
			if (GUI.Button(new Rect(340f, 110f, 90f, 30f), "断开连接"))
			{
				Debug.Log("向服务器请求断开连接......");
				NetManager.M_Instance.CloseClientWebSocket();
			}
			GUI.enabled = true;
			config.tts = GUI.Toolbar(new Rect(20f, 150f, 750f, 30f), config.tts, tts_list, toolbarStyle);
			GUI.Label(new Rect(20f, 190f, 200f, 30f), "语音模块API地址：");
			if (config.tts == 0)
			{
				config.gradio_url = GUI.TextField(new Rect(220f, 190f, 450f, 30f), config.gradio_url);
				if (GUI.Button(new Rect(680f, 190f, 90f, 30f), "测试"))
				{
					onTestVoice = true;
				}
			}
			else if (config.tts == 1)
			{
				config.simpleVitsApi_url = GUI.TextField(new Rect(220f, 190f, 450f, 30f), config.simpleVitsApi_url);
				if (GUI.Button(new Rect(680f, 190f, 90f, 30f), "测试"))
				{
					onTestVoice = true;
				}
			}
			GUI.Label(new Rect(20f, 240f, 200f, 30f), "翻译模块API地址：");
			config.translation_url = GUI.TextField(new Rect(220f, 240f, 550f, 30f), config.translation_url);
			GUI.Label(new Rect(20f, 280f, 200f, 30f), "翻译模块APPID：");
			config.translation_app_id = GUI.TextField(new Rect(220f, 280f, 550f, 30f), config.translation_app_id);
			GUI.Label(new Rect(20f, 320f, 200f, 30f), "翻译模块秘钥：");
			config.translation_key = GUI.PasswordField(new Rect(220f, 320f, 550f, 30f), config.translation_key, '*');
			GUI.Label(new Rect(20f, 360f, 200f, 30f), "翻译模块Salt：");
			config.translation_salt = GUI.TextField(new Rect(220f, 360f, 550f, 30f), config.translation_salt);
			if (GUI.Button(new Rect(560f, 400f, 90f, 30f), "确定"))
			{
				websocket_url = config.websocket_url;
				tts_page = config.tts;
				TTS_module = config.getTTS(tts_page);
				if (tts_page == 0)
				{
					TTS_module.PostURL = config.gradio_url;
				}
				else if (tts_page == 1)
				{
					TTS_module.PostURL = config.simpleVitsApi_url;
				}
				translator.Baidu_fanyi_url = config.translation_url;
				translator.App_id = config.translation_app_id;
				translator.Private_key = config.translation_key;
				translator.Salt = config.translation_salt;
				SaveSettings();
				onConfig = false;
			}
			if (GUI.Button(new Rect(680f, 400f, 90f, 30f), "取消"))
			{
				onConfig = false;
			}
		}
		else if (config_page == 1)
		{
			GUI.Label(new Rect(20f, 70f, 200f, 30f), "你的身份：");
			config.identity = GUI.TextField(new Rect(120f, 70f, 650f, 30f), config.identity);
			GUI.Label(new Rect(20f, 110f, 200f, 30f), "预设信息：");
			config.preset = GUI.TextField(new Rect(120f, 110f, 650f, 240f), config.preset);
			if (GUI.Button(new Rect(560f, 400f, 90f, 30f), "确定"))
			{
				llmFormatter.identity = config.identity;
				llmFormatter.preset_information = config.preset;
				SaveSettings();
				onConfig = false;
			}
			if (GUI.Button(new Rect(680f, 400f, 90f, 30f), "取消"))
			{
				onConfig = false;
			}
		}
		GUI.DragWindow();
	}

	public void animFunc(int window_id)
	{
		var cats = animLibrary != null ? animLibrary.GetCategories() : new List<string>();

		float y = 30f;

		if (GUI.Button(new Rect(20f, y, 80f, 30f), "刷新"))
		{
			if (animLibrary != null) animLibrary.ScanAll();
		}
		if (GUI.Button(new Rect(105f, y, 80f, 30f), "导入"))
		{
			string path = FileBrowser.OpenFileDialog("Select FBX Animation", "FBX Files|*.fbx");
			if (!string.IsNullOrEmpty(path) && animLibrary != null)
				animLibrary.ImportAnimation(path);
		}
		GUI.Label(new Rect(195f, y, 80f, 30f), "搜索:", labelStyle);
		animSearch = GUI.TextField(new Rect(265f, y, 190f, 30f), animSearch);
		y += 40f;

		if (cats.Count > 0)
		{
			float cx = 20f;
			for (int i = 0; i < cats.Count; i++)
			{
				bool sel = cats[i] == animCatFilter;
				if (GUI.Button(new Rect(cx, y, 80f, 25f), cats[i], sel ? toolbarStyle : buttonStyle))
					animCatFilter = cats[i];
				cx += 84f;
			}
		}

		y += 35f;

		if (animLibrary != null)
			animLibrary.allowRootMotion = GUI.Toggle(new Rect(20f, y, 180f, 22f), animLibrary.allowRootMotion, " Allow Root Motion");
		y += 28f;

		string cat = animCatFilter;
		var list = animLibrary != null ? animLibrary.Filter(cat, animSearch) : new List<AnimationClipData>();
		float svH = animWindowRect.height - y - 50f;
		animScrollPosition = GUI.BeginScrollView(new Rect(20f, y, animWindowRect.width - 40f, svH), animScrollPosition,
			new Rect(0, 0, animWindowRect.width - 60f, list.Count * 32f));
		float iy = 0;
		foreach (var clip in list)
		{
			GUI.Label(new Rect(0, iy, 300f, 28f), clip.name + "  [" + clip.category + "]  " + clip.duration.ToString("F1") + "s", labelStyle);
			if (GUI.Button(new Rect(animWindowRect.width - 140f, iy, 80f, 30f), "预览"))
			{
				if (animLibrary != null) animLibrary.Preview(clip);
			}
			iy += 32f;
		}
		GUI.EndScrollView();

		if (GUI.Button(new Rect(animWindowRect.width - 110f, animWindowRect.height - 40f, 90f, 30f), "关闭"))
		{
			if (animLibrary != null) animLibrary.StopPreview();
			onAnim = false;
		}
		GUI.DragWindow();
	}

	public void exprFunc(int window_id)
	{
		if (mappingManager == null) return;
		var mappings = mappingManager.GetAll();
		float y = 30f;

		if (GUI.Button(new Rect(20f, y, 120f, 30f), "恢复默认"))
			mappingManager.RestoreDefaults();
		if (GUI.Button(new Rect(145f, y, 100f, 30f), "添加映射"))
		{
			exprEditTarget = new ExpressionMappingData();
			onExprEdit = true;
		}
		y += 38f;

		float svH = exprWindowRect.height - y - 50f;
		var sv = GUI.BeginScrollView(new Rect(20f, y, exprWindowRect.width - 40f, svH), exprScrollPosition,
			new Rect(0, 0, exprWindowRect.width - 60f, mappings.Count * 28f));
		exprScrollPosition = sv;
		float iy = 0;
		foreach (var m in mappings.ToList())
		{
			GUI.Label(new Rect(0, iy, 100f, 24f), m.emotion, labelStyle);
			string facDesc = m.facialGroups.Count > 0 ? m.facialGroups[0].preset : "-";
			string actDesc = m.actionGroups.Count > 0 ? m.actionGroups[0].animationName : "-";
			GUI.Label(new Rect(105f, iy, 90f, 24f), facDesc, labelStyle);
			GUI.Label(new Rect(200f, iy, 60f, 24f), actDesc, labelStyle);
			if (GUI.Button(new Rect(exprWindowRect.width - 170f, iy, 60f, 24f), "编辑"))
			{
				exprEditTarget = m;
				onExprEdit = true;
			}
			if (GUI.Button(new Rect(exprWindowRect.width - 105f, iy, 60f, 24f), "删除"))
				mappingManager.RemoveMapping(m.emotion);
			iy += 28f;
		}
		GUI.EndScrollView();

		if (GUI.Button(new Rect(exprWindowRect.width - 110f, exprWindowRect.height - 40f, 90f, 30f), "关闭"))
		{
			if (actionController != null) actionController.facialController.ResetBlendShapesInstant();
			onExpr = false;
		}
		GUI.DragWindow();
	}

	public void exprEditFunc(int window_id)
	{
		if (exprEditTarget == null) exprEditTarget = new ExpressionMappingData();
		float y = 30f;
		float w = exprEditWindowRect.width - 40f;

		// Emotion
		GUI.Label(new Rect(20f, y, 60f, 24f), "情绪:", labelStyle);
		exprEditTarget.emotion = GUI.TextField(new Rect(80f, y, 120f, 24f), exprEditTarget.emotion);
		if (GUI.Button(new Rect(210f, y, 80f, 24f), "预览全部"))
			PreviewExpressionMapping(exprEditTarget);
		y += 34f;

		// Facial section
		GUI.Label(new Rect(20f, y, 80f, 24f), "面部表情:", labelStyle);
		if (GUI.Button(new Rect(110f, y, 80f, 24f), "预览面部"))
			PreviewFacialGroups(exprEditTarget.facialGroups);
		y += 30f;

		for (int i = 0; i < exprEditTarget.facialGroups.Count; i++)
		{
			var fg = exprEditTarget.facialGroups[i];
			int sel = System.Array.IndexOf(FacialPresets.All, fg.preset);
			if (sel < 0) sel = 0;

			float gx = 20f; int cols = 5; float bw = 130f;
			for (int k = 0; k < FacialPresets.All.Length; k++)
			{
				bool active = k == sel;
				if (GUI.Button(new Rect(gx, y, bw - 4f, 24f), FacialPresets.All[k], active ? selButtonStyle : buttonStyle))
				{ sel = k; fg.preset = FacialPresets.All[k]; }
				gx += bw;
				if ((k + 1) % cols == 0) { gx = 20f; y += 28f; }
			}
			if (FacialPresets.All.Length % cols != 0) y += 28f;

			fg.weight = GUI.HorizontalSlider(new Rect(20f, y, 160f, 20f), fg.weight, 0f, 1f);
			GUI.Label(new Rect(185f, y, 50f, 20f), fg.weight.ToString("F1"), labelStyle);
			if (GUI.Button(new Rect(240f, y, 40f, 20f), "删除"))
			{ exprEditTarget.facialGroups.RemoveAt(i); break; }
			y += 26f;
		}
		if (GUI.Button(new Rect(20f, y, 80f, 24f), "+添加"))
			exprEditTarget.facialGroups.Add(new FacialGroup { preset = "happy", weight = 1f });
		y += 34f;

		// Action section
		GUI.Label(new Rect(20f, y, 80f, 24f), "动作映射:", labelStyle);
		y += 30f;

		float actSvH = exprEditWindowRect.height - y - 100f;
		var actSv = GUI.BeginScrollView(new Rect(20f, y, w, Mathf.Max(actSvH, 120f)), Vector2.zero,
			new Rect(0, 0, w - 20f, exprEditTarget.actionGroups.Count * 90f));
		float ay = 0;
		for (int i = 0; i < exprEditTarget.actionGroups.Count; i++)
		{
			var ag = exprEditTarget.actionGroups[i];

			ag.animationName = GUI.TextField(new Rect(0, ay, 140f, 24f), ag.animationName);
			if (GUI.Button(new Rect(145f, ay, 50f, 24f), "预览"))
			{
				if (animLibrary != null && !string.IsNullOrEmpty(ag.animationName))
					PreviewActionClip(ag);
			}
			ay += 32f;

			int bpSel = System.Array.IndexOf(BodyParts.All, ag.bodyPart);
			if (bpSel < 0) bpSel = 0;
			float gx2 = 0f; int cols2 = 5; float bw2 = (w - 20f) / cols2;
			for (int k = 0; k < BodyParts.All.Length; k++)
			{
				bool active = k == bpSel;
				string label = BodyParts.All[k].Length > 9 ? BodyParts.All[k].Substring(0, 9) : BodyParts.All[k];
				if (GUI.Button(new Rect(gx2, ay, bw2 - 4f, 22f), label, active ? selButtonStyle : buttonStyle))
				{ bpSel = k; ag.bodyPart = BodyParts.All[k]; }
				gx2 += bw2;
				if ((k + 1) % cols2 == 0) { gx2 = 0f; ay += 26f; }
			}
			if (BodyParts.All.Length % cols2 != 0) ay += 26f;

			ag.weight = GUI.HorizontalSlider(new Rect(0, ay, 160f, 20f), ag.weight, 0f, 1f);
			GUI.Label(new Rect(165f, ay, 50f, 20f), ag.weight.ToString("F1"), labelStyle);
			if (GUI.Button(new Rect(220f, ay, 40f, 20f), "删除"))
			{ exprEditTarget.actionGroups.RemoveAt(i); break; }
			ay += 30f;
		}
		GUI.EndScrollView();

		float btnY = exprEditWindowRect.height - 40f;
		if (GUI.Button(new Rect(20f, btnY, 70f, 30f), "保存"))
		{
			mappingManager.AddOrUpdate(exprEditTarget.emotion, exprEditTarget.facialGroups, exprEditTarget.actionGroups);
			if (actionController != null) actionController.facialController.ResetBlendShapesInstant();
			onExprEdit = false;
		}
		if (GUI.Button(new Rect(100f, btnY, 70f, 30f), "取消"))
		{
			if (actionController != null) actionController.facialController.ResetBlendShapesInstant();
			onExprEdit = false;
		}
		GUI.DragWindow();
	}

	private void PreviewFacialGroups(List<FacialGroup> groups)
	{
		if (actionController?.facialController == null) return;
		actionController.facialController.ResetBlendShapesInstant();
		foreach (var fg in groups)
			if (!string.IsNullOrEmpty(fg.preset))
				actionController.facialController.PreviewBlendShape(fg.preset, fg.weight);
	}

	private void PreviewActionClip(ActionGroup ag)
	{
		if (animLibrary == null || string.IsNullOrEmpty(ag.animationName)) return;
		var clip = animLibrary.registry.FirstOrDefault(r => r.name == ag.animationName);
		if (clip != null) { animLibrary.Preview(clip); return; }
		if (int.TryParse(ag.animationName, out int ap) && actionController?.animator != null)
		{
			actionController.animator.SetInteger("action_param", ap);
			StartCoroutine(AutoRestoreAnim(3f));
		}
	}

	private void PreviewExpressionMapping(ExpressionMappingData data)
	{
		if (data == null) return;
		PreviewFacialGroups(data.facialGroups);
		if (data.actionGroups.Count > 0)
			PreviewActionClip(data.actionGroups[0]);
	}

	private System.Collections.IEnumerator AutoRestoreAnim(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (actionController?.animator != null)
		{
			actionController.animator.SetInteger("action_param", 0);
			actionController.animator.SetInteger("onWaiting", 0);
		}
	}

	public void modelFunc(int window_id)
	{
		GUI.Label(new Rect(20f, 30f, 200f, 30f), "VRM Model Path:", labelStyle);
		vrmPath = GUI.TextField(new Rect(20f, 60f, 460f, 30f), vrmPath);

		if (GUI.Button(new Rect(490f, 60f, 80f, 30f), "浏览"))
		{
			string path = FileBrowser.OpenFileDialog("Select VRM Model", "VRM Files|*.vrm");
			if (!string.IsNullOrEmpty(path))
				vrmPath = path;
		}
		if (GUI.Button(new Rect(580f, 60f, 90f, 30f), "加载"))
		{
			if (modelManager != null && !string.IsNullOrEmpty(vrmPath))
				modelManager.LoadModel(vrmPath);
		}
		if (GUI.Button(new Rect(680f, 60f, 90f, 30f), "恢复"))
		{
			if (modelManager != null)
				modelManager.RestoreDefault();
		}

		var history = modelManager != null ? modelManager.GetHistory() : null;
		if (history != null && history.Count > 0)
		{
			GUI.Label(new Rect(20f, 110f, 200f, 30f), "History:", labelStyle);
			float y = 140f;
			for (int i = 0; i < Mathf.Min(history.Count, 15); i++)
			{
				int idx = i;
				if (GUI.Button(new Rect(20f, y, 600f, 30f), history[i]))
				{
					modelManager.LoadFromHistory(idx);
				}
				y += 35f;
			}
		}
		if (GUI.Button(new Rect(modelWindowRect.width - 110f, modelWindowRect.height - 40f, 90f, 30f), "关闭"))
		{
			onModel = false;
		}
		GUI.DragWindow();
	}

	public void historyFunc(int window_id)
	{
		GUI.Label(new Rect(20f, 30f, 200f, 30f), "对话记录：", TextLabelStyle);
		GUIStyle gUIStyle = new GUIStyle(GUI.skin.textArea);
		float num = gUIStyle.CalcHeight(new GUIContent(llmFormatter.formatted_history), 750f);
		float scrollViewHeight = historyWindowRect.height - 120f;
		if (num <= scrollViewHeight)
		{
			num = scrollViewHeight;
		}
		if (onBottom)
		{
			onBottom = false;
			scrollPosition2.y = num - scrollViewHeight;
		}
		scrollPosition2 = GUI.BeginScrollView(new Rect(20f, 70f, 750f, scrollViewHeight), scrollPosition2, new Rect(10f, 60f, 730f, num));
		GUI.TextArea(new Rect(10f, 60f, 730f, num), llmFormatter.formatted_history, gUIStyle);
		GUI.EndScrollView();
		if (GUI.Button(new Rect(580f, historyWindowRect.height - 50f, 90f, 30f), "清空"))
		{
			llmFormatter.history.Clear();
			llmFormatter.formatted_history = string.Empty;
		}
		if (GUI.Button(new Rect(680f, historyWindowRect.height - 50f, 90f, 30f), "关闭"))
		{
			onHistory = false;
		}
		GUI.DragWindow();
	}

	private void TestVoiceFunc(int window_id)
	{
		voice_test_line = GUI.TextField(new Rect(20f, 30f, 550f, 30f), voice_test_line);
		if (GUI.Button(new Rect(580f, 30f, 90f, 30f), "发送测试") && voice_test_line != "")
		{
			if (TTS_module != null)
			{
				Debug.Log("发送语音合成请求......");
				config.getTTS(config.tts).Speak(voice_test_line, PlayVoice, SetExceptionRestore);
			}
			else
			{
				Debug.Log("未配置语音模块");
			}
		}
		if (GUI.Button(new Rect(680f, 30f, 90f, 30f), closeButton))
		{
			onTestVoice = false;
		}
		GUI.DragWindow();
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
