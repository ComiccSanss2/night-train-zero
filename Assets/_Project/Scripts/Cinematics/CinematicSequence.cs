using System;
using System.Collections.Generic;
using Subnet.Core;
using Subnet.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Cinematics
{
    /// <summary>One camera beat inside a cinematic.</summary>
    [Serializable]
    public class CinematicShot
    {
        [Tooltip("Editor label only.")]
        public string label = "Shot";

        [Tooltip("Empty GameObject whose position + rotation the camera moves to. Tag it 'CinematicPoint' for clarity.")]
        public Transform cameraPoint;

        [Tooltip("Cut instantly instead of travelling.")]
        public bool cut;

        [Min(0f)] public float travelDuration = 3f;
        public EaseType travelEase = EaseType.SmootherStep;

        [Tooltip("Seconds to linger after arriving (and after dialogue, if any).")]
        [Min(0f)] public float holdDuration = 1f;

        [Tooltip("Perspective FOV at the end of this shot. 0 = keep current.")]
        [Range(0f, 90f)] public float fieldOfView = 0f;

        [Header("Narration")]
        [Tooltip("Branching dialogue played on arrival.")]
        public DialogueSequence dialogue;

        [Tooltip("Quick subtitle-style lines played on arrival if no dialogue asset is set.")]
        [TextArea(2, 4)] public string[] narrationLines = { };

        public DialogueSpeaker narrationSpeaker;

        [Header("Feedback")]
        [Tooltip("One-shot sound when the shot starts (train horn, door hiss...).")]
        public AudioClip startSound;

        [Range(0f, 1f)] public float startSoundVolume = 1f;

        [Tooltip("Optional trauma added to screen shake on shot start (0 = none).")]
        [Range(0f, 1f)] public float shakeOnStart = 0f;

        public UnityEvent onShotStarted = new UnityEvent();
        public UnityEvent onShotArrived = new UnityEvent();
    }

    /// <summary>
    /// A reusable list of shots plus framing options. Add one per cutscene
    /// ("Intro_MovingBoxes", "Boarding_NightTrainZero", "Algorithm_Arrival") and play it through the CinematicDirector.
    /// </summary>
    public class CinematicSequence : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Start from full black and fade in.")]
        public bool fadeFromBlack = true;
        [Min(0f)] public float blackHoldBeforeFade = 1.5f;
        [Min(0f)] public float fadeInDuration = 2.5f;

        [Tooltip("Show letterbox bars during the sequence.")]
        public bool useLetterbox = true;

        [Tooltip("At the end, glide the camera back to the gameplay rig and return control.")]
        public bool blendToGameplay = true;
        [Min(0f)] public float blendToGameplayDuration = 2f;

        [Tooltip("Instead of blending, fade to black at the end (for scene transitions).")]
        public bool fadeToBlackAtEnd;
        [Min(0f)] public float fadeOutDuration = 1.2f;

        [Tooltip("Hold Cancel to skip.")]
        public bool skippable = true;

        [Header("Ambience")]
        [Tooltip("Loop started with the sequence (e.g. station rumble). Keeps playing after it ends.")]
        public AudioClip ambienceLoop;
        [Range(0f, 1f)] public float ambienceVolume = 0.6f;
        [Min(0f)] public float ambienceFadeIn = 3f;

        [Header("Shots")]
        public List<CinematicShot> shots = new List<CinematicShot>();

        [Header("Events")]
        public UnityEvent onSequenceStarted = new UnityEvent();
        public UnityEvent onSequenceFinished = new UnityEvent();

        private void OnDrawGizmos()
        {
            // Draws the camera path in the Scene view.
            Gizmos.color = new Color(0.3f, 1f, 0.95f, 0.8f);
            Transform previous = null;
            foreach (CinematicShot shot in shots)
            {
                if (shot == null || shot.cameraPoint == null) continue;
                Gizmos.DrawWireSphere(shot.cameraPoint.position, 0.25f);
                Gizmos.DrawRay(shot.cameraPoint.position, shot.cameraPoint.forward * 1.5f);
                if (previous != null && !shot.cut) Gizmos.DrawLine(previous.position, shot.cameraPoint.position);
                previous = shot.cameraPoint;
            }
        }
    }
}
