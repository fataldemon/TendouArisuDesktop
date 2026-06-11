using UnityEngine;

public class EyeTrackingController : MonoBehaviour
{
    public FacialController facialController;
    public ActionController actionController;
    public AnimationLibrary animLibrary;

    public float lookStrength = 120f;
    public float headRotationAmount = 10f;

    private float _currentX;
    private float _currentY;
    private Quaternion _headDefaultRot = Quaternion.identity;
    private bool _wasInAction;
    private float _headBlendOut = 1f;
    public bool expressionActive;

    void Start()
    {
        if (facialController == null) facialController = GetComponent<FacialController>();
        if (actionController == null) actionController = GetComponent<ActionController>();
        StartCoroutine(SaveDefaultRot());
    }

    System.Collections.IEnumerator SaveDefaultRot()
    {
        yield return null;
        var head = actionController?.animator?.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) _headDefaultRot = head.localRotation;
    }

    void Update()
    {
        if (facialController == null || facialController.skinnedMeshRenderer == null) return;
        if (actionController?.animator == null || Camera.main == null) return;

        bool inAction = (animLibrary != null && animLibrary.IsPreviewing)
            || actionController.animator.GetInteger("action_param") >= 1
            || actionController.animator.GetInteger("onWaiting") > 0
            || actionController.animator.GetBool("onAction")
            || expressionActive;

        if (inAction)
        {
            _currentX = Mathf.Lerp(_currentX, 0f, Time.deltaTime * 12f);
            _currentY = Mathf.Lerp(_currentY, 0f, Time.deltaTime * 12f);
            _wasInAction = true;
        }
        else
        {
            if (_wasInAction) { _currentX = 0f; _currentY = 0f; _wasInAction = false; }

            var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
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
        if (facialController == null || actionController?.animator == null) return;

        bool headIdle = actionController.animator.GetInteger("action_param") == 0
            && actionController.animator.GetInteger("onWaiting") == 0
            && !actionController.animator.GetBool("onAction")
            && (animLibrary == null || !animLibrary.IsPreviewing)
            && !expressionActive;

        _headBlendOut = Mathf.Lerp(_headBlendOut, headIdle ? 1f : 0f, Time.deltaTime * 10f);
        if (_headBlendOut < 0.01f) return;

        var head = actionController.animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Vector3 targetRot = new Vector3(_currentY / lookStrength * headRotationAmount, -_currentX / lookStrength * headRotationAmount, 0f);
        Quaternion target = _headDefaultRot * Quaternion.Euler(targetRot);
        Quaternion animPose = head.localRotation;
        head.localRotation = Quaternion.Slerp(animPose, target, _headBlendOut);
    }

    private void ApplyEyeWeights(float left, float right, float up, float down)
    {
        var smr = facialController.skinnedMeshRenderer;
        if (smr == null) return;
        smr.SetBlendShapeWeight(facialController.lookLeftBlendIndex, left);
        smr.SetBlendShapeWeight(facialController.lookRightBlendIndex, right);
        smr.SetBlendShapeWeight(facialController.lookUpBlendIndex, up);
        smr.SetBlendShapeWeight(facialController.lookDownBlendIndex, down);
    }
}
