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
    public override void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        StartCoroutine(GetVoice(_msg, _callback, _getException));
    }

    private IEnumerator GetVoice(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        stopwatch.Restart();
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
                Debug.Log("TTS text prepared: " + data_formatted);
                StartCoroutine(GenerateVoice(data_formatted, _msg, _callback, _getException));
            }
            else
            {
                Debug.LogError("Voice synthesis failed: " + request.error);
                _getException(true);
            }
        }

    }

    private IEnumerator GenerateVoice(string _msg_formatted, string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        stopwatch.Restart();
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
                    Debug.Log("Response info: " + _response.data[1]);
                    AudioFile2 audioFile = JsonConvert.DeserializeObject<AudioFile2>(_response.data[1].ToString());
                    string _wavPath = audioFile.path;
                    Debug.Log("Synthesis success, wav path: " + _wavPath);
                    StartCoroutine(GetAudioFromFile(_wavPath, _msg, _callback, _getException));
                }
                else 
                {
                    Debug.LogError("Voice synthesis failed: server error");
                    _getException(true);
                }
            }
            else
            {
                Debug.LogError("Voice synthesis failed: " + request.error);
                _getException(true);
            }
        }
        stopwatch.Stop();
        Debug.Log("Bert-VITS2 synthesis time: " + stopwatch.Elapsed.TotalSeconds);
    }

    private IEnumerator GetAudioFromFile(string _path, string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        string filePath = "file://" + _path;
        Debug.Log("File path: " + filePath);
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
                Debug.LogError("Audio read failed: " + request.error);
                _getException(true);
            }
        }
    }

    private string GetPostJson(string _msg_formatted, int fn_index)
    {
        if (fn_index == 0) 
        {
            var jsonData = new
            {
                data = new List<object>
                {
                    _msg_formatted,
                    "alice",
                    0.5,
                    0.6,
                    0.9,
                    1,
                    "mix",
                    null,
                    "Happy",
                    "Text prompt",
                    "",
                    0.7
                },
                fn_index = 0,
                session_hash = "1"
            };
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
            string jsonString = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
            return jsonString;
        }
        return null;
    }

    #region Data Definitions
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
