using Subnet.Core;
using TMPro;
using UnityEngine;

namespace Subnet.UI
{
    /// <summary>
    /// Put on any static TextMeshPro label: its authored English text becomes the key, and the label
    /// re-translates itself whenever the language changes.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("English source text. Empty = captured from the label on first enable.")]
        [SerializeField] private string key = string.Empty;

        private TMP_Text label;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            if (string.IsNullOrEmpty(key)) key = label.text;
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (label != null) label.text = Loc.T(key);
        }
    }
}
