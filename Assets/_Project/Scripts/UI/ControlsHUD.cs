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
    /// Always-there reminder of the controls (bottom left), in every gameplay scene.
    /// Steps aside during dialogue, cutscenes, fights and menus. H hides / shows it (remembered).
    /// Keys are physical positions, so the labels are the same on QWERTY and AZERTY (E, F, Tab, Esc, H).
    /// </summary>
    public class ControlsHUD : MonoBehaviour
    {
        private const string PrefKey = "hud.controls.hidden";

        private CanvasGroup group;
        private TextMeshProUGUI body;
        private TextMeshProUGUI collapsed;
        private bool hidden;
        private string lastLanguageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureInScene();
            EnsureInScene();
        }

        private static void EnsureInScene()
        {
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (FindFirstObjectByType<ControlsHUD>() != null) return;
            new GameObject("ControlsHUD").AddComponent<ControlsHUD>();
        }

        private void Awake()
        {
            hidden = PlayerPrefs.GetInt(PrefKey, 0) == 1;
            Canvas canvas = RuntimeUI.CreateCanvas("ControlsCanvas", transform, 40);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Image panel = RuntimeUI.Image("Panel", canvas.transform, new Color(0.03f, 0.03f, 0.06f, 0.72f));
            RuntimeUI.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f, 20f), new Vector2(430f, 210f));
            Image stripe = RuntimeUI.Image("Stripe", panel.transform, new Color(0.2f, 1f, 0.95f, 0.8f));
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(4f, 0f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;

            body = RuntimeUI.Text("Body", panel.transform, "", 20, new Color(0.88f, 0.9f, 0.95f), TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(body.rectTransform, 18, 12, 10, 10);
            body.textWrappingMode = TextWrappingModes.NoWrap;
            body.lineSpacing = 6f;

            collapsed = RuntimeUI.Text("Collapsed", canvas.transform, "", 18, new Color(0.6f, 0.65f, 0.7f, 0.8f), TextAlignmentOptions.BottomLeft);
            RuntimeUI.Place(collapsed.rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f, 16f), new Vector2(400f, 26f));

            panel.gameObject.SetActive(!hidden);
            collapsed.gameObject.SetActive(hidden);
            Refresh();
        }

        private static bool ShouldStepAside()
        {
            if (GameTime.Paused || PauseMenu.IsOpen || PhoneMenu.IsOpen || SceneLoader.IsLoading) return true;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return true;
            if (CinematicDirector.Instance != null && CinematicDirector.Instance.IsPlaying) return true;
            if (HackingManager.Instance != null && HackingManager.Instance.IsActive) return true;
            return false;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && !ShouldStepAside())
            {
                hidden = !hidden;
                PlayerPrefs.SetInt(PrefKey, hidden ? 1 : 0);
                PlayerPrefs.Save();
                body.transform.parent.gameObject.SetActive(!hidden);
                collapsed.gameObject.SetActive(hidden);
            }

            float target = ShouldStepAside() ? 0f : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);

            // Rebuild when the language changes or the handheld becomes available.
            string key = Loc.T("Move") + StoryFlags.Has(PhoneMenu.HasPhoneFlag);
            if (key != lastLanguageText) Refresh();
        }

        private void Refresh()
        {
            lastLanguageText = Loc.T("Move") + StoryFlags.Has(PhoneMenu.HasPhoneFlag);
            const string k = "<color=#33FFF2><b>";
            const string e = "</b></color>";
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#8A8F99>").Append(Loc.T("CONTROLS")).Append("</color>\n");
            sb.Append(k).Append("WASD / ←↑→↓").Append(e).Append("<pos=46%>").Append(Loc.T("Move")).Append('\n');
            sb.Append(k).Append("E / ").Append(Loc.T("Space")).Append(e).Append("<pos=46%>").Append(Loc.T("Look, talk, next line")).Append('\n');
            sb.Append(k).Append("X / Esc").Append(e).Append("<pos=46%>").Append(Loc.T("Back, pause menu")).Append('\n');
            if (StoryFlags.Has(PhoneMenu.HasPhoneFlag)) sb.Append(k).Append("Tab").Append(e).Append("<pos=46%>").Append(Loc.T("Handheld: messages, items")).Append('\n');
            sb.Append(k).Append("F").Append(e).Append("<pos=46%>").Append(Loc.T("Drink a potion (in fights)")).Append('\n');
            sb.Append("<color=#8A8F99>").Append(k).Append("H").Append(e).Append("<pos=46%>").Append(Loc.T("Hide this")).Append("</color>");
            body.text = sb.ToString();
            collapsed.text = "<color=#33FFF2><b>H</b></color>  " + Loc.T("Show controls");
        }
    }
}
