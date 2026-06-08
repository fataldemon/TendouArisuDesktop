using UnityEngine;
using System.Collections;

public class AudioMouthController : MonoBehaviour
{
    public SkinnedMeshRenderer meshRenderer;
    public int blendShapeIndex;
    public float blendWeightMultiplier = 100f;
    public float smoothTime = 0.1f;

    [SerializeField]private AudioSource audioSource;

    private float blendWeight;
    private float blendWeightVelocity;

    void Update()
    {
        if (audioSource.isPlaying)
        {
            float amplitude = GetAmplitude();
            blendWeight = Mathf.SmoothDamp(blendWeight, amplitude * blendWeightMultiplier, ref blendWeightVelocity, smoothTime);
            meshRenderer.SetBlendShapeWeight(blendShapeIndex, blendWeight);
        }
        else
        {
            blendWeight = Mathf.SmoothDamp(blendWeight, 0f, ref blendWeightVelocity, smoothTime);
            meshRenderer.SetBlendShapeWeight(blendShapeIndex, blendWeight);
        }
    }

    float GetAmplitude()
    {
        float[] samples = new float[512];
        audioSource.GetOutputData(samples, 0);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        return sum / samples.Length;
    }
}
