using UnityEngine;

/// <summary>
/// Procedural inertia lean: while the desktop window is being dragged, the whole
/// avatar tilts in the drag direction (rigid root rotation), then springs back.
///
/// Implementation note (the hard-won lesson):
///   The first attempt rotated the Hips BONE in LateUpdate. But MagicaCloth2 (hair/skirt
///   mesh cloth) samples bone positions in a PlayerLoop hook BEFORE MonoBehaviour.LateUpdate,
///   so the Hips rotation was applied AFTER the cloth sampled -> mesh desynced from bones ->
///   hair flowed/stretched downward continuously.
///   Fix: rotate the ROOT Transform instead. MagicaCloth2 treats root movement as character
///   motion (its designed input) -> cloth lags behind naturally and sways. No internal bone
///   manipulation -> no desync. Works for arbitrarily long drags and alongside the drag anim.
///
/// Ported spring math from Mate-Engine AvatarSwayController.
/// </summary>
public class DragInertiaController : MonoBehaviour
{
    [Header("References")]
    public BodyEngine bodyEngine;
    public TransparentWindow transparentWindow;

    [Header("Input")]
    [Tooltip("Mouse-delta scale used as drag-velocity fallback in the Editor (no real window drag there).")]
    public float mouseSensitivity = 0.6f;
    public bool invertHorizontal = false;
    public bool invertVertical = false;

    [Header("Sway Physics")]
    public float horizontalVelocityToLean = 0.25f;
    public float verticalVelocityToPitch = 0.15f;
    public float maxLeanZ = 10f;
    public float maxLeanX = 5f;
    public float springFrequency = 1.5f;
    [Range(0f, 2f)] public float dampingRatio = 0.6f;
    public float blendSpeed = 8f;

    private Animator _anim;
    private Quaternion _baseRootRot;
    private bool _baseCached;

    private float _leanZ, _leanZVel;
    private float _leanX, _leanXVel;
    private float _effectWeight;

    private Vector2 _filteredDelta;
    private Vector2 _prevMousePos;
    private Vector2 _prevWinPos;
    private bool _prevWinValid;

    private void Awake()
    {
        _prevMousePos = Input.mousePosition;
    }

    private void OnDisable()
    {
        // restore the base rotation so the avatar stands straight when this component is off
        if (_anim != null && _baseCached)
            _anim.transform.rotation = _baseRootRot;
        _effectWeight = 0f;
    }

    private void Update()
    {
        EnsureAnim();
        if (_anim == null) return;

        bool active = IsDragging();
        float dt = Time.deltaTime;
        Vector2 delta = Vector2.zero;

#if !UNITY_EDITOR
        if (transparentWindow != null)
        {
            Vector2 cur = new Vector2(transparentWindow.currentX, transparentWindow.currentY);
            if (_prevWinValid) delta = active ? (cur - _prevWinPos) : Vector2.zero;
            _prevWinPos = cur;
            _prevWinValid = true;
        }
#endif
        if (delta == Vector2.zero && active)
        {
            Vector2 m = Input.mousePosition;
            delta = (m - _prevMousePos) * mouseSensitivity;
            _prevMousePos = m;
        }
        else
        {
            _prevMousePos = Input.mousePosition;
        }

        _filteredDelta = SpringMath.Damp(_filteredDelta, delta, 12f, dt);

        float signH = invertHorizontal ? -1f : 1f;
        float signV = invertVertical ? -1f : 1f;
        float targetLeanZ = Mathf.Clamp(signH * _filteredDelta.x * horizontalVelocityToLean, -maxLeanZ, maxLeanZ);
        float targetLeanX = Mathf.Clamp(signV * _filteredDelta.y * verticalVelocityToPitch, -maxLeanX, maxLeanX);

        SpringMath.Spring(ref _leanZ, ref _leanZVel, active ? targetLeanZ : 0f, springFrequency, dampingRatio, dt);
        SpringMath.Spring(ref _leanX, ref _leanXVel, active ? targetLeanX : 0f, springFrequency, dampingRatio, dt);

        float outSpeed = active ? blendSpeed : blendSpeed * 2f;
        _effectWeight = Mathf.MoveTowards(_effectWeight, active ? 1f : 0f, outSpeed * dt);

        // Apply root rotation HERE (Update), not LateUpdate. MagicaCloth2 samples transforms
        // at afterUpdate/beforeLateUpdate -- BEFORE MonoBehaviour.LateUpdate. Anything changed
        // in LateUpdate is invisible to the cloth (desync -> hair loses anchor -> flows down).
        // Since BodyEngine.allowRootMotion=false, the PlayableGraph does NOT overwrite the root
        // transform, so an Update-time rotation persists into MagicaCloth2's sample. Correct.
        if (_anim != null && _baseCached)
        {
            float xH = _leanX * _effectWeight;
            float zH = _leanZ * _effectWeight;
            _anim.transform.rotation = _baseRootRot * Quaternion.Euler(xH, 0f, zH);
        }
    }

    private bool IsDragging()
    {
        if (transparentWindow != null && transparentWindow.IsDraggingWindow) return true;
#if UNITY_EDITOR
        // Editor has no real OS-window drag; hold right mouse + move to test the lean.
        return Input.GetMouseButton(1);
#else
        return false;
#endif
    }

    private void EnsureAnim()
    {
        Animator a = (bodyEngine != null) ? bodyEngine.animator : null;
        if (_anim != a)
        {
            _anim = a;
            _baseCached = false;
        }
        if (_anim != null && !_baseCached)
        {
            // cache the avatar's base (settings-driven) rotation; lean is applied on top of it
            _baseRootRot = _anim.transform.rotation;
            _baseCached = true;
        }
    }
}
