using System.Collections;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Subnet.Gameplay
{
    /// <summary>
    /// The final choice and the ending cinematic:
    /// surrender to the void (delete the corrupted memory) or learn to live with your glitches (keep it).
    /// Shows a reflective epilogue, credits, then returns to the main menu with every memory unlocked.
    /// Call Begin() (e.g. from the final HackableObstacle's onResolved event).
    /// </summary>
    public class EndingController : MonoBehaviour
    {
        public const string VoidFlag = "ending_void";
        public const string GlitchFlag = "ending_glitch";
        public const string TrueFlag = "ending_true";

        /// <summary>
        /// The secret ending opens only if Leo let people in (Openness), let them stay (Closeness),
        /// kept Mia's drawing from his old room, and answered her call.
        /// </summary>
        public static bool TrueEndingUnlocked =>
            Trust.Openness >= 6 && Trust.Closeness >= 6 && Inventory.HasKey("mia_drawing") && StoryFlags.Has("answered_mia");

        [Header("Start")]
        [Tooltip("Begins by itself once this flag is set (e.g. boarding the last train). Empty = call Begin().")]
        public string beginOnFlag = string.Empty;

        [Header("Choice")]
        public DialogueSpeaker algorithmSpeaker;
        [TextArea(2, 4)] public string[] preChoiceLines = { };
        [TextArea(2, 4)] public string choicePrompt = "What do you do with the file?";
        public string deleteOption = "[DELETE] Let the void make it quiet.";
        public string keepOption = "[KEEP] Learn to live with the glitch.";

        [Header("Epilogues")]
        [TextArea(2, 5)] public string[] voidEpilogue = { };
        public string voidTitle = "ENDING  //  FRICTIONLESS";
        [TextArea(2, 5)] public string[] glitchEpilogue = { };
        public string glitchTitle = "ENDING  //  LIVING WITH GLITCHES";
        [TextArea(2, 5)] public string[] trueEpilogue = { };
        public string trueTitle = "TRUE ENDING  //  THE LAST TRAIN HOME";
        public string trueOption = "[SHOW IT MIA'S DRAWING]";
        [Tooltip("Extra page for the keep / true endings, depending on whether Kira opened her package.")]
        [TextArea(2, 5)] public string kiraPackagePage = string.Empty;
        [TextArea(2, 5)] public string kiraNoPackagePage = string.Empty;
        [TextArea(2, 6)] public string credits = "NIGHT TRAIN ZERO\n\nThank you for playing.";

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip choiceClip;
        public AudioClip pageClip;

        [Tooltip("Chapter keys unlocked after finishing the game.")]
        public string[] unlockOnFinish = { "ch2", "ch3", "ch4", "ch5", "ch6" };

        private CanvasGroup overlay;
        private TextMeshProUGUI pageText;
        private bool running;

        private void Update()
        {
            if (!running && !string.IsNullOrEmpty(beginOnFlag) && StoryFlags.Has(beginOnFlag)) Begin();
        }

        public void Begin()
        {
            if (running) return;
            running = true;
            StoryFlags.Set("game_finished");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            InputLock.Acquire(this);
            if (Subnet.Audio.MusicDirector.Instance != null) Subnet.Audio.MusicDirector.Instance.StopCity(4f);
            DialogueManager dialogue = DialogueManager.Instance;
            while (dialogue != null && dialogue.IsOpen) yield return null;

            // Pre-choice + choice.
            if (dialogue != null)
            {
                if (preChoiceLines.Length > 0)
                {
                    bool linesDone = false;
                    dialogue.PlayLines(preChoiceLines, algorithmSpeaker, () => linesDone = true);
                    while (!linesDone) yield return null;
                }

                var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
                sequence.hideFlags = HideFlags.DontSave;
                var node = new DialogueNode { text = choicePrompt, effect = TextEffect.Shake };
                if (TrueEndingUnlocked) node.choices.Add(new DialogueChoice { text = trueOption, nextNodeId = DialogueSequence.EndNodeId, flagToSet = TrueFlag });
                node.choices.Add(new DialogueChoice { text = deleteOption, nextNodeId = DialogueSequence.EndNodeId, flagToSet = VoidFlag });
                node.choices.Add(new DialogueChoice { text = keepOption, nextNodeId = DialogueSequence.EndNodeId, flagToSet = GlitchFlag });
                sequence.nodes.Add(node);

                bool done = false;
                dialogue.Play(sequence, () => done = true);
                while (!done) yield return null;
                Destroy(sequence);
            }

            bool keep = !StoryFlags.Has(VoidFlag);
            bool trueEnding = StoryFlags.Has(TrueFlag);
            if (audioSource != null && choiceClip != null) audioSource.PlayOneShot(choiceClip);

            BuildOverlay(trueEnding ? new Color(0.06f, 0.04f, 0.1f) : keep ? Color.black : new Color(0.92f, 0.92f, 0.95f));
            yield return FadeOverlay(1f, keep ? 2.5f : 3.5f);
            yield return Wait(1f);

            string[] pages = trueEnding ? trueEpilogue : keep ? glitchEpilogue : voidEpilogue;
            for (int i = 0; i < pages.Length; i++)
            {
                yield return ShowPage(pages[i], false);
                // Kira's page comes right before the last one.
                if (keep && i == pages.Length - 2)
                {
                    string kira = StoryFlags.Has("kira_package_opened") ? kiraPackagePage : kiraNoPackagePage;
                    if (!string.IsNullOrEmpty(kira)) yield return ShowPage(kira, false);
                }
            }
            yield return ShowPage(trueEnding ? trueTitle : keep ? glitchTitle : voidTitle, true);
            yield return ShowPage(credits, true);

            if (trueEnding) { PlayerPrefs.SetInt("save.trueEndingSeen", 1); PlayerPrefs.Save(); }
            SaveData.ClearCheckpoint();
            foreach (string key in unlockOnFinish) SaveData.UnlockChapter(key);
            StoryFlags.ResetAll();
            InputLock.ReleaseAll();
            SceneManager.LoadScene(SceneLoader.MenuScene);
        }

        private void BuildOverlay(Color background)
        {
            Canvas canvas = RuntimeUI.CreateCanvas("EndingCanvas", transform, 300);
            overlay = canvas.gameObject.AddComponent<CanvasGroup>();
            overlay.alpha = 0f;
            Image image = RuntimeUI.Image("Background", canvas.transform, background);
            RuntimeUI.Stretch(image.rectTransform);

            bool light = background.grayscale > 0.5f;
            pageText = RuntimeUI.Text("Page", canvas.transform, "", 38, light ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.9f, 0.92f, 0.95f), TextAlignmentOptions.Center);
            pageText.lineSpacing = 18;
            RuntimeUI.Stretch(pageText.rectTransform, 300, 300, 200, 200);
            pageText.alpha = 0f;
        }

        private IEnumerator ShowPage(string text, bool emphasized)
        {
            TextFX.Show(pageText, text);
            pageText.fontStyle = emphasized ? FontStyles.Bold : FontStyles.Normal;
            if (audioSource != null && pageClip != null) audioSource.PlayOneShot(pageClip, 0.6f);

            for (float t = 0f; t < 1.2f; t += GameTime.UnscaledDelta)
            {
                pageText.alpha = t / 1.2f;
                yield return null;
            }
            pageText.alpha = 1f;

            // Read at your own pace: advance with confirm, or wait.
            float hold = Mathf.Clamp(text.Length * 0.06f, 3.5f, 9f);
            yield return Wait(0.5f);
            for (float t = 0f; t < hold; t += GameTime.UnscaledDelta)
            {
                if (GameInput.SubmitPressed) break;
                yield return null;
            }

            for (float t = 0f; t < 0.8f; t += GameTime.UnscaledDelta)
            {
                pageText.alpha = 1f - t / 0.8f;
                yield return null;
            }
            pageText.alpha = 0f;
            yield return Wait(0.3f);
        }

        private IEnumerator FadeOverlay(float target, float duration)
        {
            float from = overlay.alpha;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                overlay.alpha = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }
            overlay.alpha = target;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += GameTime.UnscaledDelta) yield return null;
        }
    }
}
