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

        private void Start() => SetChildren(StoryFlags.Has(flag));

        private void SetChildren(bool visible)
        {
            shown = visible;
            foreach (Transform child in transform) child.gameObject.SetActive(visible);
            if (visible) transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!shown && !string.IsNullOrEmpty(flag) && StoryFlags.Has(flag))
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
