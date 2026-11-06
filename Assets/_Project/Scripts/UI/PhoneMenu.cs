using System.Collections.Generic;
using System.Text;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Hacking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Leo's handheld (Tab / gamepad Select): every message received, plus Mia's two old voicemails.
    /// Listening to the voicemails for the first time raises the hidden Openness value (author's bible, phase 2).
    /// Keyboard/gamepad driven: Left/Right switch tab, Up/Down pick, Confirm plays, Tab/Esc closes.
    /// Built at runtime; added to every gameplay scene by the builder (and self-created if missing).
    /// </summary>
    public class PhoneMenu : MonoBehaviour
    {
        public const string HeardMiaFlag = "heard_mia";
        public const string HasPhoneFlag = "handheld_checked";

        /// <summary>Leo has his phone: from the handheld onward, and all through the prologue (three years earlier).</summary>
        public static bool HasPhone => StoryFlags.Has(HasPhoneFlag) || StoryFlags.Has("in_prologue");

        /// <summary>Mia's voicemails: (date label, lines). Lines are localization keys.</summary>
        public static readonly string[][] Voicemails =
        {
            new[]
            {
                "VOICEMAIL  //  MIA  //  MARCH 3",
                "Leo? It's me. Mom says you got the job. {p}That's... {p}that's huge.",
                "Call me back, okay? {p}I want to hear you say it. {wave}With your stupid proud voice.{/wave}"
            },
            new[]
            {
                "VOICEMAIL  //  MIA  //  MAY 28",
                "Hey. It's me again. {p}The recital's on the twelfth.",
                "You don't have to come. {p=0.8}{slow}I'm just saying it's on the twelfth.{/slow}",
                "{small}...okay. bye.{/small}"
            }
        };

        public static bool IsOpen { get; private set; }

        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip confirmClip;

        private Canvas canvas;
        private TextMeshProUGUI header;
        private TextMeshProUGUI tabs;
        private TextMeshProUGUI body;
        private TextMeshProUGUI hint;
        private AudioSource audioSource;
        private int tab;      // 0 = messages, 1 = voicemails, 2 = items, 3 = quests
        private int cursor;
        private int openedFrame;
        private float navTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureInScene();
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            // Only gameplay scenes have a player; the main menu doesn't need a handheld.
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (FindFirstObjectByType<PhoneMenu>() != null) return;
            new GameObject("PhoneMenu").AddComponent<PhoneMenu>();
        }

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
            BuildUI();
            canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (IsOpen) { IsOpen = false; InputLock.Release(this); }
        }

        private static bool TogglePressed()
        {
            bool key = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            return key || pad;
        }

        private static bool CanOpen()
        {
            if (!HasPhone) return false;
            if (GameTime.Paused || PauseMenu.IsOpen || SceneLoader.IsLoading) return false;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;
            if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return false;
            if (HackingManager.Instance != null && HackingManager.Instance.IsActive) return false;
            return true;
        }

        private void Update()
        {
            if (!IsOpen)
            {
                if (TogglePressed() && CanOpen()) Open();
                else if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame && CanOpen()) { Open(); tab = 3; cursor = 0; Refresh(); }
                return;
            }
            if (GameTime.Paused) return;

            bool jClose = Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame;
            if (Time.frameCount != openedFrame && (TogglePressed() || jClose || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || GameInput.CancelPressed))
            {
                Close();
                return;
            }

            Vector2 move = GameInput.MoveVector;
            navTimer -= Time.unscaledDeltaTime;
            if (move.magnitude < 0.5f) navTimer = 0f;
            else if (navTimer <= 0f)
            {
                navTimer = 0.22f;
                if (Mathf.Abs(move.x) > Mathf.Abs(move.y)) { tab = Mathf.Clamp(tab + (move.x > 0 ? 1 : -1), 0, 3); cursor = 0; }
                else cursor += move.y > 0 ? -1 : 1;
                Play(moveClip);
                Refresh();
            }

            if (tab == 1 && Time.frameCount != openedFrame && GameInput.SubmitPressed) PlayVoicemail(Mathf.Clamp(cursor, 0, Voicemails.Length - 1));
            if (tab == 2 && Time.frameCount != openedFrame && GameInput.SubmitPressed) UseItem();
        }

        private void Open()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            InputLock.Acquire(this);
            canvas.enabled = true;
            tab = 0;
            cursor = Mathf.Max(0, PhoneLog.Entries.Count - 1);
            Play(openClip);
            Refresh();
        }

        private void Close()
        {
            IsOpen = false;
            InputLock.Release(this);
            canvas.enabled = false;
            Play(openClip);
        }

        private void PlayVoicemail(int index)
        {
            Play(confirmClip);
            Close();
            if (DialogueManager.Instance == null) return;
            var lines = new List<string>();
            lines.Add("* " + Voicemails[index][0]);
            for (int i = 1; i < Voicemails[index].Length; i++) lines.Add(Voicemails[index][i]);
            bool firstTime = !StoryFlags.Has(HeardMiaFlag + "_" + index);
            DialogueManager.Instance.PlayLines(lines, MiaSpeaker(), () =>
            {
                if (firstTime)
                {
                    StoryFlags.Set(HeardMiaFlag + "_" + index);
                    Trust.AddOpenness();
                    if (StoryFlags.Has(HeardMiaFlag + "_0") && StoryFlags.Has(HeardMiaFlag + "_1")) StoryFlags.Set(HeardMiaFlag);
                    Gameplay.SilentBeat.Hold(4f);
                }
            });
        }

        private static DialogueSpeaker mia;

        /// <summary>Mia speaks through a phone speaker: her own name plate, a soft, slightly slow voice.</summary>
        public static DialogueSpeaker MiaSpeaker()
        {
            if (mia != null) return mia;
            mia = Resources.Load<DialogueSpeaker>("Speaker_Mia");
            if (mia == null)
            {
                mia = ScriptableObject.CreateInstance<DialogueSpeaker>();
                mia.displayName = "MIA";
                mia.nameColor = new Color(1f, 0.75f, 0.85f);
                mia.textColor = new Color(1f, 0.92f, 0.95f);
                mia.style = SpeakerStyle.Default;
                mia.typingSpeedMultiplier = 0.85f;
            }
            return mia;
        }

        private void UseItem()
        {
            List<Inventory.ItemDef> usable = Inventory.Consumables();
            if (cursor < 0 || cursor >= usable.Count) return;
            if (Inventory.HP >= Inventory.MaxHP) { itemNote = Loc.T("YOU'RE ALREADY AT FULL HP."); Refresh(); return; }
            Inventory.Use(usable[cursor].id);
            Play(confirmClip);
            itemNote = Loc.F("YOU DRINK THE {0}. HP {1}/{2}.", Loc.T(usable[cursor].name).ToUpperInvariant(), Inventory.HP, Inventory.MaxHP);
            Refresh();
        }

        private string itemNote = string.Empty;

        private static string HpBar(int hp, int max, int width)
        {
            int filled = Mathf.RoundToInt(hp / (float)max * width);
            string color = hp <= max / 4 ? "#FF4F6E" : "#FFD84F";
            return $"<color={color}>" + new string('█', filled) + "</color><color=#3A2F45>" + new string('█', width - filled) + "</color>";
        }

        private void Refresh()
        {
            header.text = Loc.T("HANDHELD") + "   <color=#8A8F99>" + System.DateTime.Now.ToString("HH:mm") + "</color>";
            string[] names = { Loc.T("MESSAGES"), Loc.T("VOICEMAILS"), Loc.T("ITEMS"), Loc.T("QUESTS") };
            var tabLine = new StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                tabLine.Append(i == tab ? $"<color=#33FFF2>[ {names[i]} ]</color>" : names[i]).Append("   ");
            }
            tabs.text = tabLine.ToString();
            body.fontSize = tab >= 2 ? 21 : 26;
            var sb = new StringBuilder();
            if (tab == 0)
            {
                IReadOnlyList<PhoneLog.Entry> log = PhoneLog.Entries;
                if (log.Count == 0) sb.Append("<color=#8A8F99>").Append(Loc.T("NO MESSAGES.")).Append("</color>");
                cursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, log.Count - 1));
                int start = Mathf.Max(0, Mathf.Min(cursor - 3, log.Count - 7));
                for (int i = start; i < Mathf.Min(log.Count, start + 7); i++)
                {
                    bool sel = i == cursor;
                    sb.Append(sel ? "<color=#FF4FD8>> </color>" : "  ")
                      .Append("<color=#B9A7FF>").Append(Loc.T(log[i].sender).ToLowerInvariant()).Append(":</color> ")
                      .Append(TextFX.Strip(Loc.T(log[i].text))).Append("\n");
                }
                hint.text = Loc.T("UP/DOWN: SCROLL   LEFT/RIGHT: TAB   TAB: CLOSE");
            }
            else if (tab == 3)
            {
                List<QuestLog.Quest> quests = QuestLog.Visible();
                if (quests.Count == 0) sb.Append("<color=#8A8F99>").Append(Loc.T("NOTHING TO DO. ENJOY IT WHILE IT LASTS.")).Append("</color>");
                cursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, quests.Count - 1));
                int start = Mathf.Max(0, Mathf.Min(cursor - 2, quests.Count - 6));
                for (int i = start; i < Mathf.Min(quests.Count, start + 6); i++)
                {
                    QuestLog.Quest q = quests[i];
                    bool sel = i == cursor;
                    string color = q.Done ? "#5E6470" : (q.main ? "#FFD84F" : "#33FFF2");
                    sb.Append(sel ? "<color=#FF4FD8>> </color>" : "  ")
                      .Append($"<color={color}>").Append(q.main ? "» " : "◊ ").Append(Loc.T(q.title)).Append("</color>")
                      .Append(q.Done ? "  <color=#5E6470>" + Loc.T("DONE") + "</color>" : $"  <color=#8A8F99>{q.DoneCount}/{q.steps.Length}</color>").Append("\n");
                    if (sel)
                    {
                        sb.Append("    <size=78%><color=#8A8F99>").Append(Loc.T(q.where)).Append("</color></size>\n");
                        foreach (QuestLog.Step step in q.steps)
                        {
                            bool done = StoryFlags.Has(step.doneFlag);
                            bool current = step == q.Current;
                            if (!done && !current) break; // future steps stay hidden
                            sb.Append("    <size=85%>").Append(done ? "<color=#5E6470>- <s>" : "<color=#E8ECF2>> ")
                              .Append(Loc.T(step.text)).Append(done ? "</s></color>" : "</color>").Append("</size>\n");
                        }
                    }
                }
                hint.text = Loc.T("UP/DOWN: PICK   LEFT/RIGHT: TAB   J / TAB: CLOSE");
            }
            else if (tab == 2)
            {
                sb.Append("<color=#FFD84F>◊ ").Append(Inventory.Tokens).Append(' ').Append(Loc.T("TOKENS")).Append("</color>      ")
                  .Append("HP ").Append(HpBar(Inventory.HP, Inventory.MaxHP, 20)).Append("  ").Append(Inventory.HP).Append('/').Append(Inventory.MaxHP).Append("\n\n");
                List<Inventory.ItemDef> usable = Inventory.Consumables();
                cursor = Mathf.Clamp(cursor, 0, Mathf.Max(0, usable.Count - 1));
                if (usable.Count == 0) sb.Append("<color=#8A8F99>").Append(Loc.T("NO ITEMS. TOKENS BUY POTIONS AT THE NEON MART.")).Append("</color>\n");
                for (int i = 0; i < usable.Count; i++)
                {
                    sb.Append(i == cursor ? "<color=#FF4FD8>> </color>" : "  ")
                      .Append(Loc.T(usable[i].name)).Append("  <color=#8A8F99>x").Append(Inventory.Count(usable[i].id)).Append("</color>");
                    if (i == cursor) sb.Append("\n    <size=80%><color=#B9A7FF>").Append(Loc.T(usable[i].description)).Append("</color></size>");
                    sb.Append("\n");
                }
                List<Inventory.ItemDef> keys = Inventory.KeyItems();
                if (keys.Count > 0)
                {
                    sb.Append("\n<color=#33FFF2>").Append(Loc.T("KEY ITEMS")).Append("</color>\n");
                    foreach (Inventory.ItemDef key in keys)
                    {
                        sb.Append("  » ").Append(Loc.T(key.name)).Append("\n    <size=75%><color=#8A8F99>").Append(Loc.T(key.description)).Append("</color></size>\n");
                    }
                }
                if (!string.IsNullOrEmpty(itemNote)) sb.Append("\n<color=#FFD84F>").Append(itemNote).Append("</color>");
                itemNote = string.Empty;
                hint.text = Loc.T("CONFIRM: USE   LEFT/RIGHT: TAB   TAB: CLOSE");
            }
            else
            {
                cursor = Mathf.Clamp(cursor, 0, Voicemails.Length - 1);
                for (int i = 0; i < Voicemails.Length; i++)
                {
                    bool heard = StoryFlags.Has(HeardMiaFlag + "_" + i);
                    sb.Append(i == cursor ? "<color=#FF4FD8>> </color>" : "  ")
                      .Append(Loc.T(Voicemails[i][0]))
                      .Append(heard ? "" : "  <color=#33FFF2>●</color>").Append("\n\n");
                }
                hint.text = Loc.T("CONFIRM: LISTEN   LEFT/RIGHT: TAB   TAB: CLOSE");
            }
            body.text = sb.ToString();
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) audioSource.PlayOneShot(clip, 0.7f);
        }

        private void BuildUI()
        {
            canvas = RuntimeUI.CreateCanvas("PhoneMenuCanvas", transform, 85);
            Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            RuntimeUI.Stretch(dim.rectTransform);

            // The handheld itself: a dark slab with a cyan screen.
            Image shell = RuntimeUI.Image("Shell", canvas.transform, new Color(0.08f, 0.08f, 0.1f, 0.98f));
            RuntimeUI.Place(shell.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 640f));
            Image screen = RuntimeUI.Image("Screen", shell.transform, new Color(0.02f, 0.06f, 0.07f, 1f));
            RuntimeUI.Stretch(screen.rectTransform, 34, 34, 34, 34);

            header = RuntimeUI.Text("Header", screen.transform, "", 30, RuntimeUI.Cyan, TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(header.rectTransform, 24, 24, 18, 520);
            tabs = RuntimeUI.Text("Tabs", screen.transform, "", 26, RuntimeUI.Dim, TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(tabs.rectTransform, 24, 24, 64, 470);
            body = RuntimeUI.Text("Body", screen.transform, "", 26, new Color(0.85f, 0.95f, 0.95f), TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(body.rectTransform, 24, 24, 118, 60);
            body.textWrappingMode = TextWrappingModes.Normal;
            hint = RuntimeUI.Text("Hint", screen.transform, "", 18, RuntimeUI.Dim, TextAlignmentOptions.BottomLeft);
            RuntimeUI.Stretch(hint.rectTransform, 24, 24, 520, 14);
        }
    }
}
