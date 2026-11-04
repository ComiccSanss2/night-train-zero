using System.Collections.Generic;
using Subnet.CameraSystem;
using Subnet.Interaction;
using UnityEngine;

namespace Subnet.Dialogue
{
    /// <summary>
    /// Stages every conversation like a little play:
    /// - Leo always turns toward whoever is talking; everyone else looks at the speaker too.
    /// - The camera leans toward the speaker and pushes in a bit.
    /// - Companions who start talking from far away walk over first ({approach=kira} forces it).
    /// - {beside=kira}: walks up next to Leo and looks where he looks (holding hands at the end).
    /// - Speakers bob while they talk and jolt on {big} words.
    /// Lives next to the DialogueManager (added automatically).
    /// </summary>
    public class DialogueDirector : MonoBehaviour
    {
        public static DialogueDirector Instance { get; private set; }

        [SerializeField] private float leoTurnSpeed = 480f;
        [SerializeField] private float actorTurnSpeed = 420f;
        [SerializeField] private float approachTrigger = 3.4f;
        [SerializeField] private float approachDistance = 1.6f;
        [SerializeField] private float cameraLean = 0.45f;
        [SerializeField] private float cameraZoom = 0.8f;
        [SerializeField] private float involvedRadius = 11f;

        private enum Mode { Free, Approach, Beside }

        private class Staging
        {
            public Mode mode;
            public Vector3 goal;
            public float side;
        }

        private DialogueManager manager;
        private Transform leo;
        private Transform speakerBody;   // who talks now (null = Leo / narrator)
        private Transform lastOther;     // the person Leo is talking with
        private bool leoSpeaking;
        private readonly Dictionary<DialogueActor, Staging> staging = new Dictionary<DialogueActor, Staging>();
        private readonly Dictionary<Transform, Quaternion> originalFacing = new Dictionary<Transform, Quaternion>();
        private bool wasOpen;

        private void Awake()
        {
            Instance = this;
            manager = GetComponent<DialogueManager>();
        }

        private void OnEnable()
        {
            DialogueManager.LineStarted += HandleLine;
            DialogueManager.Blipped += HandleBlip;
            DialogueManager.Emphasis += HandleEmphasis;
            DialogueManager.StageDirection += HandleStage;
        }

        private void OnDisable()
        {
            DialogueManager.LineStarted -= HandleLine;
            DialogueManager.Blipped -= HandleBlip;
            DialogueManager.Emphasis -= HandleEmphasis;
            DialogueManager.StageDirection -= HandleStage;
            if (Instance == this) Instance = null;
        }

        private Transform Leo
        {
            get
            {
                if (leo == null)
                {
                    GameObject p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) leo = p.transform;
                }
                return leo;
            }
        }

        // ------------------------------------------------------------------ Events

        private void HandleLine(DialogueSpeaker speaker)
        {
            if (Leo == null) return;
            bool narrator = speaker == null || speaker == manager.NarratorSpeaker || speaker.style == SpeakerStyle.Thought;
            leoSpeaking = speaker != null && speaker.style == SpeakerStyle.Leo;

            if (narrator || leoSpeaking)
            {
                speakerBody = null;
                if (lastOther == null && manager.CurrentSource != null) lastOther = manager.CurrentSource;
                return;
            }

            speakerBody = Resolve(speaker);
            if (speakerBody != null)
            {
                lastOther = speakerBody;
                DialogueActor actor = speakerBody.GetComponent<DialogueActor>();
                if (actor != null && actor.canApproach && FlatDistance(actor.transform.position, Leo.position) > approachTrigger)
                {
                    StartApproach(actor, approachDistance);
                }
            }
        }

        private void HandleStage(string verb, string id)
        {
            DialogueActor actor = FindActor(id);
            if (actor == null || Leo == null) return;
            switch (verb)
            {
                case "approach":
                    StartApproach(actor, 1.05f);
                    break;
                case "beside":
                    Staging s = Get(actor);
                    s.mode = Mode.Beside;
                    // Kira on Leo's right, Byte on his left (or whichever side is closer).
                    Vector3 local = Leo.InverseTransformPoint(actor.transform.position);
                    s.side = id == "byte" ? -1f : (id == "kira" ? 1f : Mathf.Sign(local.x == 0f ? 1f : local.x));
                    Remember(actor.transform);
                    break;
            }
        }

        private void HandleBlip(DialogueSpeaker speaker)
        {
            if (speakerBody != null && speakerBody.TryGetComponent(out DialogueActor actor)) actor.Talk();
            else if (leoSpeaking && Leo != null)
            {
                Gameplay.ProceduralLean lean = Leo.GetComponentInChildren<Gameplay.ProceduralLean>();
                if (lean != null) lean.Talk();
            }
        }

