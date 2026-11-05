using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Gameplay;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Interaction
{
    /// <summary>
    /// A named arrival point (e.g. "from_park" at the east end of the shopping street). When the previous scene
    /// was left through an exit with this id, Leo and his companions start here instead of at the scene's start,
    /// and the first time it happens a flag is set ("back_subnet"...) that unlocks things only a return reveals.
    /// </summary>
    public class SceneEntry : MonoBehaviour
    {
        [SerializeField] private string entryId = "from_park";
        [Tooltip("Set the first time Leo arrives here (unlocks return-only discoveries).")]
        [SerializeField] private string flagOnFirstArrival = string.Empty;
        [Tooltip("Only counts as a return if this flag is already set (e.g. the park after a trip back to the street).")]
        [SerializeField] private string firstArrivalRequires = string.Empty;
        [TextArea(1, 3)]
        [Tooltip("Optional inner-voice lines on the first return.")]
        [SerializeField] private string[] firstArrivalLines = { };

        private IEnumerator Start()
        {
            if (SceneLoader.PendingEntry != entryId) yield break;
            SceneLoader.ConsumeEntry();
            yield return null; // let the player and camera finish their own Start first

            GameObject leo = GameObject.FindGameObjectWithTag("Player");
            if (leo != null)
            {
                var controller = leo.GetComponent<IsometricPlayerController>();
                if (controller != null) controller.Teleport(transform.position, transform.rotation);
                else leo.transform.SetPositionAndRotation(transform.position, transform.rotation);
                int i = 0;
                foreach (CompanionFollower follower in FindObjectsByType<CompanionFollower>(FindObjectsSortMode.None))
                {
                    if (!follower.IsFollowing) continue;
                    follower.transform.position = transform.position - transform.forward * 1.2f + transform.right * (i++ == 0 ? -0.7f : 0.7f);
                }
            }
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.SnapToTarget();

            bool counts = string.IsNullOrEmpty(firstArrivalRequires) || StoryFlags.Has(firstArrivalRequires);
            if (counts && !string.IsNullOrEmpty(flagOnFirstArrival) && !StoryFlags.Has(flagOnFirstArrival))
            {
                StoryFlags.Set(flagOnFirstArrival);
                if (firstArrivalLines != null && firstArrivalLines.Length > 0)
                {
                    yield return new WaitForSeconds(1f);
                    Dialogue.DialogueManager.Instance?.PlayLines(firstArrivalLines);
                }
            }
        }
    }
}
