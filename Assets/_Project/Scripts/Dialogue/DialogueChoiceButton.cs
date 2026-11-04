using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Subnet.Dialogue
{
    /// <summary>
    /// One choice entry in the dialogue box. Navigation is driven by the DialogueManager
    /// (keyboard / gamepad); this component only reports mouse hover and clicks back to it.
    /// Prefab: a RectTransform with this component, a TMP label child, and an optional cursor child ("&gt;").
    /// </summary>
    public class DialogueChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional cursor object (e.g. a '>' text or a heart icon) shown when highlighted.")]
        [SerializeField] private GameObject cursor;
        [SerializeField] private Color normalColor = new Color(0.75f, 0.75f, 0.8f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.31f, 0.85f);
        [Tooltip("How far the label slides right when highlighted (pixels).")]
        [SerializeField] private float highlightOffset = 12f;

        private DialogueManager owner;
        private int index;
        private Vector2 labelBasePosition;
        private bool highlighted;

        /// <summary>Assigns references when the button is built from code (fallback template).</summary>
        public void Setup(TMP_Text labelText, GameObject cursorObject)
        {
            label = labelText;
            cursor = cursorObject;
        }

        public void Bind(DialogueManager manager, int choiceIndex, string text)
        {
            owner = manager;
            index = choiceIndex;
            if (label != null)
            {
                label.text = text;
                labelBasePosition = label.rectTransform.anchoredPosition;
            }
            SetHighlighted(false, true);
        }

        public void SetHighlighted(bool value, bool instant = false)
        {
            highlighted = value;
            if (cursor != null) cursor.SetActive(value);
            if (label != null)
            {
                label.color = value ? highlightColor : normalColor;
                if (instant) label.rectTransform.anchoredPosition = labelBasePosition + (value ? Vector2.right * highlightOffset : Vector2.zero);
            }
        }

        private void Update()
        {
            if (label == null) return;
            // Smooth slide toward the highlighted / idle offset.
            Vector2 target = labelBasePosition + (highlighted ? Vector2.right * highlightOffset : Vector2.zero);
            label.rectTransform.anchoredPosition = Vector2.Lerp(label.rectTransform.anchoredPosition, target, 1f - Mathf.Exp(-18f * GameTime.UnscaledDelta));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null) owner.HighlightChoice(index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null) owner.ConfirmChoice(index);
        }
    }
}
