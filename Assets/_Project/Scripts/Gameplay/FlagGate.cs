using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>A blockade (static wall, concrete barrier) that dissolves once a story flag is set.</summary>
    public class FlagGate : MonoBehaviour
    {
        [SerializeField] private string openFlag = string.Empty;
        [SerializeField] private Color dissolveColor = new Color(1f, 0.6f, 0.9f);
        [SerializeField] private AudioClip openClip;

        private bool opened;

        private void Start()
        {
            // Already opened in a saved game: no animation, just gone.
            if (!string.IsNullOrEmpty(openFlag) && StoryFlags.Has(openFlag))
            {
                opened = true;
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (opened || string.IsNullOrEmpty(openFlag) || !StoryFlags.Has(openFlag)) return;
            opened = true;
            if (openClip != null) AudioSource.PlayClipAtPoint(openClip, Camera.main != null ? Camera.main.transform.position : transform.position, 0.8f);
            DissolveFX.Play(gameObject, dissolveColor);
        }
    }
}
