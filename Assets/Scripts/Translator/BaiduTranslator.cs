using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class BaiduTranslator : MonoBehaviour
{
    [SerializeField]
    private string salt;
    [SerializeField]
    private string app_id;
    [SerializeField]
    private string baidu_fanyi_url;
    [SerializeField]
    private string private_key;

    public string Baidu_fanyi_url { get => baidu_fanyi_url; set => baidu_fanyi_url = value; }
    public string Private_key { get => private_key; set => private_key = value; }
    public string App_id { get => app_id; set => app_id = value; }
    public string Salt { get => salt; set => salt = value; }

    ///���з���
    public void translate(string _line, string _lang, Action<string> _callback, Action<bool> _getException)
    {
        StartCoroutine(CallTranslator(_line, _lang, _callback, _getException));
    }

    private IEnumerator CallTranslator(string _line, string _lang, Action<string> _callback, Action<bool> _getException)
    {
        //���ݳ�ʼ��
        string signRaw = $"{app_id}{_line}{salt}{private_key}";
        string sign = CalculateMD5(signRaw);
        string url = $"{Baidu_fanyi_url}?q={_line}&from=auto&to={_lang}&appid={app_id}&salt={salt}&sign={sign}";
            Debug.Log("[Translator] Sending request, text length=" + _line.Length + " to=" + _lang);
            //����Get����
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                // �������󲢵ȴ���Ӧ
                yield return webRequest.SendWebRequest();

                // �������Ľ��
                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("[Translator] Network Error: " + webRequest.error + ", code=" + webRequest.responseCode);
                    _getException(true);
                }
            else
            {
                // 成功接收到响应
                string _text = webRequest.downloadHandler.text;

                Response response = JsonConvert.DeserializeObject<Response>(_text);
                if (response == null || response.trans_result == null || response.trans_result.Count == 0)
                {
                    Debug.LogError("[Translator] API response invalid: " + _text);
                    _getException(true);
                    yield break;
                }
                string result_str = response.trans_result[0].ToString();
                TranslationResult trans_result = JsonConvert.DeserializeObject<TranslationResult>(result_str);
                if (trans_result == null || string.IsNullOrEmpty(trans_result.dst))
                {
                    Debug.LogError("翻译结果解析失败: " + result_str);
                    _getException(true);
                    yield break;
                }
                string result_processed = SpecializedJPWords(trans_result.dst);
                Debug.Log("Result: " + result_processed);
                //�ص�����
                _callback(result_processed);
            }
        }
    }

    private string CalculateMD5(string input)
    {
        // ����һ���µ�MD5ʵ��
        using (MD5 md5 = MD5.Create())
        {
            // �������ַ���ת��Ϊ�ֽ����鲢�����ϣ
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // ���ֽ�����ת��Ϊʮ�������ַ���
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    private string SpecializedJPWords(string _text)
    {
        return _text;
    }

    #region ���ݶ���
    [Serializable]
    public class Response
    {
        public string from;
        public string to;
        public List<object> trans_result;
    }

    [Serializable]
    public class TranslationResult
    { 
        public string src;
        public string dst;
    }
    #endregion
}
