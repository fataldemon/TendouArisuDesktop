using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TTS : MonoBehaviour
{
    /// <summary>
    /// 语音合成的api地址
    /// </summary>
    [SerializeField] protected string m_PostURL;
    /// <summary>
    /// 计算方法调用的时间
    /// </summary>
    [SerializeField] protected Stopwatch stopwatch = new Stopwatch();
    /// <summary>
    /// 语音合成，返回音频
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    public virtual void Speak(string _msg, Action<AudioClip> _callback) { }
    /// <summary>
    /// 合成语音返回音频，同时返回合成的文本
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    public virtual void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException) { }

    public String PostURL { get => m_PostURL; set => m_PostURL = value; }
}

public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavFile, string name = "wav")
    {
        int channels = wavFile[22]; // Number of channels
        int sampleRate = BitConverter.ToInt32(wavFile, 24); // Sample rate
        int sampleDataIndex = 44; // Data starts at byte 44

        // Read samples
        int samples = (wavFile.Length - sampleDataIndex) / 2; // 2 bytes per sample (16 bit PCM)
        float[] audioData = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            short sample = BitConverter.ToInt16(wavFile, sampleDataIndex + i * 2);
            audioData[i] = sample / 32768.0f; // Convert to float in range -1 to 1
        }

        // Create AudioClip
        AudioClip audioClip = AudioClip.Create(name, samples, channels, sampleRate, false);
        audioClip.SetData(audioData, 0);

        return audioClip;
    }
}