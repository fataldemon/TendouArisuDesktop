using UnityEngine;

public class AnimationBlender : MonoBehaviour
{
    public Animator animator;

    public void PlayAction(int actionParam)
    {
        if (animator == null) return;
        animator.SetInteger("action_param", actionParam);
    }

    public void RestoreToIdle()
    {
        if (animator == null) return;
        animator.SetInteger("action_param", 0);
    }

    public void PlayWaiting(int waitingType)
    {
        if (animator == null) return;
        animator.SetInteger("onWaiting", waitingType);
    }

    public bool IsInAction()
    {
        if (animator == null) return false;
        return animator.GetInteger("action_param") != 0
            || animator.GetInteger("onWaiting") != 0;
    }
}
