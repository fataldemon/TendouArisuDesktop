// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// ActionController
using System;
using System.Text.RegularExpressions;
using UnityEngine;

public class ActionController : MonoBehaviour
{
	public FacialController facialController;

	public Animator animator;

	public ExpressionMappingManager mappingManager;

	private void Start()
	{
	}

	private void Update()
	{
	}

	public bool getIdleStatus()
	{
		if (animator.GetInteger("action_param") == 0 && animator.GetInteger("onWaiting") == 0 && !animator.GetBool("onAction"))
		{
			return true;
		}
		return false;
	}

	public bool getWaitingStatus()
	{
		if (animator.GetInteger("onWaiting") == 0)
		{
			return false;
		}
		return true;
	}

	private string GetExpression(string reply)
	{
		string pattern = @"【\{'expression':\s*'([^']*)'\}\】";
		Match match = Regex.Match(reply, pattern);
		if (match.Success)
		{
			return match.Groups[1].Value;
		}
		return null;
	}

	public void SetFacialExpression(string reply_from_llm)
	{
		string emotion = GetExpression(reply_from_llm);
		if (mappingManager != null && mappingManager.TryApplyFacial(emotion))
			return;
		switch (emotion)
		{
		case "微笑":
			break;
		case "认真":
			facialController.PerformExpression("serious", null);
			break;
		case "坚定":
			facialController.PerformExpression("serious", null);
			break;
		case "承诺":
			facialController.PerformExpression("serious", null);
			break;
		case "生气":
			facialController.PerformExpression("angry", null);
			break;
		case "急切":
			facialController.PerformExpression("angry", null);
			break;
		case "烦恼":
			facialController.PerformExpression("panic", null);
			break;
		case "专注":
			facialController.PerformExpression("curious", null);
			break;
		case "诚实":
			facialController.PerformExpression("curious", null);
			break;
		case "期待":
			facialController.PerformExpression("fun", null);
			break;
		case "回答":
			facialController.PerformExpression("curious", null);
			break;
		case "回忆":
			facialController.PerformExpression("thinking", null);
			break;
		case "发愣":
			facialController.PerformExpression("curious", null);
			break;
		case "察觉":
			facialController.PerformExpression("curious", null);
			break;
		case "建议":
			facialController.PerformExpression("fun", null);
			break;
		case "好奇":
			facialController.PerformExpression("curious", null);
			break;
		case "自信":
			facialController.PerformExpression("confident", null);
			break;
		case "自豪":
			facialController.PerformExpression("confident", null);
			break;
		case "解释":
			facialController.PerformExpression("fun", null);
			break;
		case "失望":
			facialController.PerformExpression("disappointed", null);
			break;
		case "委屈":
			facialController.PerformExpression("cry", null);
			break;
		case "伤心":
			facialController.PerformExpression("cry", null);
			break;
		case "高兴":
			facialController.PerformExpression("fun", null);
			break;
		case "开心":
			facialController.PerformExpression("happy", null);
			break;
		case "欢迎":
			facialController.PerformExpression("fun", null);
			break;
		case "崇拜":
			facialController.PerformExpression("fun", null);
			break;
		case "愉快":
			facialController.PerformExpression("fun", null);
			break;
		case "贴心":
			facialController.PerformExpression("fun", null);
			break;
		case "赞同":
			facialController.PerformExpression("fun", null);
			break;
		case "邀请":
			facialController.PerformExpression("fun", null);
			break;
		case "兴奋":
			facialController.PerformExpression("happy", null);
			break;
		case "快乐":
			facialController.PerformExpression("happy", null);
			break;
		case "难过":
			facialController.PerformExpression("disappointed", null);
			break;
		case "为难":
			facialController.PerformExpression("disappointed", null);
			break;
		case "尴尬":
			facialController.PerformExpression("disappointed", null);
			break;
		case "紧张":
			facialController.PerformExpression("disappointed", null);
			break;
		case "困惑":
			facialController.PerformExpression("disappointed", null);
			break;
		case "困扰":
			facialController.PerformExpression("disappointed", null);
			break;
		case "疑惑":
			facialController.PerformExpression("disappointed", null);
			break;
		case "害怕":
			facialController.PerformExpression("sweating", null);
			break;
		case "平和":
			facialController.PerformExpression("plain", null);
			break;
		case "无聊":
			facialController.PerformExpression("plain", null);
			break;
		case "冷漠":
			facialController.PerformExpression("plain", null);
			break;
		case "慌张":
			facialController.PerformExpression("panic", null);
			break;
		case "害羞":
			facialController.PerformExpression("shy", null);
			break;
		case "羞涩":
			facialController.PerformExpression("shy", null);
			break;
		case "惊喜":
			facialController.PerformExpression("fun", null);
			break;
		case "理解":
			facialController.PerformExpression("fun", null);
			break;
		case "喜悦":
			facialController.PerformExpression("fun", null);
			break;
		case "担忧":
			facialController.PerformExpression("sweating", null);
			break;
		case "流汗":
			facialController.PerformExpression("sweating", null);
			break;
		case "犹豫":
			facialController.PerformExpression("disappointed", null);
			break;
		case "震惊":
			facialController.PerformExpression("sweating", null);
			break;
		case "惊讶":
			facialController.PerformExpression("sweating", null);
			break;
		case "思考":
			facialController.PerformExpression("thinking", null);
			break;
		case "沉思":
			facialController.PerformExpression("thinking", null);
			break;
		case "否认":
			facialController.PerformExpression("thinking", null);
			break;
		case "睡觉":
			facialController.PerformExpression("thinking", null);
			break;
		case "陈述":
			facialController.PerformExpression("plain", null);
			break;
		case "祈祷":
			facialController.PerformExpression("thinking", null);
			break;
		case "拒绝":
			facialController.PerformExpression("serious", null);
			break;
		case "感动":
			facialController.PerformExpression("touching", null);
			break;
		case "感激":
			facialController.PerformExpression("touching", null);
			break;
		case "道歉":
			facialController.PerformExpression("sweating", null);
			break;
		case "可爱":
			facialController.PerformExpression("wink", null);
			break;
		case "俏皮":
			facialController.PerformExpression("wink", null);
			break;
		case "调皮":
			facialController.PerformExpression("wink", null);
			break;
		case "卖萌":
			facialController.PerformExpression("wink", null);
			break;
		case "眨眼":
			facialController.PerformExpression("wink", null);
			break;
		}
	}

