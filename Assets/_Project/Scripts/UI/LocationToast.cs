using System.Collections;
using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>A place name that slides in at the top of the screen when Leo walks into a building.</summary>
    public class LocationToast : MonoBehaviour
    {
        private static LocationToast instance;
        private TextMeshProUGUI label;
        private Image line;
        private Coroutine routine;

        public static void Show(string placeName)
        {
            if (instance == null) instance = new GameObject("LocationToast").AddComponent<LocationToast>();
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = instance.StartCoroutine(instance.Run(Loc.T(placeName)));
        }

        private void Awake()
        {
            Canvas canvas = RuntimeUI.CreateCanvas("LocationCanvas", transform, 70);
            label = RuntimeUI.Text("Place", canvas.transform, "", 44, Color.white, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 12f;
            RuntimeUI.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 60f));
            line = RuntimeUI.Image("Line", canvas.transform, RuntimeUI.Magenta);
            RuntimeUI.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(0f, 3f));
            label.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private IEnumerator Run(string text)
        {
            label.text = text;
            for (float t = 0f; t < 0.5f; t += GameTime.UnscaledDelta)
            {
                float n = t / 0.5f;
                label.alpha = n;
                label.characterSpacing = Mathf.Lerp(30f, 12f, Easing.Evaluate(EaseType.EaseOutCubic, n));
                line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 520f, Easing.Evaluate(EaseType.EaseOutCubic, n)), 3f);
                yield return null;
            }
            for (float t = 0f; t < 1.8f; t += GameTime.UnscaledDelta) yield return null;
            for (float t = 0f; t < 0.6f; t += GameTime.UnscaledDelta)
            {
                float n = t / 0.6f;
                label.alpha = 1f - n;
                line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(520f, 0f, n), 3f);
                yield return null;
            }
            label.alpha = 0f;
            routine = null;
        }
    }
}
