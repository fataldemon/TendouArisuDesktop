using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlinkController : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public int blinkBlendIndex;
    public float blinkWeight = 0.0f;
    public float blinkDuration = 0.2f;
    public float blinkInterval = 3.0f;
    [HideInInspector] public bool suppressed;
    public List<int> blinkConflictIndices = new List<int>();

    private float blinkTimer = 0.0f;

    public void ApplyEyeProfile(ModelEyeProfile eyeProfile)
    {
        if (eyeProfile == null) return;
        blinkBlendIndex = eyeProfile.blinkIndex;
        blinkConflictIndices = eyeProfile.blinkConflictIndices ?? new List<int>();
        if (eyeProfile.lookStrength > 0)
            LookStrength = eyeProfile.lookStrength;
        if (eyeProfile.headRotationAmount > 0)
            HeadRotationAmount = eyeProfile.headRotationAmount;
    }

    public float LookStrength = 120f;
    public float HeadRotationAmount = 10f;

    void Start()
    {
        skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, blinkWeight);
    }

    void Update()
    {
        if (suppressed)
        {
            blinkTimer = 0f;
            StopAllCoroutines();
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, blinkWeight);
            return;
        }

        blinkTimer += Time.deltaTime;

        if (blinkTimer >= blinkInterval)
        {
            bool eyeBusy = false;
            if (blinkConflictIndices != null)
            {
                for (int i = 0; i < blinkConflictIndices.Count; i++)
                {
                    if (skinnedMeshRenderer.GetBlendShapeWeight(blinkConflictIndices[i]) > 0.1f)
                        { eyeBusy = true; break; }
                }
            }
            if (!eyeBusy)
        {
            StartCoroutine(BlinkCoroutine());
            blinkTimer = 0.0f;
        }
    }

    IEnumerator BlinkCoroutine()
    {
        for (float t = 0.0f; t < blinkDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(blinkWeight, 100.0f, t / blinkDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight);
            yield return null;
        }

        for (float t = 0.0f; t < blinkDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(100.0f, blinkWeight, t / blinkDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight);
            yield return null;
        }

        skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, blinkWeight);
    }
}
