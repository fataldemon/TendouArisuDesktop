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

    private List<int> restoreBlendIndexList = new List<int>(); // 用于记录各种表情修改了哪些index
    private List<float> restoreFromWeightList = new List<float>(); //用于记录各种表情修改的index的最终权重

    // Start is called before the first frame update
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

    // Update is called once per frame
    void Update()
    {
        
    }

    //设置透明度
    public void SetShyBlush()
    {
        MeshRenderer blushRender1 = Blush1.GetComponent<MeshRenderer>();
        MeshRenderer blushRender2 = Blush2.GetComponent<MeshRenderer>();
        blushRender1.material = shy;
        blushRender2.material = shy;
    }

    //设置透明度
    public void SetNormalBlush()
    {
        MeshRenderer blushRender1 = Blush1.GetComponent<MeshRenderer>();
        MeshRenderer blushRender2 = Blush2.GetComponent<MeshRenderer>();
        blushRender1.material = blush;
        blushRender2.material = blush;
    }

    public void PerformExpression(string expression, Action<bool> _callback)
    {
        switch (expression)
        {
            case "angry":
                StartCoroutine(AngryCoroutine());
                break;
            case "serious":
                StartCoroutine(SeriousCoroutine());
                break;
            case "happy":
                StartCoroutine(HappyCoroutine());
                break;
            case "fun":
                StartCoroutine(FunCoroutine());
                break;
            case "panic":
                StartCoroutine(PanicCoroutine());
                break;
            case "curious":
                StartCoroutine(CuriousCoroutine());
                break;
            case "thinking":
                StartCoroutine(ThinkingCoroutine());
                break;
            case "disappointed":
                StartCoroutine(DisappointedCoroutine());
                break;
            case "sweating":
                StartCoroutine(SweatingCoroutine());
                break;
            case "confident":
                StartCoroutine(ConfidentCoroutine());
                break;
            case "cry":
                StartCoroutine(CryCoroutine());
                break;
            case "plain":
                StartCoroutine(PlainCoroutine());
                break;
            case "shy":
                StartCoroutine(ShyCoroutine());
                break;
            case "touching":
                StartCoroutine(TouchingCoroutine());
                break;
            case "wink":
                StartCoroutine(WinkCoroutine());
                break;

            case "restore":
                StartCoroutine(RollbackCoroutine(_callback));
                break;
        }
    }

    #region 实现动画的代码
    IEnumerator AngryCoroutine() 
    {
        Debug.Log("当前表情：生气。");
        // 实现生气的表情动画（认真+嘟嘴）
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(cheekBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(angryBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(cheekBlendIndex);
        restoreFromWeightList.Add(100f);
        
    }

    IEnumerator SeriousCoroutine()
    {
        Debug.Log("当前表情：认真。");
        // 将认真表情的权重值逐渐变为100，然后再逐渐恢复为0，实现认真的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(angryBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(angryBlendIndex);
        restoreFromWeightList.Add(100f);
        
    }

    IEnumerator HappyCoroutine()
    {
        Debug.Log("当前表情：开心。");
        // 将开心表情的权重值逐渐变为100，然后再逐渐恢复为0，实现开心的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(joyBlendIndex);
        restoreFromWeightList.Add(100f);
        
    }

    IEnumerator FunCoroutine()
    {
        Debug.Log("当前表情：高兴。");
        // 将高兴表情的权重值逐渐变为80，然后再逐渐恢复为0，实现高兴的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, weight*0.5f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(funBlendIndex);
        restoreFromWeightList.Add(50f);
        
    }

    IEnumerator PanicCoroutine()
    {
        Debug.Log("当前表情：慌乱。");
        // 实现慌乱的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, weight/2);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight*0.928f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthABlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(mouthEBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f);           
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(92.8f);
        Sweat1.SetActive(true);
        Sweat2.SetActive(true);
    }

    IEnumerator CuriousCoroutine()
    {
        Debug.Log("当前表情：好奇。");
        // 实现好奇的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 50.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthSorrowBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthUBlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(mouthSorrowBlendIndex);
        restoreFromWeightList.Add(50f);
        
    }

    IEnumerator ThinkingCoroutine()
    {
        Debug.Log("当前表情：思考。");
        // 实现思考的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthUBlendIndex, weight*0.3f);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight * 0.75f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyBlendIndex, weight * 0.5f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(100f);          
        restoreBlendIndexList.Add(mouthUBlendIndex);
        restoreFromWeightList.Add(30f);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(75f);
        restoreBlendIndexList.Add(eyeJoyBlendIndex);
        restoreFromWeightList.Add(50f);
        
    }

    IEnumerator DisappointedCoroutine()
    {
        Debug.Log("当前表情：失望。");
        // 实现失望的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, weight * 0.8f);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(80f);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        Sweat2.SetActive(true);
    }

    IEnumerator SweatingCoroutine()
    {
        Debug.Log("当前表情：汗颜。");
        // 实现汗颜的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthABlendIndex, weight / 2);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthEBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthABlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(mouthEBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        Sweat2.SetActive(true);
    }

    IEnumerator ConfidentCoroutine()
    {
        Debug.Log("当前表情：自信。");
        // 实现自信的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, weight / 2);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(50f);

    }

    IEnumerator CryCoroutine()
    {
        Debug.Log("当前表情：委屈。");
        // 实现委屈的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, weight*0.8f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkBlendIndex, weight*0.1f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthIBlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(blinkBlendIndex);
        restoreFromWeightList.Add(10f);
        Tear1.SetActive(true);
        Tear2.SetActive(true);
    }

    IEnumerator PlainCoroutine()
    {
        Debug.Log("当前表情：面无表情。");
        // 实现面无表情的动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, weight * 0.5f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(50f);

    }

    IEnumerator ShyCoroutine()
    {
        Debug.Log("当前表情：害羞。");
        // 实现害羞的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthIBlendIndex, weight * 0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(mouthAngryBlendIndex, weight * 0.35f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(lookDownBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(lookLeftBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(mouthAngryBlendIndex);
        restoreFromWeightList.Add(35f);
        restoreBlendIndexList.Add(eyeSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(lookDownBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(lookLeftBlendIndex);
        restoreFromWeightList.Add(100f);
        SetShyBlush();
    }

    IEnumerator TouchingCoroutine()
    {
        Debug.Log("当前表情：感动。");
        // 实现感动的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(joyBlendIndex, weight);
            skinnedMeshRenderer.SetBlendShapeWeight(browSorrowBlendIndex, weight);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(joyBlendIndex);
        restoreFromWeightList.Add(100f);
        restoreBlendIndexList.Add(browSorrowBlendIndex);
        restoreFromWeightList.Add(100f);
        Tear1_Joy.SetActive(true);
        Tear2_Joy.SetActive(true);
    }

    IEnumerator WinkCoroutine()
    {
        Debug.Log("当前表情：眨眼。");
        // 实现眨眼的表情动画
        for (float t = 0.0f; t < transformDuration; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0.0f, 100.0f, t / transformDuration);
            skinnedMeshRenderer.SetBlendShapeWeight(funBlendIndex, weight*0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(blinkRightBlendIndex, weight*0.5f);
            skinnedMeshRenderer.SetBlendShapeWeight(eyeJoyRightBlendIndex, weight*0.5f);
            yield return null;
        }
        //记录需要恢复的值
        restoreBlendIndexList.Add(funBlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(blinkRightBlendIndex);
        restoreFromWeightList.Add(50f);
        restoreBlendIndexList.Add(eyeJoyRightBlendIndex);
        restoreFromWeightList.Add(50f);

    }

    IEnumerator RollbackCoroutine(Action<bool> _callback)
    {
        Debug.Log("回退当前表情。");
        // 实现回退表情动画
        Debug.Log($"回退目录数：{restoreBlendIndexList.Count}");
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
        Debug.Log($"回退完成");
        _callback(false);
    }
    #endregion
}
