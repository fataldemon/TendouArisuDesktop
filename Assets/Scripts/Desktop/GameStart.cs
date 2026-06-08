// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// GameStart
using System;
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

	[SerializeField]
	private bool onVoice;

	[SerializeField]
	private AudioSource m_AudioSource;

	[SerializeField]
	public int msg_position_x = 300;

	[SerializeField]
	public int msg_position_y = 150;

	[SerializeField]
	private int msg_max_length = 700;

	private int msg_length_receive;

	private int msg_length_send;

	[SerializeField]
	public int msg_height = 150;

	public Configuration config;

	public TransparentWindow windowController;

	public ActionController actionController;

	public LLMFormatter llmFormatter;

	public TTS TTS_module;

	public BaiduTranslator translator;

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

	private bool onBottom = true;

	private string tips_message = "请在对话框中输入想和爱丽丝说的话，点击对话按钮开始聊天。";

	private static string origin_tips_message = "请在对话框中输入想和爱丽丝说的话，点击对话按钮开始聊天。";

	private int config_page;

	private string[] config_page_list = new string[2] { "连接设置", "对话设置" };

	private int tts_page;

	private string[] tts_list = new string[3] { "Gradio", "Simple-Vits-API", "None" };

	private string voice_test_line = "";

	[SerializeField]
	private Vector3 screenPos;

	private Vector2 guiOffset = new Vector2(-350f, -200f);

	private GUIStyle TextAreaStyle;

	private GUIStyle TextFieldStyle;

	private GUIStyle TextLabelStyle;

	private GUIStyle InformationLabelStyle;

	private GUIStyle windowStyle;

	private Rect configWindowRect;

	private Rect historyWindowRect;

	private Rect testWindowRect;

	[SerializeField]
	private int fontSize = 20;

	private Vector2 scrollPosition = Vector2.zero;

	private Vector2 scrollPosition2 = Vector2.zero;

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
		}

		config.initConfiguration(websocket_url, tts_page, translator.Baidu_fanyi_url, translator.App_id, translator.Private_key, translator.Salt, llmFormatter.identity, llmFormatter.preset_information);
		TTS_module = config.getTTS(tts_page);
		screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
		configWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, (float)Screen.height - screenPos.y + guiOffset.y - (float)msg_height - 150f, msg_max_length + 100, msg_height + 300);
		historyWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, (float)Screen.height - screenPos.y + guiOffset.y - (float)msg_height - 150f, msg_max_length + 100, msg_height + 300);
		testWindowRect = new Rect(screenPos.x + guiOffset.x - 100f, (float)Screen.height - screenPos.y + guiOffset.y - (float)msg_height - 50f, msg_max_length + 100, 100f);
		NetManager.M_Instance.Connect(websocket_url);
		actionController.animator.SetInteger("action_param", 2);
	}

	private void Update()
	{
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
		if (choice.delta != null && !string.IsNullOrEmpty(choice.delta.content))
		{
			streamBuffer += choice.delta.content;

			// think 结束之前不显示任何内容，结束后只显示 answer
			int thinkEnd = streamBuffer.IndexOf("</think>");
			if (thinkEnd >= 0)
			{
				string displayText = streamBuffer.Substring(thinkEnd + "</think>".Length);
				if (!string.IsNullOrEmpty(displayText))
				{
					text_answer = "爱丽丝：\n" + LLMFormatter.RemoveEmotion(displayText);
					onDialogue = true;
				}
			}

				// finish_reason 非空 → 流结束
				if (!string.IsNullOrEmpty(choice.finish_reason))
				{
					if (choice.finish_reason == "abort" || choice.finish_reason == "overthink")
					{
						streamBuffer = "";
						text_answer = "";
						onDialogue = false;
					}
					else
					{
						answer = streamBuffer;
						finish_reason = choice.finish_reason;
						streamBuffer = "";
						ProcessResponse(answer, finish_reason, choice.index);
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
		text_answer = "爱丽丝：\n" + text;
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
		int wx, wy;
		windowController.GetWindowPosition(out wx, out wy);
		settings.winX = wx;
		settings.winY = wy;
		config.PopulateTo(settings);
		settings.Save();
	}

	private void OnGUI()
	{
		if (targetTransform != null && onDialogue)
		{
			screenPos = Camera.main.WorldToScreenPoint(targetTransform.position);
			TextAreaStyle = GUI.skin.textArea;
			TextAreaStyle.fontSize = fontSize;
			TextAreaStyle.wordWrap = true;
			TextAreaStyle.normal.textColor = Color.white;
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
		TextFieldStyle = new GUIStyle(GUI.skin.textField);
		TextFieldStyle.fontSize = fontSize;
		TextFieldStyle.wordWrap = true;
		TextFieldStyle.normal.textColor = Color.white;
		Rect position2 = new Rect(vector.x + guiOffset.x - (float)(msg_length_send / 2) + (float)(msg_max_length / 2), (float)Screen.height - vector.y + guiOffset.y - (float)msg_height - 10f, 0.82f * (float)msg_length_send, msg_height);
		msg = GUI.TextField(position2, msg, TextFieldStyle);
		if (!(messageTimer > dialogueInterval))
		{
			return;
		}
		if (string.IsNullOrEmpty(msg))
		{
			if (tips_message == origin_tips_message)
			{
				TextLabelStyle = new GUIStyle(GUI.skin.label);
				TextLabelStyle.fontSize = fontSize;
				TextLabelStyle.wordWrap = true;
				TextLabelStyle.normal.textColor = Color.grey;
				TextLabelStyle.hover.textColor = Color.grey;
				TextLabelStyle.alignment = TextAnchor.UpperLeft;
			}
			else
			{
				TextLabelStyle = new GUIStyle(GUI.skin.label);
				TextLabelStyle.fontSize = fontSize;
				TextLabelStyle.wordWrap = true;
				TextLabelStyle.normal.textColor = Color.grey;
				TextLabelStyle.hover.textColor = Color.white;
				TextLabelStyle.alignment = TextAnchor.UpperLeft;
			}
			GUI.Label(position2, tips_message, TextLabelStyle);
		}
		if (!NetManager.M_Instance.GetNetStatus() || llmFormatter.pending)
		{
			GUI.enabled = false;
		}
		if (GUI.Button(new Rect(vector.x + guiOffset.x + (float)msg_max_length - 110f, (float)Screen.height - vector.y + guiOffset.y - (float)msg_height, 90f, 40f), new GUIContent(chatButton)) && msg != null && !llmFormatter.pending)
		{
			llmFormatter.pending = true;
			string content = llmFormatter.LLMFormatterForWebsocket("gpt-3.5-turbo", 0.93f, 0.7f, 1.116f, stream: false, msg);
			NetManager.M_Instance.Send(content);
			tips_message = "（" + llmFormatter.identity + "对爱丽丝说）" + msg;
			msg = null;
		}
		GUI.enabled = true;
		if (GUI.Button(new Rect(vector.x + guiOffset.x + (float)msg_max_length - 110f, (float)Screen.height - vector.y + guiOffset.y - (float)msg_height + 50f, 90f, 40f), new GUIContent(historyButton)))
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
		if (GUI.Button(new Rect(vector.x + guiOffset.x + (float)msg_max_length - 110f, (float)Screen.height - vector.y + guiOffset.y - (float)msg_height + 100f, 90f, 40f), new GUIContent(configButton)))
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
		if (GUI.Button(new Rect(vector.x + guiOffset.x + (float)msg_max_length - 110f, (float)Screen.height - vector.y + guiOffset.y - (float)msg_height - 60f, 90f, 40f), new GUIContent(closeButton)))
		{
			windowController.EnableWindowPenetration();
		}
		windowStyle = GUI.skin.window;
		Texture2D texture2D = new Texture2D(1, 1);
		texture2D.SetPixel(0, 0, Color.black);
		texture2D.Apply();
		windowStyle.normal.background = texture2D;
		windowStyle.focused.background = texture2D;
		windowStyle.active.background = texture2D;
		windowStyle.hover.background = texture2D;
		windowStyle.onNormal.background = texture2D;
		windowStyle.onHover.background = texture2D;
		windowStyle.onActive.background = texture2D;
		windowStyle.onFocused.background = texture2D;
		if (onHistory)
		{
			historyWindowRect = GUI.Window(0, historyWindowRect, historyFunc, "Dialogue History", windowStyle);
		}
		if (onConfig)
		{
			InformationLabelStyle = new GUIStyle(GUI.skin.label);
			configWindowRect = GUI.Window(1, configWindowRect, configurationFunc, "Configuration", windowStyle);
		}
		if (onTestVoice)
		{
			config.setTTSUrl(config.tts);
			testWindowRect = GUI.Window(2, testWindowRect, TestVoiceFunc, "Test Voice Module", windowStyle);
		}
	}

	public void configurationFunc(int window_id)
	{
		config_page = GUI.Toolbar(new Rect(20f, 30f, 750f, 30f), config_page, config_page_list);
		if (config_page == 0)
		{
			GUI.Label(new Rect(20f, 70f, 200f, 30f), "Websocket连接地址：");
			config.websocket_url = GUI.TextField(new Rect(220f, 70f, 550f, 30f), config.websocket_url);
			if (NetManager.M_Instance.GetNetStatus())
			{
				InformationLabelStyle.normal.textColor = Color.cyan;
				InformationLabelStyle.hover.textColor = Color.cyan;
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
				InformationLabelStyle.normal.textColor = Color.red;
				InformationLabelStyle.hover.textColor = Color.red;
				GUI.Label(new Rect(40f, 110f, 140f, 30f), "<未连接>", InformationLabelStyle);
				GUI.enabled = false;
			}
			if (GUI.Button(new Rect(340f, 110f, 90f, 30f), "断开连接"))
			{
				Debug.Log("向服务器请求断开连接......");
				NetManager.M_Instance.CloseClientWebSocket();
			}
			GUI.enabled = true;
			config.tts = GUI.Toolbar(new Rect(20f, 150f, 750f, 30f), config.tts, tts_list);
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
			if (GUI.Button(new Rect(560f, 400f, 90f, 30f), yesButton))
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
			if (GUI.Button(new Rect(680f, 400f, 90f, 30f), closeButton))
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
			if (GUI.Button(new Rect(560f, 400f, 90f, 30f), yesButton))
			{
				llmFormatter.identity = config.identity;
				llmFormatter.preset_information = config.preset;
				SaveSettings();
				onConfig = false;
			}
			if (GUI.Button(new Rect(680f, 400f, 90f, 30f), closeButton))
			{
				onConfig = false;
			}
		}
		GUI.DragWindow();
	}

	public void historyFunc(int window_id)
	{
		GUI.Label(new Rect(20f, 30f, 200f, 30f), "对话记录：", TextLabelStyle);
		GUIStyle gUIStyle = new GUIStyle(GUI.skin.textArea);
		float num = gUIStyle.CalcHeight(new GUIContent(llmFormatter.formatted_history), 750f);
		if (num <= 310f)
		{
			num = 310f;
		}
		if (onBottom)
		{
			onBottom = false;
			scrollPosition2.y = num - 310f;
		}
		scrollPosition2 = GUI.BeginScrollView(new Rect(20f, 70f, 750f, 310f), scrollPosition2, new Rect(10f, 60f, 730f, num));
		GUI.TextArea(new Rect(10f, 60f, 730f, num), llmFormatter.formatted_history, gUIStyle);
		GUI.EndScrollView();
		if (GUI.Button(new Rect(580f, 390f, 90f, 30f), deleteButton))
		{
			llmFormatter.history.Clear();
			llmFormatter.formatted_history = string.Empty;
		}
		if (GUI.Button(new Rect(680f, 390f, 90f, 30f), closeButton))
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
