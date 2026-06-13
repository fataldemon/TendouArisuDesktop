// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// LLMFormatter
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

public class LLMFormatter : MonoBehaviour
{
	[Serializable]
	public class Property
	{
		public string type;

		public string description;
	}

	[Serializable]
	public class Parameter
	{
		public string type;

		public Dictionary<string, Property> properties;

		public List<string> required;
	}

	[Serializable]
	public class Function
	{
		public string name;

		public string description;

		public Parameter parameters;
	}

	[Serializable]
	public class Message
	{
		public string role;

		public string content;
	}

	[Serializable]
	public class LLMRequest
	{
		public string model;

		public float temperature;

		public float top_p;

		public float repetition_penalty;

		public bool stream;

		public string information;

		public List<Function> functions;

		public List<Message> messages;

		public List<int> embedding_buffer;
	}

	[Serializable]
	public class LLMResponse
	{
		public int index;

		public string thought;

		public MessageData message;

		public string finish_reason;

		public List<int> embedding_list;
	}

	[Serializable]
	public class LLMResponseWrapper
	{
		public string model;
		public string @object;
		public List<LLMResponseChoice> choices;
	}

	[Serializable]
	public class LLMResponseChoice
	{
		public int index;
		public Delta delta;
		public MessageData message;
		public string thought;
		public string finish_reason;
		public List<int> embedding_list;
	}

	[Serializable]
	public class Delta
	{
		public string content;
		public string role;
	}

	[Serializable]
	public class MessageData
	{
		public string role;

		public string content;

		public Action function_call;
	}

	public class Action
	{
		public string name;

		public Dictionary<string, string> arguments;
	}

	public List<Message> history;

	public string formatted_history = "";

	public List<Function> functions;

	private List<int> embedding_list;

	public string identity = "老师";

	public string preset_information = "";

	public bool pending;

	private void Start()
	{
		Function item = new Function
		{
			name = "sword_of_light",
			description = "使用电磁炮“光之剑”发起攻击",
			parameters = new Parameter
			{
				type = "object",
				properties = new Dictionary<string, Property> { 
				{
					"target",
					new Property
					{
						type = "string",
						description = "攻击目标的名字"
					}
				} },
				required = new List<string> { "target" }
			}
		};
		Function item2 = new Function
		{
			name = "move",
			description = "离开当前场景，前往其他地点",
			parameters = new Parameter
			{
				type = "object",
				properties = new Dictionary<string, Property> { 
				{
					"to",
					new Property
					{
						type = "string",
						description = "接下来要前往的场景或地点的名称"
					}
				} },
				required = new List<string> { "to" }
			}
		};
		new Function
		{
			name = "search_for_item",
			description = "道具搜索",
			parameters = new Parameter
			{
				type = "object",
				properties = new Dictionary<string, Property> { 
				{
					"object",
					new Property
					{
						type = "string",
						description = "指定具体的搜索对象，例如宝箱、房屋、垃圾箱等"
					}
				} },
				required = new List<string> { "object" }
			}
		};
		new Function
		{
			name = "search_on_internet",
			description = "上网搜索、查找相关信息",
			parameters = new Parameter
			{
				type = "object",
				properties = new Dictionary<string, Property> { 
				{
					"query",
					new Property
					{
						type = "string",
						description = "需要查找信息的条目"
					}
				} },
				required = new List<string> { "query" }
			}
		};
		functions.Add(item);
		functions.Add(item2);
		embedding_list = new List<int>();
	}

	public string LLMFormatterForWebsocket(string model, float temperature, float top_p, float repetition_penalty, bool stream, string message)
	{
		string text = JsonConvert.SerializeObject(new LLMRequest
		{
			model = model,
			temperature = temperature,
			top_p = top_p,
			repetition_penalty = repetition_penalty,
			stream = true,
			information = preset_information,
			functions = functions,
			messages = GetMessages(message),
			embedding_buffer = embedding_list
		}, Formatting.Indented);
		Debug.Log(text);
		return text;
	}

	private List<Message> GetMessages(string message)
	{
		Message item = new Message
		{
			role = "user",
			content = "（" + identity + "对爱丽丝说）" + message
		};
		history.Add(item);
		formatted_history = formatted_history + identity + "说：\n" + message + "\n\n";
		return history;
	}

	public void SaveResponse(LLMResponse response)
	{
		Message item = new Message
		{
			role = "assistant",
			content = "Thought: " + response.thought + "\nFinal Answer: " + response.message.content
		};
		history.Add(item);
		formatted_history = formatted_history + "爱丽丝说：\n" + RemoveEmotion(response.message.content) + "\n\n";
		embedding_list = response.embedding_list;
		pending = false;
	}

	public static string RemoveAction(string line)
	{
		line = line.Replace("(", "（");
		line = line.Replace(")", "）");
		string pattern = "（[^（）]*）";
		MatchCollection matchCollection = Regex.Matches(line, pattern);
		if (matchCollection.Count == 0)
		{
			return line;
		}
		foreach (Match item in matchCollection)
		{
			line = line.Replace(item.Value, "");
		}
		return line;
	}

	public static string RemoveEmotion(string message)
	{
		string pattern = @"\【\{'expression':\s*'[^']*'\}\】";
		MatchCollection matchCollection = Regex.Matches(message, pattern);
		if (matchCollection.Count != 0)
		{
			string value = matchCollection[0].Value;
			Debug.Log("emotion:" + value);
			return message.Replace(value, "");
		}
		return message;
	}
}
