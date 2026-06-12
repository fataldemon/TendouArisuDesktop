using System;
using UnityEngine;

public class ActionGroupInstance
{
    public ActionGroupConfig config;
    public ActionGroupState state;
    public float stateTimer;
    public float holdTimer;
    public bool ttsStarted;
    public bool ttsEnded;
    public bool clipFinished;
    public AnimationClip resolvedClip;

    public ActionGroupInstance(ActionGroupConfig config, AnimationClip clip)
    {
        this.config = config;
        this.resolvedClip = clip;
        state = ActionGroupState.Idle;
    }

    public bool ShouldEnd()
    {
        if (config.isIdle) return false;

        if (!config.loop)
            return clipFinished;

        if (ttsEnded)
            return holdTimer >= config.holdAfterTTS;

        if (!ttsStarted && stateTimer > 1f)
            return holdTimer >= config.holdNoTTS;

        return false;
    }
}
