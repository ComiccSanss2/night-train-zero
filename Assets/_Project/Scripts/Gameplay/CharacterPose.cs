using System.Collections.Generic;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// Poses for any character: sitting down and short emotes (nod, head shake, a little dance...).
    /// People use the "sit" / "emote-yes" / "emote-no" clips of their animator; Byte (a cube pet with no sit
    /// clip) folds down procedurally: hips lowered, chest up, tail end on the floor.
    /// </summary>
    public static class CharacterPose
    {
        private static readonly int SitId = Animator.StringToHash("Sit");

        private class Folded
        {
            public Transform model;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private static readonly Dictionary<Transform, Folded> folded = new Dictionary<Transform, Folded>();

        public static bool HasParameter(Animator animator, string name)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }

        /// <summary>Sits the character down (or stands it up).</summary>
        public static void SetSitting(Transform root, bool sitting)
        {
            if (root == null) return;
            Animator animator = root.GetComponentInChildren<Animator>();
            if (HasParameter(animator, "Sit"))
            {
                animator.SetBool(SitId, sitting);
                // Snap straight into the pose (no half-second blend), so the body can be placed on the seat right away.
                if (sitting && animator.HasState(0, SitId))
                {
                    animator.Play(SitId, 0, 0.5f);
                    animator.Update(0f);
                }
                return;
            }

            // No sit clip: fold the model.
            Transform model = root.Find("Model");
            if (model == null) return;
            if (sitting)
            {
                if (folded.ContainsKey(root)) return;
                folded[root] = new Folded { model = model, position = model.localPosition, rotation = model.localRotation, scale = model.localScale };
                model.localScale = Vector3.Scale(model.localScale, new Vector3(1f, 0.82f, 1f));
                model.localRotation = model.localRotation * Quaternion.Euler(-14f, 0f, 0f);
                model.localPosition += new Vector3(0f, -0.02f, -0.04f);
            }
            else if (folded.TryGetValue(root, out Folded f))
            {
                if (f.model != null)
                {
                    f.model.localPosition = f.position;
                    f.model.localRotation = f.rotation;
                    f.model.localScale = f.scale;
                }
                folded.Remove(root);
            }
        }

        /// <summary>Plays a short emote: "yes", "no", "dance", "pick"... Ignored if the character has no such clip.</summary>
        public static void Emote(Transform root, string emote)
        {
            if (root == null || string.IsNullOrEmpty(emote)) return;
            Animator animator = root.GetComponentInChildren<Animator>();
            string trigger = "Emote_" + emote.ToLowerInvariant();
            bool sitting = HasParameter(animator, "Sit") && animator.GetBool(SitId);
            if (!sitting && HasParameter(animator, trigger)) animator.SetTrigger(trigger);
            else
            {
                ProceduralLean lean = root.GetComponentInChildren<ProceduralLean>();
                if (lean != null) lean.Emphasize(1.2f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => folded.Clear();
    }
}
