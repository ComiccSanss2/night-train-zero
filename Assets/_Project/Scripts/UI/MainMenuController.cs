using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Subnet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Title screen framed as the boot screen of Leo's modified handheld ("LEO-OS").
    /// - Typed boot log, then the four OS entries: BOOT SYSTEM / CORRUPTED MEMORIES / SYSTEM CONFIG / SHUT DOWN.
    /// - CRT overlay glitches (scanline shader "_Glitch") + URP chromatic aberration pulses on hover and at random idle moments.
    /// - Retro click sounds for navigation, ambience loops (subway hum, rain) faded in on boot.
    /// - Async scene loading behind a fade; CRT "power-off" collapse on quit.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Serializable]
        public class MemoryChapter
        {
            [Tooltip("Title shown in CORRUPTED MEMORIES (e.g. 'MEM_01 // THE MOVING BOXES').")]
            public string title = "MEM_00 // UNTITLED";
            [Tooltip("Scene to load. Must be in File → Build Profiles → Scene List.")]
            public string sceneName = string.Empty;
            [Tooltip("SaveData chapter key. Empty = always unlocked.")]
            public string unlockKey = string.Empty;
        }

        [Header("Scenes")]
        [Tooltip("First gameplay scene for a new session.")]
        [SerializeField] private string newGameScene = "01_Tenement";

        [Header("Panels")]
        [Tooltip("Root of everything inside the handheld screen. Scaled for the CRT power-off effect.")]
        [SerializeField] private RectTransform screenRoot;
        [SerializeField] private CanvasGroup mainPanel;
        [SerializeField] private CanvasGroup memoriesPanel;
        [SerializeField] private CanvasGroup configPanel;
        [SerializeField] private float panelFadeDuration = 0.18f;

        [Header("Main Entries")]
        [SerializeField] private RetroMenuButton bootButton;
        [SerializeField] private RetroMenuButton memoriesButton;
        [SerializeField] private RetroMenuButton configButton;
        [SerializeField] private RetroMenuButton shutdownButton;

        [Header("Corrupted Memories")]
        [Tooltip("Vertical Layout Group that receives one entry per chapter.")]
        [SerializeField] private RectTransform memoriesListRoot;
        [SerializeField] private RetroMenuButton memoryEntryPrefab;
        [SerializeField] private RetroMenuButton memoriesBackButton;
        [SerializeField] private List<MemoryChapter> chapters = new List<MemoryChapter>();

        [Header("System Config")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [Tooltip("Optional value readouts, rendered as [#####-----].")]
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private TMP_Text musicValueText;
        [SerializeField] private TMP_Text sfxValueText;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private RetroMenuButton configBackButton;
        [Tooltip("Row that cycles EN / IT / FR (left/right or click).")]
        [SerializeField] private RetroMenuButton languageButton;
        [Tooltip("Row that cycles LOW / MEDIUM / HIGH graphics.")]
        [SerializeField] private RetroMenuButton qualityButton;

        [Header("OS Chrome")]
        [SerializeField] private TMP_Text bootLogText;
        [Tooltip("Top-right clock.")]
        [SerializeField] private TMP_Text clockText;
        [Tooltip("Footer line describing the highlighted entry.")]
        [SerializeField] private TMP_Text statusText;
        [TextArea(1, 2)]
        [SerializeField] private string[] bootLogLines =
        {
            "LEO-OS v0.9.3 (unofficial build)",
            "checking battery............ 12%",
            "mounting /home/leo.......... OK",
            "mounting /memories.......... <color=#FF4FD8>DAMAGED SECTORS: 3</color>",
            "scanning local network...... CENTRAL_STATION_PUBLIC (weak)",
            "loading shell............... OK",
            "",
            "good evening, leo. you're still awake."
        };
        [SerializeField] private float bootCharacterDelay = 0.012f;
        [SerializeField] private float bootLineDelay = 0.14f;

        [Header("CRT / Glitch FX")]
        [Tooltip("Full-screen Image using the 'Subnet/UI/CRTOverlay' material.")]
        [SerializeField] private Image crtOverlay;
        [Tooltip("Global Volume with a Chromatic Aberration override (the menu canvas must be Screen Space - Camera).")]
        [SerializeField] private Volume postProcessVolume;
        [SerializeField] private float baseChromaticAberration = 0.12f;
        [SerializeField] private float hoverGlitchStrength = 0.35f;
        [SerializeField] private float hoverGlitchDuration = 0.12f;
        [SerializeField] private Vector2 idleGlitchInterval = new Vector2(4f, 10f);

        [Header("Audio")]
        [Tooltip("2D source for UI one-shots (Kenney Interface Sounds).")]
        [SerializeField] private AudioSource uiSource;
        [Tooltip("Looping low subway hum.")]
        [SerializeField] private AudioSource humSource;
        [Tooltip("Looping distant city rain.")]
        [SerializeField] private AudioSource rainSource;
        [SerializeField] private float ambienceTargetVolume = 0.5f;
        [SerializeField] private float ambienceFadeIn = 3f;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip backClip;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private AudioClip openPanelClip;
        [SerializeField] private AudioClip closePanelClip;
        [SerializeField] private AudioClip toggleClip;
        [SerializeField] private AudioClip sliderTickClip;
        [SerializeField] private AudioClip bootBlipClip;
        [SerializeField] private AudioClip powerOffClip;
        [SerializeField] private AudioClip[] glitchClips = { };

        [Header("Transitions")]
        [SerializeField] private ScreenFader fader;
        [SerializeField] private float sceneFadeDuration = 1.2f;

        private static readonly int GlitchPropertyId = Shader.PropertyToID("_Glitch");

        private readonly List<RetroMenuButton> spawnedMemoryEntries = new List<RetroMenuButton>();
        private readonly StringBuilder logBuilder = new StringBuilder(512);
        private Material crtMaterialInstance;
        private ChromaticAberration chromaticAberration;
        private CanvasGroup activePanel;
        private Coroutine glitchRoutine;
        private float idleGlitchTimer;
        private float lastSliderTickTime;
        private bool busy = true;
        private bool bootSkipRequested;

        // ------------------------------------------------------------------ Lifecycle

        private void Awake()
        {
            InputLock.ReleaseAll();
            StoryFlags.ResetAll();

            if (crtOverlay != null)
            {
                crtMaterialInstance = new Material(crtOverlay.material);
                crtOverlay.material = crtMaterialInstance;
            }

            if (postProcessVolume != null && postProcessVolume.profile != null &&
                postProcessVolume.profile.TryGet(out chromaticAberration))
            {
                chromaticAberration.intensity.overrideState = true;
                chromaticAberration.intensity.value = baseChromaticAberration;
            }

            WireButtons();
            InitConfigControls();

            SetPanelInstant(mainPanel, false);
            SetPanelInstant(memoriesPanel, false);
            SetPanelInstant(configPanel, false);
            if (bootLogText != null) bootLogText.text = string.Empty;
            if (statusText != null) statusText.text = string.Empty;
        }

        private void Start()
        {
            GameSettings.ApplyAudio(audioMixer);
            ResetIdleGlitchTimer();
            StartCoroutine(BootSequence());
        }

        private void OnDestroy()
        {
            if (crtMaterialInstance != null) Destroy(crtMaterialInstance);
        }

        private void Update()
        {
            UpdateClock();
            UpdateIdleGlitch();

            if (GameInput.SubmitPressed || GameInput.CancelPressed) bootSkipRequested = true;

            HandleConfigCycling();

            if (!busy && GameInput.CancelPressed && activePanel != mainPanel && activePanel != null)
            {
                ReturnToMain();
            }

            // Recover focus if the mouse clicked empty space (keyboard/gamepad would otherwise be lost).
            if (!busy && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                GameInput.MoveVector.sqrMagnitude > 0.25f)
            {
                SelectDefaultFor(activePanel);
            }
        }

        // ------------------------------------------------------------------ Boot

        private IEnumerator BootSequence()
        {
            busy = true;
            bootSkipRequested = false;
            if (fader != null)
            {
                fader.SetInstant(1f);
                fader.FadeTo(0f, 1.2f);
            }
            StartCoroutine(FadeInAmbience());

            yield return WaitRealtime(0.6f);
            bootSkipRequested = false;

            if (bootLogText != null)
            {
                logBuilder.Clear();
                foreach (string line in bootLogLines)
                {
                    yield return TypeBootLine(Loc.T(line));
                    logBuilder.Append('\n');
                    bootLogText.text = logBuilder.ToString();
                    if (!bootSkipRequested) yield return WaitRealtime(bootLineDelay);
                }
            }

            Glitch(0.8f, 0.3f);
            yield return WaitRealtime(0.25f);

            busy = false;
            ShowPanel(mainPanel);
        }

        private IEnumerator TypeBootLine(string line)
        {
            // Rich-text tags are appended whole so they never render half-typed.
            int i = 0;
            int blipCounter = 0;
            while (i < line.Length)
            {
                if (line[i] == '<')
                {
                    int close = line.IndexOf('>', i);
                    if (close > i)
                    {
                        logBuilder.Append(line, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }

                logBuilder.Append(line[i]);
                i++;

                if (bootSkipRequested) continue;

                bootLogText.text = logBuilder.ToString();
                if (++blipCounter % 3 == 0) PlayUi(bootBlipClip, 0.35f);
                yield return WaitRealtime(bootCharacterDelay);
            }
            bootLogText.text = logBuilder.ToString();
        }

        // ------------------------------------------------------------------ Wiring

        private void WireButtons()
        {
            Register(bootButton, OnBootPressed, () => SaveData.HasSave
                ? Loc.F("RESTORE LAST SESSION // {0}", SaveData.CheckpointScene.ToUpperInvariant())
                : "INITIALIZE NEW SESSION");
            Register(languageButton, b => CycleLanguage(1), () => "LANGUAGE");
            Register(qualityButton, b => CycleQuality(1), () => "LOWER IT IF THE GAME FEELS SLOW.");
            Register(memoriesButton, b => OpenMemories(), () => "BROWSE RECOVERED MEMORY FILES");
            Register(configButton, b => OpenConfig(), () => "ADJUST SYSTEM PARAMETERS");
            Register(shutdownButton, b => StartCoroutine(ShutDownRoutine()), () => "POWER OFF DEVICE. THE CITY WILL STILL BE THERE.");
            Register(memoriesBackButton, b => ReturnToMain(), () => "RETURN");
            Register(configBackButton, b => ReturnToMain(), () => "SAVE AND RETURN");
        }

        private void Register(RetroMenuButton button, Action<RetroMenuButton> onActivated, Func<string> describe)
        {
            if (button == null) return;
            button.Highlighted += b =>
            {
                OnAnyHighlighted();
                if (statusText != null) statusText.text = Loc.T(describe());
            };
            button.Activated += b =>
            {
                if (busy) return;
                onActivated(b);
            };
        }

        private void OnAnyHighlighted()
        {
            if (busy) return;
            PlayUi(hoverClip);
            Glitch(hoverGlitchStrength, hoverGlitchDuration);
        }

        // ------------------------------------------------------------------ Main actions

        private void OnBootPressed(RetroMenuButton button)
        {
            string scene = SaveData.HasSave ? SaveData.CheckpointScene : newGameScene;
            if (SaveData.HasSave) StoryFlags.LoadFromPrefs(); // continue with the saved story progress
            LoadScene(scene);
        }

        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MainMenu] Scene '{sceneName}' is missing from the Build Profile scene list.");
                PlayUi(errorClip);
                Glitch(0.6f, 0.25f);
                if (statusText != null) statusText.text = Loc.F("ERROR: FILE NOT FOUND // {0}", sceneName);
                return;
            }
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            busy = true;
            PlayUi(confirmClip);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (activePanel != null) activePanel.interactable = false;

            Glitch(1f, 0.5f);
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            load.allowSceneActivation = false;

            StartCoroutine(FadeOutAmbience(sceneFadeDuration));
            if (fader != null) yield return fader.FadeTo(1f, sceneFadeDuration);
            else yield return WaitRealtime(sceneFadeDuration);

            while (load.progress < 0.9f) yield return null;
            load.allowSceneActivation = true;
        }

        private IEnumerator ShutDownRoutine()
        {
            busy = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PlayUi(powerOffClip != null ? powerOffClip : backClip);
            StartCoroutine(FadeOutAmbience(0.4f));

            if (screenRoot != null)
            {
                Vector3 start = screenRoot.localScale;
                // Classic CRT collapse: squash to a horizontal line, then to a dot.
                for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
                {
                    float e = Easing.Evaluate(EaseType.EaseInCubic, t / 0.18f);
                    screenRoot.localScale = new Vector3(start.x, Mathf.Lerp(start.y, start.y * 0.004f, e), start.z);
                    yield return null;
                }
                for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
                {
                    float e = Easing.Evaluate(EaseType.EaseInCubic, t / 0.12f);
                    screenRoot.localScale = new Vector3(Mathf.Lerp(start.x, 0f, e), start.y * 0.004f, start.z);
                    yield return null;
                }
                screenRoot.localScale = Vector3.zero;
            }

            if (fader != null) fader.SetInstant(1f);
            yield return WaitRealtime(0.4f);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Corrupted Memories

        private void OpenMemories()
        {
            BuildMemoryList();
            PlayUi(openPanelClip);
            ShowPanel(memoriesPanel);
        }

        private void BuildMemoryList()
        {
            foreach (RetroMenuButton entry in spawnedMemoryEntries)
            {
                if (entry != null) Destroy(entry.gameObject);
            }
            spawnedMemoryEntries.Clear();

            if (memoriesListRoot == null || memoryEntryPrefab == null) return;

            foreach (MemoryChapter chapter in chapters)
            {
                RetroMenuButton entry = Instantiate(memoryEntryPrefab, memoriesListRoot);
                entry.gameObject.SetActive(true);
                bool unlocked = SaveData.IsChapterUnlocked(chapter.unlockKey);
                entry.SetLabel(chapter.title);
                entry.SetLocked(!unlocked);

                MemoryChapter captured = chapter;
                entry.Highlighted += b =>
                {
                    OnAnyHighlighted();
                    if (statusText != null) statusText.text = Loc.T(unlocked ? "REPLAY MEMORY" : "DATA CORRUPTED. KEEP GOING TO RECOVER IT.");
                };
                entry.Activated += b =>
                {
                    if (busy) return;
                    if (!unlocked)
                    {
                        PlayUi(errorClip);
                        Glitch(0.5f, 0.2f);
                        b.Glitch();
                        return;
                    }
                    LoadScene(captured.sceneName);
                };
                spawnedMemoryEntries.Add(entry);
            }

            // Keep the back button last in the list.
            if (memoriesBackButton != null && memoriesBackButton.transform.parent == memoriesListRoot)
            {
                memoriesBackButton.transform.SetAsLastSibling();
            }
        }

        // ------------------------------------------------------------------ System Config

        private void InitConfigControls()
        {
            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
                masterSlider.onValueChanged.AddListener(v => { GameSettings.MasterVolume = v; OnVolumeChanged(); });
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
                musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; OnVolumeChanged(); });
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
                sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; OnVolumeChanged(); });
            }
            if (screenShakeToggle != null)
            {
                screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShake);
                screenShakeToggle.onValueChanged.AddListener(on =>
                {
                    GameSettings.ScreenShake = on;
                    PlayUi(toggleClip);
                    if (on) Glitch(0.4f, 0.15f); // a tiny preview jolt
                });
            }
            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
                fullscreenToggle.onValueChanged.AddListener(on =>
                {
                    GameSettings.Fullscreen = on;
                    GameSettings.ApplyDisplay();
                    PlayUi(toggleClip);
                });
            }
            RefreshConfigRows();
        }

        private float cycleTimer;
        private bool cycleHeld;

        private void OnEnable() => Loc.LanguageChanged += RefreshConfigRows;

        private void OnDisable() => Loc.LanguageChanged -= RefreshConfigRows;

        /// <summary>Left/right on the LANGUAGE / GRAPHICS rows cycles their value.</summary>
        private void HandleConfigCycling()
        {
            if (busy || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            bool onLanguage = languageButton != null && selected == languageButton.gameObject;
            bool onQuality = qualityButton != null && selected == qualityButton.gameObject;
            if (!onLanguage && !onQuality) return;

            float x = GameInput.MoveVector.x;
            if (Mathf.Abs(x) < 0.5f)
            {
                cycleHeld = false;
                return;
            }
            cycleTimer -= Time.unscaledDeltaTime;
            if (cycleHeld && cycleTimer > 0f) return;
            cycleTimer = cycleHeld ? 0.25f : 0.35f;
            cycleHeld = true;
            if (onLanguage) CycleLanguage(x > 0f ? 1 : -1);
            else CycleQuality(x > 0f ? 1 : -1);
        }

        private void CycleLanguage(int direction)
        {
            Loc.Cycle(direction);
            PlayUi(toggleClip);
            Glitch(0.4f, 0.15f);
        }

        private void CycleQuality(int direction)
        {
            GameSettings.GraphicsQuality = (GameSettings.GraphicsQuality + direction + 3) % 3;
            GameSettings.ApplyGraphics();
            PlayUi(toggleClip);
            RefreshConfigRows();
        }

        private void RefreshConfigRows()
        {
            if (languageButton != null) languageButton.SetLabel($"{Loc.T("LANGUAGE")}   < {Loc.NativeName(Loc.Current)} >");
            if (qualityButton != null) qualityButton.SetLabel($"{Loc.T("GRAPHICS")}   < {Loc.T(GameSettings.QualityName(GameSettings.GraphicsQuality))} >");
            RefreshVolumeLabels();
        }

        private void OnVolumeChanged()
        {
            GameSettings.ApplyAudio(audioMixer);
            RefreshVolumeLabels();
            // Throttled tick so dragging doesn't machine-gun the sound.
            if (Time.unscaledTime - lastSliderTickTime > 0.06f)
            {
                lastSliderTickTime = Time.unscaledTime;
                PlayUi(sliderTickClip);
            }
        }

        private void RefreshVolumeLabels()
        {
            if (masterValueText != null) masterValueText.text = BuildBar(GameSettings.MasterVolume);
            if (musicValueText != null) musicValueText.text = BuildBar(GameSettings.MusicVolume);
            if (sfxValueText != null) sfxValueText.text = BuildBar(GameSettings.SfxVolume);
        }

        private static string BuildBar(float value)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value) * 10f);
            return "[" + new string('#', filled) + new string('-', 10 - filled) + "] " + Mathf.RoundToInt(value * 100f).ToString().PadLeft(3) + "%";
        }

        private void OpenConfig()
        {
            PlayUi(openPanelClip);
            ShowPanel(configPanel);
        }

        // ------------------------------------------------------------------ Panels

        private void ReturnToMain()
        {
            if (activePanel == configPanel) GameSettings.Save();
            PlayUi(activePanel == mainPanel ? backClip : closePanelClip);
            RetroMenuButton reselect = activePanel == configPanel ? configButton : activePanel == memoriesPanel ? memoriesButton : bootButton;
            ShowPanel(mainPanel, reselect);
        }

        private void ShowPanel(CanvasGroup panel, RetroMenuButton selectOverride = null)
        {
            if (panel == null) return;
            CanvasGroup previous = activePanel;
            activePanel = panel;

            if (previous != null && previous != panel) StartCoroutine(FadePanel(previous, false));
            StartCoroutine(FadePanel(panel, true));

            if (selectOverride != null) Select(selectOverride.gameObject);
            else SelectDefaultFor(panel);
        }

        private void SelectDefaultFor(CanvasGroup panel)
        {
            if (panel == mainPanel && bootButton != null) Select(bootButton.gameObject);
            else if (panel == memoriesPanel)
            {
                GameObject first = spawnedMemoryEntries.Count > 0 ? spawnedMemoryEntries[0].gameObject : memoriesBackButton != null ? memoriesBackButton.gameObject : null;
                Select(first);
            }
            else if (panel == configPanel && masterSlider != null) Select(masterSlider.gameObject);
        }

        private static void Select(GameObject target)
        {
            if (EventSystem.current == null || target == null) return;
            // Clear first so OnSelect fires even if it was already selected.
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }

        private IEnumerator FadePanel(CanvasGroup panel, bool show)
        {
            panel.interactable = show;
            panel.blocksRaycasts = show;
            if (show) panel.gameObject.SetActive(true);

            float from = panel.alpha;
            float to = show ? 1f : 0f;
            for (float t = 0f; t < panelFadeDuration; t += Time.unscaledDeltaTime)
            {
                panel.alpha = Mathf.Lerp(from, to, t / panelFadeDuration);
                yield return null;
            }
            panel.alpha = to;
            if (!show && activePanel != panel) panel.gameObject.SetActive(false);
        }

        private static void SetPanelInstant(CanvasGroup panel, bool show)
        {
            if (panel == null) return;
            panel.alpha = show ? 1f : 0f;
            panel.interactable = show;
            panel.blocksRaycasts = show;
            panel.gameObject.SetActive(show);
        }

        // ------------------------------------------------------------------ FX

        /// <summary>Pulses the CRT overlay glitch bands and chromatic aberration.</summary>
        private void Glitch(float strength, float duration)
        {
            if (glitchRoutine != null) StopCoroutine(glitchRoutine);
            glitchRoutine = StartCoroutine(GlitchRoutine(strength, duration));
            if (glitchClips != null && glitchClips.Length > 0 && strength >= 0.5f)
            {
                PlayUi(glitchClips[UnityEngine.Random.Range(0, glitchClips.Length)], 0.6f);
            }
        }

        private IEnumerator GlitchRoutine(float strength, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                // Flickery decay: random spikes that shrink over time.
                float decay = 1f - t / duration;
                float amount = strength * decay * UnityEngine.Random.Range(0.5f, 1f);
                SetGlitchAmount(amount);
                yield return null;
            }
            SetGlitchAmount(0f);
            glitchRoutine = null;
        }

        private void SetGlitchAmount(float amount)
        {
            if (crtMaterialInstance != null) crtMaterialInstance.SetFloat(GlitchPropertyId, amount);
            if (chromaticAberration != null) chromaticAberration.intensity.value = Mathf.Clamp01(baseChromaticAberration + amount);
        }

        private void UpdateIdleGlitch()
        {
            idleGlitchTimer -= Time.unscaledDeltaTime;
            if (idleGlitchTimer > 0f) return;
            ResetIdleGlitchTimer();
            Glitch(UnityEngine.Random.Range(0.15f, 0.45f), UnityEngine.Random.Range(0.06f, 0.18f));
        }

        private void ResetIdleGlitchTimer()
        {
            idleGlitchTimer = UnityEngine.Random.Range(idleGlitchInterval.x, idleGlitchInterval.y);
        }

        private void UpdateClock()
        {
            if (clockText == null) return;
            DateTime now = DateTime.Now;
            bool colon = now.Second % 2 == 0;
            clockText.text = now.ToString(colon ? "HH:mm" : "HH mm");
        }

        // ------------------------------------------------------------------ Audio

        private void PlayUi(AudioClip clip, float volume = 1f)
        {
            if (uiSource != null && clip != null) uiSource.PlayOneShot(clip, volume);
        }

        private IEnumerator FadeInAmbience()
        {
            StartLoop(humSource);
            StartLoop(rainSource);
            for (float t = 0f; t < ambienceFadeIn; t += Time.unscaledDeltaTime)
            {
                float v = Mathf.Lerp(0f, ambienceTargetVolume, t / ambienceFadeIn);
                if (humSource != null) humSource.volume = v;
                if (rainSource != null) rainSource.volume = v;
                yield return null;
            }
        }

        private IEnumerator FadeOutAmbience(float duration)
        {
            float humStart = humSource != null ? humSource.volume : 0f;
            float rainStart = rainSource != null ? rainSource.volume : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / duration;
                if (humSource != null) humSource.volume = humStart * k;
                if (rainSource != null) rainSource.volume = rainStart * k;
                yield return null;
            }
            if (humSource != null) humSource.Stop();
            if (rainSource != null) rainSource.Stop();
        }

        private static void StartLoop(AudioSource source)
        {
            if (source == null || source.clip == null) return;
            source.loop = true;
            source.volume = 0f;
            if (!source.isPlaying) source.Play();
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }
    }
}
