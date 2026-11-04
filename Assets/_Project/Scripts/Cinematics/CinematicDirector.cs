using System;
using System.Collections;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.Cinematics
{
    /// <summary>
    /// Code-driven cutscene orchestrator:
    /// - locks gameplay input (InputLock) for the whole sequence,
    /// - animates cinematic letterbox bars,
    /// - fades from / to black,
    /// - glides the camera between shot points with easing, handheld drift and optional shake,
    /// - plays narration / dialogue per shot and waits for it,
    /// - blends smoothly back into the isometric gameplay camera and returns control,
    /// - hold-to-skip (Cancel) that still leaves the game in a valid state.
    /// One per gameplay scene, on the "CinematicDirector" GameObject.
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        /// <summary>Scene singleton for mid-game triggers.</summary>
        public static CinematicDirector Instance { get; private set; }

        [Header("Camera")]
        [Tooltip("Camera that performs the cinematic. Empty = Camera.main.")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("Gameplay follow rig on the same camera. Disabled during cutscenes.")]
        [SerializeField] private IsometricCameraRig gameplayRig;

        [Header("Handheld Feel")]
        [Tooltip("World-unit amplitude of the slow 'breathing' drift while holding a shot.")]
        [SerializeField] private float driftAmplitude = 0.04f;
        [SerializeField] private float driftSpeed = 0.35f;
        [SerializeField] private float driftRollDegrees = 0.35f;
        [SerializeField] private float shakeMaxOffset = 0.25f;
        [SerializeField] private float shakeDecay = 1.5f;

        [Header("Screen")]
        [SerializeField] private ScreenFader fader;
        [Tooltip("Top letterbox bar: anchors (0,1)-(1,1), pivot (0.5,1), height 0.")]
        [SerializeField] private RectTransform topBar;
        [Tooltip("Bottom letterbox bar: anchors (0,0)-(1,0), pivot (0.5,0), height 0.")]
        [SerializeField] private RectTransform bottomBar;
        [Tooltip("Bar height in canvas units (reference resolution 1920x1080 → ~120 = 2.39:1 feel).")]
        [SerializeField] private float letterboxHeight = 120f;
        [SerializeField] private float letterboxDuration = 0.7f;

        [Header("Skip")]
        [SerializeField] private float skipHoldDuration = 1.2f;
        [Tooltip("Optional group containing 'HOLD [X] TO SKIP'. Fades in while Cancel is held.")]
        [SerializeField] private CanvasGroup skipHintGroup;
        [Tooltip("Optional radial/horizontal filled Image showing skip progress.")]
        [SerializeField] private Image skipProgressFill;

        [Header("Audio")]
        [Tooltip("Looping source for sequence ambience (rain, station rumble).")]
        [SerializeField] private AudioSource ambienceSource;
        [Tooltip("One-shot source for shot sounds (train horn, door hiss).")]
        [SerializeField] private AudioSource sfxSource;

        [Header("Auto Play")]
        [Tooltip("The intro ('The Moving Boxes'). Played on Start if enabled.")]
        [SerializeField] private CinematicSequence introSequence;
        [SerializeField] private bool playIntroOnStart = true;

        /// <summary>True while a sequence is running.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Raised when any sequence ends (normally or skipped).</summary>
        public event Action<CinematicSequence> SequenceFinished;

        private CinematicSequence current;
        private Action pendingCallback;
        private Coroutine mainRoutine;
        private Coroutine letterboxRoutine;
        private Coroutine ambienceRoutine;
        private float skipTimer;
        private bool skipping;
        private float trauma;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CinematicDirector] Duplicate instance destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (targetCamera == null) targetCamera = Camera.main;
            if (gameplayRig == null && targetCamera != null) gameplayRig = targetCamera.GetComponent<IsometricCameraRig>();

            SetBarHeight(0f);
            if (skipHintGroup != null) skipHintGroup.alpha = 0f;
            if (skipProgressFill != null) skipProgressFill.fillAmount = 0f;

            // Lock immediately so the player can't move during the first frame before Start().
            if (playIntroOnStart && introSequence != null)
            {
                InputLock.Acquire(this);
                if (introSequence.fadeFromBlack && fader != null) fader.SetInstant(1f);
            }
        }

        private void Start()
        {
            if (playIntroOnStart && introSequence != null) Play(introSequence);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InputLock.Release(this);
        }

        // ------------------------------------------------------------------ Public API

        /// <summary>Plays <paramref name="sequence"/>. <paramref name="onComplete"/> fires when control returns.</summary>
        public void Play(CinematicSequence sequence, Action onComplete = null)
        {
            if (sequence == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (IsPlaying)
            {
                Debug.LogWarning($"[CinematicDirector] '{sequence.name}' requested while '{current.name}' is playing. Ignored.", sequence);
                return;
            }

            current = sequence;
            pendingCallback = onComplete;
            skipTimer = 0f;
            skipping = false;
            mainRoutine = StartCoroutine(RunSequence(sequence));
        }

        /// <summary>Immediately ends the running sequence in a valid gameplay state.</summary>
        public void Skip()
        {
            if (!IsPlaying || skipping) return;
            skipping = true;
            if (mainRoutine != null) StopCoroutine(mainRoutine);
            mainRoutine = StartCoroutine(SkipRoutine(current));
        }

        /// <summary>Adds shake during cinematics (respects the accessibility toggle).</summary>
        public void AddTrauma(float amount)
        {
            if (GameSettings.ScreenShake) trauma = Mathf.Clamp01(trauma + amount);
        }

        // ------------------------------------------------------------------ Sequence flow

        private IEnumerator RunSequence(CinematicSequence sequence)
        {
            IsPlaying = true;
            InputLock.Acquire(this);
            if (gameplayRig != null) gameplayRig.enabled = false;
            sequence.onSequenceStarted?.Invoke();

            StartAmbience(sequence);

            bool startsBlack = sequence.fadeFromBlack && fader != null;
            if (startsBlack) fader.SetInstant(1f);
            if (sequence.useLetterbox) SetLetterbox(true, startsBlack);

            // Under black, pre-place the camera on the first shot so the fade reveals it directly.
            CinematicShot first = sequence.shots.Count > 0 ? sequence.shots[0] : null;
            if (startsBlack && first != null && first.cameraPoint != null) SnapTo(first);

            if (startsBlack)
            {
                yield return WaitRealtime(sequence.blackHoldBeforeFade);
                fader.FadeTo(0f, sequence.fadeInDuration); // runs alongside the first shot
            }

            for (int i = 0; i < sequence.shots.Count; i++)
            {
                CinematicShot shot = sequence.shots[i];
                if (shot == null || shot.cameraPoint == null)
                {
                    Debug.LogWarning($"[CinematicDirector] Shot {i} in '{sequence.name}' has no camera point. Skipped.", sequence);
                    continue;
                }

                if (shot.startSound != null && sfxSource != null) sfxSource.PlayOneShot(shot.startSound, shot.startSoundVolume);
                if (shot.shakeOnStart > 0f) AddTrauma(shot.shakeOnStart);
                shot.onShotStarted?.Invoke();

                bool instant = shot.cut || shot.travelDuration <= 0f || (i == 0 && startsBlack);
                if (instant) SnapTo(shot);
                else yield return TravelTo(shot);

                shot.onShotArrived?.Invoke();

                yield return PlayShotNarration(shot);
                yield return HoldOn(shot, shot.holdDuration);
            }

            yield return EndSequence(sequence);
        }

        private IEnumerator EndSequence(CinematicSequence sequence)
        {
            if (sequence.fadeToBlackAtEnd && fader != null)
            {
                yield return fader.FadeTo(1f, sequence.fadeOutDuration);
            }

            if (sequence.useLetterbox) SetLetterbox(false, false);

            if (sequence.blendToGameplay && gameplayRig != null && !sequence.fadeToBlackAtEnd)
            {
                yield return BlendToGameplay(sequence.blendToGameplayDuration);
            }

            Complete(sequence);
        }

        private IEnumerator SkipRoutine(CinematicSequence sequence)
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.ForceClose(false);
            HideSkipUI();

            if (fader != null) yield return fader.FadeTo(1f, 0.35f);

            SetLetterbox(false, true);

            if (sequence.fadeToBlackAtEnd)
            {
                // Stay black: the caller is about to change scene.
                Complete(sequence);
                yield break;
            }

            if (sequence.blendToGameplay && gameplayRig != null)
            {
                gameplayRig.enabled = true;
                gameplayRig.SnapToTarget();
            }
            else
            {
                // Rest on the last valid shot.
                for (int i = sequence.shots.Count - 1; i >= 0; i--)
                {
                    if (sequence.shots[i] != null && sequence.shots[i].cameraPoint != null)
                    {
                        SnapTo(sequence.shots[i]);
                        break;
                    }
                }
            }

            if (fader != null) fader.FadeTo(0f, 0.6f);
            Complete(sequence);
        }

        private void Complete(CinematicSequence sequence)
        {
            if (gameplayRig != null && sequence.blendToGameplay && !sequence.fadeToBlackAtEnd)
            {
                gameplayRig.enabled = true;
            }

            IsPlaying = false;
            skipping = false;
            mainRoutine = null;
            trauma = 0f;
            HideSkipUI();
            InputLock.Release(this);

            sequence.onSequenceFinished?.Invoke();
            SequenceFinished?.Invoke(sequence);

            Action callback = pendingCallback;
            pendingCallback = null;
            current = null;
            callback?.Invoke();
        }

        // ------------------------------------------------------------------ Camera moves

        private IEnumerator TravelTo(CinematicShot shot)
        {
            Vector3 fromPosition = targetCamera.transform.position;
            Quaternion fromRotation = targetCamera.transform.rotation;
            float fromFov = targetCamera.fieldOfView;
            float toFov = shot.fieldOfView > 0f ? shot.fieldOfView : fromFov;

            for (float t = 0f; t < shot.travelDuration; t += Time.deltaTime)
            {
                float e = Easing.Evaluate(shot.travelEase, t / shot.travelDuration);
                // Read the point every frame so moving points (train interiors) are tracked.
                Vector3 position = Vector3.LerpUnclamped(fromPosition, shot.cameraPoint.position, e);
                Quaternion rotation = Quaternion.SlerpUnclamped(fromRotation, shot.cameraPoint.rotation, e);
                ApplyPose(position, rotation, Mathf.Lerp(fromFov, toFov, e), 1f);
                yield return null;
            }
            ApplyPose(shot.cameraPoint.position, shot.cameraPoint.rotation, toFov, 1f);
        }

        private IEnumerator HoldOn(CinematicShot shot, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                ApplyPose(shot.cameraPoint.position, shot.cameraPoint.rotation, targetCamera.fieldOfView, 1f);
                yield return null;
            }
        }

        private IEnumerator PlayShotNarration(CinematicShot shot)
        {
            bool hasDialogue = shot.dialogue != null;
            bool hasLines = shot.narrationLines != null && shot.narrationLines.Length > 0;
            if (!hasDialogue && !hasLines) yield break;

            if (DialogueManager.Instance == null)
            {
                Debug.LogWarning("[CinematicDirector] Shot has narration but no DialogueManager exists in the scene.", this);
                yield break;
            }

            bool done = false;
            if (hasDialogue) DialogueManager.Instance.Play(shot.dialogue, () => done = true);
            else DialogueManager.Instance.PlayLines(shot.narrationLines, shot.narrationSpeaker, () => done = true);

            // Keep the handheld drift alive while text is on screen.
            while (!done)
            {
                ApplyPose(shot.cameraPoint.position, shot.cameraPoint.rotation, targetCamera.fieldOfView, 1f);
                yield return null;
            }
        }

        private IEnumerator BlendToGameplay(float duration)
        {
            Vector3 fromPosition = targetCamera.transform.position;
            Quaternion fromRotation = targetCamera.transform.rotation;
            float fromFov = targetCamera.fieldOfView;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float e = Easing.Evaluate(EaseType.SmootherStep, t / duration);
                gameplayRig.GetDesiredPose(out Vector3 toPosition, out Quaternion toRotation);
                // Drift fades out as we approach the gameplay framing.
                ApplyPose(
                    Vector3.LerpUnclamped(fromPosition, toPosition, e),
                    Quaternion.SlerpUnclamped(fromRotation, toRotation, e),
                    Mathf.Lerp(fromFov, gameplayRig.FieldOfView, e),
                    1f - e);
                yield return null;
            }

            gameplayRig.enabled = true;
            gameplayRig.SnapToTarget();
        }

        private void SnapTo(CinematicShot shot)
        {
            float fov = shot.fieldOfView > 0f ? shot.fieldOfView : targetCamera.fieldOfView;
            ApplyPose(shot.cameraPoint.position, shot.cameraPoint.rotation, fov, 1f);
        }

        /// <summary>Applies a pose with handheld drift and shake layered on top.</summary>
        private void ApplyPose(Vector3 position, Quaternion rotation, float fov, float driftWeight)
        {
            float time = Time.time * driftSpeed;
            Vector3 drift = new Vector3(
                Mathf.PerlinNoise(time, 0.37f) * 2f - 1f,
                Mathf.PerlinNoise(0.71f, time) * 2f - 1f,
                0f) * (driftAmplitude * driftWeight);
            float roll = (Mathf.PerlinNoise(time * 0.7f, time * 0.3f) * 2f - 1f) * driftRollDegrees * driftWeight;

            if (trauma > 0f)
            {
                float shake = trauma * trauma;
                float fast = Time.time * 24f;
                drift += new Vector3(Mathf.PerlinNoise(fast, 3.1f) * 2f - 1f, Mathf.PerlinNoise(5.3f, fast) * 2f - 1f, 0f) * (shakeMaxOffset * shake);
                trauma = Mathf.Max(0f, trauma - shakeDecay * Time.deltaTime);
            }

            Quaternion finalRotation = rotation * Quaternion.Euler(0f, 0f, roll);
            targetCamera.transform.SetPositionAndRotation(position + rotation * drift, finalRotation);
            if (!targetCamera.orthographic && fov > 0f) targetCamera.fieldOfView = fov;
        }

        // ------------------------------------------------------------------ Letterbox

        private void SetLetterbox(bool show, bool instant)
        {
            if (topBar == null || bottomBar == null) return;
            if (letterboxRoutine != null) StopCoroutine(letterboxRoutine);

            float target = show ? letterboxHeight : 0f;
            if (instant || letterboxDuration <= 0f)
            {
                SetBarHeight(target);
                letterboxRoutine = null;
            }
            else
            {
                letterboxRoutine = StartCoroutine(AnimateLetterbox(target));
            }
        }

        private IEnumerator AnimateLetterbox(float target)
        {
            float from = topBar.sizeDelta.y;
            for (float t = 0f; t < letterboxDuration; t += GameTime.UnscaledDelta)
            {
                SetBarHeight(Mathf.Lerp(from, target, Easing.Evaluate(EaseType.SmootherStep, t / letterboxDuration)));
                yield return null;
            }
            SetBarHeight(target);
            letterboxRoutine = null;
        }

        private void SetBarHeight(float height)
        {
            if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, height);
            if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, height);
        }

        // ------------------------------------------------------------------ Ambience

        private void StartAmbience(CinematicSequence sequence)
        {
            if (ambienceSource == null || sequence.ambienceLoop == null) return;
            if (ambienceSource.clip == sequence.ambienceLoop && ambienceSource.isPlaying) return;

            ambienceSource.clip = sequence.ambienceLoop;
            ambienceSource.loop = true;
            ambienceSource.volume = 0f;
            ambienceSource.Play();

            if (ambienceRoutine != null) StopCoroutine(ambienceRoutine);
            ambienceRoutine = StartCoroutine(FadeAmbience(sequence.ambienceVolume, sequence.ambienceFadeIn));
        }

        private IEnumerator FadeAmbience(float target, float duration)
        {
            float from = ambienceSource.volume;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                ambienceSource.volume = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }
            ambienceSource.volume = target;
            ambienceRoutine = null;
        }

        // ------------------------------------------------------------------ Skip input

        private void Update()
        {
            if (!IsPlaying || skipping || current == null || !current.skippable)
            {
                return;
            }

            if (GameInput.Cancel.IsPressed()) skipTimer += GameTime.UnscaledDelta;
            else skipTimer = Mathf.Max(0f, skipTimer - GameTime.UnscaledDelta * 2f);

            float progress = Mathf.Clamp01(skipTimer / skipHoldDuration);
            if (skipProgressFill != null) skipProgressFill.fillAmount = progress;
            if (skipHintGroup != null)
            {
                float targetAlpha = skipTimer > 0.05f ? 1f : 0f;
                skipHintGroup.alpha = Mathf.MoveTowards(skipHintGroup.alpha, targetAlpha, GameTime.UnscaledDelta * 4f);
            }

            if (progress >= 1f) Skip();
        }

        private void HideSkipUI()
        {
            skipTimer = 0f;
            if (skipProgressFill != null) skipProgressFill.fillAmount = 0f;
            if (skipHintGroup != null) skipHintGroup.alpha = 0f;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            for (float t = 0f; t < seconds; t += GameTime.UnscaledDelta) yield return null;
        }
    }
}
