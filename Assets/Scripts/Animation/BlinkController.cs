using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlinkController : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public int blinkBlendIndex;
    public float blinkDuration = 0.2f;
    public float blinkInterval = 3.0f;
    public List<int> blinkConflictIndices = new List<int>();

    private float blinkTimer = 0.0f;

    public void ApplyEyeProfile(ModelEyeProfile eyeProfile)
    {
        if (eyeProfile == null) return;
        blinkBlendIndex = eyeProfile.blinkIndex;
        blinkConflictIndices = eyeProfile.blinkConflictIndices ?? new List<int>();
        if (eyeProfile.lookStrength >= 0)
            LookStrength = eyeProfile.lookStrength;
        if (eyeProfile.headRotationAmount >= 0)
            HeadRotationAmount = eyeProfile.headRotationAmount;
        ComputeConflictsFromMesh();
    }

    public float LookStrength = 120f;
    public float HeadRotationAmount = 10f;

    void Start()
    {
        skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, 0f);
        ComputeConflictsFromMesh();
    }

    public void ComputeConflictsFromMesh()
    {
        if (skinnedMeshRenderer == null || blinkBlendIndex < 0) return;
        var mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh == null || blinkBlendIndex >= mesh.blendShapeCount) return;

        var blinkDeltas = new Vector3[mesh.vertexCount];
        mesh.GetBlendShapeFrameVertices(blinkBlendIndex, 0, blinkDeltas, null, null);
        var blinkVerts = new HashSet<int>();
        for (int i = 0; i < blinkDeltas.Length; i++)
            if (blinkDeltas[i].sqrMagnitude > 1e-8f)
                blinkVerts.Add(i);

        if (blinkVerts.Count == 0) return;

        blinkConflictIndices.Clear();
        var deltas = new Vector3[mesh.vertexCount];
        float threshold = blinkVerts.Count * 0.3f;

        for (int bs = 0; bs < mesh.blendShapeCount; bs++)
        {
            if (bs == blinkBlendIndex) continue;
            System.Array.Clear(deltas, 0, deltas.Length);
            mesh.GetBlendShapeFrameVertices(bs, 0, deltas, null, null);

            int overlap = 0;
            foreach (int vi in blinkVerts)
                if (deltas[vi].sqrMagnitude > 1e-8f)
                    overlap++;

            if (overlap >= threshold)
                blinkConflictIndices.Add(bs);
        }

        Debug.Log("[BlinkController] Auto-computed " + blinkConflictIndices.Count +
                  " blink conflicts from " + mesh.blendShapeCount + " shapes" +
                  " (blinkVerts=" + blinkVerts.Count + ")");
    }

    void Update()
    {
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

            float currentWeight = skinnedMeshRenderer.GetBlendShapeWeight(blinkBlendIndex);
            if (!eyeBusy && currentWeight < 80f)
            {
                StartCoroutine(BlinkCoroutine(currentWeight));
            }
            blinkTimer = 0.0f;
        }
    }

    IEnumerator BlinkCoroutine(float baseWeight)
    {
        for (float t = 0.0f; t < blinkDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(baseWeight, 100.0f, t / blinkDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight);
            yield return null;
        }

        for (float t = 0.0f; t < blinkDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(100.0f, baseWeight, t / blinkDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight);
            yield return null;
        }

        skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, baseWeight);
    }
}
