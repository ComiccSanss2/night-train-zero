using System.Collections;
using System.Collections.Generic;
using Subnet.CameraSystem;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Interaction;
using Subnet.Player;
using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>
    /// A place to sit and talk (a bench, a crate, the roof's ledge). Inspecting it sits Leo down for real — and Kira
    /// and/or Byte next to him if they're around — then the camera leaves the isometric angle for a framed shot
    /// with cinema bars while the conversation plays. When it ends, everyone gets up and the camera comes back.
    /// Works on top of the InspectableObject on the same object (its lines are the conversation).
    /// </summary>
    [RequireComponent(typeof(InspectableObject))]
    public class SitSpot : MonoBehaviour
    {
        [SerializeField] private Transform leoSeat;
        [Tooltip("Actor ids (kira, byte) and where each sits.")]
        [SerializeField] private string[] sitterIds = { };
        [SerializeField] private Transform[] sitterSeats = { };
        [Tooltip("Height of the seat surface for Leo / each sitter (world y). Bodies are lowered/raised onto it.")]
        [SerializeField] private float leoSurface = float.NaN;
        [SerializeField] private float[] sitterSurfaces = { };
        [Header("Camera")]
        [SerializeField] private Transform shotPoint;
        [SerializeField] private Transform shotLook;
        [SerializeField] private float shotFov = 32f;

        private class Sitter
        {
            public Transform body;
            public Vector3 position;
            public Quaternion rotation;
            public CompanionFollower follower;
            public bool followerWasEnabled;
            public float surface = float.NaN;
        }

        private readonly List<Sitter> sitters = new List<Sitter>();
        private Transform leo;
        private Vector3 leoStand;
        private Quaternion leoStandRotation;
        private bool seated;

        private void Awake()
        {
            var inspectable = GetComponent<InspectableObject>();
            inspectable.onInspectStarted.AddListener(Begin);
            inspectable.onInspectFinished.AddListener(End);
        }

        private void OnDisable()
        {
            if (seated) StandUp();
        }

        private void Begin()
        {
            if (seated || leoSeat == null) return;
            StartCoroutine(SitDown());
        }

        private IEnumerator SitDown()
        {
            seated = true;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            leo = p != null ? p.transform : null;
            ScreenFader fader = FindFirstObjectByType<ScreenFader>();
            if (fader != null) yield return fader.FadeTo(0.85f, 0.18f);

            if (leo != null)
            {
                leoStand = leo.position;
                leoStandRotation = leo.rotation;
                Place(leo, leoSeat);
                Freeze(leo, true);
                CharacterPose.SetSitting(leo, true);
                DialogueDirector.Frozen.Add(leo);
            }

            sitters.Clear();
            for (int i = 0; i < sitterIds.Length && i < sitterSeats.Length; i++)
            {
                DialogueActor actor = Nearest(sitterIds[i]);
                if (actor == null || sitterSeats[i] == null) continue;
                var s = new Sitter { body = actor.transform, position = actor.transform.position, rotation = actor.transform.rotation,
                    surface = i < sitterSurfaces.Length ? sitterSurfaces[i] : float.NaN };
                s.follower = actor.GetComponent<CompanionFollower>();
                if (s.follower != null) { s.followerWasEnabled = s.follower.enabled; s.follower.enabled = false; }
                Place(actor.transform, sitterSeats[i]);
                CharacterPose.SetSitting(actor.transform, true);
                DialogueDirector.Frozen.Add(actor.transform);
                sitters.Add(s);
            }

            // Let the sit pose settle for a frame, then put every bottom exactly on the seat (no sinking through
            // benches, no floating), then frame everyone who sat down, whole, between the bars.
            yield return null;
            if (leo != null && !float.IsNaN(leoSurface)) SnapOnto(leo, leoSurface);
            foreach (Sitter st in sitters) if (!float.IsNaN(st.surface)) SnapOnto(st.body, st.surface);
            IsometricCameraRig rig = IsometricCameraRig.Main;
            if (rig != null)
            {
                bool any = false;
                Bounds group = default;
                if (leo != null) { group = IsometricCameraRig.BodyBounds(leo); any = true; }
                foreach (Sitter s in sitters)
                {
                    Bounds b = IsometricCameraRig.BodyBounds(s.body);
                    if (!any) { group = b; any = true; } else group.Encapsulate(b);
                }
                Vector3 view = shotPoint != null && shotLook != null ? shotLook.position - shotPoint.position : rig.transform.forward;
                if (any) rig.FrameBounds(group, view, shotFov);
                else if (shotPoint != null && shotLook != null) rig.SetShot(shotPoint.position, shotLook.position, shotFov);
            }
            if (CinematicDirector.Instance != null) CinematicDirector.Instance.DialogueLetterbox(true);
            if (fader != null) yield return fader.FadeTo(0f, 0.35f);
        }

        private void End()
        {
            if (!seated) return;
            StopAllCoroutines();
            StartCoroutine(GetUp());
        }

        private IEnumerator GetUp()
        {
            ScreenFader fader = FindFirstObjectByType<ScreenFader>();
            // A beat of silence on the bench before anyone moves.
            yield return new WaitForSeconds(0.4f);
            if (fader != null) yield return fader.FadeTo(0.85f, 0.2f);
            StandUp();
            if (fader != null) yield return fader.FadeTo(0f, 0.4f);
        }

        private void StandUp()
        {
            seated = false;
            if (leo != null)
            {
                CharacterPose.SetSitting(leo, false);
                DialogueDirector.Frozen.Remove(leo);
                Freeze(leo, false);
                Place(leo, leoStand, leoStandRotation);
            }
            foreach (Sitter s in sitters)
            {
                if (s.body == null) continue;
                CharacterPose.SetSitting(s.body, false);
                DialogueDirector.Frozen.Remove(s.body);
                s.body.SetPositionAndRotation(s.position, s.rotation);
                if (s.follower != null) s.follower.enabled = s.followerWasEnabled;
            }
            sitters.Clear();
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.ClearShot();
            if (CinematicDirector.Instance != null) CinematicDirector.Instance.DialogueLetterbox(false);
        }

        /// <summary>Moves a seated body vertically so its lowest point rests on the seat surface.</summary>
        public static void SnapOnto(Transform body, float surfaceY)
        {
            if (body == null) return;
            Bounds b = IsometricCameraRig.BodyBounds(body);
            body.position += Vector3.up * (surfaceY + 0.01f - b.min.y);
        }

        /// <summary>Leo's controller would apply gravity and push him off the seat: switch it off while seated.</summary>
        private static void Freeze(Transform t, bool frozen)
        {
            var controller = t.GetComponent<IsometricPlayerController>();
            if (controller != null) controller.enabled = !frozen;
            var cc = t.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = !frozen;
        }

        private static void Place(Transform t, Transform seat) => Place(t, seat.position, seat.rotation);

        private static void Place(Transform t, Vector3 position, Quaternion rotation)
        {
            var controller = t.GetComponent<IsometricPlayerController>();
            if (controller != null && controller.enabled) controller.Teleport(position, rotation);
            else t.SetPositionAndRotation(position, rotation);
        }

        private DialogueActor Nearest(string id)
        {
            DialogueActor best = null;
            float bestDistance = 25f;
            foreach (DialogueActor actor in DialogueActor.All)
            {
                if (actor == null || actor.Id != id) continue;
                float d = Vector3.Distance(actor.transform.position, transform.position);
                if (d < bestDistance) { best = actor; bestDistance = d; }
            }
            return best;
        }
    }
}
