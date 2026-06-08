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

    private float blinkTimer = 0.0f;

    void Start()
    {
        skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, blinkWeight);
    }

    void Update()
    {
        blinkTimer += Time.deltaTime;

        if (blinkTimer >= blinkInterval && skinnedMeshRenderer.GetBlendShapeWeight(11) == 0 
            && skinnedMeshRenderer.GetBlendShapeWeight(35) == 0 && skinnedMeshRenderer.GetBlendShapeWeight(16) == 0
            && skinnedMeshRenderer.GetBlendShapeWeight(12) == 0 && skinnedMeshRenderer.GetBlendShapeWeight(17) == 0
            && skinnedMeshRenderer.GetBlendShapeWeight(13) == 0 && skinnedMeshRenderer.GetBlendShapeWeight(18) == 0)
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
