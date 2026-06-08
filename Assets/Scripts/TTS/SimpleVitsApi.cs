using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BertVits2;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text;
using static UnityEngine.Rendering.DebugUI;
using Unity.Mathematics;

public class SimpleVitsApi : TTS
{
    public override void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        StartCoroutine(GenerateVoice(_msg, _callback, _getException));
    }

    private IEnumerator GenerateVoice(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException)
    {
        stopwatch.Restart();
        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            string boundary = "----VoiceConversionFormBoundary" + RandomString(16);
            byte[] data = GetPostJson(_msg, boundary);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                AudioClip audioClip = WavUtility.ToAudioClip(request.downloadHandler.data, "GeneratedAudio");
                _callback(audioClip, _msg);
            }
            else
            {
                Debug.LogError("TTS Error: " + request.error);
                _getException(true);
            }
        }
        stopwatch.Stop();
        Debug.Log("Voice synthesis time: " + stopwatch.Elapsed.TotalSeconds);
    }

    private byte[] GetPostJson(string _msg, string boundary)
    {
        var jsonData = new Dictionary<string, string>
        {
            { "text", _msg },
            { "id", "0" },
            { "format", "wav" },
            { "lang", "auto" },
            { "noise", "0.667" },
            { "noisew", "0.8" },
            { "segment_size", "50" },
            { "sdp_ratio", "0.2" }
        };

        byte[] formData = GetMultipartFormData(jsonData, boundary);
        return formData;
    }

    private string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Random rand = new System.Random();
        for (int i = 0; i < length; i++)
        {
            sb.Append(chars[rand.Next(chars.Length)]);
        }
        return sb.ToString();
    }

    private byte[] GetMultipartFormData(Dictionary<string, string> fields, string boundary)
    {
        List<byte> formData = new List<byte>();

        foreach (KeyValuePair<string, string> field in fields)
        {
            string fieldData = "--" + boundary + "\r\n" +
                               "Content-Disposition: form-data; name=\"" + field.Key + "\"\r\n\r\n" +
                               field.Value + "\r\n";
            formData.AddRange(Encoding.UTF8.GetBytes(fieldData));
        }

        formData.AddRange(Encoding.UTF8.GetBytes("--" + boundary + "--\r\n"));
        return formData.ToArray();
    }
}
