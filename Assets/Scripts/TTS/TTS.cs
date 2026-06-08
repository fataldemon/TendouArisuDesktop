using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TTS : MonoBehaviour
{
    [SerializeField] protected string m_PostURL;
    [SerializeField] protected Stopwatch stopwatch = new Stopwatch();

    public virtual void Speak(string _msg, Action<AudioClip> _callback) { }

    public virtual void Speak(string _msg, Action<AudioClip, string> _callback, Action<bool> _getException) { }

    public String PostURL { get => m_PostURL; set => m_PostURL = value; }
}

public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavFile, string name = "wav")
    {
        int channels = wavFile[22];
        int sampleRate = BitConverter.ToInt32(wavFile, 24);
        int sampleDataIndex = 44;

        int samples = (wavFile.Length - sampleDataIndex) / 2;
        float[] audioData = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            short sample = BitConverter.ToInt16(wavFile, sampleDataIndex + i * 2);
            audioData[i] = sample / 32768.0f;
        }

        AudioClip audioClip = AudioClip.Create(name, samples, channels, sampleRate, false);
        audioClip.SetData(audioData, 0);

        return audioClip;
    }
}
