using System;
using System.Collections;
using System.Collections.Generic;
using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Subnet.Dialogue
{
    /// <summary>
    /// Undertale / Omori-style dialogue box:
    /// - typewriter reveal with per-speaker pitch-varied voice blips and punctuation pauses,
    /// - bouncy pop-in / pop-out of the box (EaseOutBack / EaseInBack),
    /// - Shake / Wave per-letter effects,
    /// - branching choices with keyboard, gamepad and mouse support,
    /// - story-flag conditions on nodes.
    /// Locks gameplay input while open. One instance per scene (lives on the Dialogue canvas).
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        /// <summary>Scene singleton.</summary>
        public static DialogueManager Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("CanvasGroup on the dialogue root (controls fade + raycasts).")]
        [SerializeField] private CanvasGroup rootGroup;
        [Tooltip("The text box panel that scales during pop-in.")]
        [SerializeField] private RectTransform boxTransform;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Parent object of the name label (hidden for narration).")]
        [SerializeField] private GameObject namePlate;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("Optional portrait image (hidden when the speaker has none).")]
        [SerializeField] private Image portraitImage;
        [Tooltip("Small blinking arrow / icon shown when the line is fully typed.")]
        [SerializeField] private RectTransform advanceIndicator;
        [Tooltip("Vertical Layout Group that receives the spawned choice buttons.")]
        [SerializeField] private RectTransform choiceContainer;
        [SerializeField] private DialogueChoiceButton choiceButtonPrefab;

        [Header("Audio")]
        [Tooltip("Source used for typewriter voice blips (pitch is modified per blip).")]
        [SerializeField] private AudioSource voiceSource;
        [Tooltip("Source used for box / choice UI sounds (pitch untouched).")]
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip advanceClip;
        [SerializeField] private AudioClip choiceMoveClip;
        [SerializeField] private AudioClip choiceConfirmClip;

        [Header("Speakers")]
        [Tooltip("Used when a node has no speaker: Leo's inner voice / environmental introspection.")]
        [SerializeField] private DialogueSpeaker defaultSpeaker;

        [Header("Typing")]
        [SerializeField] private float charactersPerSecond = 38f;
        [Tooltip("Pause after , ; :")]
        [SerializeField] private float commaPause = 0.12f;
        [Tooltip("Pause after . ! ? when followed by a space or the end of the line.")]
        [SerializeField] private float sentencePause = 0.32f;
        [Tooltip("Pause between the dots of an ellipsis (...).")]
        [SerializeField] private float ellipsisPause = 0.18f;
        [SerializeField] private bool allowMouseAdvance = true;

        [Header("Juice")]
        [SerializeField] private float popInDuration = 0.28f;
        [SerializeField] private float popOutDuration = 0.16f;
        [SerializeField] private float popStartScale = 0.6f;
        [SerializeField] private float indicatorBobAmplitude = 6f;
        [SerializeField] private float indicatorBobSpeed = 6f;
        [SerializeField] private float shakeStrength = 1.8f;
        [SerializeField] private float waveAmplitude = 4f;
        [SerializeField] private float waveSpeed = 6f;
        [SerializeField] private float choiceInputCooldown = 0.15f;

        /// <summary>Raised when a conversation starts.</summary>
        public event Action Opened;
        /// <summary>Raised when a conversation ends.</summary>
        public event Action Closed;

        /// <summary>Raised when a new line starts (speaker may be the narrator = default speaker).</summary>
        public static event Action<DialogueSpeaker> LineStarted;
        /// <summary>Raised on every voice blip (actors bob while they talk).</summary>
        public static event Action<DialogueSpeaker> Blipped;
        /// <summary>Raised when a {big} word slams in or a {quake} fires.</summary>
        public static event Action<float> Emphasis;
        /// <summary>Stage tags in a line's key, e.g. {beside=kira} → ("beside", "kira").</summary>
        public static event Action<string, string> StageDirection;

        private static readonly System.Text.RegularExpressions.Regex StageTag =
            new System.Text.RegularExpressions.Regex(@"\{(approach|beside)=([a-z]+)\}");

        /// <summary>The world object that started the current conversation (an NPC, a prop...). May be null.</summary>
        public Transform CurrentSource { get; private set; }
        /// <summary>Who is talking right now.</summary>
        public DialogueSpeaker CurrentSpeaker { get; private set; }
        /// <summary>The narrator profile (Leo's inner voice).</summary>
        public DialogueSpeaker NarratorSpeaker => defaultSpeaker;

        /// <summary>Set right before Play/PlayLines to tell the director who started the conversation.</summary>
        public static Transform PendingSource;

        private struct QueuedConversation
        {
            public DialogueSequence sequence;
            public Action onComplete;
            public bool isRuntime;
            public Transform source;
        }
        private readonly Queue<QueuedConversation> queue = new Queue<QueuedConversation>();
        private TextFXAnimator bodyFX;
        private TMP_FontAsset defaultFont;
        private TMP_FontAsset defaultNameFont;
        private float defaultFontSize;
        private float defaultNameSize;
        private Color defaultTextColor = Color.white;

        /// <summary>True while a conversation is on screen.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// True on the frame the dialogue closed and the next one. Interaction systems check this so the
        /// confirm press that closed a dialogue does not immediately re-open it.
        /// </summary>
        public bool ClosedRecently => Time.frameCount - lastClosedFrame <= 1;

        private readonly List<DialogueChoiceButton> spawnedChoices = new List<DialogueChoiceButton>();
        private Coroutine runningRoutine;
        private Action pendingCallback;
        private DialogueSequence runtimeSequence;
        private TextEffect currentEffect;
        private int openedFrame = -10;
        private int lastClosedFrame = -10;
        private bool choicesActive;
        private int highlightedChoice;
        private int confirmedChoice = -1;
        private bool navigationHeld;
        private float navigationRepeatTimer;
        private Vector2 indicatorBasePosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DialogueManager] Duplicate instance destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (advanceIndicator != null) indicatorBasePosition = advanceIndicator.anchoredPosition;
            if (boxTransform != null) boxBasePosition = boxTransform.anchoredPosition;
            if (bodyText != null)
            {
                bodyFX = bodyText.GetComponent<TextFXAnimator>();
                if (bodyFX == null) bodyFX = bodyText.gameObject.AddComponent<TextFXAnimator>();
                bodyFX.shakeStrength = shakeStrength;
                bodyFX.waveAmplitude = waveAmplitude;
                bodyFX.waveSpeed = waveSpeed;
                defaultFont = bodyText.font;
                defaultFontSize = bodyText.fontSize;
                defaultTextColor = bodyText.color;
            }
            if (nameText != null)
            {
                defaultNameFont = nameText.font;
                defaultNameSize = nameText.fontSize;
            }
            HideImmediate();
            if (GetComponent<DialogueDirector>() == null) gameObject.AddComponent<DialogueDirector>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            InputLock.Release(this);
        }

        // ------------------------------------------------------------------ Public API

        /// <summary>Plays a dialogue asset. <paramref name="onComplete"/> fires after the box closes.</summary>
        public void Play(DialogueSequence sequence, Action onComplete = null)
        {
            StartSequence(sequence, onComplete, false);
        }

        /// <summary>Plays plain lines (inspection reflections, quick barks) as a linear conversation.</summary>
        public void PlayLines(IList<string> lines, DialogueSpeaker speaker = null, Action onComplete = null, TextEffect effect = TextEffect.None)
        {
            StartSequence(DialogueSequence.FromLines(lines, speaker, effect), onComplete, true);
        }

        /// <summary>Instantly closes the box (used when skipping cinematics).</summary>
        public void ForceClose(bool invokeCallback = true)
        {
            if (!IsOpen) return;
            if (runningRoutine != null) StopCoroutine(runningRoutine);
            runningRoutine = null;
            ClearChoices();
            HideImmediate();
            Finish(invokeCallback);
        }

        /// <summary>Called by DialogueChoiceButton on mouse hover.</summary>
        public void HighlightChoice(int index)
        {
            if (!choicesActive || spawnedChoices.Count == 0 || confirmedChoice >= 0) return;
            index = (index % spawnedChoices.Count + spawnedChoices.Count) % spawnedChoices.Count;
            if (index != highlightedChoice) PlayUi(choiceMoveClip);
            highlightedChoice = index;
            for (int i = 0; i < spawnedChoices.Count; i++) spawnedChoices[i].SetHighlighted(i == highlightedChoice);
        }

        /// <summary>Called by DialogueChoiceButton on click, or internally on submit.</summary>
        public void ConfirmChoice(int index)
        {
            if (!choicesActive || confirmedChoice >= 0 || index < 0 || index >= spawnedChoices.Count) return;
            highlightedChoice = index;
            confirmedChoice = index;
        }

        // ------------------------------------------------------------------ Flow

        private void StartSequence(DialogueSequence sequence, Action onComplete, bool isRuntime)
        {
            if (sequence == null || sequence.nodes == null || sequence.nodes.Count == 0)
            {
                if (isRuntime && sequence != null) Destroy(sequence);
                onComplete?.Invoke();
                return;
            }

            Transform source = PendingSource;
            PendingSource = null;

            // Never cut someone off: a conversation requested while another is on screen waits its turn.
            if (IsOpen || runningRoutine != null)
            {
                queue.Enqueue(new QueuedConversation { sequence = sequence, onComplete = onComplete, isRuntime = isRuntime, source = source });
                return;
            }

            CurrentSource = source;
            pendingCallback = onComplete;
            runtimeSequence = isRuntime ? sequence : null;
            runningRoutine = StartCoroutine(RunSequence(sequence));
        }

        private IEnumerator RunSequence(DialogueSequence sequence)
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            InputLock.Acquire(this);
            Opened?.Invoke();

            bodyText.text = string.Empty;
            bodyText.maxVisibleCharacters = 0;
            if (namePlate != null) namePlate.SetActive(false);
            if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(false);

            PlayUi(openClip);
            yield return Pop(true);

            int index = sequence.IndexOf(sequence.startNodeId);
            if (index < 0) index = 0;

            int safety = 0;
            while (index >= 0 && index < sequence.nodes.Count && safety++ < 1024)
            {
                DialogueNode node = sequence.nodes[index];

                // Conditional nodes that fail are simply skipped.
                if (!IsFlagConditionMet(node.requiredFlag))
                {
                    index++;
                    continue;
                }

                StoryFlags.Set(node.flagToSet);
                DialogueSpeaker speaker = node.speaker != null ? node.speaker : defaultSpeaker;
                ApplySpeaker(speaker);
                currentEffect = node.effect != TextEffect.None ? node.effect : (speaker != null ? speaker.signatureEffect : TextEffect.None);
                CurrentSpeaker = speaker;
                LineStarted?.Invoke(speaker);
                if (!string.IsNullOrEmpty(node.text) && node.text.IndexOf('{') >= 0)
                {
                    foreach (System.Text.RegularExpressions.Match m in StageTag.Matches(node.text))
                    {
                        StageDirection?.Invoke(m.Groups[1].Value, m.Groups[2].Value);
                    }
                }

                yield return TypeLine(node.text, speaker);

                if (node.choices != null && node.choices.Count > 0)
                {
                    // Let the press that finished typing pass before choices accept input.
                    yield return null;
                    yield return RunChoices(node.choices);
                    DialogueChoice picked = node.choices[confirmedChoice];
                    StoryFlags.Set(picked.flagToSet);
                    index = ResolveNext(sequence, index, picked.nextNodeId);
                }
                else
                {
                    yield return WaitForAdvance();
                    PlayUi(advanceClip);
                    index = ResolveNext(sequence, index, node.nextNodeId);
                }
            }

            PlayUi(closeClip);
            yield return Pop(false);
            HideImmediate();
            runningRoutine = null;
            Finish(true);
        }

        private IEnumerator TypeLine(string text, DialogueSpeaker speaker)
        {
            if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(false);

            bodyFX.SetText(Loc.T(text) ?? string.Empty, false, currentEffect);
            TextFX.Parsed parsed = bodyFX.Parsed;

            TMP_TextInfo info = bodyText.textInfo;
            int total = info.characterCount;
            float speedMultiplier = speaker != null ? speaker.typingSpeedMultiplier : 1f;
            int blipEvery = speaker != null ? Mathf.Max(1, speaker.blipEveryNCharacters) : 2;
            float baseCps = Mathf.Max(1f, charactersPerSecond * speedMultiplier);

            int visible = 0;
            int cueIndex = 0;
            float budget = 1f; // reveal the first character immediately
            bool skip = false;

            while (visible < total && !skip)
            {
                float cps = baseCps;
                if (parsed.Has(TextFX.Kind.Slow, visible)) cps *= 0.4f;
                else if (parsed.Has(TextFX.Kind.Fast, visible)) cps *= 1.9f;
                if (parsed.Has(TextFX.Kind.Big, visible)) cps *= 0.6f;
                budget += GameTime.UnscaledDelta * cps;

                float pause = 0f;
                while (budget >= 1f && visible < total)
                {
                    budget -= 1f;
                    char c = info.characterInfo[visible].character;
                    char next = visible + 1 < total ? info.characterInfo[visible + 1].character : '\0';
                    bool bigStart = parsed.Has(TextFX.Kind.Big, visible) && (visible == 0 || !parsed.Has(TextFX.Kind.Big, visible - 1));
                    bodyFX.Reveal(visible);
                    visible++;
                    bodyText.maxVisibleCharacters = visible;

                    if (!char.IsWhiteSpace(c) && visible % blipEvery == 0) PlayBlip(speaker);
                    if (bigStart) Emphasize(0.25f);

                    // Inline cues ({p}, {quake}) placed right after this character.
                    while (cueIndex < parsed.cues.Count && parsed.cues[cueIndex].index <= visible)
                    {
                        TextFX.Cue cue = parsed.cues[cueIndex++];
                        if (cue.quake) Emphasize(0.5f);
                        pause = Mathf.Max(pause, cue.pause);
                    }

                    pause = Mathf.Max(pause, GetPunctuationPause(c, next));
                    if (pause > 0f) break;
                }

                if (pause > 0f)
                {
                    budget = 0f;
                    for (float waited = 0f; waited < pause; waited += GameTime.UnscaledDelta)
                    {
                        yield return null;
                        if (TypingSkipPressed()) { skip = true; break; }
                    }
                    continue;
                }

                if (skip || visible >= total) break;
                yield return null;
                if (TypingSkipPressed()) skip = true;
            }

            // Cues that were skipped over still land (a skipped {quake} is still a quake).
            while (cueIndex < parsed.cues.Count)
            {
                if (parsed.cues[cueIndex++].quake) Emphasize(0.35f);
            }
            bodyFX.RevealAll();
        }

        /// <summary>Box punch + screen shake + event for actors.</summary>
        private void Emphasize(float strength)
        {
            if (boxPunchRoutine != null) StopCoroutine(boxPunchRoutine);
            boxPunchRoutine = StartCoroutine(BoxPunch(strength));
            if (strength >= 0.4f && CameraSystem.IsometricCameraRig.Main != null) CameraSystem.IsometricCameraRig.Main.AddTrauma(strength * 0.6f);
            Emphasis?.Invoke(strength);
        }

        private Coroutine boxPunchRoutine;
        private Vector2 boxBasePosition;

        private IEnumerator BoxPunch(float strength)
        {
            const float duration = 0.3f;
            Vector2 basePos = boxBasePosition;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                float n = t / duration;
                float decay = 1f - n;
                boxTransform.localScale = Vector3.one * (1f + Mathf.Sin(n * Mathf.PI * 3f) * 0.05f * strength * 2f * decay);
                boxTransform.anchoredPosition = basePos + new Vector2(Mathf.Sin(n * 70f) * 10f * strength * decay, 0f);
                yield return null;
            }
            boxTransform.localScale = Vector3.one;
            boxTransform.anchoredPosition = basePos;
            boxPunchRoutine = null;
        }

        private IEnumerator WaitForAdvance()
        {
            if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(true);
            // Wait at least one frame so the press that completed the typewriter is not reused.
            yield return null;
            while (!AdvancePressed()) yield return null;
            if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(false);
        }

        private IEnumerator RunChoices(List<DialogueChoice> choices)
        {
            ClearChoices();
            confirmedChoice = -1;
            highlightedChoice = 0;
            choiceContainer.gameObject.SetActive(true);

            if (choiceButtonPrefab == null) choiceButtonPrefab = BuildFallbackChoiceTemplate();

            for (int i = 0; i < choices.Count; i++)
            {
                DialogueChoiceButton button = Instantiate(choiceButtonPrefab, choiceContainer);
                button.gameObject.SetActive(true);
                button.Bind(this, i, TextFX.Strip(Loc.T(choices[i].text)));
                spawnedChoices.Add(button);
                StartCoroutine(PopTransform(button.transform, i * 0.05f));
            }

            choicesActive = true;
            for (int i = 0; i < spawnedChoices.Count; i++) spawnedChoices[i].SetHighlighted(i == 0, true);

            float cooldown = choiceInputCooldown;
            navigationHeld = false;

            while (confirmedChoice < 0)
            {
                cooldown -= GameTime.UnscaledDelta;
                HandleChoiceNavigation();
                if (cooldown <= 0f && GameInput.SubmitPressed) ConfirmChoice(highlightedChoice);
                yield return null;
            }

            PlayUi(choiceConfirmClip);
            // Brief flash on the picked entry.
            for (float t = 0f; t < 0.15f; t += GameTime.UnscaledDelta)
            {
                bool on = Mathf.FloorToInt(t / 0.05f) % 2 == 0;
                spawnedChoices[confirmedChoice].SetHighlighted(on, true);
                yield return null;
            }

            choicesActive = false;
            ClearChoices();
        }

        private void HandleChoiceNavigation()
        {
            if (GameTime.Paused) return;
            float vertical = GameInput.MoveVector.y;
            if (Mathf.Abs(vertical) < 0.5f)
            {
                navigationHeld = false;
                return;
            }

            navigationRepeatTimer -= GameTime.UnscaledDelta;
            if (navigationHeld && navigationRepeatTimer > 0f) return;

            navigationRepeatTimer = navigationHeld ? 0.12f : 0.35f; // initial delay, then fast repeat
            navigationHeld = true;
            HighlightChoice(highlightedChoice + (vertical > 0f ? -1 : 1));
        }

        /// <summary>
        /// Builds a choice button template in code, so branching dialogue never gets stuck
        /// if the prefab reference is missing.
        /// </summary>
        private DialogueChoiceButton BuildFallbackChoiceTemplate()
        {
            Debug.LogWarning("[DialogueManager] Choice button prefab missing: using a generated template.", this);
            var go = new GameObject("ChoiceTemplate", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(860f, 56f);
            Image background = go.AddComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.065f, 0.92f);
            go.AddComponent<LayoutElement>().preferredHeight = 56f;

            TMP_Text cursorText = CreateFallbackText(go.transform, "Cursor", ">", new Color(1f, 0.31f, 0.85f));
            RectTransform cursorRect = cursorText.rectTransform;
            cursorRect.anchorMin = cursorRect.anchorMax = new Vector2(0f, 0.5f);
            cursorRect.pivot = new Vector2(0f, 0.5f);
            cursorRect.anchoredPosition = new Vector2(8f, 0f);
            cursorRect.sizeDelta = new Vector2(40f, 50f);

            TMP_Text labelText = CreateFallbackText(go.transform, "Label", "...", Color.white);
            labelText.alignment = TextAlignmentOptions.Left;
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(56f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            var button = go.AddComponent<DialogueChoiceButton>();
            button.Setup(labelText, cursorText.gameObject);
            go.SetActive(false);
            return button;
        }

        private TMP_Text CreateFallbackText(Transform parent, string name, string content, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (bodyText != null) text.font = bodyText.font;
            text.text = content;
            text.fontSize = 28f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private void Finish(bool invokeCallback)
        {
            IsOpen = false;
            CurrentSpeaker = null;
            CurrentSource = null;
            lastClosedFrame = Time.frameCount;
            currentEffect = TextEffect.None;
            InputLock.Release(this);

            if (runtimeSequence != null)
            {
                Destroy(runtimeSequence);
                runtimeSequence = null;
            }

            Action callback = pendingCallback;
            pendingCallback = null;
            Closed?.Invoke();
            if (invokeCallback) callback?.Invoke();

            // Next in line (a bark that arrived while we were talking).
            if (!IsOpen && runningRoutine == null && queue.Count > 0) StartCoroutine(PlayNextQueued());
        }

        private IEnumerator PlayNextQueued()
        {
            yield return GameTime.Wait(0.25f);
            if (IsOpen || runningRoutine != null || queue.Count == 0) yield break;
            QueuedConversation next = queue.Dequeue();
            PendingSource = next.source;
            StartSequence(next.sequence, next.onComplete, next.isRuntime);
        }

        // ------------------------------------------------------------------ Helpers

        private static int ResolveNext(DialogueSequence sequence, int currentIndex, string nextNodeId)
        {
            if (string.IsNullOrEmpty(nextNodeId)) return currentIndex + 1;
            if (nextNodeId == DialogueSequence.EndNodeId) return -1;

            int target = sequence.IndexOf(nextNodeId);
            if (target < 0) Debug.LogWarning($"[DialogueManager] Node id '{nextNodeId}' not found in '{sequence.name}'. Ending dialogue.", sequence);
            return target;
        }

        private static bool IsFlagConditionMet(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (condition[0] == '!') return !StoryFlags.Has(condition.Substring(1));
            return StoryFlags.Has(condition);
        }

        private float GetPunctuationPause(char c, char next)
        {
            bool nextIsBreak = next == '\0' || char.IsWhiteSpace(next);
            switch (c)
            {
                case '.':
                    if (next == '.') return ellipsisPause;
                    return nextIsBreak ? sentencePause : 0f;
                case '!':
                case '?':
                    return nextIsBreak ? sentencePause : 0f;
                case ',':
                case ';':
                case ':':
                    return nextIsBreak ? commaPause : 0f;
                default:
                    return 0f;
            }
        }

        private bool TypingSkipPressed()
        {
            if (Time.frameCount == openedFrame || GameTime.Paused || GameTime.PauseKeyPressedThisFrame) return false;
            return GameInput.SubmitPressed || GameInput.CancelPressed || MouseAdvancePressed();
        }

        private bool AdvancePressed()
        {
            if (Time.frameCount == openedFrame || GameTime.Paused) return false;
            return GameInput.SubmitPressed || MouseAdvancePressed();
        }

        private bool MouseAdvancePressed()
        {
            if (GameTime.Paused || GameTime.InputBlockedAfterResume) return false;
            return allowMouseAdvance && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private void ApplySpeaker(DialogueSpeaker speaker)
        {
            bool hasName = speaker != null && !string.IsNullOrEmpty(speaker.displayName);
            if (namePlate != null) namePlate.SetActive(hasName);
            if (hasName && nameText != null)
            {
                nameText.text = Loc.T(speaker.displayName);
                nameText.color = speaker.nameColor;
            }

            // Every voice has its own typeface and styling.
            SpeakerStyle style = speaker != null ? speaker.style : SpeakerStyle.Default;
            DialogueFonts.Look look = DialogueFonts.Get(style, defaultFont);
            if (bodyText != null)
            {
                bodyText.font = look.font != null ? look.font : defaultFont;
                bodyText.fontStyle = look.fontStyle;
                bodyText.fontSize = defaultFontSize * look.sizeScale;
                bodyText.characterSpacing = look.characterSpacing;
                bodyText.color = speaker != null && speaker.textColor.a > 0.01f ? speaker.textColor : defaultTextColor;
            }
            if (nameText != null && hasName)
            {
                nameText.font = look.font != null ? look.font : defaultNameFont;
                nameText.fontSize = defaultNameSize * Mathf.Lerp(1f, look.sizeScale, 0.6f);
                nameText.characterSpacing = look.characterSpacing * 0.5f;
            }

            if (portraitImage != null)
            {
                bool hasPortrait = speaker != null && speaker.portrait != null;
                portraitImage.gameObject.SetActive(hasPortrait);
                if (hasPortrait) portraitImage.sprite = speaker.portrait;
            }
        }

        private void PlayBlip(DialogueSpeaker speaker)
        {
            if (voiceSource == null || speaker == null || speaker.voiceBlip == null) return;
            voiceSource.pitch = UnityEngine.Random.Range(speaker.pitchRange.x, speaker.pitchRange.y);
            voiceSource.PlayOneShot(speaker.voiceBlip, speaker.voiceVolume);
            Blipped?.Invoke(speaker);
        }

        private void PlayUi(AudioClip clip)
        {
            if (uiSource != null && clip != null) uiSource.PlayOneShot(clip);
        }

        private void ClearChoices()
        {
            foreach (DialogueChoiceButton button in spawnedChoices)
            {
                if (button != null) Destroy(button.gameObject);
            }
            spawnedChoices.Clear();
            choicesActive = false;
            if (choiceContainer != null) choiceContainer.gameObject.SetActive(false);
        }

        private void HideImmediate()
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
                rootGroup.interactable = false;
            }
            if (boxTransform != null) boxTransform.localScale = Vector3.one * popStartScale;
            if (advanceIndicator != null) advanceIndicator.gameObject.SetActive(false);
            if (choiceContainer != null) choiceContainer.gameObject.SetActive(false);
            if (bodyText != null) bodyText.text = string.Empty;
            if (bodyFX != null) bodyFX.Clear();
        }

        /// <summary>Bouncy scale + fade of the whole box.</summary>
        private IEnumerator Pop(bool open)
        {
            float duration = open ? popInDuration : popOutDuration;
            EaseType ease = open ? EaseType.EaseOutBack : EaseType.EaseInBack;
            float fromScale = open ? popStartScale : 1f;
            float toScale = open ? 1f : popStartScale;
            float fromAlpha = open ? 0f : 1f;
            float toAlpha = open ? 1f : 0f;

            if (boxPunchRoutine != null)
            {
                StopCoroutine(boxPunchRoutine);
                boxPunchRoutine = null;
                boxTransform.anchoredPosition = boxBasePosition;
            }
            rootGroup.blocksRaycasts = true;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                float n = t / duration;
                boxTransform.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, Easing.Evaluate(ease, n));
                rootGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, Easing.Evaluate(EaseType.EaseOutCubic, n));
                yield return null;
            }
            boxTransform.localScale = Vector3.one * toScale;
            rootGroup.alpha = toAlpha;
            rootGroup.blocksRaycasts = open;
        }

        /// <summary>Small staggered pop for choice entries.</summary>
        private IEnumerator PopTransform(Transform target, float delay)
        {
            target.localScale = Vector3.zero;
            for (float d = 0f; d < delay; d += GameTime.UnscaledDelta) yield return null;
            const float duration = 0.18f;
            for (float t = 0f; t < duration && target != null; t += GameTime.UnscaledDelta)
            {
                target.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, t / duration);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        private void Update()
        {
            if (advanceIndicator != null && advanceIndicator.gameObject.activeSelf)
            {
                float bob = Mathf.Abs(Mathf.Sin(GameTime.UnscaledTime * indicatorBobSpeed)) * indicatorBobAmplitude;
                advanceIndicator.anchoredPosition = indicatorBasePosition + Vector2.down * bob;
            }
        }
    }
}
