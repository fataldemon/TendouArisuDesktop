using UnityEngine;

/// <summary>
/// Frame-rate independent smoothing utilities for procedural animation.
/// Spring ported from Mate-Engine AvatarSwayController; Damp replaces the
/// framerate-dependent Mathf.Lerp(a, b, dt*k) pattern (which runs faster on
/// high-refresh displays). All methods are framerate-independent.
/// </summary>
public static class SpringMath
{
    /// <summary>
    /// Critically-damped spring towards a target (semi-implicit Euler).
    /// freq   = oscillation frequency in Hz (higher = snappier).
    /// damping = damping ratio (0 = undamped/oscillates, 1 = critical, >1 = overdamped).
    /// ~0.3-0.5 gives a lively settle with slight overshoot; 1 = no overshoot.
    /// </summary>
    public static void Spring(ref float x, ref float v, float target, float freq, float damping, float dt)
    {
        float w = Mathf.Max(0.01f, freq) * 2f * Mathf.PI;
        float a = w * w * (target - x) - 2f * damping * w * v;
        v += a * dt;
        x += v * dt;
    }

    /// <summary>
    /// Frame-rate independent exponential smoothing of a scalar.
    /// lambda = convergence rate (higher = faster). At any framerate this converges
    /// to ~63% of the remaining distance after 1/lambda seconds.
    /// Replaces: Mathf.Lerp(current, target, Time.deltaTime * k)  ->  Damp(current, target, k, dt)
    /// </summary>
    public static float Damp(float current, float target, float lambda, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * Mathf.Max(0f, dt)));
    }

    /// <summary>Frame-rate independent angle (degrees) smoothing, handles wraparound.</summary>
    public static float DampAngle(float current, float target, float lambda, float dt)
    {
        return Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-lambda * Mathf.Max(0f, dt)));
    }

    /// <summary>Frame-rate independent Vector2 smoothing.</summary>
    public static Vector2 Damp(Vector2 current, Vector2 target, float lambda, float dt)
    {
        float t = 1f - Mathf.Exp(-lambda * Mathf.Max(0f, dt));
        return Vector2.Lerp(current, target, t);
    }

    /// <summary>Frame-rate independent Vector3 smoothing.</summary>
    public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt)
    {
        float t = 1f - Mathf.Exp(-lambda * Mathf.Max(0f, dt));
        return Vector3.Lerp(current, target, t);
    }

    /// <summary>Frame-rate independent Quaternion smoothing (spherical).</summary>
    public static Quaternion Damp(Quaternion current, Quaternion target, float lambda, float dt)
    {
        float t = 1f - Mathf.Exp(-lambda * Mathf.Max(0f, dt));
        return Quaternion.Slerp(current, target, t);
    }
}
