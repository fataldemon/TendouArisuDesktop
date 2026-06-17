using System;
using System.Collections.Generic;
using UnityEngine;

public struct ResolvedClip
{
    public AnimationClip clip;
    public string bodyPart;
}

public class ActionGroupInstance
{
    public ActionGroupConfig config;
    public ActionGroupState state;
    public float stateTimer;
    public float holdTimer;
    public bool ttsStarted;
    public bool ttsEnded;
    public bool clipFinished;
    public bool suppressAutoEnd;
    public List<ResolvedClip> resolvedClips = new List<ResolvedClip>();

    public ActionGroupInstance(ActionGroupConfig config, List<ResolvedClip> clips)
    {
        this.config = config;
        this.resolvedClips = clips;
        state = ActionGroupState.Idle;
    }

    public ResolvedClip? GetClip(string bodyPart)
    {
        for (int i = 0; i < resolvedClips.Count; i++)
            if (resolvedClips[i].bodyPart == bodyPart) return resolvedClips[i];
        return null;
    }

    public bool ShouldEnd()
    {
        if (config.isIdle) return false;
        if (suppressAutoEnd) return false;

        if (!config.loop)
            return clipFinished;

        if (ttsEnded)
            return true;

        if (!ttsStarted && stateTimer > 1f)
            return holdTimer >= config.holdNoTTS;

        return false;
    }
}
