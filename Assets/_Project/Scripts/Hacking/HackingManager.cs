using System;
using System.Collections;
using System.Collections.Generic;
using Subnet.Audio;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Subnet.Hacking
{
    /// <summary>
    /// OBSTACLE HACKING, presented Undertale-style:
    ///  • Overworld → fight: "!" over Leo, camera punch-in, glitch bands sweep the screen, Leo's soul blinks
    ///    and flies into the arena, the arena powers on like an old CRT. Music crossfades (city keeps running muted).
    ///  • In the fight: a live 3D "SOURCE FEED" of the obstacle sits above the arena; it talks in its own
    ///    speech bubble (intro, taunts during waves, a line after each firewall segment falls).
    ///    Lines starting with "* " are flavor text in the bottom box, like Undertale's narration.
    ///  • Evasion waves + empathy puzzles (pick the right memory fragment). No game over: losing all
    ///    SANITY just reconnects and restarts the wave.
    ///  • Fight → overworld: arena collapses, bands sweep out, warm flash, city music opens back up in sync.
    /// The UI is built at runtime; one instance per gameplay scene.
    /// </summary>
    public partial class HackingManager : MonoBehaviour
    {
        public static HackingManager Instance { get; private set; }

        [Header("Arena")]
        [SerializeField] private Vector2 arenaSize = new Vector2(640f, 330f);
        [SerializeField] private float arenaCenterY = -20f;
        [SerializeField] private float soulSpeed = 290f;
        [SerializeField] private float soulRadius = 8f;
        [SerializeField] private float invulnerabilitySeconds = 1.1f;
        [SerializeField] private float grazeDistance = 14f;

        [Header("Look")]
        [Tooltip("Optional CRT overlay material (Subnet/UI/CRTOverlay).")]
        [SerializeField] private Material crtMaterial;
        [SerializeField] private float portraitFov = 32f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip connectClip;
        [SerializeField] private AudioClip waveStartClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip grazeClip;
        [SerializeField] private AudioClip tickClip;
        [SerializeField] private AudioClip firewallBreakClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip resolvedClip;
        [SerializeField] private AudioClip disconnectClip;
        [SerializeField] private AudioClip alertClip;
        [SerializeField] private AudioClip transitionClip;
        [SerializeField] private AudioClip soulBlinkClip;
        [SerializeField] private AudioClip transitionOutClip;
        [Tooltip("Optional fight music override (otherwise the scene's SceneMusic fight track is used).")]
        [SerializeField] private AudioClip fightMusic;

        /// <summary>True while a hacking session is on screen (including transitions).</summary>
        public bool IsActive { get; private set; }

        private const string CorrectFlag = "__hack_fragment_ok";
        private const string LastWrongFlag = "__hack_fragment_wrong_";
        private const string TutorialFlag = "hack_tutorial_seen";
        private const int SliceCount = 12;

        private readonly List<Image> firewallSegments = new List<Image>();
        private readonly List<RectTransform> slices = new List<RectTransform>();

        // Runtime UI
        private Canvas canvas;
        private RectTransform canvasRect;
        private Image background;
        private CanvasGroup content;
        private RectTransform arena;
        private RectTransform soul;
        private Image soulImage;
        private Image flash;
        private RectTransform firewallRow;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI statusText;
        private RectTransform portraitFrame;
        private RawImage portraitImage;
        private Image portraitFlash;
        private RectTransform bubble;
        private TextMeshProUGUI bubbleText;
        private TMP_FontAsset bubbleDefaultFont;
        private float bubbleDefaultSize;
        private TextMeshProUGUI bubbleArrow;
        private RectTransform transitionLayer;
        private RectTransform travelSoul;
        private TextMeshProUGUI alertMark;
        private Vector2 arenaBasePosition;
        private Vector2 portraitBasePosition;

        // Portrait camera
        private Camera portraitCamera;
        private RenderTexture portraitTexture;

        // Session state
        private HackableObstacle obstacle;
        private int roundIndex;
        private RectTransform companionRoot;
        private readonly List<Image> companionPixels = new List<Image>();
        private TextMeshProUGUI companionLabel;
        private Vector2 soulPosition;
        private Vector2 arenaHalf;
        private float invulnerable;
        private float arenaShake;
        private float portraitShake;
        private float spawnTimer;
        private float patternAngle;
        private float tauntTimer;
        private HackWave currentWave;
        private Coroutine bubbleRoutine;
        private bool bubbleBusy;
        private float savedFov;

        // Opponent animation (the live feed + its 3D model)
        private Transform animTarget;
        private Vector3 animBaseScale;
        private Vector3 lastHop;
        private bool animCanHop;
        private float attackPulse;
        private float lastPulseTime;
        private bool waveActive;
        private bool defeated;
        private bool feedReady;
        private Vector2 frameMotion;
        private float frameTilt;
        private float frameBeat = 1f;
        private Vector2 arenaDrift;
        private float arenaTilt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            arenaHalf = arenaSize * 0.5f;
            BuildUI();
            canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InputLock.Release(this);
            if (portraitTexture != null) portraitTexture.Release();
            // Left mid-fight from the pause menu (load / main menu): bring the city music back.
            if (IsActive && MusicDirector.Instance != null) MusicDirector.Instance.ExitFight();
        }

        // ================================================================== Public API

        /// <summary>Starts a session against <paramref name="target"/>; <paramref name="onResolved"/> fires on success.</summary>
        public void Begin(HackableObstacle target, Action onResolved)
        {
            if (IsActive || target == null) return;
            StartCoroutine(Run(target, onResolved));
        }

        // ================================================================== Flow

        private IEnumerator Run(HackableObstacle target, Action onResolved)
        {
            IsActive = true;
            obstacle = target;
            skinPrefix = SkinPrefixFor(target);
            InputLock.Acquire(this);

            titleText.text = string.Empty;
            statusText.text = string.Empty;
            SetProgress(0f, string.Empty);
            BuildFirewall(Mathf.Max(1, target.puzzles.Count));
            BeginCombatSession();
            soul.gameObject.SetActive(false);
            HideBubbleImmediate();

            MusicDirector.Ensure().EnterFight(target.musicOverride != null ? target.musicOverride : fightMusic);
            defeated = false;
            yield return TransitionIn(target);
            if (DialogueManager.Instance != null) DialogueManager.Instance.SetBattleSkin(true);

            StartCoroutine(TypeTitle(Loc.F("LINK ESTABLISHED  //  {0}", Loc.T(target.encounterTitle))));
            statusText.text = Loc.T("CONNECTED TO PSYCHOLOGICAL SOURCE CODE.");
            yield return SpeakLines(target.introLines, target.speaker);

            int rounds = Mathf.Max(target.waves.Count, target.puzzles.Count);
            yield return MaybeTutorial();

            for (int round = 0; round < rounds; round++)
            {
                bool firstAttempt = true;
                var eliminated = new HashSet<int>();
                while (true)
                {
                    if (round < target.waves.Count)
                    {
                        roundIndex = round;
                        yield return RunWave(target.waves[round], firstAttempt ? 1f : 0.6f);
                        if (defeated)
                        {
                            // Game over: back to the overworld in front of the obstacle, which stays until beaten.
                            yield return GameOver();
                            yield break;
                        }
                    }
                    firstAttempt = false;

                    if (round >= target.puzzles.Count) break;

                    bool correct = false;
                    yield return RunFragments(target.puzzles[round], eliminated, result => correct = result);
                    if (correct)
                    {
                        BreakFirewall(round);
                        yield return Wait(0.5f);
                        if (target.betweenRoundLines != null && round < target.betweenRoundLines.Length && round < rounds - 1)
                        {
                            yield return Say(target.betweenRoundLines[round], target.speaker, true);
                        }
                        break;
                    }
                    statusText.text = Loc.T("FRAGMENT REJECTED. RECALIBRATING...");
                    yield return Wait(0.4f);
                }
            }

            statusText.text = Loc.T("FIREWALL DOWN. BLOCKAGE RESOLVED.");
            Play(resolvedClip);
            StartCoroutine(Flash(new Color(1f, 0.85f, 0.6f), 0.8f));
            StartCoroutine(PortraitHit(new Color(1f, 0.9f, 0.7f), 0.6f));
            yield return Wait(0.6f);
            yield return GiveReward(target);
            yield return SpeakLines(target.resolutionLines, target.speaker);

            HideBubbleImmediate();
            Play(disconnectClip);
            if (DialogueManager.Instance != null) DialogueManager.Instance.SetBattleSkin(false);
            MusicDirector.Ensure().ExitFight();
            yield return TransitionOut();
            ClearBullets();

            obstacle = null;
            IsActive = false;
            InputLock.Release(this);
            onResolved?.Invoke();
        }

        // ================================================================== Transitions

        private IEnumerator TransitionIn(HackableObstacle target)
        {
            canvas.enabled = true;
            background.color = new Color(background.color.r, background.color.g, background.color.b, 0f);
            content.alpha = 0f;
            travelSoul.gameObject.SetActive(false);
            SetSlices(-1f);

            Camera main = Camera.main;
            Transform player = GameObject.FindGameObjectWithTag("Player") != null ? GameObject.FindGameObjectWithTag("Player").transform : null;
            Vector2 leoLocal = Vector2.zero;
            if (main != null && player != null)
            {
                Vector3 screen = main.WorldToScreenPoint(player.position + Vector3.up * 0.6f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(transitionLayer, screen, null, out leoLocal);
            }

            // 1) "!" pops above Leo.
            alertMark.gameObject.SetActive(true);
            alertMark.rectTransform.anchoredPosition = leoLocal + new Vector2(0f, 120f);
            Play(alertClip);
            for (float t = 0f; t < 0.25f; t += GameTime.UnscaledDelta)
            {
                alertMark.rectTransform.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, t / 0.25f);
                yield return null;
            }
            alertMark.rectTransform.localScale = Vector3.one;
            yield return Wait(0.35f);
            alertMark.gameObject.SetActive(false);

            // 2) Camera punch toward the obstacle + glitch bands sweep in.
            PreparePortraitCamera(target);
            if (main != null) StartCoroutine(CameraPunch(main, true));
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(0.45f);
            Play(transitionClip);
            yield return SweepSlices(-1f, 0f, 0.32f);

            // 3) Under the bands: the hacking background appears; bands leave on the other side.
            background.color = new Color(background.color.r, background.color.g, background.color.b, 0.93f);
            yield return SweepSlices(0f, 1f, 0.28f);

            // 4) Leo's soul blinks where Leo stands, then flies into the arena.
            travelSoul.gameObject.SetActive(true);
            travelSoul.anchoredPosition = leoLocal;
            for (int i = 0; i < 3; i++)
            {
                travelSoul.gameObject.SetActive(true);
                Play(soulBlinkClip, 0.7f);
                yield return Wait(0.1f);
                travelSoul.gameObject.SetActive(false);
                yield return Wait(0.07f);
            }
            travelSoul.gameObject.SetActive(true);

            Vector2 soulStartArena = new Vector2(0f, -arenaHalf.y * 0.65f);
            Vector3 targetWorld = arena.TransformPoint(soulStartArena);
            Vector2 targetLocal = transitionLayer.InverseTransformPoint(targetWorld);
            for (float t = 0f; t < 0.45f; t += GameTime.UnscaledDelta)
            {
                float e = Easing.Evaluate(EaseType.EaseInOutCubic, t / 0.45f);
                travelSoul.anchoredPosition = Vector2.LerpUnclamped(leoLocal, targetLocal, e);
                travelSoul.localRotation = Quaternion.Euler(0, 0, 45f + e * 360f);
                yield return null;
            }

            // 5) Arena powers on like a CRT; the source feed pops in.
            Play(connectClip);
            content.alpha = 1f;
            arena.localScale = new Vector3(1f, 0.02f, 1f);
            portraitFrame.localScale = Vector3.zero;
            for (float t = 0f; t < 0.3f; t += GameTime.UnscaledDelta)
            {
                float n = t / 0.3f;
                arena.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.02f, 1f, Easing.Evaluate(EaseType.EaseOutBack, n)), 1f);
                portraitFrame.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, n);
                yield return null;
            }
            arena.localScale = Vector3.one;
            portraitFrame.localScale = Vector3.one;
            travelSoul.gameObject.SetActive(false);
            ResetSoul();
            soul.gameObject.SetActive(true);
            feedReady = true;
            StartCoroutine(Flash(RuntimeUI.Cyan, 0.3f));
        }

        private IEnumerator TransitionOut()
        {
            Play(transitionOutClip);
            soul.gameObject.SetActive(false);
            feedReady = false;
            waveActive = false;
            RestoreAnimTarget();
            DestroyCompanion();
            arena.localRotation = Quaternion.identity;

            // Arena collapses to a line, then a dot.
            for (float t = 0f; t < 0.18f; t += GameTime.UnscaledDelta)
            {
                float n = Easing.Evaluate(EaseType.EaseInCubic, t / 0.18f);
                arena.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.02f, n), 1f);
                portraitFrame.localScale = Vector3.one * (1f - n);
                yield return null;
            }
            for (float t = 0f; t < 0.12f; t += GameTime.UnscaledDelta)
            {
                arena.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.12f), 0.02f, 1f);
                yield return null;
            }

            yield return SweepSlices(1f, 0f, 0.28f);
            content.alpha = 0f;
            background.color = new Color(background.color.r, background.color.g, background.color.b, 0f);
            if (portraitCamera != null) portraitCamera.enabled = false;
            Camera main = Camera.main;
            if (main != null) StartCoroutine(CameraPunch(main, false));
            yield return SweepSlices(0f, -1f, 0.32f);
            yield return Flash(new Color(1f, 0.85f, 0.6f), 0.5f);

            arena.localScale = Vector3.one;
            canvas.enabled = false;
        }

        private IEnumerator CameraPunch(Camera cam, bool punchIn)
        {
            if (cam.orthographic) yield break;
            if (punchIn) savedFov = cam.fieldOfView;
            float from = cam.fieldOfView;
            float to = punchIn ? savedFov * 0.72f : savedFov;
            float duration = punchIn ? 0.45f : 0.6f;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                cam.fieldOfView = Mathf.Lerp(from, to, Easing.Evaluate(punchIn ? EaseType.EaseInCubic : EaseType.EaseOutCubic, t / duration));
                yield return null;
            }
            cam.fieldOfView = to;
        }

        /// <summary>Moves every band from position <paramref name="from"/> to <paramref name="to"/> (-1 left/right off-screen, 0 covering).</summary>
        private IEnumerator SweepSlices(float from, float to, float duration)
        {
            float width = Mathf.Max(canvasRect.rect.width, 1920f) + 400f;
            for (float t = 0f; t < duration + 0.12f; t += GameTime.UnscaledDelta)
            {
                for (int i = 0; i < slices.Count; i++)
                {
                    float delay = (i % 4) * 0.03f;
                    float n = Mathf.Clamp01((t - delay) / duration);
                    float e = Easing.Evaluate(EaseType.EaseInOutCubic, n);
                    float side = i % 2 == 0 ? 1f : -1f;
                    float x = Mathf.Lerp(from, to, e) * width * side;
                    slices[i].anchoredPosition = new Vector2(x, slices[i].anchoredPosition.y);
                }
                yield return null;
            }
            SetSlices(to);
        }

        private void SetSlices(float position)
        {
            float width = Mathf.Max(canvasRect.rect.width, 1920f) + 400f;
            for (int i = 0; i < slices.Count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                slices[i].anchoredPosition = new Vector2(position * width * side, slices[i].anchoredPosition.y);
            }
        }

        // ================================================================== Portrait (live source feed)

        private void PreparePortraitCamera(HackableObstacle target)
        {
            if (portraitCamera == null)
            {
                portraitTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "HackingPortrait" };
                var go = new GameObject("PortraitCamera");
                go.transform.SetParent(transform, false);
                portraitCamera = go.AddComponent<Camera>();
                portraitCamera.targetTexture = portraitTexture;
                portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                portraitCamera.backgroundColor = new Color(0.05f, 0.03f, 0.09f, 1f);
                portraitCamera.nearClipPlane = 0.05f;
                portraitCamera.farClipPlane = 60f;
                var data = portraitCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                portraitImage.texture = portraitTexture;

                int mask = ~0;
                foreach (string layer in new[] { "Player", "NPC", "UI" })
                {
                    int index = LayerMask.NameToLayer(layer);
                    if (index >= 0) mask &= ~(1 << index);
                }
                portraitCamera.cullingMask = mask;
            }

            Transform focusTransform = target.portraitTarget != null ? target.portraitTarget : target.transform;
            Bounds bounds = new Bounds(focusTransform.position + Vector3.up, Vector3.one);
            Renderer[] renderers = focusTransform.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            }
            // Frame a little above the middle: faces and heads are at the top of these bodies.
            Vector3 focus = bounds.center + Vector3.up * (bounds.extents.y * 0.25f);

            animTarget = focusTransform;
            animBaseScale = focusTransform.localScale;
            lastHop = Vector3.zero;
            animCanHop = focusTransform.GetComponent<Gameplay.Spinner>() == null;

            // Film from the side Leo is standing on.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 dir = player != null ? player.transform.position - focus : -focusTransform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            dir.Normalize();
            // Far enough that the whole body fits the (narrow) feed lens, head to toe.
            float fit = bounds.size.y * 1.15f / (2f * Mathf.Tan(portraitFov * 0.5f * Mathf.Deg2Rad));
            float distance = Mathf.Max(target.portraitDistance, fit);
            portraitCamera.transform.position = focus + dir * distance + Vector3.up * (0.25f * distance);
            portraitCamera.transform.LookAt(focus);
            portraitCamera.fieldOfView = portraitFov;
            // Clip away anything standing between the lens and the enemy (a gate, a wall, a train seat):
            // the feed only ever shows the enemy, never the back of a concrete barrier.
            float camToFocus = Vector3.Distance(portraitCamera.transform.position, focus);
            portraitCamera.nearClipPlane = Mathf.Max(0.05f, camToFocus - bounds.extents.magnitude - 0.4f);
            portraitCamera.farClipPlane = camToFocus + 40f;
            portraitCamera.enabled = true;
        }

        private IEnumerator PortraitHit(Color color, float duration)
        {
            portraitShake = 1f;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                color.a = 0.8f * (1f - t / duration);
                portraitFlash.color = color;
                yield return null;
            }
            portraitFlash.color = Color.clear;
        }

        // ================================================================== Speech

        /// <summary>Lines starting with "* " go to the bottom box (narration); others to the obstacle's bubble.</summary>
        private IEnumerator SpeakLines(string[] lines, DialogueSpeaker speaker)
        {
            if (lines == null) yield break;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("* "))
                {
                    if (DialogueManager.Instance == null) continue;
                    bool done = false;
                    DialogueManager.Instance.PlayLines(new[] { line }, null, () => done = true);
                    while (!done) yield return null;
                }
                else
                {
                    yield return Say(line, speaker, true);
                }
            }
            HideBubbleImmediate();
        }

        /// <summary>Types a line into the bubble next to the portrait. waitForInput=false → auto-hides (taunts).</summary>
        private IEnumerator Say(string line, DialogueSpeaker speaker, bool waitForInput)
        {
            if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
            bubbleBusy = true;
            bubble.gameObject.SetActive(true);
            bubbleArrow.gameObject.SetActive(false);
            // Enemies speak in their own typeface, with inline {effects}.
            if (bubbleDefaultFont == null) { bubbleDefaultFont = bubbleText.font; bubbleDefaultSize = bubbleText.fontSize; }
            DialogueFonts.Look look = DialogueFonts.Get(speaker != null ? speaker.style : SpeakerStyle.Enemy, bubbleDefaultFont);
            bubbleText.font = look.font != null ? look.font : bubbleDefaultFont;
            bubbleText.fontStyle = look.fontStyle;
            bubbleText.fontSize = bubbleDefaultSize * look.sizeScale;
            bubbleText.characterSpacing = look.characterSpacing;
            TextFXAnimator bubbleFX = bubbleText.GetComponent<TextFXAnimator>();
            if (bubbleFX == null) bubbleFX = bubbleText.gameObject.AddComponent<TextFXAnimator>();
            bubbleFX.SetText(Loc.T(line), false, speaker != null ? speaker.signatureEffect : TextEffect.None);
            int total = bubbleText.textInfo.characterCount;

            // Pop in.
            for (float t = 0f; t < 0.15f; t += GameTime.UnscaledDelta)
            {
                bubble.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, t / 0.15f);
                yield return null;
            }
            bubble.localScale = Vector3.one;

            float cps = 40f * (speaker != null ? speaker.typingSpeedMultiplier : 1f);
            float budget = 1f;
            int frame = Time.frameCount;
            for (int visible = 0; visible < total;)
            {
                budget += GameTime.UnscaledDelta * cps;
                while (budget >= 1f && visible < total)
                {
                    budget -= 1f;
                    bubbleFX.Reveal(visible);
                    visible++;
                    bubbleText.maxVisibleCharacters = visible;
                    if (speaker != null && speaker.voiceBlip != null && visible % Mathf.Max(1, speaker.blipEveryNCharacters) == 0)
                    {
                        audioSource.pitch = UnityEngine.Random.Range(speaker.pitchRange.x, speaker.pitchRange.y);
                        audioSource.PlayOneShot(speaker.voiceBlip, speaker.voiceVolume);
                    }
                }
                portraitShake = Mathf.Max(portraitShake, 0.08f); // the feed "talks"
                if (waitForInput && Time.frameCount != frame && GameInput.SubmitPressed) visible = total;
                bubbleText.maxVisibleCharacters = visible;
                yield return null;
            }
            audioSource.pitch = 1f;
            bubbleFX.RevealAll();

            if (waitForInput)
            {
                bubbleArrow.gameObject.SetActive(true);
                yield return null;
                while (!GameInput.SubmitPressed) yield return null;
                Play(tickClip, 0.5f);
                yield return null;
            }
            else
            {
                yield return Wait(2f);
                HideBubbleImmediate();
            }
            bubbleBusy = false;
        }

        private void Taunt()
        {
            if (obstacle == null || obstacle.tauntLines == null || obstacle.tauntLines.Length == 0 || bubbleBusy) return;
            string line = obstacle.tauntLines[UnityEngine.Random.Range(0, obstacle.tauntLines.Length)];
            bubbleRoutine = StartCoroutine(Say(line, obstacle.speaker, false));
        }

        private void HideBubbleImmediate()
        {
            if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
            bubbleRoutine = null;
            bubbleBusy = false;
            if (bubble != null) bubble.gameObject.SetActive(false);
            if (audioSource != null) audioSource.pitch = 1f;
        }

        private IEnumerator TypeTitle(string text)
        {
            for (int i = 0; i <= text.Length; i++)
            {
                titleText.text = text.Substring(0, i) + (i < text.Length ? "_" : string.Empty);
                yield return GameTime.Wait(0.015f);
            }
        }

        // ================================================================== Evasion

        private IEnumerator RunWave(HackWave wave, float durationScale)
        {
            currentWave = wave;
            float duration = Mathf.Max(3f, wave.duration * durationScale);
            ResetSoul();
            soul.gameObject.SetActive(true);
            spawnTimer = 0.6f;
            tauntTimer = UnityEngine.Random.Range(1.5f, 3f);
            patternAngle = UnityEngine.Random.Range(0f, 360f);
            statusText.text = Loc.T(wave.surrender ? "SOME THINGS CAN'T BE DODGED." : WaveFlavor(wave.pattern));
            Play(waveStartClip);
            BeginWavePattern(wave);
            StartCoroutine(WaveBanner(wave.surrender ? "LET GO" : WaveName(wave.pattern)));
            waveActive = true;
            AttackPulse(1f);
            if (obstacle != null && obstacle.fadeCompanion && companionRoot == null) BuildCompanion(obstacle.companionLabel);

            float elapsed = 0f;
            float running = 0f;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                if (wave.surrender)
                {
                    // Time only moves forward while Leo stops running.
                    bool still = GameInput.MoveVector.sqrMagnitude < 0.01f;
                    if (still) elapsed += dt;
                    else if ((running += dt) > 4f)
                    {
                        running = float.MinValue;
                        statusText.text = Loc.T("...STOP RUNNING.");
                    }
                    SetProgress(elapsed / duration, Loc.F("LETTING GO  {0}%", Mathf.FloorToInt(elapsed / duration * 100f)));
                }
                else
                {
                    elapsed += dt;
                    SetProgress(elapsed / duration, Loc.F("DECRYPTING  {0}%", Mathf.FloorToInt(elapsed / duration * 100f)));
                }

                UpdateSoul(dt);
                wavePhase = elapsed / duration;
                SpawnPattern(dt);
                UpdateBullets(dt);
                UpdateLasers(dt);
                UpdateScheduled(dt);
                UpdateCombatExtras(dt);
                if (companionRoot != null && obstacle != null && obstacle.waves.Count > 0)
                {
                    UpdateCompanion((roundIndex + elapsed / duration) / obstacle.waves.Count);
                }

                tauntTimer -= dt;
                if (tauntTimer <= 0f)
                {
                    tauntTimer = UnityEngine.Random.Range(3.5f, 5.5f);
                    Taunt();
                }

                if (Inventory.HP <= 0)
                {
                    defeated = true;
                    waveActive = false;
                    yield break;
                }
                yield return null;
            }

            waveActive = false;
            ClearBullets();
            HideBubbleImmediate();
            SetProgress(1f, Loc.T("DECRYPTED  100%"));
        }

        private IEnumerator RunPuzzle(HackPuzzle puzzle, HashSet<int> eliminated, Action<bool> result)
        {
            statusText.text = Loc.T("EMOTIONAL FIREWALL DETECTED. SELECT A MEMORY FRAGMENT.");
            SetProgress(0f, string.Empty);
            soul.gameObject.SetActive(false);

            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue == null)
            {
                result(true);
                yield break;
            }

            var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.hideFlags = HideFlags.DontSave;
            var prompt = new DialogueNode { id = "prompt", text = puzzle.prompt };
            for (int i = 0; i < puzzle.options.Length; i++)
            {
                if (eliminated.Contains(i)) continue; // fragments that already bounced off are gone
                bool isCorrect = i == puzzle.correctIndex;
                bool isAlt = i == puzzle.altCorrectIndex;
                prompt.choices.Add(new DialogueChoice
                {
                    text = puzzle.options[i],
                    nextNodeId = isCorrect ? "ok" : isAlt ? "alt" : "bad",
                    flagToSet = isCorrect || isAlt ? CorrectFlag : LastWrongFlag + i
                });
            }
            // Always offer a way to think it over.
            prompt.choices.Add(new DialogueChoice { text = "[Listen closer]", nextNodeId = "hint" });
            sequence.nodes.Add(prompt);
            string hintText = string.IsNullOrEmpty(puzzle.hint)
                ? "Everything they said during the fight is a clue. Which answer would actually help them, not just shut them up?"
                : puzzle.hint;
            sequence.nodes.Add(new DialogueNode { id = "hint", text = hintText, nextNodeId = "prompt", effect = TextEffect.None });
            sequence.nodes.Add(new DialogueNode { id = "ok", text = puzzle.correctLine, nextNodeId = DialogueSequence.EndNodeId, effect = TextEffect.Wave });
            sequence.nodes.Add(new DialogueNode { id = "alt", text = string.IsNullOrEmpty(puzzle.altCorrectLine) ? puzzle.correctLine : puzzle.altCorrectLine, nextNodeId = DialogueSequence.EndNodeId, effect = TextEffect.Wave });
            sequence.nodes.Add(new DialogueNode { id = "bad", text = puzzle.wrongLine, nextNodeId = "badhint", effect = TextEffect.Shake });
            sequence.nodes.Add(new DialogueNode { id = "badhint", text = "(That fragment is gone now. One more wave, then try again.)", nextNodeId = DialogueSequence.EndNodeId });
            for (int i = 0; i < puzzle.options.Length; i++) StoryFlags.Clear(LastWrongFlag + i);

            StoryFlags.Clear(CorrectFlag);
            bool done = false;
            dialogue.Play(sequence, () => done = true);
            while (!done) yield return null;
            Destroy(sequence);

            bool correct = StoryFlags.Has(CorrectFlag);
            StoryFlags.Clear(CorrectFlag);
            for (int i = 0; i < puzzle.options.Length; i++)
            {
                if (StoryFlags.Has(LastWrongFlag + i)) eliminated.Add(i);
                StoryFlags.Clear(LastWrongFlag + i);
            }
            if (!correct)
            {
                Play(wrongClip);
                Shake(0.2f);
            }
            result(correct);
        }

        private void ResetSoul()
        {
            soulPosition = new Vector2(0f, -arenaHalf.y * 0.65f);
            soul.anchoredPosition = soulPosition;
            invulnerable = 0.6f;
        }

        private void UpdateSoul(float dt)
        {
            Vector2 input = GameInput.MoveVector;
            soulPosition += input * (soulSpeed * dt);
            float limitX = arenaHalf.x - soulRadius - 4f;
            float limitY = arenaHalf.y - soulRadius - 4f;
            soulPosition.x = Mathf.Clamp(soulPosition.x, -limitX, limitX);
            soulPosition.y = Mathf.Clamp(soulPosition.y, -limitY, limitY);
            soul.anchoredPosition = soulPosition;

            if (invulnerable > 0f)
            {
                invulnerable -= dt;
                soulImage.enabled = Mathf.FloorToInt(invulnerable * 20f) % 2 == 0;
            }
            else
            {
                soulImage.enabled = true;
            }
        }

        // ================================================================== Feedback

        private void BreakFirewall(int index)
        {
            if (index >= 0 && index < firewallSegments.Count)
            {
                firewallSegments[index].color = new Color(0.15f, 0.1f, 0.15f, 0.8f);
                StartCoroutine(PunchScale(firewallSegments[index].rectTransform));
            }
            Play(firewallBreakClip);
            Shake(0.5f);
            StartCoroutine(Flash(RuntimeUI.Magenta, 0.35f));
            StartCoroutine(PortraitHit(Color.white, 0.4f));
            statusText.text = Loc.F("FIREWALL SEGMENT {0} DOWN.", index + 1);
        }

        private void Shake(float amount)
        {
            if (!GameSettings.ScreenShake) return;
            arenaShake = Mathf.Max(arenaShake, amount);
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.AddTrauma(amount * 0.6f);
        }

        /// <summary>The opponent "attacks": the feed lunges toward the arena and its 3D model squashes and hops.</summary>
        private void AttackPulse(float strength)
        {
            if (GameTime.UnscaledTime - lastPulseTime < 0.3f) return;
            lastPulseTime = GameTime.UnscaledTime;
            attackPulse = Mathf.Max(attackPulse, strength);
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Editor check: F9 saves what the enemy feed camera sees to Logs/portrait.png.
            if (portraitTexture != null && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = portraitTexture;
                var shot = new Texture2D(portraitTexture.width, portraitTexture.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, portraitTexture.width, portraitTexture.height), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "portrait.png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log("[Hack] Feed saved. cam " + (portraitCamera != null ? portraitCamera.transform.position + " near " + portraitCamera.nearClipPlane : "none"));
            }
#endif
            if (canvas == null || !canvas.enabled) return;
            float dt = GameTime.UnscaledDelta;
            float t = GameTime.UnscaledTime;
            attackPulse = Mathf.MoveTowards(attackPulse, 0f, dt * 3.5f);
            UpdateDecor(dt);

            // Per-pattern choreography of the feed (and sometimes the whole arena).
            Vector2 motionGoal = Vector2.zero;
            float tiltGoal = 0f, beatGoal = 1f, arenaTiltGoal = 0f;
            Vector2 driftGoal = Vector2.zero;
            if (waveActive && currentWave != null)
            {
                switch (currentWave.pattern)
                {
                    case HackPattern.PaperRain:
                        motionGoal = new Vector2(Mathf.Sin(t * 2f) * 70f, 0f);
                        tiltGoal = Mathf.Sin(t * 2f) * 5f;
                        break;
                    case HackPattern.ClockHands:
                        tiltGoal = Mathf.Round(Mathf.Sin(t * 3f) * 2f) * 6f; // ticking, stepped wobble
                        beatGoal = 1f + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.04f;
                        break;
                    case HackPattern.Crossfire:
                        motionGoal = new Vector2(Mathf.Sin(t * 1.3f) * 280f, 0f);
                        driftGoal = new Vector2(Mathf.Sin(t * 0.9f) * 45f, 0f);
                        break;
                    case HackPattern.Spiral:
                        motionGoal = new Vector2(Mathf.Cos(t * 2.2f) * 80f, Mathf.Sin(t * 2.2f) * 28f);
                        arenaTiltGoal = Mathf.Sin(t * 0.8f) * 4f;
                        break;
                    case HackPattern.Aimed:
                        motionGoal = new Vector2(Mathf.Clamp(soulPosition.x * 0.9f, -260f, 260f), 0f);
                        break;
                    case HackPattern.Ripple:
                        beatGoal = 1f + Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 2.6f)), 8f) * 0.12f; // heartbeat
                        driftGoal = new Vector2(0f, Mathf.Sin(t * 1.1f) * 14f);
                        break;
                    case HackPattern.Lasers:
                        tiltGoal = Mathf.Sin(t * 8f) * 2f;
                        beatGoal = 1f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.05f;
                        break;
                    case HackPattern.Bloom:
                        motionGoal = new Vector2(Mathf.Sin(t * 1.4f) * 120f, Mathf.Abs(Mathf.Sin(t * 2.8f)) * 20f);
                        beatGoal = 1f + Mathf.Sin(t * 2.8f) * 0.05f;
                        break;
                    case HackPattern.Serpent:
                        motionGoal = new Vector2(Mathf.Sin(t * 0.9f) * 220f, Mathf.Sin(t * 3.6f) * 18f);
                        tiltGoal = Mathf.Cos(t * 3.6f) * 6f;
                        break;
                    case HackPattern.Fireworks:
                        motionGoal = new Vector2(0f, Mathf.Abs(Mathf.Sin(t * 3f)) * 26f);
                        arenaTiltGoal = Mathf.Sin(t * 0.6f) * 2f;
                        break;
                    case HackPattern.Walls:
                        driftGoal = new Vector2(Mathf.Sin(t * 0.7f) * 20f, 0f);
                        tiltGoal = Mathf.Round(Mathf.Sin(t * 2f)) * 4f;
                        break;
                    case HackPattern.Homing:
                        motionGoal = new Vector2(Mathf.Clamp(soulPosition.x * 0.6f, -200f, 200f), Mathf.Sin(t * 2f) * 12f);
                        break;
                    case HackPattern.Orbit:
                        motionGoal = new Vector2(Mathf.Cos(t * 1.6f) * 110f, Mathf.Sin(t * 1.6f) * 30f);
                        arenaTiltGoal = Mathf.Sin(t * 0.5f) * 5f;
                        break;
                    case HackPattern.TrainRush:
                        driftGoal = new Vector2(Mathf.Sin(t * 13f) * 3f, Mathf.Sin(t * 17f) * 2f); // rumbling tracks
                        motionGoal = new Vector2(Mathf.Sin(t * 0.8f) * 160f, 0f);
                        break;
                }
            }

            // Keep the moving feed clear of the firewall (left) and inside the screen.
            motionGoal.x = Mathf.Clamp(motionGoal.x, -140f, 140f);
            float follow = 1f - Mathf.Exp(-6f * dt);
            frameMotion = Vector2.Lerp(frameMotion, motionGoal, follow);
            frameTilt = Mathf.Lerp(frameTilt, tiltGoal, follow);
            frameBeat = Mathf.Lerp(frameBeat, beatGoal, 1f - Mathf.Exp(-14f * dt));
            arenaDrift = Vector2.Lerp(arenaDrift, driftGoal, 1f - Mathf.Exp(-3f * dt));
            arenaTilt = Mathf.Lerp(arenaTilt, arenaTiltGoal, 1f - Mathf.Exp(-3f * dt));

            // Arena: drift + tilt + shake.
            Vector2 shakeOffset = Vector2.zero;
            if (arenaShake > 0f)
            {
                arenaShake = Mathf.Max(0f, arenaShake - dt * 2.5f);
                float s = arenaShake * arenaShake * 40f;
                shakeOffset = new Vector2(UnityEngine.Random.Range(-s, s), UnityEngine.Random.Range(-s, s));
            }
            arena.anchoredPosition = arenaBasePosition + (GameSettings.ScreenShake ? arenaDrift + shakeOffset : arenaDrift * 0.3f);
            arena.localRotation = Quaternion.Euler(0f, 0f, arenaTilt);

            // Feed: choreography + lunge toward the arena on each attack + talk/hit jitter + breathing.
            portraitShake = Mathf.MoveTowards(portraitShake, 0f, dt * 3f);
            float jitter = GameSettings.ScreenShake ? portraitShake * 8f : 0f;
            Vector2 lunge = new Vector2(0f, -26f * attackPulse);
            portraitFrame.anchoredPosition = portraitBasePosition + frameMotion + lunge
                + new Vector2(UnityEngine.Random.Range(-jitter, jitter), UnityEngine.Random.Range(-jitter, jitter));
            portraitFrame.localRotation = Quaternion.Euler(0f, 0f, frameTilt + Mathf.Sin(t * 30f) * attackPulse * 3f);
            if (feedReady)
            {
                float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.015f;
                float size = breathe * frameBeat * (1f + attackPulse * 0.12f);
                portraitFrame.localScale = new Vector3(size, size, 1f);
            }

            // Bubble follows the feed so the opponent keeps "speaking" from it.
            if (bubble != null) bubble.anchoredPosition = new Vector2(170f, 320f) + frameMotion + lunge;
        }

        private void LateUpdate()
        {
            if (animTarget == null || !feedReady) return;

            // The 3D opponent squashes and hops on each attack (visible in the live feed).
            float p = attackPulse;
            animTarget.localScale = new Vector3(animBaseScale.x * (1f + 0.14f * p), animBaseScale.y * (1f - 0.12f * p), animBaseScale.z * (1f + 0.14f * p));
            if (animCanHop)
            {
                Vector3 hop = Vector3.up * (Mathf.Sin(p * Mathf.PI) * 0.18f);
                animTarget.localPosition += hop - lastHop;
                lastHop = hop;
            }
        }

        private void RestoreAnimTarget()
        {
            if (animTarget == null) return;
            animTarget.localScale = animBaseScale;
            if (animCanHop) animTarget.localPosition -= lastHop;
            lastHop = Vector3.zero;
            animTarget = null;
            attackPulse = 0f;
            frameMotion = Vector2.zero;
            frameTilt = 0f;
            frameBeat = 1f;
            arenaDrift = Vector2.zero;
            arenaTilt = 0f;
            portraitFrame.localRotation = Quaternion.identity;
        }

        private IEnumerator Flash(Color color, float duration)
        {
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                color.a = 0.35f * (1f - t / duration);
                flash.color = color;
                yield return null;
            }
            flash.color = Color.clear;
        }

        private IEnumerator PunchScale(RectTransform target)
        {
            for (float t = 0f; t < 0.3f; t += GameTime.UnscaledDelta)
            {
                target.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.5f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += GameTime.UnscaledDelta) yield return null;
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        // ================================================================== Companion fading at the edge (act IV)

        private static readonly string[] CatPixels =
        {
            "1.......1",
            "11.....11",
            "111111111",
            "11.111.11",
            "111111111",
            ".1111111.",
            "..1...1..",
        };

        private void BuildCompanion(string label)
        {
            companionRoot = RuntimeUI.Rect("Companion", canvas.transform);
            RuntimeUI.Place(companionRoot, Vector2.zero, Vector2.zero, new Vector2(36f, 60f), new Vector2(120f, 110f));
            const float px = 12f;
            var order = new List<(Image img, float key)>();
            for (int y = 0; y < CatPixels.Length; y++)
            {
                for (int x = 0; x < CatPixels[y].Length; x++)
                {
                    if (CatPixels[y][x] != '1') continue;
                    Image pixel = RuntimeUI.Image("Px", companionRoot, new Color(0.7f, 1f, 0.8f, 0.95f));
                    RuntimeUI.Place(pixel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(x * px, (CatPixels.Length - 1 - y) * px + 22f), new Vector2(px - 1f, px - 1f));
                    // Disappears from the outer edge inward, with a little noise.
                    order.Add((pixel, x + UnityEngine.Random.value * 3f));
                }
            }
            order.Sort((a, b) => a.key.CompareTo(b.key));
            companionPixels.Clear();
            foreach (var entry in order) companionPixels.Add(entry.img);
            companionLabel = RuntimeUI.Text("Label", companionRoot, label, 18, new Color(0.7f, 1f, 0.8f), TextAlignmentOptions.BottomLeft);
            RuntimeUI.Place(companionLabel.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 20f));
        }

        private void UpdateCompanion(float progress)
        {
            int gone = Mathf.FloorToInt(Mathf.Clamp01(progress) * companionPixels.Count);
            for (int i = 0; i < companionPixels.Count; i++)
            {
                bool visible = i >= gone;
                if (companionPixels[i].enabled != visible)
                {
                    companionPixels[i].enabled = visible;
                    if (!visible) Play(grazeClip);
                }
            }
            if (companionLabel != null) companionLabel.alpha = 1f - Mathf.Clamp01(progress);
        }

        private void DestroyCompanion()
        {
            if (companionRoot != null) Destroy(companionRoot.gameObject);
            companionRoot = null;
            companionPixels.Clear();
        }

        // ================================================================== UI construction

        private void BuildUI()
        {
            canvas = RuntimeUI.CreateCanvas("HackingCanvas", transform, 8);
            canvasRect = (RectTransform)canvas.transform;

            background = RuntimeUI.Image("Background", canvas.transform, new Color(0.02f, 0.01f, 0.05f, 0f));
            RuntimeUI.Stretch(background.rectTransform);

            // ---------- Fight content
            RectTransform contentRect = RuntimeUI.Rect("Content", canvas.transform);
            RuntimeUI.Stretch(contentRect);
            content = contentRect.gameObject.AddComponent<CanvasGroup>();
            content.alpha = 0f;
            content.blocksRaycasts = false;

            titleText = RuntimeUI.Text("Title", contentRect, "", 24, RuntimeUI.Cyan, TextAlignmentOptions.Center);
            RuntimeUI.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(1600, 34));

            // Source feed (portrait) above the arena
            portraitBasePosition = new Vector2(0f, 300f);
            Image frame = RuntimeUI.Image("SourceFeed", contentRect, new Color(0.02f, 0.02f, 0.04f, 1f));
            portraitFrame = frame.rectTransform;
            RuntimeUI.Place(portraitFrame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), portraitBasePosition, new Vector2(270, 270));
            Outline frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = RuntimeUI.Magenta;
            frameOutline.effectDistance = new Vector2(3, -3);
            var rawGo = RuntimeUI.Rect("Feed", portraitFrame);
            RuntimeUI.Stretch(rawGo, 6, 6, 6, 6);
            portraitImage = rawGo.gameObject.AddComponent<RawImage>();
            portraitImage.raycastTarget = false;
            if (crtMaterial != null)
            {
                Image feedCrt = RuntimeUI.Image("FeedCRT", portraitFrame, Color.white);
                feedCrt.material = crtMaterial;
                RuntimeUI.Stretch(feedCrt.rectTransform, 6, 6, 6, 6);
            }
            portraitFlash = RuntimeUI.Image("FeedFlash", portraitFrame, Color.clear);
            RuntimeUI.Stretch(portraitFlash.rectTransform, 6, 6, 6, 6);
            feedLabel = RuntimeUI.Text("FeedLabel", portraitFrame, Loc.T("SOURCE FEED  [LIVE]"), 16, RuntimeUI.Magenta, TextAlignmentOptions.Left);
            feedLabel.textWrappingMode = TextWrappingModes.NoWrap;
            // Inside the feed, top-left (above it, it collided with the controls hint).
            RuntimeUI.Place(feedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -10), new Vector2(246, 20));
            feedLabel.enableAutoSizing = true;
            feedLabel.fontSizeMin = 9f;
            feedLabel.fontSizeMax = 14f;
            feedLabel.outlineWidth = 0.25f;
            feedLabel.outlineColor = new Color32(5, 3, 10, 255);
            BuildFeedFx();

            // Speech bubble to the right of the feed
            Image bubbleImage = RuntimeUI.Image("Bubble", contentRect, Color.white);
            bubble = bubbleImage.rectTransform;
            RuntimeUI.Place(bubble, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 320f), new Vector2(520, 150));
            Outline bubbleOutline = bubbleImage.gameObject.AddComponent<Outline>();
            bubbleOutline.effectColor = Color.black;
            bubbleOutline.effectDistance = new Vector2(3, -3);
            Image tail = RuntimeUI.Image("Tail", bubble, Color.white);
            RuntimeUI.Place(tail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4, -10), new Vector2(26, 26));
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            bubbleText = RuntimeUI.Text("Text", bubble, "", 26, new Color(0.08f, 0.08f, 0.1f), TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(bubbleText.rectTransform, 22, 22, 16, 16);
            bubbleArrow = RuntimeUI.Text("Arrow", bubble, "v", 24, RuntimeUI.Magenta, TextAlignmentOptions.Center);
            RuntimeUI.Place(bubbleArrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10, 4), new Vector2(30, 30));
            bubble.gameObject.SetActive(false);

            // Arena
            Image arenaImage = RuntimeUI.Image("Arena", contentRect, new Color(0.03f, 0.05f, 0.08f, 1f));
            arena = arenaImage.rectTransform;
            arenaBasePosition = new Vector2(0f, arenaCenterY);
            RuntimeUI.Place(arena, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), arenaBasePosition, arenaSize);
            Outline outline = arena.gameObject.AddComponent<Outline>();
            outline.effectColor = RuntimeUI.Cyan;
            outline.effectDistance = new Vector2(3, -3);
            arena.gameObject.AddComponent<RectMask2D>();

            BuildArenaDecor(contentRect, arenaImage);
            soulImage = RuntimeUI.Image("Soul", arena, RuntimeUI.Magenta);
            soulImage.sprite = HackSprites.Get(HackSprites.Shape.Heart);
            soul = soulImage.rectTransform;
            soul.anchorMin = soul.anchorMax = new Vector2(0.5f, 0.5f);
            soul.sizeDelta = new Vector2(24, 24);

            float rowY = arenaCenterY - arenaHalf.y - 14f;
            BuildCombatUI(contentRect, rowY);

            firewallRow = RuntimeUI.Rect("Firewall", contentRect);
            // The ENEMY's firewall sits next to its live feed (top left), far from Leo's HP bar, so nobody mixes them up.
            RuntimeUI.Place(firewallRow, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-300f, 300f), new Vector2(300, 18));
            TextMeshProUGUI firewallLabel = RuntimeUI.Text("FirewallLabel", contentRect, Loc.T("ENEMY FIREWALL"), 18, RuntimeUI.Magenta, TextAlignmentOptions.Right);
            RuntimeUI.Place(firewallLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), new Vector2(-300f, 316f), new Vector2(300, 24));
            var rowLayout = firewallRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            statusText = RuntimeUI.Text("Status", contentRect, "", 20, RuntimeUI.Lilac, TextAlignmentOptions.Center);
            RuntimeUI.Place(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, rowY - 44f), new Vector2(1400, 30));
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            if (battleHud != null) statusText.rectTransform.SetParent(battleHud.transform, false);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 14f;
            statusText.fontSizeMax = 20f;

            if (crtMaterial != null)
            {
                Image crt = RuntimeUI.Image("CRT", canvas.transform, Color.white);
                crt.material = crtMaterial;
                RuntimeUI.Stretch(crt.rectTransform);
            }

            flash = RuntimeUI.Image("Flash", canvas.transform, Color.clear);
            RuntimeUI.Stretch(flash.rectTransform);

            // ---------- Transition layer (on top of everything)
            transitionLayer = RuntimeUI.Rect("Transition", canvas.transform);
            RuntimeUI.Stretch(transitionLayer);
            Color[] sliceColors = { new Color(0.02f, 0.01f, 0.05f), RuntimeUI.Magenta * 0.8f, new Color(0.02f, 0.01f, 0.05f), RuntimeUI.Cyan * 0.6f };
            for (int i = 0; i < SliceCount; i++)
            {
                Image slice = RuntimeUI.Image("Band_" + i, transitionLayer, sliceColors[i % sliceColors.Length]);
                RectTransform r = slice.rectTransform;
                r.anchorMin = new Vector2(0f, i / (float)SliceCount);
                r.anchorMax = new Vector2(1f, (i + 1) / (float)SliceCount);
                r.offsetMin = new Vector2(-200f, 0f);
                r.offsetMax = new Vector2(200f, 2f);
                Color c = slice.color;
                c.a = 1f;
                slice.color = c;
                slices.Add(r);
            }

            Image soulTravel = RuntimeUI.Image("TravelSoul", transitionLayer, RuntimeUI.Magenta);
            travelSoul = soulTravel.rectTransform;
            travelSoul.anchorMin = travelSoul.anchorMax = new Vector2(0.5f, 0.5f);
            travelSoul.sizeDelta = new Vector2(30, 30);
            travelSoul.localRotation = Quaternion.Euler(0, 0, 45);
            travelSoul.gameObject.SetActive(false);

            alertMark = RuntimeUI.Text("Alert", transitionLayer, "!", 96, RuntimeUI.Magenta, TextAlignmentOptions.Center);
            alertMark.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(alertMark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(80, 110));
            alertMark.gameObject.SetActive(false);
        }

        private void BuildFirewall(int segments)
        {
            foreach (Image image in firewallSegments)
            {
                if (image != null) Destroy(image.gameObject);
            }
            firewallSegments.Clear();
            for (int i = 0; i < segments; i++)
            {
                Image segment = RuntimeUI.Image("Segment", firewallRow, RuntimeUI.Magenta);
                segment.sprite = HackSprites.Get(HackSprites.Shape.Rounded);
                segment.rectTransform.sizeDelta = new Vector2(70, 16);
                firewallSegments.Add(segment);
            }
        }
    }
}
