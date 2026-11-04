using System.Collections;
using UnityEngine;

namespace Subnet.Core
{
    /// <summary>
    /// Full-screen black fade used by the menu, cinematics and scene transitions.
    /// Put it on a stretched black Image with a CanvasGroup, on the top-most canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("Alpha the fader starts with when the scene loads (1 = fully black).")]
        [Range(0f, 1f)] [SerializeField] private float startAlpha = 1f;

        [Tooltip("If > 0, automatically fades from black to clear over this many seconds on Start (scenes without an intro cinematic).")]
        [SerializeField] private float fadeInOnStart = 0f;

        private CanvasGroup group;
        private Coroutine activeFade;

        /// <summary>Current opacity of the black overlay.</summary>
        public float Alpha => group != null ? group.alpha : 0f;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            SetInstant(startAlpha);
        }

        private void Start()
        {
            if (fadeInOnStart > 0f) FadeTo(0f, fadeInOnStart);
        }

        /// <summary>Immediately sets the overlay opacity.</summary>
        public void SetInstant(float alpha)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (activeFade != null)
            {
                StopCoroutine(activeFade);
                activeFade = null;
            }
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.01f;
            group.interactable = false;
        }

        /// <summary>Starts a fade and returns the running coroutine so callers can yield on it.</summary>
        public Coroutine FadeTo(float targetAlpha, float duration)
        {
            if (activeFade != null) StopCoroutine(activeFade);
            activeFade = StartCoroutine(FadeRoutine(targetAlpha, duration));
            return activeFade;
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            float from = group.alpha;
            group.blocksRaycasts = true;

            if (duration <= 0f)
            {
                group.alpha = targetAlpha;
            }
            else
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    group.alpha = Mathf.Lerp(from, targetAlpha, Easing.Evaluate(EaseType.SmoothStep, t / duration));
                    yield return null;
                }
                group.alpha = targetAlpha;
            }

            group.blocksRaycasts = targetAlpha > 0.01f;
            activeFade = null;
        }
    }
}
