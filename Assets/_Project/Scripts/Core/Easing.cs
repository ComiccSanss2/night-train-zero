using UnityEngine;

namespace Subnet.Core
{
    /// <summary>Easing curves used for UI tweens and camera moves (no DOTween dependency).</summary>
    public enum EaseType
    {
        Linear,
        SmoothStep,
        SmootherStep,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic,
        EaseOutBack,
        EaseInBack
    }

    public static class Easing
    {
        /// <summary>Overshoot amount used by the "Back" curves (1.70158 is the classic Penner value).</summary>
        public const float BackOvershoot = 1.70158f;

        /// <summary>Evaluates <paramref name="type"/> at normalized time <paramref name="t"/> (clamped 0..1).</summary>
        public static float Evaluate(EaseType type, float t)
        {
            t = Mathf.Clamp01(t);
            switch (type)
            {
                case EaseType.SmoothStep: return t * t * (3f - 2f * t);
                case EaseType.SmootherStep: return t * t * t * (t * (t * 6f - 15f) + 10f);
                case EaseType.EaseInCubic: return t * t * t;
                case EaseType.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.EaseInOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case EaseType.EaseOutBack:
                {
                    const float c3 = BackOvershoot + 1f;
                    float u = t - 1f;
                    return 1f + c3 * u * u * u + BackOvershoot * u * u;
                }
                case EaseType.EaseInBack:
                {
                    const float c3 = BackOvershoot + 1f;
                    return c3 * t * t * t - BackOvershoot * t * t;
                }
                default: return t;
            }
        }
    }
}
