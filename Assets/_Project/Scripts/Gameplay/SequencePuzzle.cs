using System.Collections.Generic;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Environmental logic puzzle: activate the switches in the right order.
    /// The order is never shown directly; it is hidden in inspection texts (statue plaques).
    /// A wrong step resets everything with a short reaction line.
    /// </summary>
    public class SequencePuzzle : MonoBehaviour
    {
        [Tooltip("Switches in the CORRECT order.")]
        [SerializeField] private List<PuzzleSwitch> correctOrder = new List<PuzzleSwitch>();
        [SerializeField] private string flagOnSolved = "puzzle_solved";
        [TextArea(2, 3)] [SerializeField] private string[] wrongLines = { "* The lamps flicker out. Wrong order." };
        [TextArea(2, 3)] [SerializeField] private string[] solvedLines = { "* Something unlocks, deep in the concrete." };
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip correctClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip solvedClip;
        public UnityEvent onSolved = new UnityEvent();

        private int progress;
        private bool solved;

        public bool IsSolved => solved;

        private void Awake()
        {
            foreach (PuzzleSwitch s in correctOrder)
            {
                if (s != null) s.Bind(this);
            }
        }

        /// <summary>Called by a PuzzleSwitch when the player activates it.</summary>
        public void Report(PuzzleSwitch activated)
        {
            if (solved || activated.IsLit) return;

            if (progress < correctOrder.Count && correctOrder[progress] == activated)
            {
                activated.SetLit(true);
                progress++;
                Play(correctClip);

                if (progress >= correctOrder.Count)
                {
                    solved = true;
                    Play(solvedClip);
                    if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(0.35f);
                    StoryFlags.Set(flagOnSolved);
                    if (DialogueManager.Instance != null && solvedLines.Length > 0) DialogueManager.Instance.PlayLines(solvedLines);
                    onSolved?.Invoke();
                }
                return;
            }

            // Wrong: reset all.
            activated.SetLit(true);
            Play(wrongClip);
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(0.2f);
            progress = 0;
            foreach (PuzzleSwitch s in correctOrder) s.SetLit(false);
            if (DialogueManager.Instance != null && wrongLines.Length > 0) DialogueManager.Instance.PlayLines(wrongLines);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
