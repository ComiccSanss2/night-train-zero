using System;
using System.Collections;
using System.Collections.Generic;
using Subnet.Core;
using TMPro;
using Subnet.Dialogue;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Diegetic objective hints: early-2000s SMS popping up on Leo's handheld (mostly from Byte's collar).
    /// PhoneNotifier.Show("BYTE", "go left. trust me im a cat");
    /// Builds its own UI at runtime; one instance per scene (auto-created if missing).
    /// </summary>
    public class PhoneNotifier : MonoBehaviour
    {
        private static PhoneNotifier instance;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip messageClip;
        [SerializeField] private float displaySeconds = 5f;

        private readonly Queue<(string sender, string text)> queue = new Queue<(string, string)>();
        private RectTransform panel;
        private CanvasGroup group;
        private TextMeshProUGUI header;
        private TextMeshProUGUI body;
        private bool showing;

        private const float ShownX = -40f;
        private const float HiddenX = 640f;

        public static void Show(string sender, string message)
        {
            if (instance == null) instance = new GameObject("PhoneNotifier").AddComponent<PhoneNotifier>();
            instance.queue.Enqueue((sender, message));
            if (!instance.showing) instance.StartCoroutine(instance.ProcessQueue());
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            BuildUI();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void BuildUI()
        {
            Canvas canvas = RuntimeUI.CreateCanvas("PhoneCanvas", transform, 20);
            panel = RuntimeUI.Rect("SMS", canvas.transform);
            RuntimeUI.Place(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(HiddenX, -40), new Vector2(560, 160));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.06f, 0.95f);
            background.raycastTarget = false;
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = RuntimeUI.Cyan;
            outline.effectDistance = new Vector2(2, -2);

            header = RuntimeUI.Text("Header", panel, "", 22, new Color(0.4f, 0.85f, 0.7f), TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(header.rectTransform, 20, 20, 12, 120);
            body = RuntimeUI.Text("Body", panel, "", 28, new Color(0.65f, 1f, 0.78f), TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(body.rectTransform, 20, 20, 48, 12);
        }

        private TMP_FontAsset bodyDefaultFont;
        private float bodyDefaultSize;

        private IEnumerator ProcessQueue()
        {
            showing = true;
            while (queue.Count > 0)
            {
                (string sender, string text) = queue.Dequeue();
                header.text = $"[SMS] {Loc.T(sender).ToUpperInvariant()}  -  {DateTime.Now:HH:mm}";
                // Byte's texts come in Byte's typeface.
                if (bodyDefaultFont == null) { bodyDefaultFont = body.font; bodyDefaultSize = body.fontSize; }
                DialogueFonts.Look look = DialogueFonts.Get(sender == "BYTE" ? SpeakerStyle.Byte : SpeakerStyle.Default, bodyDefaultFont);
                body.font = look.font != null ? look.font : bodyDefaultFont;
                body.fontStyle = look.fontStyle;
                body.fontSize = bodyDefaultSize * Mathf.Min(look.sizeScale, 1.2f);
                TextFX.Show(body, text).SetText(Loc.T(text), false, TextEffect.None);
                if (audioSource != null && messageClip != null) audioSource.PlayOneShot(messageClip);

                // Slide in with a bounce.
                for (float t = 0f; t < 0.35f; t += GameTime.UnscaledDelta)
                {
                    float e = Easing.Evaluate(EaseType.EaseOutBack, t / 0.35f);
                    SetX(Mathf.LerpUnclamped(HiddenX, ShownX, e));
                    group.alpha = Mathf.Clamp01(t / 0.2f);
                    yield return null;
                }
                SetX(ShownX);
                group.alpha = 1f;

                // Quick typewriter.
                body.ForceMeshUpdate();
                int total = body.textInfo.characterCount;
                for (int i = 0; i <= total; i++)
                {
                    body.maxVisibleCharacters = i;
                    yield return GameTime.Wait(0.018f);
                }

                float hold = displaySeconds + text.Length * 0.03f;
                for (float t = 0f; t < hold; t += GameTime.UnscaledDelta) yield return null;

                for (float t = 0f; t < 0.3f; t += GameTime.UnscaledDelta)
                {
                    float e = Easing.Evaluate(EaseType.EaseInCubic, t / 0.3f);
                    SetX(Mathf.Lerp(ShownX, HiddenX, e));
                    group.alpha = 1f - e;
                    yield return null;
                }
                group.alpha = 0f;
            }
            showing = false;
        }

        private void SetX(float x) => panel.anchoredPosition = new Vector2(x, panel.anchoredPosition.y);
    }
}