        private void HandleEmphasis(float strength)
        {
            if (speakerBody != null && speakerBody.TryGetComponent(out DialogueActor actor)) actor.Emphasize(strength * 2f);
        }

        // ------------------------------------------------------------------ Per frame

        private void LateUpdate()
        {
            bool open = manager != null && manager.IsOpen;
            bool hacking = Hacking.HackingManager.Instance != null && Hacking.HackingManager.Instance.IsActive;

            if (!open || hacking)
            {
                if (wasOpen) EndConversation();
                wasOpen = false;
                return;
            }
            wasOpen = true;
            if (Leo == null) return;
            float dt = Time.deltaTime;

            // 1. Who does Leo look at?
            Transform leoLook = speakerBody != null ? speakerBody : lastOther;
            if (leoLook != null && leoLook != Leo) TurnTowards(Leo, leoLook.position, leoTurnSpeed * dt, 0f);

            // 2. Everyone involved reacts.
            foreach (DialogueActor actor in DialogueActor.All)
            {
                if (actor == null || actor.transform == Leo) continue;
                // Only people who are part of this scene react: companions, the speaker, whoever Leo is talking to.
                bool involved = actor.canApproach || actor.transform == speakerBody || actor.transform == lastOther ||
                                actor.transform == manager.CurrentSource || staging.ContainsKey(actor);
                if (!involved) continue;
                if (FlatDistance(actor.transform.position, Leo.position) > involvedRadius && !staging.ContainsKey(actor)) continue;
                UpdateActor(actor, dt);
            }

            // Non-actor NPCs that started the conversation still look at Leo.
            Transform source = manager.CurrentSource;
            if (source != null && source.GetComponent<DialogueActor>() == null &&
                source.TryGetComponent(out InspectableObject inspectable) && inspectable.FacesInteractor)
            {
                Remember(source, inspectable.ReturnsToOriginalFacing);
                TurnTowards(source, Leo.position, actorTurnSpeed * dt, 0f);
            }

            // 3. Camera leans toward the conversation.
            IsometricCameraRig rig = IsometricCameraRig.Main;
            if (rig != null && rig.isActiveAndEnabled)
            {
                Transform other = speakerBody != null ? speakerBody : lastOther;
                if (other != null)
                {
                    float d = FlatDistance(other.position, Leo.position);
                    float lean = d > 9f ? 0.3f : cameraLean;
                    Vector3 point = Vector3.Lerp(Leo.position, other.position, lean);
                    point.y = Leo.position.y;
                    rig.SetDialogueFocus(point, d > 9f ? 0.95f : cameraZoom);
                }
                else
                {
                    rig.SetDialogueFocus(Leo.position, 0.9f);
                }
            }
        }

