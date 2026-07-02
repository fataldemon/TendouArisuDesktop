using UnityEngine;
using MagicaCloth2;

/// <summary>
/// Subtle procedural breathing via small chest rotation (ribcage pitch).
///
/// CRITICAL timing (the hard-won lesson):
///   MagicaCloth2 (hair/skirt mesh cloth) samples bones via ReadTransform() inside
///   ClothManager.ClothUpdate(), which runs in the PreLateUpdate PlayerLoop phase --
///   BEFORE MonoBehaviour.LateUpdate. So any bone rotation done in LateUpdate is applied
///   AFTER the cloth sampled -> mesh desyncs from bones -> cloth loses anchor -> hair flows
///   downward continuously (looked like the hair was melting/sliding off).
///
///   Fix: apply the chest rotation in MagicaManager.OnPreSimulation (public static Action,
///   MagicaManagerAPI.cs:17). It is invoked at the START of ClothUpdate, RIGHT BEFORE
///   ReadTransform samples the bones. So the cloth always sees the breathed Chest pose.
///
///   No neutralize needed: the Humanoid PlayableGraph writes Chest every frame (before
///   OnPreSimulation), so last frame's additive is naturally reset; we just add fresh each
///   time. No accumulation.
///
/// Breathing turns off while an action is playing (EmotionPlayer.IsPlaying).
/// </summary>
public class IdleBreathingController : MonoBehaviour
{
    [Header("References")]
    public BodyEngine bodyEngine;
    public EmotionPlayer emotionPlayer;

    [Header("Breathing")]
    [Tooltip("Max chest pitch on inhale, degrees. ~1-2 subtle, 3-5 visible.")]
    [Range(0f, 6f)] public float chestPitchDegrees = 2.5f;
    [Tooltip("UpperChest follows Chest with this fraction (0 = chest only).")]
    [Range(0f, 1f)] public float upperChestFraction = 0.6f;
    [Range(0.05f, 1f)] public float frequency = 0.25f;   // ~0.25 Hz = ~15 breaths/min
    public float fadeSpeed = 4f;
    [Tooltip("Flip if inhale sinks the chest instead of lifting it.")]
    public bool invertPitch = false;

    private Animator _anim;
    private Animator _cachedForBones;
    private Transform _chest, _upperChest;

    private float _weight;
    private float _phase;

    private void OnEnable()
    {
        // Subscribe so the chest rotation runs immediately before MagicaCloth2 samples bones.
        MagicaManager.OnPreSimulation += ApplyBreathing;
    }

    private void OnDisable()
    {
        MagicaManager.OnPreSimulation -= ApplyBreathing;
    }

    private void Update()
    {
        EnsureBones();
        // advance phase continuously so breathing resumes mid-cycle after an action ends
        _phase += Time.deltaTime * frequency * 2f * Mathf.PI;

        bool idle = emotionPlayer == null || !emotionPlayer.IsPlaying;
        _weight = SpringMath.Damp(_weight, idle ? 1f : 0f, fadeSpeed, Time.deltaTime);
    }

    /// <summary>
    /// Called by MagicaCloth2 right before it samples bones. Applying the chest rotation
    /// here guarantees the cloth sees the breathed pose (no desync -> no hair flow).
    /// </summary>
    private void ApplyBreathing()
    {
        if (_anim == null || _chest == null) return;
        if (_weight < 0.001f) return;

        float sign = invertPitch ? 1f : -1f;
        float breath = Mathf.Sin(_phase) * _weight;

        // additive on top of the anim pose the graph just wrote; graph resets it next frame
        Quaternion addChest = Quaternion.Euler(sign * chestPitchDegrees * breath, 0f, 0f);
        _chest.localRotation = _chest.localRotation * addChest;

        if (_upperChest != null && upperChestFraction > 0f)
        {
            Quaternion addUC = Quaternion.Euler(sign * chestPitchDegrees * upperChestFraction * breath, 0f, 0f);
            _upperChest.localRotation = _upperChest.localRotation * addUC;
        }
    }

    private void EnsureBones()
    {
        Animator a = (bodyEngine != null) ? bodyEngine.animator : null;
        if (a == null) return;
        if (_anim != a) { _anim = a; _cachedForBones = null; _chest = null; }
        if (_cachedForBones != _anim || _chest == null)
        {
            _chest = _anim.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = _anim.GetBoneTransform(HumanBodyBones.UpperChest);
            _cachedForBones = _anim;
        }
    }
}
