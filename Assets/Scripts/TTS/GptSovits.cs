using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GptSovits : TTS
{
    [SerializeField] private string m_RefAudioBaseDir;
    public string RefAudioBaseDir { get => m_RefAudioBaseDir; set => m_RefAudioBaseDir = value; }

    public struct TtsRequest
    {
        public string text;
        public string textLang;
        public string refAudioPath;
        public string promptText;
        public string promptLang;
        public int topK;
        public float topP;
        public float temperature;
        public string textSplitMethod;
        public int batchSize;
        public float speedFactor;
        public bool streamingMode;
    }

    [Serializable]
    private class TtsRequestJson
    {
        public string text;
        public string text_lang;
        public string ref_audio_path;
        public string prompt_text;
        public string prompt_lang;
        public int top_k;
        public float top_p;
        public float temperature;
        public string text_split_method;
        public int batch_size;
        public float speed_factor;
        public bool streaming_mode;
    }

    public void Speak(TtsRequest req, Action<AudioClip> onComplete, Action<bool> onException)
    {
        StartCoroutine(GenerateVoice(req, onComplete, onException));
    }

    private IEnumerator GenerateVoice(TtsRequest req, Action<AudioClip> onComplete, Action<bool> onException)
    {
        stopwatch.Restart();

        var jsonObj = new TtsRequestJson
        {
            text = req.text,
            text_lang = !string.IsNullOrEmpty(req.textLang) ? req.textLang : "auto",
            ref_audio_path = req.refAudioPath,
            prompt_text = !string.IsNullOrEmpty(req.promptText) ? req.promptText : "",
            prompt_lang = !string.IsNullOrEmpty(req.promptLang) ? req.promptLang : "ja",
            top_k = req.topK > 0 ? req.topK : 15,
            top_p = req.topP > 0 ? req.topP : 1.0f,
            temperature = req.temperature > 0 ? req.temperature : 1.0f,
            text_split_method = !string.IsNullOrEmpty(req.textSplitMethod) ? req.textSplitMethod : "cut5",
            batch_size = req.batchSize > 0 ? req.batchSize : 1,
            speed_factor = req.speedFactor > 0 ? req.speedFactor : 1.0f,
            streaming_mode = req.streamingMode
        };

        string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj);

        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            byte[] data = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 120;

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                byte[] wavData = request.downloadHandler.data;
                if (wavData != null && wavData.Length > 44)
                {
                    AudioClip audioClip = WavUtility.ToAudioClip(wavData, "GptSoVitsAudio");
                    onComplete(audioClip);
                }
                else
                {
                    Debug.LogError("GPT-SoVITS returned empty or invalid audio data");
                    onException(true);
                }
            }
            else
            {
                Debug.LogError($"GPT-SoVITS TTS Error: {request.responseCode} {request.error}\n{request.downloadHandler.text}");
                onException(true);
            }
        }

        stopwatch.Stop();
        Debug.Log($"GPT-SoVITS synthesis time: {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    public override void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        var req = new TtsRequest
        {
            text = _msg,
            textLang = "auto",
            promptLang = "ja",
            streamingMode = false
        };
        StartCoroutine(GenerateVoiceLegacy(req, _msg, _callback, _getException));
    }

    private IEnumerator GenerateVoiceLegacy(TtsRequest req, string originalText, Action<AudioClip, string> callback, Action<bool> onException)
    {
        stopwatch.Restart();

        var jsonObj = new TtsRequestJson
        {
            text = req.text,
            text_lang = !string.IsNullOrEmpty(req.textLang) ? req.textLang : "auto",
            ref_audio_path = req.refAudioPath,
            prompt_text = !string.IsNullOrEmpty(req.promptText) ? req.promptText : "",
            prompt_lang = !string.IsNullOrEmpty(req.promptLang) ? req.promptLang : "ja",
            top_k = req.topK > 0 ? req.topK : 15,
            top_p = req.topP > 0 ? req.topP : 1.0f,
            temperature = req.temperature > 0 ? req.temperature : 1.0f,
            text_split_method = !string.IsNullOrEmpty(req.textSplitMethod) ? req.textSplitMethod : "cut5",
            batch_size = req.batchSize > 0 ? req.batchSize : 1,
            speed_factor = req.speedFactor > 0 ? req.speedFactor : 1.0f,
            streaming_mode = req.streamingMode
        };

        string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj);

        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            byte[] data = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 120;

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                byte[] wavData = request.downloadHandler.data;
                if (wavData != null && wavData.Length > 44)
                {
                    AudioClip audioClip = WavUtility.ToAudioClip(wavData, "GptSoVitsAudio");
                    callback(audioClip, originalText);
                }
                else
                {
                    Debug.LogError("GPT-SoVITS returned empty or invalid audio data");
                    onException(true);
                }
            }
            else
            {
                Debug.LogError($"GPT-SoVITS TTS Error: {request.responseCode} {request.error}\n{request.downloadHandler.text}");
                onException(true);
            }
        }

        stopwatch.Stop();
        Debug.Log($"GPT-SoVITS synthesis time: {stopwatch.Elapsed.TotalSeconds:F2}s");
    }
}