        private void UpdateActor(DialogueActor actor, float dt)
        {
            Transform t = actor.transform;
            staging.TryGetValue(actor, out Staging s);
            Mode mode = s != null ? s.mode : Mode.Free;

            if (mode == Mode.Beside)
            {
                // Stand shoulder to shoulder with Leo, looking where he looks.
                Vector3 goal = Leo.position + Leo.right * (0.85f * s.side) + Leo.forward * 0.1f;
                if (MoveTo(actor, goal, dt))
                {
                    actor.SetWalkSpeed(0f);
                    TurnTo(t, actor.LookRotation(Leo.forward), actorTurnSpeed * 0.6f * dt);
                }
                return;
            }

            if (mode == Mode.Approach)
            {
                Vector3 goal = s.goal;
                if (MoveTo(actor, goal, dt))
                {
                    actor.SetWalkSpeed(0f);
                    s.mode = Mode.Free;
                }
                return;
            }

            actor.SetWalkSpeed(0f);
            if (!actor.turnToFace) return;

            Vector3? look = null;
            if (speakerBody == t) look = Leo.position;                     // I'm talking: look at Leo
            else if (speakerBody != null) look = speakerBody.position;     // someone else talks: look at them
            else if (leoSpeaking) look = Leo.position;                      // Leo talks: look at Leo
            else if (lastOther == t) look = Leo.position;                   // Leo is thinking at me
            if (look.HasValue)
            {
                Remember(t);
                Vector3 dir = look.Value - t.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) TurnTo(t, actor.LookRotation(dir), actorTurnSpeed * dt);
            }
        }

        /// <summary>Walks toward a point; returns true once arrived.</summary>
        private bool MoveTo(DialogueActor actor, Vector3 goal, float dt)
        {
            Transform t = actor.transform;
            Vector3 to = goal - t.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 0.08f) return true;
            float step = Mathf.Min(d, actor.walkSpeed * Mathf.Clamp01(d * 1.5f + 0.3f) * dt);
            t.position += to / d * step;
            TurnTo(t, actor.LookRotation(to), actorTurnSpeed * 1.5f * dt);
            actor.SetWalkSpeed(Mathf.Clamp01(step / dt / 3.5f));
            return false;
        }

        private void StartApproach(DialogueActor actor, float stopDistance)
        {
            Staging s = Get(actor);
            if (s.mode == Mode.Beside) return;
            Vector3 dir = actor.transform.position - Leo.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Leo.forward;
            s.goal = Leo.position + dir.normalized * stopDistance;
            s.goal.y = actor.transform.position.y;
            s.mode = Mode.Approach;
        }

        private void EndConversation()
        {
            foreach (KeyValuePair<Transform, Quaternion> pair in originalFacing)
            {
                if (pair.Key != null) returning.Add(new KeyValuePair<Transform, Quaternion>(pair.Key, pair.Value));
            }
            originalFacing.Clear();
            foreach (KeyValuePair<DialogueActor, Staging> pair in staging)
            {
                if (pair.Key != null) pair.Key.SetWalkSpeed(0f);
            }
            staging.Clear();
            speakerBody = null;
            lastOther = null;
            leoSpeaking = false;
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.SetDialogueFocus(null);
        }

        // NPCs who asked to go back to their pose turn back after the talk.
        private readonly List<KeyValuePair<Transform, Quaternion>> returning = new List<KeyValuePair<Transform, Quaternion>>();

        private void Update()
        {
            if (manager != null && manager.IsOpen) return;
            for (int i = returning.Count - 1; i >= 0; i--)
            {
                Transform t = returning[i].Key;
                if (t == null) { returning.RemoveAt(i); continue; }
                t.rotation = Quaternion.RotateTowards(t.rotation, returning[i].Value, actorTurnSpeed * 0.5f * Time.deltaTime);
                if (Quaternion.Angle(t.rotation, returning[i].Value) < 0.5f) returning.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ Helpers

        private void Remember(Transform t, bool returnAfter = false)
        {
            if (!returnAfter || originalFacing.ContainsKey(t)) return;
            originalFacing[t] = t.rotation;
        }

        private Staging Get(DialogueActor actor)
        {
            if (!staging.TryGetValue(actor, out Staging s))
            {
                s = new Staging();
                staging[actor] = s;
            }
            return s;
        }

        private DialogueActor FindActor(string id)
        {
            DialogueActor best = null;
            float bestDistance = float.MaxValue;
            foreach (DialogueActor actor in DialogueActor.All)
            {
                if (actor == null || actor.Id != id) continue;
                float d = Leo != null ? FlatDistance(actor.transform.position, Leo.position) : 0f;
                if (d < bestDistance) { best = actor; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Finds the body of a speaker: an actor, the conversation's source, or the nearest thing with that voice.</summary>
        private Transform Resolve(DialogueSpeaker speaker)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            void Consider(Transform t)
            {
                if (t == null || !t.gameObject.activeInHierarchy) return;
                float d = FlatDistance(t.position, Leo.position);
                if (d < bestDistance) { best = t; bestDistance = d; }
            }

            foreach (DialogueActor actor in DialogueActor.All)
            {
                if (actor != null && actor.speaker == speaker) Consider(actor.transform);
            }
            if (best != null) return best;

            Transform source = manager.CurrentSource;
            if (source != null && source.TryGetComponent(out InspectableObject src) && src.Speaker == speaker) return source;

            foreach (InspectableObject inspectable in FindObjectsByType<InspectableObject>(FindObjectsSortMode.None))
            {
                if (inspectable.Speaker == speaker) Consider(inspectable.transform);
            }
            foreach (Hacking.HackableObstacle obstacle in FindObjectsByType<Hacking.HackableObstacle>(FindObjectsSortMode.None))
            {
                if (obstacle.speaker == speaker) Consider(obstacle.portraitTarget != null ? obstacle.portraitTarget : obstacle.transform);
            }
            if (best != null && bestDistance < 30f) return best;
            return source;
        }

        private static void TurnTowards(Transform t, Vector3 point, float maxDegrees, float yawOffset)
        {
            Vector3 dir = point - t.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            TurnTo(t, Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f), maxDegrees);
        }

        private static void TurnTo(Transform t, Quaternion goal, float maxDegrees)
        {
            t.rotation = Quaternion.RotateTowards(t.rotation, goal, maxDegrees);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
