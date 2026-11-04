using Subnet.Core;
using UnityEngine;

namespace Subnet.Cinematics
{
    /// <summary>
    /// Plays a CinematicSequence when the player walks into this trigger volume
    /// (e.g. stepping onto the platform of Night Train Zero). Needs a Collider with "Is Trigger" on.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CinematicTrigger : MonoBehaviour
    {
        [SerializeField] private CinematicSequence sequence;
        [Tooltip("Tag of the object that can fire the trigger.")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool playOnce = true;
        [Tooltip("Only fires while this story flag is set (prefix ! for 'not set'). Empty = always.")]
        [SerializeField] private string requiredFlag = string.Empty;
        [Tooltip("Story flag set when the cinematic finishes.")]
        [SerializeField] private string flagOnFinished = string.Empty;

        private bool fired;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired && playOnce) return;
            if (!other.CompareTag(playerTag)) return;
            if (!FlagConditionMet()) return;
            if (CinematicDirector.Instance == null || CinematicDirector.Instance.IsPlaying) return;

            fired = true;
            CinematicDirector.Instance.Play(sequence, () => StoryFlags.Set(flagOnFinished));
        }

        private bool FlagConditionMet()
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            return requiredFlag[0] == '!' ? !StoryFlags.Has(requiredFlag.Substring(1)) : StoryFlags.Has(requiredFlag);
        }
    }
}
