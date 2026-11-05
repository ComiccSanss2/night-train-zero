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
        private bool framed;

        /// <summary>
        /// People who are sitting (or otherwise posed) keep their facing: the director won't turn them toward
        /// whoever talks. Filled by SitSpot.
        /// </summary>
        public static readonly HashSet<Transform> Frozen = new HashSet<Transform>();

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
            if (verb == "cam") { Frame(id); return; }
            if (verb == "sit") { SitDown(id); return; }
            if (verb == "emote")
            {
                // The current speaker (or Leo, for his own lines and thoughts) acts it out.
                Transform who = speakerBody != null ? speakerBody : Leo;
                Gameplay.CharacterPose.Emote(who, id);
                return;
            }
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
            // Big words land a little harder while the camera is framing a moment.
            if (framed && IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(Mathf.Clamp(0.08f * strength, 0.04f, 0.18f));
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
            if (leoLook != null && leoLook != Leo && !Frozen.Contains(Leo)) TurnTowards(Leo, leoLook.position, leoTurnSpeed * dt, 0f);

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
            if (Frozen.Contains(t)) return;
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
            if (seated.Count > 0) StandEveryone();
            if (framed)
            {
                framed = false;
                if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.ClearShot();
                if (Cinematics.CinematicDirector.Instance != null) Cinematics.CinematicDirector.Instance.DialogueLetterbox(false);
            }
        }

        // ------------------------------------------------------------------ Sitting ({sit=...})

        private class Seated
        {
            public Transform body;
            public Gameplay.SeatAnchor seat;
            public Vector3 position;
            public Quaternion rotation;
            public Gameplay.CompanionFollower follower;
        }

        private readonly List<Seated> seated = new List<Seated>();

        /// <summary>{sit=kira} / {sit=leo} / {sit=byte}: that character sits on the nearest free seat until the talk ends.</summary>
        private void SitDown(string id)
        {
            Transform who = id == "leo" ? Leo : (FindActor(id) != null ? FindActor(id).transform : null);
            if (who == null) return;
            foreach (Seated s in seated) if (s.body == who) return;
            Gameplay.SeatAnchor seat = Gameplay.SeatAnchor.Nearest(who.position, 6f);
            if (seat == null) return;
            var entry = new Seated { body = who, seat = seat, position = who.position, rotation = who.rotation, follower = who.GetComponent<Gameplay.CompanionFollower>() };
            seat.occupant = who;
            if (entry.follower != null) entry.follower.enabled = false;
            var controller = who.GetComponent<Player.IsometricPlayerController>();
            if (controller != null) controller.enabled = false;
            var cc = who.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            bool person = Gameplay.CharacterPose.HasParameter(who.GetComponentInChildren<Animator>(), "Sit");
            float hip = person ? Height(who) * 0.3f : 0f;
            who.SetPositionAndRotation(seat.transform.position + Vector3.up * (seat.seatHeight - hip), seat.transform.rotation);
            Gameplay.CharacterPose.SetSitting(who, true);
            Frozen.Add(who);
            seated.Add(entry);
            StartCoroutine(SnapNextFrame(who, seat.transform.position.y + seat.seatHeight));
        }

        private System.Collections.IEnumerator SnapNextFrame(Transform who, float surface)
        {
            yield return null; // the sit pose is applied by the animator first
            if (who != null && Frozen.Contains(who)) Gameplay.SitSpot.SnapOnto(who, surface);
        }

        private void StandEveryone()
        {
            foreach (Seated s in seated)
            {
                if (s.body == null) continue;
                Gameplay.CharacterPose.SetSitting(s.body, false);
                Frozen.Remove(s.body);
                if (s.seat != null) s.seat.occupant = null;
                // Stand up in front of the seat, not back where they were (they did walk over to sit).
                Vector3 front = s.seat != null ? s.seat.transform.position + s.seat.transform.forward * 0.55f : s.position;
                front.y = s.position.y;
                s.body.SetPositionAndRotation(front, s.seat != null ? s.seat.transform.rotation : s.rotation);
                var cc = s.body.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                var controller = s.body.GetComponent<Player.IsometricPlayerController>();
                if (controller != null) controller.enabled = true;
                if (s.follower != null) s.follower.enabled = true;
            }
            seated.Clear();
        }

        // ------------------------------------------------------------------ Framing ({cam=...})

        /// <summary>
        /// {cam=close}: close-up on whoever talks. {cam=leo} / {cam=kira} / {cam=byte}: close-up on them.
        /// {cam=two}: Leo and the person he's talking with. {cam=off}: back to the isometric camera.
        /// Framed moments get cinema bars; they end with the conversation.
        /// </summary>
        private void Frame(string id)
        {
            IsometricCameraRig rig = IsometricCameraRig.Main;
            if (rig == null || Leo == null) return;
            if (id == "off")
            {
                framed = false;
                rig.ClearShot();
                if (Cinematics.CinematicDirector.Instance != null) Cinematics.CinematicDirector.Instance.DialogueLetterbox(false);
                return;
            }

            Vector3 pos, look;
            float fov;
            Bounds subject;
            float margin;
            if (id == "two")
            {
                Transform other = speakerBody != null ? speakerBody : lastOther;
                if (other == null) other = manager.CurrentSource;
                if (other == null) { CloseUp(Leo, out pos, out look, out fov); subject = Chest(Leo); margin = 1.3f; }
                else
                {
                    TwoShot(Leo, other, out pos, out look, out fov);
                    subject = IsometricCameraRig.BodyBounds(Leo);
                    subject.Encapsulate(IsometricCameraRig.BodyBounds(other));
                    margin = 1.12f;
                }
            }
            else
            {
                Transform who = id == "close" ? (speakerBody != null ? speakerBody : Leo)
                    : id == "leo" ? Leo
                    : (FindActor(id) != null ? FindActor(id).transform : (speakerBody != null ? speakerBody : Leo));
                CloseUp(who, out pos, out look, out fov);
                subject = Chest(who);
                margin = 1.3f;
            }
            // The angle comes from CloseUp/TwoShot (they pick a side nobody blocks); the distance is fitted so the
            // whole face/body stays inside the frame between the cinema bars.
            rig.FrameBounds(subject, look - pos, fov, margin);
            framed = true;
            if (Cinematics.CinematicDirector.Instance != null) Cinematics.CinematicDirector.Instance.DialogueLetterbox(true);
        }

        private static float Height(Transform t)
        {
            float top = t.position.y + 1f;
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                any = true;
            }
            return Mathf.Max(0.3f, top - t.position.y);
        }

        /// <summary>The way the isometric camera looks, flattened (so framed shots stay on the same side of the scene).</summary>
        private static Vector3 CameraBack()
        {
            Transform cam = IsometricCameraRig.Main != null ? IsometricCameraRig.Main.transform : null;
            Vector3 back = cam != null ? -cam.forward : new Vector3(-0.7f, 0f, -0.7f);
            back.y = 0f;
            return back.sqrMagnitude > 0.001f ? back.normalized : Vector3.back;
        }

        private static void CloseUp(Transform who, out Vector3 pos, out Vector3 look, out float fov)
        {
            float h = Height(who);
            Vector3 head = who.position + Vector3.up * (h * 0.8f);
            Vector3 front = who.forward;
            front.y = 0f;
            if (front.sqrMagnitude < 0.01f) front = CameraBack();
            front.Normalize();
            float distance = 1.7f + h * 1.1f;
            look = head - Vector3.up * (h * 0.05f);
            fov = 32f;

            // Try angles around the face, preferring the camera's side, and keep the first one where nobody
            // stands between the lens and the face (the listener's head used to fill the shot).
            float side = Vector3.Dot(Vector3.Cross(Vector3.up, front), CameraBack()) >= 0f ? 1f : -1f;
            float[] angles = { 35f * side, -35f * side, 60f * side, -60f * side, 15f * side, 90f * side, 0f };
            pos = head + (front * 0.6f + CameraBack() * 0.4f).normalized * distance + Vector3.up * (0.2f + h * 0.15f);
            foreach (float a in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * front;
                Vector3 candidate = head + dir * distance + Vector3.up * (0.2f + h * 0.15f);
                if (!Blocked(who, candidate, head)) { pos = candidate; return; }
            }
        }

        /// <summary>Is another character (or a wall) between the camera and the face?</summary>
        /// <summary>Head and shoulders: the top 60% of a body.</summary>
        private static Bounds Chest(Transform who)
        {
            Bounds b = IsometricCameraRig.BodyBounds(who);
            Vector3 min = b.min, max = b.max;
            min.y = max.y - b.size.y * 0.6f;
            b.SetMinMax(min, max);
            return b;
        }

        private static bool Blocked(Transform subject, Vector3 from, Vector3 to)
        {
            var bodies = new List<Transform>();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) bodies.Add(player.transform);
            foreach (DialogueActor actor in Object.FindObjectsByType<DialogueActor>(FindObjectsSortMode.None)) bodies.Add(actor.transform);
            foreach (Transform b in bodies)
            {
                if (b == null || b == subject || b.IsChildOf(subject) || subject.IsChildOf(b)) continue;
                float bh = Height(b);
                for (float k = 0.35f; k <= 0.95f; k += 0.3f)
                {
                    Vector3 p = b.position + Vector3.up * (bh * k);
                    Vector3 seg = to - from;
                    float t = Mathf.Clamp01(Vector3.Dot(p - from, seg) / Mathf.Max(0.0001f, seg.sqrMagnitude));
                    if (t > 0.02f && t < 0.95f && (from + seg * t - p).magnitude < 0.35f + bh * 0.15f) return true;
                }
            }
            // Walls and props (ignore characters' own colliders, handled above).
            foreach (RaycastHit hit in Physics.RaycastAll(to, (from - to).normalized, (from - to).magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(subject)) continue;
                if (hit.collider.GetComponentInParent<DialogueActor>() != null || hit.collider.CompareTag("Player")) continue;
                if (hit.distance > 0.3f) return true;
            }
            return false;
        }

        private static void TwoShot(Transform a, Transform b, out Vector3 pos, out Vector3 look, out float fov)
        {
            float h = Mathf.Max(Height(a), Height(b));
            Vector3 mid = (a.position + b.position) * 0.5f + Vector3.up * (h * 0.6f);
            Vector3 axis = b.position - a.position;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
            Vector3 perp = Vector3.Cross(Vector3.up, axis.normalized);
            if (Vector3.Dot(perp, CameraBack()) < 0f) perp = -perp;
            float distance = Mathf.Max(2.4f, axis.magnitude * 1.5f + 1.2f);
            pos = mid + (perp * 0.85f + CameraBack() * 0.15f).normalized * distance + Vector3.up * (0.5f + h * 0.3f);
            look = mid;
            fov = 34f;
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
