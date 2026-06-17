using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TtsCoordinator : MonoBehaviour
{
    [SerializeField] private GptSovits gptSovits;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameStart gameStart;
    [SerializeField] private Configuration config;

    public AudioClip bangbangkabangClip;
    private List<RefAudioEntry> refAudioEntries = new List<RefAudioEntry>();
    private bool refAudioLoaded;

    public GptSovits GptSovits { get => gptSovits; set => gptSovits = value; }
    public AudioSource AudioSource { get => audioSource; set => audioSource = value; }
    public GameStart GameStart { get => gameStart; set => gameStart = value; }
    public Configuration Config { get => config; set => config = value; }

    void Start()
    {
        LoadRefAudioConfig();
        PreloadBangWav();
    }

    public void ReloadRefAudio()
    {
        refAudioLoaded = false;
        LoadRefAudioConfig();
    }

    private void LoadRefAudioConfig()
    {
        if (config == null) return;

        string baseDir = config.gptSovitsRefAudioBaseDir;
        if (string.IsNullOrEmpty(baseDir))
            baseDir = System.IO.Path.Combine(Application.streamingAssetsPath, "RefAudio");

        ActionSystemRuntime.EnsureInit();
        var emotionKeys = new System.Collections.Generic.List<string>();
        foreach (var m in ActionSystemRuntime.EmotionMappings)
        {
            if (m.emotion == "触摸" || m.emotion == "拖拽" || m.emotion.StartsWith("随机-"))
                continue;
            if (!emotionKeys.Contains(m.emotion))
                emotionKeys.Add(m.emotion);
        }

        var saved = SettingsData.Load()?.refAudioConfigs;
        var savedDict = new System.Collections.Generic.Dictionary<string, RefAudioDataEntry>();
        if (saved != null)
            foreach (var se in saved)
                if (!string.IsNullOrEmpty(se.emotionKey))
                    savedDict[se.emotionKey] = se;

        refAudioEntries.Clear();
        foreach (var key in emotionKeys)
        {
            if (savedDict.TryGetValue(key, out var savedEntry))
            {
                string fullPath = savedEntry.audioFullPath;
                if (string.IsNullOrEmpty(fullPath) || !System.IO.File.Exists(fullPath))
                    fullPath = System.IO.Path.Combine(baseDir, savedEntry.audioFileName);

                refAudioEntries.Add(new RefAudioEntry
                {
                    emotionKey = savedEntry.emotionKey,
                    audioFileName = savedEntry.audioFileName,
                    promptText = savedEntry.promptText,
                    promptLang = savedEntry.promptLang,
                    audioFullPath = fullPath
                });
            }
            else
            {
                var defaultEntry = RefAudioConfig.GetDefaultEntry(key, baseDir);
                if (defaultEntry != null)
                    refAudioEntries.Add(defaultEntry);
            }
        }

        SaveRefAudioConfig();
        refAudioLoaded = true;
        Debug.Log($"[TtsCoordinator] Loaded {refAudioEntries.Count} reference audio mappings, baseDir={baseDir}");
    }

    private void SaveRefAudioConfig()
    {
        var settings = SettingsData.Load();
        settings.refAudioConfigs = new System.Collections.Generic.List<RefAudioDataEntry>();
        foreach (var entry in refAudioEntries)
        {
            settings.refAudioConfigs.Add(new RefAudioDataEntry
            {
                emotionKey = entry.emotionKey,
                audioFileName = entry.audioFileName,
                promptText = entry.promptText,
                promptLang = entry.promptLang,
                audioFullPath = entry.audioFullPath
            });
        }
        settings.gptSovitsUrl = config != null ? config.gptSovitsUrl : settings.gptSovitsUrl;
        settings.gptSovitsRefAudioBaseDir = config != null ? config.gptSovitsRefAudioBaseDir : settings.gptSovitsRefAudioBaseDir;
        settings.bangbangkabangWavPath = settings.bangbangkabangWavPath ?? "";
        settings.Save();
    }

    private void PreloadBangWav()
    {
        string path = null;
        var settings = SettingsData.Load();
        if (settings != null && !string.IsNullOrEmpty(settings.bangbangkabangWavPath))
            path = settings.bangbangkabangWavPath;

        if (string.IsNullOrEmpty(path))
            path = System.IO.Path.Combine(Application.streamingAssetsPath, "Bangbangkabang", "bangbangkabang.wav");

        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            StartCoroutine(LoadBangWavCoroutine(path));
        }
        else
        {
            Debug.LogWarning($"[TtsCoordinator] 邦邦咔邦 WAV not found: {path}");
        }
    }

    private IEnumerator LoadBangWavCoroutine(string path)
    {
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                bangbangkabangClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                Debug.Log($"[TtsCoordinator] Loaded bangbangkabang.wav, duration={bangbangkabangClip.length:F2}s");
            }
            else
            {
                Debug.LogError($"[TtsCoordinator] Failed to load bangbangkabang.wav: {www.error}");
            }
        }
    }

    public void Generate(string text, string emotion, Action<AudioClip, string> onComplete, Action<bool> onError, string textLang = "auto")
    {
        if (!refAudioLoaded) LoadRefAudioConfig();
        StartCoroutine(GenerateCoroutine(text, emotion, onComplete, onError, textLang));
    }

    private IEnumerator GenerateCoroutine(string text, string emotion, Action<AudioClip, string> onComplete, Action<bool> onError, string textLang = "auto")
    {
        if (string.IsNullOrEmpty(text))
        {
            onError(true);
            yield break;
        }

        List<Segment> segments = SplitTextAndBang(text);

        if (segments.Count == 1 && segments[0].isBang)
        {
            if (bangbangkabangClip != null)
            {
                onComplete(bangbangkabangClip, text);
            }
            else
            {
                Debug.LogWarning("[TtsCoordinator] 邦邦咔邦 clip not loaded");
                onError(true);
            }
            yield break;
        }

        List<AudioClip> clips = new List<AudioClip>();
        bool hasError = false;

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].isBang)
            {
                if (bangbangkabangClip != null)
                    clips.Add(bangbangkabangClip);
            }
            else if (!string.IsNullOrEmpty(segments[i].text))
            {
                string segText = segments[i].text.Trim();
                if (string.IsNullOrEmpty(segText)) continue;

                var req = BuildRequest(segText, emotion, textLang);
                AudioClip clip = null;
                bool err = false;

                yield return StartCoroutine(WaitForTtsClip(req, c => clip = c, e => err = e));

                if (err || clip == null)
                {
                    hasError = true;
                    break;
                }
                clips.Add(clip);
            }
        }

        if (hasError)
        {
            onError(true);
            yield break;
        }

        AudioClip combined = CombineClips(clips);
        if (combined != null)
        {
            onComplete(combined, text);
        }
        else
        {
            onError(true);
        }
    }

    private struct Segment
    {
        public string text;
        public bool isBang;
    }

    private List<Segment> SplitTextAndBang(string text)
    {
        var result = new List<Segment>();
        if (string.IsNullOrEmpty(text)) return result;

        string kw = "パンパカパーン";

        int idx = text.IndexOf(kw);
        if (idx < 0)
        {
            result.Add(new Segment { text = text, isBang = false });
            return result;
        }

        while (idx >= 0)
        {
            if (idx > 0)
            {
                string prefix = text.Substring(0, idx);
                if (!string.IsNullOrEmpty(prefix.Trim()))
                    result.Add(new Segment { text = prefix, isBang = false });
            }

            result.Add(new Segment { text = "邦邦咔邦", isBang = true });

            text = text.Substring(idx + kw.Length);
            idx = text.IndexOf(kw);
        }

        if (!string.IsNullOrEmpty(text.Trim()))
            result.Add(new Segment { text = text, isBang = false });

        return result;
    }

    private GptSovits.TtsRequest BuildRequest(string text, string emotion, string textLang = "auto")
    {
        var entry = RefAudioConfig.FindForEmotion(refAudioEntries, emotion);

        var req = new GptSovits.TtsRequest
        {
            text = text,
            textLang = textLang,
            promptLang = "ja",
            topK = 15,
            topP = 1.0f,
            temperature = 1.0f,
            textSplitMethod = "cut5",
            batchSize = 1,
            speedFactor = 1.0f,
            streamingMode = true
        };

        if (entry != null)
        {
            req.refAudioPath = entry.audioFullPath;
            req.promptText = entry.promptText;
            req.promptLang = entry.promptLang;
        }

        return req;
    }

    private IEnumerator WaitForTtsClip(GptSovits.TtsRequest req, Action<AudioClip> onClip, Action<bool> onErr)
    {
        AudioClip result = null;
        bool error = false;

        gptSovits.Speak(req, clip =>
        {
            result = clip;
        }, err =>
        {
            error = err;
        });

        float timeout = 60f;
        float elapsed = 0f;
        while (result == null && !error && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.05f);
            elapsed += 0.05f;
        }

        if (result != null)
            onClip(result);
        else
            onErr(true);
    }

    private AudioClip CombineClips(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1) return clips[0];

        int totalSamples = 0;
        int maxChannels = 0;
        int sampleRate = clips[0].frequency;

        foreach (var c in clips)
        {
            if (c == null) continue;
            totalSamples += c.samples;
            if (c.channels > maxChannels) maxChannels = c.channels;
            if (c.frequency > sampleRate) sampleRate = c.frequency;
        }

        if (totalSamples == 0) return null;

        AudioClip combined = AudioClip.Create("CombinedTTS", totalSamples, maxChannels, sampleRate, false);

        float[] combinedData = new float[totalSamples * maxChannels];
        int offset = 0;

        foreach (var c in clips)
        {
            if (c == null) continue;
            float[] data = new float[c.samples * c.channels];
            c.GetData(data, 0);

            for (int i = 0; i < data.Length; i++)
            {
                int targetIdx = offset + i;
                if (targetIdx < combinedData.Length)
                    combinedData[targetIdx] = data[i];
            }

            offset += c.samples * maxChannels;
        }

        combined.SetData(combinedData, 0);
        return combined;
    }

    public void TestTts(string text, Action onSuccess = null, Action<string> onError = null)
    {
        if (gptSovits == null)
        {
            Debug.LogError("[TtsCoordinator] GPT-SoVITS not assigned");
            onError?.Invoke("GPT-SoVITS not assigned");
            return;
        }

        if (!refAudioLoaded) LoadRefAudioConfig();

        var first = RefAudioConfig.GetFirst(refAudioEntries);
        var req = new GptSovits.TtsRequest
        {
            text = text,
            textLang = "auto",
            promptLang = "ja",
            topK = 15,
            topP = 1.0f,
            temperature = 1.0f,
            textSplitMethod = "cut5",
            batchSize = 1,
            speedFactor = 1.0f,
            streamingMode = true
        };

        if (first != null)
        {
            req.refAudioPath = first.audioFullPath;
            req.promptText = first.promptText;
            req.promptLang = first.promptLang;
        }
        else
        {
            onError?.Invoke("No reference audio configured");
            return;
        }

        gptSovits.Speak(req, clip =>
        {
            if (gameStart != null && clip != null)
            {
                gameStart.PlayVoicePublic(clip, text);
                onSuccess?.Invoke();
            }
            else
            {
                onError?.Invoke("TTS returned empty audio");
            }
        }, err =>
        {
            string msg = "TTS request failed";
            if (gameStart != null)
                gameStart.SetExceptionRestorePublic(true);
            onError?.Invoke(msg);
        });
    }
}
