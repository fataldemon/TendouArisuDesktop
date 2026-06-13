using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActionGroupConfig
{
    public string groupName;
    public string facialPreset;
    public float facialWeight = 1f;
    public List<PartClipEntry> bodyClips = new List<PartClipEntry>();
    public bool loop;
    public float blendInBody = 0.35f;
    public float blendInFacial = 0.15f;
    public float blendOutBody = 0.35f;
    public float blendOutFacial = 0.2f;
    public float holdAfterTTS = 3f;
    public float holdNoTTS = 4f;
    public bool isIdle;
    public bool allowRootMotion;

    public AnimationClip GetBodyClip(string bodyPart)
    {
        for (int i = 0; i < bodyClips.Count; i++)
            if (bodyClips[i].bodyPart == bodyPart) return bodyClips[i].clip;
        return null;
    }
}

public enum ActionGroupState
{
    Idle,
    BlendingIn,
    Active,
    BlendingOut
}
