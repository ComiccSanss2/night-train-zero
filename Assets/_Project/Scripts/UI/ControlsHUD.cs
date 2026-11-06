using System;
using System.Collections.Generic;
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
    /// Real keycaps (a WASD cluster and an arrow cluster drawn as little keys), a label per action,
    /// and the matching keycap lights up whenever you press it. The panel sizes itself to the longest label
    /// in the current language, slides in, and steps aside during dialogue, cutscenes, fights and menus.
    /// H hides / shows it (remembered). Keys are physical positions, so the labels are the same on QWERTY and AZERTY.
    /// </summary>
    public class ControlsHUD : MonoBehaviour
    {
        private const string PrefKey = "hud.controls.hidden";
        private static readonly Color CapFill = new Color(0.10f, 0.11f, 0.17f, 0.95f);
        private static readonly Color CapEdge = new Color(0.2f, 1f, 0.95f, 0.55f);
        private static readonly Color CapText = new Color(0.75f, 1f, 0.98f);
        private static readonly Color LabelColor = new Color(0.88f, 0.9f, 0.95f);
        private static readonly Color PanelColor = new Color(0.035f, 0.03f, 0.07f, 0.82f);

        private const float CapH = 30f;
        private const float MiniCap = 20f;
        private const float RowGap = 8f;
        private const float Pad = 18f;

        private class Cap
        {
            public Image fill;
            public Image edge;
            public Graphic content;
            public Func<bool> held;
            public float flash;
        }

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private RectTransform collapsed;
        private readonly List<Cap> caps = new List<Cap>();
        private bool hidden;
        private string lastKey;
        private float slide;

        private static Sprite capSprite, capEdgeSprite, triangleSprite;

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
            canvas = RuntimeUI.CreateCanvas("ControlsCanvas", transform, 40);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            Refresh();
        }

        // ------------------------------------------------------------------ sprites (procedural, 9-sliced)

        private static Sprite RoundedSprite(bool outline)
        {
            const int size = 32;
            const float radius = 9f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius; // <0 inside
                    float a = Mathf.Clamp01(0.5f - d);
                    if (outline) a *= Mathf.Clamp01(d + 2.5f); // 2px ring
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }

        private static Sprite Triangle()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Upward triangle: apex at top centre.
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size;
                    float half = (1f - v) * 0.5f;
                    float d = Mathf.Min(half - Mathf.Abs(u), v - 0.08f, 0.92f - v) * size;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ building

        private static Func<bool> Held(params Key[] keys)
        {
            return () =>
            {
                Keyboard kb = Keyboard.current;
                if (kb == null) return false;
                foreach (Key k in keys) if (kb[k].isPressed) return true;
                return false;
            };
        }

        private Cap MakeCap(RectTransform parent, Vector2 pos, Vector2 size, Func<bool> held) =>
            MakeCap(parent, new Vector2(0f, 1f), pos, size, held);

        private Cap MakeCap(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, Func<bool> held)
        {
            float ppu = size.y < 25f ? 1.6f : 1f;
            // A darker base under the cap so it reads as a key with some depth.
            Image shadow = RuntimeUI.Image("Base", parent, new Color(0f, 0f, 0f, 0.5f));
            shadow.sprite = capSprite;
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = ppu;
            RuntimeUI.Place(shadow.rectTransform, anchor, anchor, pos + new Vector2(0f, -3f), size);

            Image fill = RuntimeUI.Image("Cap", parent, CapFill);
            fill.sprite = capSprite;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = ppu;
            RuntimeUI.Place(fill.rectTransform, anchor, anchor, pos, size);

            Image edge = RuntimeUI.Image("Edge", fill.transform, CapEdge);
            edge.sprite = capEdgeSprite;
            edge.type = Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = ppu;
            RuntimeUI.Stretch(edge.rectTransform);
            var cap = new Cap { fill = fill, edge = edge, held = held };
            caps.Add(cap);
            return cap;
        }

        private float TextCap(RectTransform parent, float x, float y, string label, Func<bool> held)
        {
            TextMeshProUGUI t = RuntimeUI.Text("CapLabel", parent, label, 17, CapText, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Max(CapH, t.GetPreferredValues(label).x + 18f);
            Cap cap = MakeCap(parent, new Vector2(x, y), new Vector2(w, CapH), held);
            t.transform.SetParent(cap.fill.transform, false);
            RuntimeUI.Stretch(t.rectTransform, 0, 0, 0, 2);
            cap.content = t;
            return w;
        }

        /// <summary>Inverted-T cluster of four mini keys (W/A/S/D or the arrows). Returns its width.</summary>
        private float Cluster(RectTransform parent, float x, float y, bool arrows)
        {
            string[] letters = { "W", "A", "S", "D" };
            Key[] keys = arrows ? new[] { Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow } : new[] { Key.W, Key.A, Key.S, Key.D };
            float[] angles = { 0f, 90f, 180f, -90f };
            const float g = 3f;
            Vector2[] slots =
            {
                new Vector2(x + MiniCap + g, y),
                new Vector2(x, y - MiniCap - g),
                new Vector2(x + MiniCap + g, y - MiniCap - g),
                new Vector2(x + 2f * (MiniCap + g), y - MiniCap - g),
            };
            for (int i = 0; i < 4; i++)
            {
                Cap cap = MakeCap(parent, slots[i], new Vector2(MiniCap, MiniCap), Held(keys[i]));
                if (arrows)
                {
                    Image tri = RuntimeUI.Image("Arrow", cap.fill.transform, CapText);
                    tri.sprite = triangleSprite;
                    RuntimeUI.Place(tri.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(9f, 9f));
                    tri.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                    cap.content = tri;
                }
                else
                {
                    TextMeshProUGUI t = RuntimeUI.Text("Letter", cap.fill.transform, letters[i], 13, CapText, TextAlignmentOptions.Center);
                    t.fontStyle = FontStyles.Bold;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    RuntimeUI.Stretch(t.rectTransform, 0, 0, 0, 1);
                    cap.content = t;
                }
            }
            return 3f * MiniCap + 2f * g;
        }

        private TextMeshProUGUI Slash(RectTransform parent, float x, float y, float h)
        {
            TextMeshProUGUI s = RuntimeUI.Text("Or", parent, "/", 16, new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Center);
            RuntimeUI.Place(s.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(14f, h));
            return s;
        }

        private void Refresh()
        {
            lastKey = Loc.T("Move") + PhoneMenu.HasPhone;
            if (capSprite == null)
            {
                capSprite = RoundedSprite(false);
                capEdgeSprite = RoundedSprite(true);
                triangleSprite = Triangle();
            }
            caps.Clear();
            if (panel != null) Destroy(panel.gameObject);
            if (collapsed != null) Destroy(collapsed.gameObject);

            // ---------------- expanded panel
            Image bg = RuntimeUI.Image("Panel", canvas.transform, PanelColor);
            bg.sprite = capSprite;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 0.8f;
            panel = bg.rectTransform;
            Image glowEdge = RuntimeUI.Image("Edge", panel, new Color(0.2f, 1f, 0.95f, 0.18f));
            glowEdge.sprite = capEdgeSprite;
            glowEdge.type = Image.Type.Sliced;
            glowEdge.pixelsPerUnitMultiplier = 0.8f;
            RuntimeUI.Stretch(glowEdge.rectTransform);
            Image accent = RuntimeUI.Image("Accent", panel, new Color(0.2f, 1f, 0.95f, 0.9f));
            RuntimeUI.Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad, -14f), new Vector2(22f, 3f));

            // Header: "CONTROLS" on the left, [H] Hide on the right.
            TextMeshProUGUI title = RuntimeUI.Text("Title", panel, Loc.T("CONTROLS"), 15, new Color(0.6f, 0.95f, 0.95f, 0.85f), TextAlignmentOptions.Left);
            title.characterSpacing = 12f;
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            RuntimeUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad + 30f, -4f), new Vector2(240f, 24f));

            // Rows: (draw the keys) → label.
            var rows = new List<(Func<RectTransform, float, float, float> keys, string label, float height)>();
            rows.Add(((p, x, y) =>
            {
                float w = Cluster(p, x, y, false);
                Slash(p, x + w + 2f, y, 2f * MiniCap + 3f);
                return w + 18f + Cluster(p, x + w + 18f, y, true);
            }, Loc.T("Move"), 2f * MiniCap + 3f));
            rows.Add(((p, x, y) =>
            {
                float w = TextCap(p, x, y, "E", Held(Key.E));
                Slash(p, x + w + 2f, y, CapH);
                return w + 18f + TextCap(p, x + w + 18f, y, Loc.T("Space"), Held(Key.Space));
            }, Loc.T("Look, talk, next line"), CapH));
            rows.Add(((p, x, y) =>
            {
                float w = TextCap(p, x, y, "X", Held(Key.X));
                Slash(p, x + w + 2f, y, CapH);
                return w + 18f + TextCap(p, x + w + 18f, y, "Esc", Held(Key.Escape));
            }, Loc.T("Back, pause menu"), CapH));
            if (PhoneMenu.HasPhone)
            {
                rows.Add(((p, x, y) => TextCap(p, x, y, "Tab", Held(Key.Tab)), Loc.T("Handheld: messages, items"), CapH));
                rows.Add(((p, x, y) => TextCap(p, x, y, "J", Held(Key.J)), Loc.T("Quests"), CapH));
            }
            rows.Add(((p, x, y) => TextCap(p, x, y, "F", Held(Key.F)), Loc.T("Drink a potion (in fights)"), CapH));

            float yCursor = -38f;
            float keysWidth = 0f;
            var labels = new List<(TextMeshProUGUI text, float y, float h)>();
            foreach (var row in rows)
            {
                keysWidth = Mathf.Max(keysWidth, row.keys(panel, Pad, yCursor));
                TextMeshProUGUI label = RuntimeUI.Text("Label", panel, row.label, 19, LabelColor, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                labels.Add((label, yCursor, row.height));
                yCursor -= row.height + RowGap;
            }

            float labelX = Pad + keysWidth + 18f;
            float labelsWidth = 0f;
            foreach (var l in labels)
            {
                float w = l.text.GetPreferredValues(l.text.text).x;
                labelsWidth = Mathf.Max(labelsWidth, w);
                RuntimeUI.Place(l.text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(labelX, l.y - 3f), new Vector2(w + 4f, l.h));
            }

            float width = labelX + labelsWidth + Pad;
            float height = -yCursor - RowGap + 16f;

            // [H] Hide, top right of the panel.
            TextMeshProUGUI hide = RuntimeUI.Text("Hide", panel, Loc.T("Hide this"), 14, new Color(0.6f, 0.62f, 0.68f), TextAlignmentOptions.Right);
            hide.textWrappingMode = TextWrappingModes.NoWrap;
            float hideW = hide.GetPreferredValues(hide.text).x;
            RuntimeUI.Place(hide.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad, -6f), new Vector2(hideW + 2f, 20f));
            Cap hCap = MakeCap(panel, new Vector2(1f, 1f), new Vector2(-Pad - hideW - 8f, -6f), new Vector2(20f, 20f), Held(Key.H));
            TextMeshProUGUI hLetter = RuntimeUI.Text("H", hCap.fill.transform, "H", 12, CapText, TextAlignmentOptions.Center);
            hLetter.fontStyle = FontStyles.Bold;
            RuntimeUI.Stretch(hLetter.rectTransform, 0, 0, 0, 1);
            hCap.content = hLetter;

            width = Mathf.Max(width, Pad + 30f + title.GetPreferredValues(title.text).x + 40f + hideW + 30f);
            RuntimeUI.Place(panel, Vector2.zero, Vector2.zero, new Vector2(20f, 20f), new Vector2(width, height));

            // ---------------- collapsed pill
            Image pill = RuntimeUI.Image("Collapsed", canvas.transform, PanelColor);
            pill.sprite = capSprite;
            pill.type = Image.Type.Sliced;
            collapsed = pill.rectTransform;
            Cap pc = MakeCap(collapsed, new Vector2(8f, -6f), new Vector2(22f, 22f), Held(Key.H));
            TextMeshProUGUI pl = RuntimeUI.Text("H", pc.fill.transform, "H", 13, CapText, TextAlignmentOptions.Center);
            pl.fontStyle = FontStyles.Bold;
            RuntimeUI.Stretch(pl.rectTransform, 0, 0, 0, 1);
            pc.content = pl;
            TextMeshProUGUI show = RuntimeUI.Text("Show", collapsed, Loc.T("Show controls"), 15, new Color(0.7f, 0.74f, 0.8f), TextAlignmentOptions.Left);
            show.textWrappingMode = TextWrappingModes.NoWrap;
            float sw = show.GetPreferredValues(show.text).x;
            RuntimeUI.Place(show.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -4f), new Vector2(sw + 4f, 26f));
            RuntimeUI.Place(collapsed, Vector2.zero, Vector2.zero, new Vector2(20f, 20f), new Vector2(sw + 50f, 34f));

            panel.gameObject.SetActive(!hidden);
            collapsed.gameObject.SetActive(hidden);
            slide = 0f;
        }

        // ------------------------------------------------------------------ runtime

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
            bool aside = ShouldStepAside();
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && !aside)
            {
                hidden = !hidden;
                PlayerPrefs.SetInt(PrefKey, hidden ? 1 : 0);
                PlayerPrefs.Save();
                panel.gameObject.SetActive(!hidden);
                collapsed.gameObject.SetActive(hidden);
                slide = 0f;
            }

            float target = aside ? 0f : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 4f);

            // Slide in from the left (ease-out) whenever the panel (re)appears.
            slide = Mathf.MoveTowards(slide, aside ? 0f : 1f, Time.unscaledDeltaTime * 3f);
            float ease = 1f - Mathf.Pow(1f - slide, 3f);
            float x = Mathf.Lerp(-24f, 20f, ease);
            if (panel != null) panel.anchoredPosition = new Vector2(x, 20f);
            if (collapsed != null) collapsed.anchoredPosition = new Vector2(x, 20f);

            // Keycaps light up while held, then fade back.
            float dt = Time.unscaledDeltaTime;
            foreach (Cap cap in caps)
            {
                if (cap.fill == null) continue;
                bool down = !aside && cap.held();
                cap.flash = down ? 1f : Mathf.MoveTowards(cap.flash, 0f, dt * 3.5f);
                cap.fill.color = Color.Lerp(CapFill, new Color(0.2f, 1f, 0.95f, 0.95f), cap.flash * 0.85f);
                cap.edge.color = Color.Lerp(CapEdge, Color.white, cap.flash);
                if (cap.content != null) cap.content.color = Color.Lerp(CapText, new Color(0.03f, 0.05f, 0.1f), cap.flash);
                cap.fill.rectTransform.localScale = Vector3.one * (down ? 0.92f : 1f); // pressed in
            }

            // Rebuild when the language changes or the handheld becomes available.
            string key = Loc.T("Move") + PhoneMenu.HasPhone;
            if (key != lastKey) Refresh();
        }
    }
}
