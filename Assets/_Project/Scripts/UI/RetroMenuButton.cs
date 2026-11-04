using System;
using System.Collections;
using System.Text;
using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Terminal-style menu entry for Leo's handheld OS:
    ///   idle:        "  BOOT SYSTEM"
    ///   highlighted: "&gt; BOOT SYSTEM_"  (with a brief character-scramble glitch and jitter)
    /// Mouse hover selects it through the EventSystem, so keyboard, gamepad and mouse share one highlight.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RetroMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        private const string GlitchGlyphs = "#%&@$*+=?!<>/\\|01";

        [SerializeField] private TMP_Text label;
        [Tooltip("Text shown on the button. Set at runtime via SetLabel().")]
        [SerializeField] private string labelText = "BUTTON";

        [Header("Look")]
        [SerializeField] private Color normalColor = new Color(0.45f, 0.75f, 0.72f);
        [SerializeField] private Color highlightColor = new Color(0.55f, 1f, 0.95f);
        [SerializeField] private Color lockedColor = new Color(0.45f, 0.2f, 0.3f);
        [SerializeField] private string highlightPrefix = "> ";
        [SerializeField] private string idlePrefix = "  ";
        [Tooltip("Blinking terminal caret appended when highlighted.")]
        [SerializeField] private string caret = "_";
        [SerializeField] private float caretBlinkRate = 2.5f;

        [Header("Glitch")]
        [SerializeField] private float glitchDuration = 0.14f;
        [Tooltip("Fraction of characters scrambled during the glitch.")]
        [Range(0f, 1f)] [SerializeField] private float glitchDensity = 0.35f;
        [Tooltip("Max horizontal jitter in pixels during the glitch.")]
        [SerializeField] private float jitterPixels = 6f;

        /// <summary>Raised when this entry gains focus (mouse, keyboard or gamepad).</summary>
        public event Action<RetroMenuButton> Highlighted;
        /// <summary>Raised when this entry is activated (also raised when locked, so the owner can play an error).</summary>
        public event Action<RetroMenuButton> Activated;

        /// <summary>Locked entries stay navigable but render as corrupted.</summary>
        public bool IsLocked { get; private set; }

        public bool IsHighlighted { get; private set; }

        public Button Button { get; private set; }

        private readonly StringBuilder builder = new StringBuilder(64);
        private RectTransform labelRect;
        private Vector2 labelBasePosition;
        private Coroutine glitchRoutine;
        private bool glitching;

        private void Awake()
        {
            Button = GetComponent<Button>();
            // Visual feedback is fully handled here; keep the Button's own transition from fighting it.
            Button.transition = Selectable.Transition.None;
            if (label != null)
            {
                labelRect = label.rectTransform;
                labelBasePosition = labelRect.anchoredPosition;
            }
            Refresh();
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        /// <summary>Wires the label when the button is built from code (pause menu).</summary>
        public void Configure(TMP_Text labelComponent, string text)
        {
            label = labelComponent;
            labelText = text;
            if (label != null)
            {
                labelRect = label.rectTransform;
                labelBasePosition = labelRect.anchoredPosition;
            }
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
            glitching = false;
            glitchRoutine = null;
            if (labelRect != null) labelRect.anchoredPosition = labelBasePosition;
        }

        public void SetLabel(string text)
        {
            labelText = text;
            Refresh();
        }

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Button.interactable && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            IsHighlighted = true;
            Glitch();
            Highlighted?.Invoke(this);
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            IsHighlighted = false;
            Refresh();
        }

        public void OnSubmit(BaseEventData eventData) => Activated?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Activated?.Invoke(this);
        }

        /// <summary>Plays the scramble + jitter glitch on this label.</summary>
        public void Glitch()
        {
            if (!isActiveAndEnabled || label == null) return;
            if (glitchRoutine != null) StopCoroutine(glitchRoutine);
            glitchRoutine = StartCoroutine(GlitchRoutine());
        }

        private IEnumerator GlitchRoutine()
        {
            glitching = true;
            for (float t = 0f; t < glitchDuration; t += Time.unscaledDeltaTime)
            {
                label.text = BuildLabel(true);
                labelRect.anchoredPosition = labelBasePosition + new Vector2(UnityEngine.Random.Range(-jitterPixels, jitterPixels), 0f);
                yield return null;
            }
            glitching = false;
            labelRect.anchoredPosition = labelBasePosition;
            glitchRoutine = null;
            Refresh();
        }

        private void Update()
        {
            // Caret blink.
            if (IsHighlighted && !glitching && label != null) label.text = BuildLabel(false);
        }

        private void Refresh()
        {
            if (label == null) return;
            label.color = IsLocked ? lockedColor : IsHighlighted ? highlightColor : normalColor;
            if (!glitching) label.text = BuildLabel(false);
        }

        private string BuildLabel(bool scrambled)
        {
            builder.Clear();
            builder.Append(IsHighlighted ? highlightPrefix : idlePrefix);

            string localized = Loc.T(labelText);
            string body = IsLocked ? Corrupt(localized) : localized;
            if (scrambled)
            {
                foreach (char c in body)
                {
                    bool swap = !char.IsWhiteSpace(c) && UnityEngine.Random.value < glitchDensity;
                    builder.Append(swap ? GlitchGlyphs[UnityEngine.Random.Range(0, GlitchGlyphs.Length)] : c);
                }
            }
            else
            {
                builder.Append(body);
            }

            if (IsHighlighted && !scrambled && Mathf.FloorToInt(Time.unscaledTime * caretBlinkRate) % 2 == 0) builder.Append(caret);
            return builder.ToString();
        }

        /// <summary>Deterministic "corruption" for locked entries so the text doesn't flicker every frame.</summary>
        private static string Corrupt(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsWhiteSpace(chars[i]) && (i * 7 + 3) % 3 == 0) chars[i] = GlitchGlyphs[(i * 13) % GlitchGlyphs.Length];
            }
            return new string(chars);
        }
    }
}