	public void RestoreFacialExpression(Action<bool> _callback)
	{
		facialController.PerformExpression("restore", _callback);
	}

	public void AnimatorControl(string reply_from_llm)
	{
		string expression = GetExpression(reply_from_llm);
		if (reply_from_llm.Contains("耶！"))
		{
			animator.SetInteger("action_param", 6);
			return;
		}
		if (mappingManager != null && mappingManager.TryApplyAction(expression))
			return;
		switch (expression)
		{
		case "认真":
			animator.SetInteger("action_param", 24);
			break;
		case "坚定":
			animator.SetInteger("action_param", 11);
			break;
		case "承诺":
			animator.SetInteger("action_param", 11);
			break;
		case "生气":
			animator.SetInteger("action_param", 20);
			break;
		case "急切":
			animator.SetInteger("action_param", 27);
			break;
		case "烦恼":
			animator.SetInteger("action_param", 1);
			break;
		case "专注":
			animator.SetInteger("action_param", 22);
			break;
		case "诚实":
			animator.SetInteger("action_param", 1);
			break;
		case "期待":
			animator.SetInteger("action_param", 19);
			break;
		case "回答":
			animator.SetInteger("action_param", 24);
			break;
		case "回忆":
			animator.SetInteger("action_param", 17);
			break;
		case "发愣":
			animator.SetInteger("action_param", 15);
			break;
		case "察觉":
			animator.SetInteger("action_param", 1);
			break;
		case "建议":
			animator.SetInteger("action_param", 24);
			break;
		case "好奇":
			animator.SetInteger("action_param", 22);
			break;
		case "自信":
			animator.SetInteger("action_param", 4);
			break;
		case "自豪":
			animator.SetInteger("action_param", 4);
			break;
		case "解释":
			animator.SetInteger("action_param", 24);
			break;
		case "失望":
			animator.SetInteger("action_param", 25);
			break;
		case "委屈":
			animator.SetInteger("action_param", 7);
			break;
		case "伤心":
			animator.SetInteger("action_param", 28);
			break;
		case "高兴":
			animator.SetInteger("action_param", 25);
			break;
		case "开心":
			animator.SetInteger("action_param", 25);
			break;
		case "欢迎":
			animator.SetInteger("action_param", 5);
			break;
		case "崇拜":
			animator.SetInteger("action_param", 19);
			break;
		case "愉快":
			animator.SetInteger("action_param", 1);
			break;
		case "贴心":
			animator.SetInteger("action_param", 22);
			break;
		case "赞同":
			animator.SetInteger("action_param", 16);
			break;
		case "邀请":
			animator.SetInteger("action_param", 13);
			break;
		case "兴奋":
			animator.SetInteger("action_param", 25);
			break;
		case "快乐":
			animator.SetInteger("action_param", 25);
			break;
		case "难过":
			animator.SetInteger("action_param", 1);
			break;
		case "为难":
			animator.SetInteger("action_param", 24);
			break;
		case "尴尬":
			animator.SetInteger("action_param", 24);
			break;
		case "紧张":
			animator.SetInteger("action_param", 1);
			break;
		case "困惑":
			animator.SetInteger("action_param", 24);
			break;
		case "困扰":
			animator.SetInteger("action_param", 24);
			break;
		case "疑惑":
			animator.SetInteger("action_param", 1);
			break;
		case "害怕":
			animator.SetInteger("action_param", 23);
			break;
		case "平和":
			animator.SetInteger("action_param", 1);
			break;
		case "无聊":
			animator.SetInteger("action_param", 1);
			break;
		case "冷漠":
			animator.SetInteger("action_param", 1);
			break;
		case "慌张":
			animator.SetInteger("action_param", 23);
			break;
		case "害羞":
			animator.SetInteger("action_param", 28);
			break;
		case "羞涩":
			animator.SetInteger("action_param", 7);
			break;
		case "微笑":
			animator.SetInteger("action_param", 1);
			break;
		case "惊喜":
			animator.SetInteger("action_param", 25);
			break;
		case "理解":
			animator.SetInteger("action_param", 16);
			break;
		case "喜悦":
			animator.SetInteger("action_param", 25);
			break;
		case "担忧":
			animator.SetInteger("action_param", 24);
			break;
		case "流汗":
			animator.SetInteger("action_param", 24);
			break;
		case "犹豫":
			animator.SetInteger("action_param", 24);
			break;
		case "震惊":
			animator.SetInteger("action_param", 23);
			break;
		case "惊讶":
			animator.SetInteger("action_param", 23);
			break;
		case "思考":
			animator.SetInteger("action_param", 26);
			break;
		case "沉思":
			animator.SetInteger("action_param", 17);
			break;
		case "否认":
			animator.SetInteger("action_param", 14);
			break;
		case "睡觉":
			animator.SetInteger("action_param", 18);
			break;
		case "陈述":
			animator.SetInteger("action_param", 1);
			break;
		case "祈祷":
			animator.SetInteger("action_param", 1);
			break;
		case "拒绝":
			animator.SetInteger("action_param", 10);
			break;
		case "感动":
			animator.SetInteger("action_param", 25);
			break;
		case "感激":
			animator.SetInteger("action_param", 25);
			break;
		case "道歉":
			animator.SetInteger("action_param", 29);
			break;
		case "可爱":
			animator.SetInteger("action_param", 12);
			break;
		case "俏皮":
			animator.SetInteger("action_param", 15);
			break;
		case "调皮":
			animator.SetInteger("action_param", 15);
			break;
		case "卖萌":
			animator.SetInteger("action_param", 3);
			break;
		case "眨眼":
			animator.SetInteger("action_param", 12);
			break;
		default:
			animator.SetInteger("action_param", 1);
			break;
		}
	}

	public void RestoreAnimator()
	{
		if (mappingManager == null || !mappingManager.TryApplyAction("待机"))
			animator.SetInteger("action_param", 0);
	}
}
