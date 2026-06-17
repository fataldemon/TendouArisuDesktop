using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Net.WebRequestMethods;

public class Configuration : MonoBehaviour
{
    public string websocket_url;
    public int tts;
    
    public string translation_url;
    public string translation_app_id;
    public string translation_key;
    public string translation_salt;
    public string identity;
    public string preset;
    public bool translationEnabled;

    [SerializeField]
    public GptSovits gptSovits_TTS;
    [SerializeField]
    public BertVits2 bertVits2_TTS;
    [SerializeField]
    public SimpleVitsApi simpleVitsApi_TTS;
    [SerializeField]
    public TtsCoordinator ttsCoordinator;

    public string gptSovitsUrl;
    public string gradio_url;
    public string simpleVitsApi_url;
    public string gptSovitsRefAudioBaseDir;

    public void initConfiguration(string _websocket_url, int _tts, string _translation_url, 
        string _translation_app_id, string _translation_key, string _translation_salt, string _identity, string _preset)
    { 
        websocket_url = _websocket_url;
        tts = _tts;
        gptSovitsUrl = gptSovits_TTS != null ? gptSovits_TTS.PostURL : "";
        gradio_url = bertVits2_TTS.PostURL;
        simpleVitsApi_url = simpleVitsApi_TTS.PostURL;
        translation_url = _translation_url;
        translation_app_id = _translation_app_id;
        translation_key = _translation_key;
        translation_salt = _translation_salt;
        identity = _identity;
        preset = _preset;
    }

    public TTS getTTS(int index)
    {
        if (index == 0)
        {
            return gptSovits_TTS;
        }
        else if (index == 1)
        {
            return bertVits2_TTS;
        }
        else if (index == 2)
        {
            return simpleVitsApi_TTS;
        }
        else return null;
    }

    public void setTTSUrl(int index)
    {
        if (index == 0)
        {
            if (gptSovits_TTS != null) gptSovits_TTS.PostURL = gptSovitsUrl;
        }
        else if (index == 1)
        {
            bertVits2_TTS.PostURL = gradio_url;
        }
        else if (index == 2)
        {
            simpleVitsApi_TTS.PostURL = simpleVitsApi_url;
        }
    }

    public void ApplyFrom(SettingsData settings)
    {
        if (settings == null) return;

        if (!string.IsNullOrEmpty(settings.websocketUrl))
            websocket_url = settings.websocketUrl;

        tts = settings.ttsMode;

        if (tts == 0 && string.IsNullOrEmpty(settings.gptSovitsUrl) && !string.IsNullOrEmpty(settings.gradioUrl))
        {
            tts = 1;
        }

        if (!string.IsNullOrEmpty(settings.gptSovitsUrl))
            gptSovitsUrl = settings.gptSovitsUrl;

        if (!string.IsNullOrEmpty(settings.gradioUrl))
            gradio_url = settings.gradioUrl;

        if (!string.IsNullOrEmpty(settings.simpleVitsUrl))
            simpleVitsApi_url = settings.simpleVitsUrl;

        if (!string.IsNullOrEmpty(settings.gptSovitsRefAudioBaseDir))
            gptSovitsRefAudioBaseDir = settings.gptSovitsRefAudioBaseDir;

        translationEnabled = settings.translationEnabled;

        if (!string.IsNullOrEmpty(settings.translationUrl))
            translation_url = settings.translationUrl;

        if (!string.IsNullOrEmpty(settings.translationAppId))
            translation_app_id = settings.translationAppId;

        if (!string.IsNullOrEmpty(settings.translationKey))
            translation_key = settings.translationKey;

        if (!string.IsNullOrEmpty(settings.translationSalt))
            translation_salt = settings.translationSalt;

        if (!string.IsNullOrEmpty(settings.identity))
            identity = settings.identity;

        if (!string.IsNullOrEmpty(settings.preset))
            preset = settings.preset;
    }

    public void PopulateTo(SettingsData settings)
    {
        if (settings == null) return;

        settings.websocketUrl = websocket_url;
        settings.ttsMode = tts;
        settings.gptSovitsUrl = gptSovitsUrl;
        settings.gradioUrl = gradio_url;
        settings.simpleVitsUrl = simpleVitsApi_url;
        settings.gptSovitsRefAudioBaseDir = gptSovitsRefAudioBaseDir;
        settings.translationEnabled = translationEnabled;
        settings.translationUrl = translation_url;
        settings.translationAppId = translation_app_id;
        settings.translationKey = translation_key;
        settings.translationSalt = translation_salt;
        settings.identity = identity;
        settings.preset = preset;
    }
}
