using System.Collections;
using System.Collections.Generic;
using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Little pop-ups in the top right corner when Leo gets tokens or items ("+5 TOKENS", "GOT: Fizzy Potion").
    /// Self-created in every gameplay scene.
    /// </summary>
    public class InventoryToast : MonoBehaviour
    {
        private readonly Queue<string> queue = new Queue<string>();
        private Canvas canvas;
        private RectTransform panel;
        private TextMeshProUGUI label;
        private Image icon;
        private AudioSource audioSource;
        private AudioClip coinClip;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureInScene();
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (FindFirstObjectByType<InventoryToast>() != null) return;
            new GameObject("InventoryToast").AddComponent<InventoryToast>();
        }

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            coinClip = CoinBlip();
            canvas = RuntimeUI.CreateCanvas("ToastCanvas", transform, 90);
            Image bg = RuntimeUI.Image("Toast", canvas.transform, new Color(0.05f, 0.03f, 0.09f, 0.92f));
            panel = bg.rectTransform;
            // Top left: the top right corner belongs to incoming text messages.
            RuntimeUI.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(460f, 58f));
            Outline outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.3f);
            outline.effectDistance = new Vector2(3, -3);
            icon = RuntimeUI.Image("Coin", panel, new Color(1f, 0.85f, 0.3f));
            RuntimeUI.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32f, 0f), new Vector2(22f, 22f));
            icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            label = RuntimeUI.Text("Text", panel, "", 26, Color.white, TextAlignmentOptions.MidlineLeft);
            RuntimeUI.Stretch(label.rectTransform, 60, 14, 0, 0);
            panel.gameObject.SetActive(false);
            Inventory.Changed += OnChanged;
        }

        private void OnDestroy() => Inventory.Changed -= OnChanged;

        /// <summary>A tiny two-note "coin" chime, generated so no asset is needed.</summary>
        public static AudioClip CoinBlip()
        {
            const int rate = 44100;
            int length = (int)(rate * 0.28f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float freq = t < 0.07f ? 988f : 1319f;
                float env = Mathf.Exp(-t * 14f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) > 0f ? 0.18f : -0.18f) * env;
            }
            AudioClip clip = AudioClip.Create("CoinBlip", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnChanged(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            queue.Enqueue(text);
            if (!showing) StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            showing = true;
            while (queue.Count > 0)
            {
                label.text = queue.Dequeue();
                panel.gameObject.SetActive(true);
                if (coinClip != null) audioSource.PlayOneShot(coinClip, 0.6f);
                Vector2 home = new Vector2(24f, -24f);
                for (float t = 0f; t < 0.25f; t += GameTime.UnscaledDelta)
                {
                    panel.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(80f, 0f, Easing.Evaluate(EaseType.EaseOutBack, t / 0.25f)));
                    yield return null;
                }
                panel.anchoredPosition = home;
                for (float t = 0f; t < 1.8f; t += GameTime.UnscaledDelta)
                {
                    icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + t * 180f);
                    yield return null;
                }
                for (float t = 0f; t < 0.2f; t += GameTime.UnscaledDelta)
                {
                    panel.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(0f, 80f, t / 0.2f));
                    yield return null;
                }
                panel.gameObject.SetActive(false);
            }
            showing = false;
        }
    }
}
