using UnityEngine;

public class EyeTrackingController : MonoBehaviour
{
    public SkinnedMeshRenderer meshRenderer;
    public BodyEngine bodyEngine;
    public PreviewController previewController;

    public float lookStrength = 120f;
    public float headRotationAmount = 10f;

    public int lookLeftBlendIndex = 30;
    public int lookRightBlendIndex = 31;
    public int lookUpBlendIndex = 28;
    public int lookDownBlendIndex = 29;

    private float _currentX;
    private float _currentY;
    private Quaternion _headDefaultRot = Quaternion.identity;
    private bool _wasInAction;
    private float _headBlendOut = 1f;
    public bool expressionActive;

    void Start()
    {
        StartCoroutine(SaveDefaultRot());
    }

    System.Collections.IEnumerator SaveDefaultRot()
    {
        yield return null;
        if (bodyEngine != null && bodyEngine.animator != null)
        {
            var head = bodyEngine.animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) _headDefaultRot = head.localRotation;
        }
    }

    void Update()
    {
        if (meshRenderer == null) return;
        if (bodyEngine == null || bodyEngine.animator == null || Camera.main == null) return;

        bool inAction = expressionActive;
        if (Time.frameCount % 120 == 0)
            Debug.Log("[EyeTracking] inAction: preview=" + (previewController != null && previewController.IsPreviewing) + " exprActive=" + expressionActive);

        if (inAction)
        {
            _currentX = Mathf.Lerp(_currentX, 0f, Time.deltaTime * 12f);
            _currentY = Mathf.Lerp(_currentY, 0f, Time.deltaTime * 12f);
            _wasInAction = true;
        }
        else
        {
            if (_wasInAction) { _currentX = 0f; _currentY = 0f; _wasInAction = false; }

            var head = bodyEngine.animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            Vector3 headScreen = Camera.main.WorldToScreenPoint(head.position);
            float headWinX = headScreen.x;
            float headWinY = Screen.height - headScreen.y;

            Vector3 mp = Input.mousePosition;
            float dx = mp.x - headWinX;
            float dy = headWinY - mp.y;

            float targetX = (dx / (Screen.width * 0.5f)) * lookStrength;
            float targetY = (dy / (Screen.height * 0.5f)) * lookStrength;

            _currentX = Mathf.Lerp(_currentX, targetX, Time.deltaTime * 8f);
            _currentY = Mathf.Lerp(_currentY, targetY, Time.deltaTime * 8f);
        }

        float left = Mathf.Max(0f, _currentX);
        float right = Mathf.Max(0f, -_currentX);
        float up = Mathf.Max(0f, _currentY);
        float down = Mathf.Max(0f, -_currentY);
        ApplyEyeWeights(left, right, up, down);
    }

    void LateUpdate()
    {
        if (meshRenderer == null || bodyEngine == null || bodyEngine.animator == null) return;

        bool headIdle = !expressionActive;

        _headBlendOut = Mathf.Lerp(_headBlendOut, headIdle ? 1f : 0f, Time.deltaTime * 10f);
        if (_headBlendOut < 0.01f) return;

        var head = bodyEngine.animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Vector3 targetRot = new Vector3(_currentY / lookStrength * headRotationAmount, -_currentX / lookStrength * headRotationAmount, 0f);
        Quaternion target = _headDefaultRot * Quaternion.Euler(targetRot);
        Quaternion animPose = head.localRotation;
        head.localRotation = Quaternion.Slerp(animPose, target, _headBlendOut);
    }

    private void ApplyEyeWeights(float left, float right, float up, float down)
    {
        if (meshRenderer == null) return;
        meshRenderer.SetBlendShapeWeight(lookLeftBlendIndex, left);
        meshRenderer.SetBlendShapeWeight(lookRightBlendIndex, right);
        meshRenderer.SetBlendShapeWeight(lookUpBlendIndex, up);
        meshRenderer.SetBlendShapeWeight(lookDownBlendIndex, down);
    }
}
