using System;
using System.Collections.Generic;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Hacking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Esc / Start → pause menu styled like Leo's handheld OS:
    ///   RESUME · SETTINGS · SAVE GAME · LOAD LAST SAVE · MAIN MENU · QUIT GAME
    /// SETTINGS: volumes, screen shake, fullscreen, LANGUAGE (EN / IT / FR, live) and GRAPHICS (performance).
    /// Value rows change with left/right (or by clicking). Time stops while the menu is open.
    /// Not available during cutscenes, conversations or hacking (Esc has other jobs there).
    /// The UI is built at runtime; one instance per gameplay scene.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        /// <summary>True while the menu is open.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Frame the menu last closed (so the confirm press doesn't also interact in the world).</summary>
        public static int LastClosedFrame { get; private set; } = -10;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip errorClip;

        private class Row
        {
            public RetroMenuButton button;
            public Func<string> label;
            public Action<int> change;
        }

        private readonly List<RetroMenuButton> mainButtons = new List<RetroMenuButton>();
        private readonly List<Row> rows = new List<Row>();

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform mainPage;
        private RectTransform settingsPage;
        private TextMeshProUGUI title;
        private TextMeshProUGUI status;
        private bool open;
        private bool inSettings;
        private int openedFrame;
        private float navTimer;
        private bool navHeld;
        private float statusTimer;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.ignoreListenerPause = true; // menu sounds still play while the game's audio is paused
            BuildUI();
            canvas.enabled = false;
        }

        private void OnEnable() => Loc.LanguageChanged += RefreshTexts;

        private void OnDisable() => Loc.LanguageChanged -= RefreshTexts;

        private void OnDestroy()
        {
            if (open)
            {
                GameTime.SetPaused(false);
                IsOpen = false;
                InputLock.Release(this);
            }
        }

        // ================================================================== Input

        private void Update()
        {
            if (!open)
            {
                if (PausePressed() && CanOpen()) Open();
                return;
            }

            if (Time.frameCount != openedFrame && (PausePressed() || GameInput.Cancel.WasPressedThisFrame()))
            {
                if (inSettings) ShowMain(true);
                else Close();
                return;
            }

            HandleValueRows();

            if (statusTimer > 0f)
            {
                statusTimer -= Time.unscaledDeltaTime;
                if (statusTimer <= 0f) status.text = string.Empty;
            }
        }

        private static bool PausePressed()
        {
            bool key = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
            bool pad = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            return key || pad;
        }

        private static bool CanOpen()
        {
            // Pause works everywhere (dialogue, cutscenes, hacking): everything freezes and resumes in place.
            if (SceneLoader.IsLoading) return false;
            return true;
        }

        private void HandleValueRows()
        {
            if (!inSettings || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Row row = rows.Find(r => r.button != null && r.button.gameObject == selected);
            if (row == null || row.change == null) return;

            float x = GameInput.MoveVector.x;
            if (Mathf.Abs(x) < 0.5f)
            {
                navHeld = false;
                return;
            }
            navTimer -= Time.unscaledDeltaTime;
            if (navHeld && navTimer > 0f) return;
            navTimer = navHeld ? 0.1f : 0.3f;
            navHeld = true;
            row.change(x > 0f ? 1 : -1);
            Play(moveClip, 0.6f);
            RefreshTexts();
        }

        // ================================================================== Open / close

        private void Open()
        {
            open = true;
            IsOpen = true;
            openedFrame = Time.frameCount;
            GameTime.SetPaused(true);
            InputLock.Acquire(this);
            canvas.enabled = true;
            group.alpha = 1f;
            Play(openClip);
            ShowMain(false);
        }

        private void Close()
        {
            open = false;
            IsOpen = false;
            LastClosedFrame = Time.frameCount;
            GameTime.SetPaused(false);
            InputLock.Release(this);
            canvas.enabled = false;
            GameSettings.Save();
            Play(closeClip);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ShowMain(bool fromSettings)
        {
            if (fromSettings)
            {
                GameSettings.Save();
                Play(closeClip);
            }
            inSettings = false;
            mainPage.gameObject.SetActive(true);
            settingsPage.gameObject.SetActive(false);
            title.text = Loc.T("PAUSED");
            Select(fromSettings ? mainButtons[1] : mainButtons[0]);
        }

        private void ShowSettings()
        {
            inSettings = true;
            mainPage.gameObject.SetActive(false);
            settingsPage.gameObject.SetActive(true);
            title.text = Loc.T("SETTINGS");
            RefreshTexts();
            Select(rows[0].button);
        }

        private static void Select(RetroMenuButton button)
        {
            if (EventSystem.current == null || button == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        // ================================================================== Actions

        private void SaveGame()
        {
            SaveData.SetCheckpoint(SceneManager.GetActiveScene().name);
            Play(confirmClip);
            ShowStatus(Loc.T("GAME SAVED."));
        }

        private void LoadGame()
        {
            if (!SaveData.HasSave)
            {
                Play(errorClip);
                ShowStatus(Loc.T("NO SAVE FOUND."));
                return;
            }
            Play(confirmClip);
            StoryFlags.LoadFromPrefs();
            string scene = SaveData.CheckpointScene;
            Close();
            SceneLoader.Load(scene, null, false);
        }

        private void GoToMainMenu()
        {
            Play(confirmClip);
            Close();
            SceneLoader.Load(SceneLoader.MenuScene, null, false);
        }

        private void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowStatus(string text)
        {
            status.text = text;
            statusTimer = 2.5f;
        }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        // ================================================================== Texts

        private static string Bar(float value)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value) * 10f);
            return new string('#', filled) + new string('-', 10 - filled);
        }

        private static string Value(string name, string value) => $"{Loc.T(name)}   < {value} >";

        private void RefreshTexts()
        {
            if (title != null) title.text = Loc.T(inSettings ? "SETTINGS" : "PAUSED");
            foreach (Row row in rows)
            {
                if (row.button != null && row.label != null) row.button.SetLabel(row.label());
            }
            foreach (RetroMenuButton b in mainButtons) b.SetLabel(b.name);
        }

        // ================================================================== UI construction

        private void BuildUI()
        {
            canvas = RuntimeUI.CreateCanvas("PauseCanvas", transform, 400);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0.02f, 0.01f, 0.05f, 0.82f));
            dim.raycastTarget = true;
            RuntimeUI.Stretch(dim.rectTransform);

            Image panel = RuntimeUI.Image("Panel", canvas.transform, new Color(0.035f, 0.05f, 0.065f, 0.97f));
            RuntimeUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 680));
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = RuntimeUI.Cyan;
            outline.effectDistance = new Vector2(3, -3);

            TextMeshProUGUI os = RuntimeUI.Text("OS", panel.rectTransform, "LEO-OS  //  PAUSE", 20, RuntimeUI.Dim, TextAlignmentOptions.Left);
            RuntimeUI.Place(os.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40, -24), new Vector2(600, 30));
            title = RuntimeUI.Text("Title", panel.rectTransform, "PAUSED", 54, RuntimeUI.Magenta, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40, -58), new Vector2(740, 70));

            mainPage = Page(panel.rectTransform, "Main");
            settingsPage = Page(panel.rectTransform, "Settings");

            mainButtons.Add(MakeButton(mainPage, "RESUME", () => Close()));
            mainButtons.Add(MakeButton(mainPage, "SETTINGS", () => { Play(confirmClip); ShowSettings(); }));
            mainButtons.Add(MakeButton(mainPage, "SAVE GAME", SaveGame));
            mainButtons.Add(MakeButton(mainPage, "LOAD LAST SAVE", LoadGame));
            mainButtons.Add(MakeButton(mainPage, "MAIN MENU", GoToMainMenu));
            mainButtons.Add(MakeButton(mainPage, "QUIT GAME", Quit));

            AddRow("MASTER", () => Value("MASTER", Bar(GameSettings.MasterVolume)), d => { GameSettings.MasterVolume += d * 0.1f; GameSettings.ApplyAudio(null); });
            AddRow("MUSIC", () => Value("MUSIC", Bar(GameSettings.MusicVolume)), d => { GameSettings.MusicVolume += d * 0.1f; });
            AddRow("SFX", () => Value("SFX", Bar(GameSettings.SfxVolume)), d => { GameSettings.SfxVolume += d * 0.1f; GameSettings.ApplyAudio(null); });
            AddRow("SCREEN SHAKE", () => Value("SCREEN SHAKE", Loc.T(GameSettings.ScreenShake ? "ON" : "OFF")), d => GameSettings.ScreenShake = !GameSettings.ScreenShake);
            AddRow("FULLSCREEN", () => Value("FULLSCREEN", Loc.T(GameSettings.Fullscreen ? "ON" : "OFF")), d => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.ApplyDisplay(); });
            AddRow("LANGUAGE", () => Value("LANGUAGE", Loc.NativeName(Loc.Current)), d => Loc.Cycle(d));
            AddRow("GRAPHICS", () => Value("GRAPHICS", Loc.T(GameSettings.QualityName(GameSettings.GraphicsQuality))), d =>
            {
                GameSettings.GraphicsQuality = (GameSettings.GraphicsQuality + d + 3) % 3;
                GameSettings.ApplyGraphics();
            });
            RetroMenuButton back = MakeButton(settingsPage, "BACK", () => ShowMain(true));
            rows.Add(new Row { button = back, label = () => "BACK" });

            status = RuntimeUI.Text("Status", panel.rectTransform, "", 24, RuntimeUI.Cyan, TextAlignmentOptions.Left);
            RuntimeUI.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40, 24), new Vector2(740, 34));

            settingsPage.gameObject.SetActive(false);
        }

        private static RectTransform Page(RectTransform parent, string name)
        {
            RectTransform page = RuntimeUI.Rect(name, parent);
            RuntimeUI.Place(page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(740, 460));
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return page;
        }

        private void AddRow(string key, Func<string> label, Action<int> change)
        {
            RetroMenuButton button = MakeButton(settingsPage, key, () =>
            {
                change(1);
                Play(moveClip, 0.6f);
                RefreshTexts();
            });
            rows.Add(new Row { button = button, label = label, change = change });
        }

        private RetroMenuButton MakeButton(RectTransform parent, string key, Action onActivated)
        {
            RectTransform rect = RuntimeUI.Rect(key, parent);
            rect.sizeDelta = new Vector2(740, 52);
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Button uiButton = rect.gameObject.AddComponent<Button>();
            uiButton.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 52;

            TextMeshProUGUI label = RuntimeUI.Text("Label", rect, string.Empty, 32, RuntimeUI.Cyan, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            RuntimeUI.Stretch(label.rectTransform, 8, 0, 0, 0);

            RetroMenuButton button = rect.gameObject.AddComponent<RetroMenuButton>();
            button.Configure(label, key);
            button.Highlighted += _ => Play(moveClip, 0.5f);
            button.Activated += _ =>
            {
                if (open) onActivated();
            };
            return button;
        }
    }
}
