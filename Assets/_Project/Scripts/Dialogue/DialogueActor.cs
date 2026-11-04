using System.Collections.Generic;
using Subnet.Gameplay;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// Marks a world character as the body of a speaker (Byte, Kira, an NPC, the Algorithm).
    /// The DialogueDirector uses it to turn heads, walk companions over and point the camera at whoever talks.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueActor : MonoBehaviour
    {
        private static readonly List<DialogueActor> all = new List<DialogueActor>();
        public static IReadOnlyList<DialogueActor> All => all;

        [Tooltip("The speaker profile this body belongs to.")]
        public DialogueSpeaker speaker;
        [Tooltip("Lowercase id used by stage tags, e.g. {beside=kira}. Defaults to the speaker name.")]
        public string actorId = string.Empty;
        [Tooltip("Walks over to Leo when it starts talking from far away (companions).")]
        public bool canApproach;
        [Tooltip("Turns to look at whoever is talking.")]
        public bool turnToFace = true;
        [Tooltip("Model rotation offset if its nose doesn't point along +Z.")]
        public float modelYawOffset;
        public float walkSpeed = 3.6f;

        private ProceduralLean lean;
        private Animator animator;
        private bool hasSpeed;
        private static readonly int SpeedId = Animator.StringToHash("Speed");

        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(actorId)) return actorId.ToLowerInvariant();
                if (speaker != null && !string.IsNullOrEmpty(speaker.displayName)) return speaker.displayName.ToLowerInvariant();
                return name.ToLowerInvariant().Replace("npc_", "");
            }
        }

        private void Awake()
        {
            Transform model = transform.Find("Model");
            if (model != null)
            {
                lean = model.GetComponent<ProceduralLean>();
                if (lean == null && GetComponent<Spinner>() == null) lean = model.gameObject.AddComponent<ProceduralLean>();
            }
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == SpeedId) hasSpeed = true;
                }
            }
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void Talk() { if (lean != null) lean.Talk(); }
        public void Emphasize(float amount) { if (lean != null) lean.Emphasize(amount); }

        public void SetWalkSpeed(float normalized)
        {
            if (hasSpeed) animator.SetFloat(SpeedId, normalized, 0.08f, Time.deltaTime);
        }

        public Quaternion LookRotation(Vector3 direction)
        {
            return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        }
    }
}
