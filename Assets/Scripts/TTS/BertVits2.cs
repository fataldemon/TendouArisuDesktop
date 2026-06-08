using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniGLTF;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Timeline;

public class BertVits2 : TTS
{
    /// <summary>
    /// 语音合成，返回合成文本
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback">回调函数</param>
    /// <param name="_getException">异常处理函数</param>
    public override void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        StartCoroutine(GetVoice(_msg, _callback, _getException));
    }

    /// <summary>
    /// 合成音频（先格式化数据，再合成）
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    /// <param name="_getException">异常处理函数</param>
    /// <returns></returns>
    private IEnumerator GetVoice(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        stopwatch.Restart();
        //发送报文
        string _postJson = GetPostJson(_msg, 5);
        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(_postJson);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                string _text = request.downloadHandler.text;

                Response_Formatter _response = JsonUtility.FromJson<Response_Formatter>(_text);
                string data_formatted = _response.data[1];
                Debug.Log("TTS文本预处理成功，结果为：" + data_formatted);
                StartCoroutine(GenerateVoice(data_formatted, _msg, _callback, _getException));
            }
            else
            {
                Debug.LogError("语音合成失败: " + request.error);
                _getException(true);
            }
        }

    }

    /// <summary>
    /// 合成音频
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    /// <param name="_getException">异常处理函数</param>
    /// <returns></returns>
    private IEnumerator GenerateVoice(string _msg_formatted, string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        stopwatch.Restart();
        //发送报文
        string _postJson = GetPostJson(_msg_formatted, 0);
        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(_postJson);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                string _text = request.downloadHandler.text;
                Response _response = JsonConvert.DeserializeObject<Response>(_text);
                string status = _response.data[0].ToString();
                if (status == "Success")
                {
                    Debug.Log("回复的语音信息：" + _response.data[1]);
                    AudioFile2 audioFile = JsonConvert.DeserializeObject<AudioFile2>(_response.data[1].ToString());
                    string _wavPath = audioFile.path;
                    Debug.Log("合成成功，语音文件地址：" + _wavPath);
                    StartCoroutine(GetAudioFromFile(_wavPath, _msg, _callback, _getException));
                }
                else 
                {
                    Debug.LogError("语音合成失败: 语音合成发生错误。" );
                    _getException(true);
                }
            }
            else
            {
                Debug.LogError("语音合成失败: " + request.error);
                _getException(true);
            }
        }
        stopwatch.Stop();
        Debug.Log("Bert-VITS2合成耗时：" + stopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// 从本地获取合成后的音频文件
    /// </summary>
    /// <param name="_path"></param>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    /// <param name="_getException">异常处理函数</param>
    /// <returns></returns>
    private IEnumerator GetAudioFromFile(string _path, string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        string filePath = "file://" + _path;
        Debug.Log("文件地址为" + filePath);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);
                _callback(audioClip, _msg);
            }
            else
            {
                Debug.LogError("音频读取失败 ：" + request.error);
                _getException(true);
            }
        }
    }

    /// <summary>
    /// 处理发送的Json报文
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_lan"></param>
    /// <returns></returns>
    private string GetPostJson(string _msg_formatted, int fn_index)
    {
        // 创建数据结构
        if (fn_index == 0) 
        {
            var jsonData = new
            {
                data = new List<object>
                {
                    _msg_formatted,  // str  in '输入文本内容' Textbox component
                    "alice",         // str (Option from: [('alice', 'alice')]) in 'Speaker' Dropdown component
                    0.5,             // int | float (numeric value between 0 and 1) in 'SDP Ratio' Slider component
                    0.6,             // int | float (numeric value between 0.1 and 2) in 'Noise' Slider component
                    0.9,             // int | float (numeric value between 0.1 and 2) in 'Noise_W' Slider component
                    1,               // int | float (numeric value between 0.1 and 2) in 'Length' Slider component
                    "mix",           // str (Option from: [('ZH', 'ZH'), ('JP', 'JP'), ('EN', 'EN'), ('mix', 'mix'), ('auto', 'auto')]) in 'Language' Dropdown component
                    null,            // str (filepath on your computer (or URL) of file) in 'Audio prompt' Audio component
                    "Happy",         // str  in 'Text prompt' Textbox component
                    "Text prompt",   // str  in 'Prompt Mode' Radio component
                    "",              // str  in '辅助文本' Textbox component
                    0.7              // int | float (numeric value between 0 and 1) in 'Weight' Slider component
                },
                fn_index = 0,
                session_hash = "1"
            };
            // 将数据转换为JSON格式
            string jsonString = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            return jsonString;
        }
        else if (fn_index == 5) {
            var jsonData = new
            {
                data = new List<object>
                {
                    _msg_formatted,
                    "alice"
                },
                fn_index = 5,
                session_hash = "1"
            };
            // 将数据转换为JSON格式
            string jsonString = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            return jsonString;
        }
        return null;
    }

    #region 数据定义
    [Serializable]
    public class Response_Formatter
    {
        public List<string> data = new List<string>();
        public bool is_generating;
        public float duration;
        public float average_duration;
    }

    [Serializable]
    public class Response
    {
        public List<object> data;
        public bool is_generating;
        public float duration;
        public float average_duration;
    }

    [Serializable]
    public class AudioFile 
    {
        public string name;
        public string data;
        public bool is_file;
        public string orig_name;
    }

    [Serializable]
    public class AudioFile2
    {
        public string path;
        public string url;
        public string orig_name;
    }


    #endregion
}
