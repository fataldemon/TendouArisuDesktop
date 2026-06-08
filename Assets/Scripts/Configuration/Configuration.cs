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

    [SerializeField]
    public BertVits2 bertVits2_TTS;
    [SerializeField]
    public SimpleVitsApi simpleVitsApi_TTS;

    public string gradio_url;
    public string simpleVitsApi_url;

    public void initConfiguration(string _websocket_url, int _tts, string _translation_url, 
        string _translation_app_id, string _translation_key, string _translation_salt, string _identity, string _preset)
    { 
        websocket_url = _websocket_url;
        tts = _tts;
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
            return bertVits2_TTS;
        }
        else if (index == 1)
        {
            return simpleVitsApi_TTS;
        }
        else return null;
    }

    public void setTTSUrl(int index)
    {
        if (index == 0)
        {
            bertVits2_TTS.PostURL = gradio_url;
        }
        else if (index == 1)
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

        if (!string.IsNullOrEmpty(settings.gradioUrl))
            gradio_url = settings.gradioUrl;

        if (!string.IsNullOrEmpty(settings.simpleVitsUrl))
            simpleVitsApi_url = settings.simpleVitsUrl;

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
        settings.gradioUrl = gradio_url;
        settings.simpleVitsUrl = simpleVitsApi_url;
        settings.translationUrl = translation_url;
        settings.translationAppId = translation_app_id;
        settings.translationKey = translation_key;
        settings.translationSalt = translation_salt;
        settings.identity = identity;
        settings.preset = preset;
    }
}
