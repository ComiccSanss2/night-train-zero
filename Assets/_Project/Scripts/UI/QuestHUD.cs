using System.Collections.Generic;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Hacking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Top-right objective tracker (current main step, plus the nearest side quest) and the banner that slides in
    /// when a quest starts, moves on or ends. Quests come from QuestLog (story flags only). J opens the full list.
    /// </summary>
    public class QuestHUD : MonoBehaviour
    {
        private CanvasGroup trackerGroup;
        private TextMeshProUGUI tracker;
        private RectTransform banner;
        private CanvasGroup bannerGroup;
        private TextMeshProUGUI bannerTitle;
        private TextMeshProUGUI bannerText;
        private AudioSource audioSource;
        private AudioClip chime;

        private readonly Dictionary<string, int> known = new Dictionary<string, int>(); // -1 not started, n = steps done
        private readonly Queue<(string head, string text, Color color)> pending = new Queue<(string, string, Color)>();
        private float pollTimer;
        private float bannerTimer;
        private string lastLanguage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureInScene();
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (FindFirstObjectByType<QuestHUD>() != null) return;
            new GameObject("QuestHUD").AddComponent<QuestHUD>();
        }

        private void Awake()
        {
            Canvas canvas = RuntimeUI.CreateCanvas("QuestCanvas", transform, 41);

            // Tracker (top right).
            RectTransform t = RuntimeUI.Rect("Tracker", canvas.transform);
            RuntimeUI.Place(t, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(520f, 120f));
            trackerGroup = t.gameObject.AddComponent<CanvasGroup>();
            trackerGroup.blocksRaycasts = false;
            tracker = RuntimeUI.Text("Text", t, "", 20, new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.TopRight);
            RuntimeUI.Stretch(tracker.rectTransform);
            tracker.textWrappingMode = TextWrappingModes.Normal;
            tracker.outlineWidth = 0.22f;
            tracker.outlineColor = new Color32(5, 3, 12, 255);

            // Banner (top centre).
            Image b = RuntimeUI.Image("Banner", canvas.transform, new Color(0.035f, 0.03f, 0.07f, 0.9f));
            banner = b.rectTransform;
            RuntimeUI.Place(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(640f, 92f));
            Outline o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 0.85f, 0.3f, 0.8f);
            o.effectDistance = new Vector2(2f, -2f);
            bannerGroup = b.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0f;
            bannerTitle = RuntimeUI.Text("Head", banner, "", 17, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center);
            bannerTitle.characterSpacing = 10f;
            bannerTitle.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(600f, 24f));
            bannerText = RuntimeUI.Text("Text", banner, "", 26, Color.white, TextAlignmentOptions.Center);
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 16f;
            bannerText.fontSizeMax = 26f;
            RuntimeUI.Place(bannerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(600f, 48f));

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            chime = Resources.Load<AudioClip>("Audio/quest_chime");
        }

        private static bool Busy()
        {
            if (GameTime.Paused || PauseMenu.IsOpen || PhoneMenu.IsOpen || SceneLoader.IsLoading) return true;
            if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return true;
            if (HackingManager.Instance != null && HackingManager.Instance.IsActive) return true;
            return false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool busy = Busy();
            bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
            trackerGroup.alpha = Mathf.MoveTowards(trackerGroup.alpha, busy || talking ? 0f : 1f, dt * 3f);

            pollTimer -= dt;
            if (pollTimer <= 0f || lastLanguage != Loc.Current.ToString())
            {
                pollTimer = 0.4f;
                lastLanguage = Loc.Current.ToString();
                Poll();
                RefreshTracker();
            }

            // Banner: slide in, hold, fade (not over fights or cutscenes).
            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                float shown = Mathf.Clamp01(Mathf.Min(3.6f - bannerTimer, bannerTimer) * 3f);
                bannerGroup.alpha = shown;
                banner.anchoredPosition = new Vector2(0f, Mathf.Lerp(-130f, -170f, shown));
            }
            else
            {
                bannerGroup.alpha = 0f;
                if (pending.Count > 0 && !busy)
                {
                    var next = pending.Dequeue();
                    bannerTitle.text = next.head;
                    bannerTitle.color = next.color;
                    bannerText.text = next.text;
                    bannerTimer = 3.6f;
                    if (chime != null) audioSource.PlayOneShot(chime, 0.6f);
                }
            }
        }

        private void Poll()
        {
            bool first = known.Count == 0;
            foreach (QuestLog.Quest q in QuestLog.All)
            {
                int state = q.Started ? q.DoneCount : -1;
                if (!known.TryGetValue(q.id, out int before)) { known[q.id] = state; continue; }
                if (state == before) continue;
                known[q.id] = state;
                if (first) continue;
                Color gold = new Color(1f, 0.85f, 0.3f), cyan = new Color(0.2f, 1f, 0.95f), grey = new Color(0.7f, 0.75f, 0.8f);
                if (before < 0 && state >= 0)
                    pending.Enqueue((Loc.T(q.main ? "NEW OBJECTIVE" : "NEW QUEST"), Loc.T(q.title), q.main ? gold : cyan));
                else if (q.Done)
                    pending.Enqueue((Loc.T("QUEST COMPLETE"), Loc.T(q.title), grey));
                else if (state > before && q.Current != null)
                    pending.Enqueue((Loc.T(q.title).ToUpperInvariant(), Loc.T(q.Current.text), q.main ? gold : cyan));
            }
        }

        private void RefreshTracker()
        {
            var sb = new System.Text.StringBuilder();
            QuestLog.Quest main = QuestLog.CurrentMain();
            if (main != null && main.Current != null)
            {
                sb.Append("<size=70%><color=#FFD84F>").Append(Loc.T(main.title).ToUpperInvariant()).Append("</color></size>\n")
                  .Append(Loc.T(main.Current.text)).Append('\n');
            }
            int sides = 0;
            foreach (QuestLog.Quest q in QuestLog.All)
            {
                if (q.main || !q.Started || q.Done || q.Current == null) continue;
                sides++;
            }
            if (sides > 0)
                sb.Append("<size=70%><color=#33FFF2>").Append(Loc.F("+{0} SIDE QUESTS", sides)).Append("</color>  <size=65%><color=#8A8F99>[J]</color></size></size>");
            tracker.text = sb.ToString();
        }
    }
}
