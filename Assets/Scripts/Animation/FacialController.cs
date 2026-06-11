using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacialController : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public AudioSource m_AudioSource;
    public float transformDuration = 0.2f;
    public int mouthABlendIndex = 1;
    public int mouthIBlendIndex = 2;
    public int mouthUBlendIndex = 3;
    public int mouthEBlendIndex = 4;
    public int mouthOBlendIndex = 5;

    public int blinkBlendIndex = 11;
    public int blinkLeftBlendIndex = 12;
    public int blinkRightBlendIndex = 13;

    public int mouthAngryBlendIndex = 6;
    public int mouthSorrowBlendIndex = 9;
    public int mouthSurpriseBlendIndex = 10;

    public int eyeJoyBlendIndex = 16;
    public int eyeJoyLeftBlendIndex = 17;
    public int eyeJoyRightBlendIndex = 18;
    public int eyeSorrowBlendIndex = 19;
    public int eyeSurpriseBlendIndex = 20;
    public int browSorrowBlendIndex = 26;

    public int lookUpBlendIndex = 28;
    public int lookDownBlendIndex = 29;
    public int lookLeftBlendIndex = 30;
    public int lookRightBlendIndex = 31;

    public int angryBlendIndex = 33;
    public int funBlendIndex = 34;
    public int joyBlendIndex = 35;
    public int sorrowBlendIndex = 36;
    public int surprisedBlendIndex = 37;
    public int cheekBlendIndex = 32;

    public GameObject Tear1;
    public GameObject Tear2;
    public GameObject Tear1_Joy;
    public GameObject Tear2_Joy;
    public GameObject Sweat1;
    public GameObject Sweat2;
    public GameObject Blush1;
    public GameObject Blush2;
    public Material blush;
    public Material shy;

    private List<int> restoreBlendIndexList = new List<int>();
    private List<float> restoreFromWeightList = new List<float>();

    void Start()
    {
        Tear1.SetActive(false); 
        Tear2.SetActive(false);
        Tear1_Joy.SetActive(false);
        Tear2_Joy.SetActive(false);
        Sweat1.SetActive(false); 
        Sweat2.SetActive(false);
        SetNormalBlush();
    }

    void Update()
    {
        
    }

    public void SetShyBlush()
    {
        MeshRenderer blushRender1 = Blush1.GetComponent<MeshRenderer>();
        MeshRenderer blushRender2 = Blush2.GetComponent<MeshRenderer>();
        blushRender1.material = shy;
        blushRender2.material = shy;
    }

    public void SetNormalBlush()
    {
        MeshRenderer blushRender1 = Blush1.GetComponent<MeshRenderer>();
        MeshRenderer blushRender2 = Blush2.GetComponent<MeshRenderer>();
        blushRender1.material = blush;
        blushRender2.material = blush;
    }

    public void PreviewBlendShape(string expression, float weight = 1f)
    {
        float w = Mathf.Clamp01(weight) * 100f;
        switch (expression)
        {
            case "angry":
                skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(cheekBlendIndex, w);
                break;
            case "serious":
                skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, w);
                break;
            case "happy":
                skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, w);
                break;
            case "fun":
                skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, w * 0.5f);
                break;
            case "panic":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.928f);
                Sweat1.SetActive(true); Sweat2.SetActive(true);
                break;
            case "curious":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(mouthSorrowBlendIndex, w * 0.5f);
                break;
            case "thinking":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, w * 0.3f);
                skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.75f);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyBlendIndex, w * 0.5f);
                break;
            case "disappointed":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.8f);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                Sweat2.SetActive(true);
                break;
            case "sweating":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                Sweat2.SetActive(true);
                break;
            case "confident":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w * 0.5f);
                break;
            case "cry":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w * 0.8f);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.1f);
                Tear1.SetActive(true); Tear2.SetActive(true);
                break;
            case "plain":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.5f);
                break;
            case "shy":
                skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.35f);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(lookDownBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(lookLeftBlendIndex, w);
                break;
            case "touching":
                skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, w);
                skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
                Tear1_Joy.SetActive(true); Tear2_Joy.SetActive(true);
                break;
            case "wink":
                skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(blinkRightBlendIndex, w * 0.5f);
                skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyRightBlendIndex, w * 0.5f);
                break;
        }
    }

    public void PerformExpression(string expression, Action<bool> _callback, float _weight = 1f)
    {
        switch (expression)
        {
            case "angry":
                StartCoroutine(AngryCoroutine(_weight));
                break;
            case "serious":
                StartCoroutine(SeriousCoroutine(_weight));
                break;
            case "happy":
                StartCoroutine(HappyCoroutine(_weight));
                break;
            case "fun":
                StartCoroutine(FunCoroutine(_weight));
                break;
            case "panic":
                StartCoroutine(PanicCoroutine(_weight));
                break;
            case "curious":
                StartCoroutine(CuriousCoroutine(_weight));
                break;
            case "thinking":
                StartCoroutine(ThinkingCoroutine(_weight));
                break;
            case "disappointed":
                StartCoroutine(DisappointedCoroutine(_weight));
                break;
            case "sweating":
                StartCoroutine(SweatingCoroutine(_weight));
                break;
            case "confident":
                StartCoroutine(ConfidentCoroutine(_weight));
                break;
            case "cry":
                StartCoroutine(CryCoroutine(_weight));
                break;
            case "plain":
                StartCoroutine(PlainCoroutine(_weight));
                break;
            case "shy":
                StartCoroutine(ShyCoroutine(_weight));
                break;
            case "touching":
                StartCoroutine(TouchingCoroutine(_weight));
                break;
            case "wink":
                StartCoroutine(WinkCoroutine(_weight));
                break;

            case "restore":
                StartCoroutine(RollbackCoroutine(_callback));
                break;
        }
    }

    #region Expression Coroutines
    IEnumerator AngryCoroutine(float _weight)
    {
        Debug.Log("Expression: angry");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(cheekBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(angryBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(cheekBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
    }

    IEnumerator SeriousCoroutine(float _weight)
    {
        Debug.Log("Expression: serious");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(angryBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
    }

    IEnumerator HappyCoroutine(float _weight)
    {
        Debug.Log("Expression: happy");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(joyBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
    }

    IEnumerator FunCoroutine(float _weight)
    {
        Debug.Log("Expression: fun");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, w * 0.5f);
            yield return null;
        }
        restoreBlendIndexList.Add(funBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator PanicCoroutine(float _weight)
    {
        Debug.Log("Expression: panic");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, w / 2);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.928f);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthABlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(mouthEBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(92.8f * _weight);
        Sweat1.SetActive(true);
        Sweat2.SetActive(true);
    }

    IEnumerator CuriousCoroutine(float _weight)
    {
        Debug.Log("Expression: curious");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 50.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthSorrowBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthUBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(mouthSorrowBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator ThinkingCoroutine(float _weight)
    {
        Debug.Log("Expression: thinking");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, w * 0.3f);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.75f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyBlendIndex, w * 0.5f);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(mouthUBlendIndex);
        restoreFromWeightList.Add(30f * _weight);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(75f * _weight);
        restoreBlendIndexList.Add(eyeJoyBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator DisappointedCoroutine(float _weight)
    {
        Debug.Log("Expression: disappointed");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.8f);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(80f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        Sweat2.SetActive(true);
    }

    IEnumerator SweatingCoroutine(float _weight)
    {
        Debug.Log("Expression: sweating");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, w / 2);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthABlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(mouthEBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        Sweat2.SetActive(true);
    }

    IEnumerator ConfidentCoroutine(float _weight)
    {
        Debug.Log("Expression: confident");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w / 2);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator CryCoroutine(float _weight)
    {
        Debug.Log("Expression: cry");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w * 0.8f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, w * 0.1f);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(80f * _weight);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(10f * _weight);
        Tear1.SetActive(true);
        Tear2.SetActive(true);
    }

    IEnumerator PlainCoroutine(float _weight)
    {
        Debug.Log("Expression: plain");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.5f);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator ShyCoroutine(float _weight)
    {
        Debug.Log("Expression: shy");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, w * 0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, w * 0.35f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(lookDownBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(lookLeftBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(35f * _weight);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(lookDownBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(lookLeftBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        SetShyBlush();
    }

    IEnumerator TouchingCoroutine(float _weight)
    {
        Debug.Log("Expression: touching");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, w);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, w);
            yield return null;
        }
        restoreBlendIndexList.Add(joyBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f * _weight);
        Tear1_Joy.SetActive(true);
        Tear2_Joy.SetActive(true);
    }

    IEnumerator WinkCoroutine(float _weight)
    {
        Debug.Log("Expression: wink");
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float w = Mathf.Lerp(0.0f, 100.0f, t / transformDuration) * _weight;
            skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, w * 0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkRightBlendIndex, w * 0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyRightBlendIndex, w * 0.5f);
            yield return null;
        }
        restoreBlendIndexList.Add(funBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(blinkRightBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
        restoreBlendIndexList.Add(eyeJoyRightBlendIndex);
        restoreFromWeightList.Add(50f * _weight);
    }

    IEnumerator RollbackCoroutine(Action<bool> _callback)
    {
        Debug.Log("Restoring expression.");
        Debug.Log("Restore count: " + restoreBlendIndexList.Count);
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            for (int i = 0; i < restoreBlendIndexList.Count; i++) 
            {
                int index = restoreBlendIndexList[i];
                float initialWeight = restoreFromWeightList[i];
                skinnedMeshRenderer.SetBlendShapeWeight(index, weight*initialWeight/100f);
            }
            yield return null;
        }

        for (int i = 0; i < restoreBlendIndexList.Count; i++)
        {
            int index = restoreBlendIndexList[i];
            skinnedMeshRenderer.SetBlendShapeWeight(index, 0.0f);
        }
        restoreBlendIndexList.Clear();
        restoreFromWeightList.Clear();
        Tear1.SetActive(false);
        Tear2.SetActive(false);
        Tear1_Joy.SetActive(false);
        Tear2_Joy.SetActive(false);
        Sweat1.SetActive(false);
        Sweat2.SetActive(false);
        SetNormalBlush();
        Debug.Log("Restore complete.");
        if (_callback != null) _callback(false);
    }

    public void ResetBlendShapesInstant()
    {
        Tear1.SetActive(false); Tear2.SetActive(false);
        Tear1_Joy.SetActive(false); Tear2_Joy.SetActive(false);
        Sweat1.SetActive(false); Sweat2.SetActive(false);
        SetNormalBlush();
        for (int i = 0; i < 39; i++)
            skinnedMeshRenderer.SetBlendShapeWeight(i, 0f);
        restoreBlendIndexList.Clear();
        restoreFromWeightList.Clear();
    }
    #endregion
}
