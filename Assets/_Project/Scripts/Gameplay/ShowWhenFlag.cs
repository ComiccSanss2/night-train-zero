using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>Keeps its children hidden until a story flag is set, then pops them in (the last train in the Core).</summary>
    public class ShowWhenFlag : MonoBehaviour
    {
        [SerializeField] private string flag = string.Empty;
        [SerializeField] private float popSeconds = 0.6f;

        private bool shown;
        private float popTimer;

        // "flag" shows the children once it's set; "!flag" shows them only until it's set.
        private bool Met()
        {
            if (string.IsNullOrEmpty(flag)) return false;
            return flag[0] == '!' ? !StoryFlags.Has(flag.Substring(1)) : StoryFlags.Has(flag);
        }

        private void Start() => SetChildren(Met());

        private void SetChildren(bool visible)
        {
            shown = visible;
            foreach (Transform child in transform) child.gameObject.SetActive(visible);
            if (visible) transform.localScale = Vector3.one;
        }

        private void Update()
        {
            bool met = Met();
            if (shown && !met) SetChildren(false);
            if (!shown && met)
            {
                SetChildren(true);
                popTimer = popSeconds;
                transform.localScale = new Vector3(1f, 0.01f, 1f);
            }
            if (popTimer > 0f)
            {
                popTimer -= Time.deltaTime;
                float n = 1f - Mathf.Clamp01(popTimer / popSeconds);
                transform.localScale = new Vector3(1f, Easing.Evaluate(EaseType.EaseOutBack, n), 1f);
            }
        }
    }
}
