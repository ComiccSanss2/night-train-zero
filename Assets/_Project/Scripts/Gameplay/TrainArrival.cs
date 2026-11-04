using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Night Train Zero pulls into the platform when a story flag is set:
    /// hidden at start, slides in from far away with a long deceleration, then sets an "arrived" flag.
    /// </summary>
    public class TrainArrival : MonoBehaviour
    {
        [SerializeField] private Transform train;
        [SerializeField] private string triggerFlag = "read_timetable";
        [SerializeField] private string arrivedFlag = "train_arrived";
        [SerializeField] private Vector3 startOffset = new Vector3(45f, 0f, 0f);
        [SerializeField] private float duration = 6f;
        [SerializeField] private float delay = 1f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip arrivalClip;
        [SerializeField] private AudioClip doorsClip;

        private Vector3 finalPosition;
        private bool started;

        private void Awake()
        {
            if (train == null) return;
            finalPosition = train.position;
            if (StoryFlags.Has(arrivedFlag))
            {
                started = true; // already at the platform (saved game)
                return;
            }
            train.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (started || train == null || !StoryFlags.Has(triggerFlag)) return;
            started = true;
            StartCoroutine(Arrive());
        }

        private IEnumerator Arrive()
        {
            yield return new WaitForSeconds(delay);
            train.gameObject.SetActive(true);
            if (audioSource != null && arrivalClip != null) audioSource.PlayOneShot(arrivalClip);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float e = Easing.Evaluate(EaseType.EaseOutCubic, t / duration);
                train.position = Vector3.LerpUnclamped(finalPosition + startOffset, finalPosition, e);
                if (IsometricCameraRig.Main != null && t < duration * 0.6f) IsometricCameraRig.Main.AddTrauma(0.6f * Time.deltaTime);
                yield return null;
            }

            train.position = finalPosition;
            if (audioSource != null && doorsClip != null) audioSource.PlayOneShot(doorsClip);
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(0.25f);
            StoryFlags.Set(arrivedFlag);
        }
    }
}
