using System.Collections;
using System.Collections.Generic;
using System.Text;
using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// Signs you can actually read. Inspecting one pushes the camera up to it, then the whole sign opens up
    /// full-size on screen (above the dialogue box) while Leo comments on it:
    ///   Departures — Central Station's split-flap board; every row flips in, and NIGHT TRAIN ZERO flickers into
    ///                the last row like the dialogue says.
    ///   ParkMap    — the Concrete Park map, drawn to match the real layout: the three statues, their lamps, the
    ///                sealed corridor, the benches, YOU ARE HERE, and the note someone scratched into it.
    /// </summary>
    [RequireComponent(typeof(InspectableObject))]
    public class SignBoard : MonoBehaviour
    {
        public enum Layout { Departures, ParkMap }

        [SerializeField] private Layout layout = Layout.Departures;
        [SerializeField] private Transform cameraPoint;
        [SerializeField] private Transform cameraLook;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform panel;
        private Coroutine routine;
        private readonly List<(TextMeshProUGUI text, string final, float delay)> flaps = new List<(TextMeshProUGUI, string, float)>();
        private TextMeshProUGUI blinkRow;
        private readonly List<TextMeshProUGUI> zeroRow = new List<TextMeshProUGUI>();
        private float openTime;

        private static readonly Color Amber = new Color(1f, 0.72f, 0.25f);
        private static readonly Color BoardRed = new Color(1f, 0.32f, 0.3f);
        private static readonly Color Zero = new Color(1f, 0.42f, 0.88f);
        private static readonly Color Paper = new Color(0.86f, 0.87f, 0.8f);
        private static readonly Color Ink = new Color(0.16f, 0.2f, 0.2f);

        private void Awake()
        {
            var inspectable = GetComponent<InspectableObject>();
            inspectable.onInspectStarted.AddListener(Open);
            inspectable.onInspectFinished.AddListener(Close);
        }

        private void Open()
        {
            IsometricCameraRig rig = IsometricCameraRig.Main;
            if (rig != null && cameraPoint != null && cameraLook != null) rig.SetShot(cameraPoint.position, cameraLook.position, 30f);
            if (canvas == null) Build();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Show(true));
        }

        private void Close()
        {
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.ClearShot();
            if (canvas == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Show(false));
        }

        private IEnumerator Show(bool open)
        {
            canvas.gameObject.SetActive(true);
            if (open)
            {
                ResetFlaps();
                group.alpha = 0f;
                yield return Wait(0.45f); // let the camera arrive first
                openTime = Time.unscaledTime;
            }
            float from = group.alpha, to = open ? 1f : 0f;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = Easing.Evaluate(open ? EaseType.EaseOutBack : EaseType.SmoothStep, t / 0.35f);
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / 0.35f));
                panel.localScale = Vector3.one * (open ? Mathf.LerpUnclamped(0.7f, 1f, k) : Mathf.Lerp(1f, 0.85f, k));
                yield return null;
            }
            group.alpha = to;
            panel.localScale = Vector3.one;
            if (!open) canvas.gameObject.SetActive(false);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private void Update()
        {
            if (canvas == null || !canvas.gameObject.activeSelf) return;
            float t = Time.unscaledTime - openTime;
            // Split-flap: each cell spins through random letters, then lands.
            foreach (var f in flaps)
            {
                if (f.text == null) continue;
                if (t < f.delay) { f.text.text = Scramble(f.final, 0.0f); continue; }
                float k = (t - f.delay) / 0.55f;
                f.text.text = k >= 1f ? f.final : Scramble(f.final, k);
            }
            if (blinkRow != null)
            {
                // The last row isn't there at first, then flickers in and keeps glowing.
                float appear = 2.6f;
                float a = t < appear ? 0f : (t < appear + 0.8f ? (Mathf.PerlinNoise(t * 20f, 0.3f) > 0.45f ? 1f : 0.15f) : 0.85f + Mathf.Sin(t * 3f) * 0.15f);
                foreach (TextMeshProUGUI z in zeroRow) z.alpha = a;
            }
        }

        private static string Scramble(string final, float settled)
        {
            const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var sb = new StringBuilder(final.Length);
            int fixedCount = Mathf.FloorToInt(final.Length * settled);
            for (int i = 0; i < final.Length; i++)
            {
                char c = final[i];
                if (c == ' ' || i < fixedCount) sb.Append(c);
                else sb.Append(glyphs[Random.Range(0, glyphs.Length)]);
            }
            return sb.ToString();
        }

        private void ResetFlaps()
        {
            foreach (var f in flaps) if (f.text != null) f.text.text = string.Empty;
            foreach (TextMeshProUGUI z in zeroRow) z.alpha = 0f;
        }

        // ------------------------------------------------------------------ Building the UI

        private void Build()
        {
            canvas = RuntimeUI.CreateCanvas("SignCanvas", transform, 45);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            // Dim the world behind the sign so its own (tiny, faraway) lettering doesn't fight with the readable one.
            Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0.01f, 0.01f, 0.03f, 0.72f));
            RuntimeUI.Stretch(dim.rectTransform);
            if (layout == Layout.Departures) BuildDepartures();
            else BuildParkMap();
            canvas.gameObject.SetActive(false);
        }

        private RectTransform Panel(Vector2 size, Color color, Color frame)
        {
            Image border = RuntimeUI.Image("Frame", canvas.transform, frame);
            RuntimeUI.Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), size + new Vector2(24f, 24f));
            Image body = RuntimeUI.Image("Board", border.transform, color);
            RuntimeUI.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            panel = border.rectTransform;
            return body.rectTransform;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align, bool bold = false)
        {
            TextMeshProUGUI t = RuntimeUI.Text("Label", parent, text, size, color, align);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.5f;
            t.fontSizeMax = size;
            if (bold) t.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, box);
            return t;
        }

        private void BuildDepartures()
        {
            RectTransform b = Panel(new Vector2(1000f, 470f), new Color(0.03f, 0.03f, 0.04f), new Color(0.3f, 0.22f, 0.12f));
            Label(b, Loc.T("DEPARTURES"), 44, Color.white, new Vector2(36f, -22f), new Vector2(520f, 56f), TextAlignmentOptions.Left, true);
            Label(b, "23:57", 44, Amber, new Vector2(760f, -22f), new Vector2(200f, 56f), TextAlignmentOptions.Right, true);
            Image rule = RuntimeUI.Image("Rule", b, new Color(1f, 0.72f, 0.25f, 0.35f));
            RuntimeUI.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -86f), new Vector2(928f, 3f));

            float[] cols = { 36f, 160f, 610f, 700f };
            string[] heads = { Loc.T("TIME"), Loc.T("DESTINATION"), Loc.T("PLAT."), Loc.T("STATUS") };
            for (int i = 0; i < heads.Length; i++)
                Label(b, heads[i], 22, new Color(1f, 1f, 1f, 0.55f), new Vector2(cols[i], -96f), new Vector2(i == 1 ? 430f : (i == 2 ? 80f : 260f), 30f), TextAlignmentOptions.Left, true);

            (string time, string dest, string plat, string status, Color color)[] rows =
            {
                ("23:12", Loc.T("LINE 3  OUTER RING"), "2", Loc.T("CANCELLED"), BoardRed),
                ("23:40", Loc.T("LINE 7  HARBOUR"), "4", Loc.T("DELAYED"), Amber),
                ("23:55", Loc.T("LINE 9  NORTH HILLS"), "1", Loc.T("DELAYED INDEF."), Amber),
            };
            for (int r = 0; r < rows.Length; r++)
            {
                float y = -138f - r * 66f;
                // Dark cell strips behind each row, like real flap modules.
                Image strip = RuntimeUI.Image("Row", b, new Color(1f, 1f, 1f, 0.04f));
                RuntimeUI.Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y + 6f), new Vector2(944f, 56f));
                string[] cells = { rows[r].time, rows[r].dest, rows[r].plat, rows[r].status };
                for (int c = 0; c < cells.Length; c++)
                {
                    Color col = c == 3 ? rows[r].color : Amber;
                    TextMeshProUGUI t = Label(b, string.Empty, 34, col, new Vector2(cols[c], y), new Vector2(c == 1 ? 430f : (c == 3 ? 270f : (c == 2 ? 70f : 120f)), 46f), TextAlignmentOptions.Left, true);
                    flaps.Add((t, cells[c], 0.2f + r * 0.35f + c * 0.08f));
                }
            }

            // The row that wasn't there a second ago.
            float zy = -138f - 3 * 66f - 8f;
            Image zstrip = RuntimeUI.Image("ZeroRow", b, new Color(1f, 0.3f, 0.85f, 0.08f));
            RuntimeUI.Place(zstrip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, zy + 6f), new Vector2(944f, 56f));
            zeroRow.Add(blinkRow = Label(b, "00:00", 36, Zero, new Vector2(cols[0], zy), new Vector2(120f, 48f), TextAlignmentOptions.Left, true));
            zeroRow.Add(Label(b, Loc.T("NIGHT TRAIN ZERO"), 36, Zero, new Vector2(cols[1], zy), new Vector2(430f, 48f), TextAlignmentOptions.Left, true));
            zeroRow.Add(Label(b, "0", 36, Zero, new Vector2(cols[2], zy), new Vector2(70f, 48f), TextAlignmentOptions.Left, true));
            zeroRow.Add(Label(b, Loc.T("ON TIME"), 36, Zero, new Vector2(cols[3], zy), new Vector2(270f, 48f), TextAlignmentOptions.Left, true));
            foreach (TextMeshProUGUI z in zeroRow) z.alpha = 0f;
        }

        private void BuildParkMap()
        {
            const float W = 960f, H = 560f;
            RectTransform b = Panel(new Vector2(W, H), Paper, new Color(0.22f, 0.24f, 0.26f));
            Label(b, Loc.T("CONCRETE PARK"), 40, Ink, new Vector2(30f, -16f), new Vector2(520f, 50f), TextAlignmentOptions.Left, true);
            Label(b, Loc.T("Please enjoy responsibly."), 20, new Color(0.3f, 0.34f, 0.34f), new Vector2(32f, -62f), new Vector2(520f, 28f), TextAlignmentOptions.Left);
            // Compass.
            Label(b, "N", 26, Ink, new Vector2(W - 70f, -16f), new Vector2(40f, 30f), TextAlignmentOptions.Center, true);
            Label(b, "^", 22, Ink, new Vector2(W - 70f, -44f), new Vector2(40f, 30f), TextAlignmentOptions.Center);

            // World → map: plaza x -14.3..8.8 and corridor to 14.6, z -7.3..7.3. North is up.
            Rect area = new Rect(40f, -H + 30f, W - 80f, H - 130f); // in panel coords (x right, y up from top-left)
            Vector2 Map(float x, float z)
            {
                float u = Mathf.InverseLerp(-14.5f, 15f, x);
                float v = Mathf.InverseLerp(-7.5f, 7.5f, z);
                return new Vector2(area.x + u * area.width, area.y + v * area.height);
            }
            Image Box(string name, float x0, float z0, float x1, float z1, Color c)
            {
                Vector2 a = Map(x0, z0), d = Map(x1, z1);
                Image img = RuntimeUI.Image(name, b, c);
                RuntimeUI.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(Mathf.Min(a.x, d.x), Mathf.Min(a.y, d.y)), new Vector2(Mathf.Abs(d.x - a.x), Mathf.Abs(d.y - a.y)));
                return img;
            }
            void Dot(float x, float z, float size, Color c, HackSpritesShape shape = HackSpritesShape.Circle)
            {
                Vector2 p = Map(x, z);
                Image img = RuntimeUI.Image("Dot", b, c);
                img.sprite = Hacking.HackSprites.Get(shape == HackSpritesShape.Circle ? Hacking.HackSprites.Shape.Circle : Hacking.HackSprites.Shape.Diamond);
                RuntimeUI.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), p, new Vector2(size, size));
            }
            void Text(string s, float x, float z, float size, Color c, Vector2 offset, TextAlignmentOptions align = TextAlignmentOptions.Center, float rot = 0f)
            {
                Vector2 p = Map(x, z) + offset;
                TextMeshProUGUI t = RuntimeUI.Text("MapText", b, s, size, c, align);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.fontStyle = FontStyles.Bold;
                RuntimeUI.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), p, new Vector2(320f, size * 1.4f));
                t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
            }

            Color grass = new Color(0.55f, 0.6f, 0.55f), concrete = new Color(0.72f, 0.73f, 0.7f), wall = new Color(0.32f, 0.34f, 0.36f);
            Box("Plaza", -14.3f, -7.3f, 8.8f, 7.3f, concrete);
            Box("Corridor", 8.5f, -1.9f, 14.6f, 1.9f, concrete);
            Box("WallN", -14.3f, 7.0f, 8.8f, 7.5f, wall);
            Box("WallE1", 8.5f, 1.5f, 9.1f, 7.3f, wall);
            Box("WallE2", 8.5f, -7.3f, 9.1f, -1.5f, wall);
            Box("Barrier", 8.45f, -1.5f, 9.15f, 1.5f, new Color(0.45f, 0.35f, 0.35f));
            Box("Fountain", -3.5f, -0.5f, -1.5f, 1.5f, new Color(0.6f, 0.7f, 0.75f));
            Box("Bench1", -7.8f, -3.8f, -6.2f, -3.2f, new Color(0.5f, 0.4f, 0.32f));
            Box("Bench2", 2.2f, -4.3f, 3.8f, -3.7f, new Color(0.5f, 0.4f, 0.32f));
            Box("Planter", -2.9f, -1.9f, -2.1f, -1.1f, grass);

            (string name, float x, float z)[] statues =
            {
                (Loc.T("THE ONE WHO STAYED"), -5f, 3.6f),
                (Loc.T("THE ONE WHO WAITED"), 0f, 4.6f),
                (Loc.T("THE ONE WHO LEFT"), 5f, 3.6f),
            };
            foreach (var s in statues)
            {
                Box("Pedestal", s.x - 0.6f, s.z - 0.6f, s.x + 0.6f, s.z + 0.6f, wall);
                Dot(s.x + 1.1f, s.z - 1.5f, 16f, new Color(1f, 0.75f, 0.3f));
                Text(s.name, s.x, s.z, 15f, Ink, new Vector2(0f, 30f));
            }
            Text(Loc.T("SEALED"), 8.8f, 0f, 15f, new Color(0.55f, 0.2f, 0.2f), new Vector2(0f, -26f));
            Text(Loc.T("CORRIDOR") + " >", 12f, 0f, 15f, Ink, Vector2.zero);
            Text("< " + Loc.T("SUBNET STREET"), -12.6f, 0f, 15f, Ink, Vector2.zero);

            // YOU ARE HERE: where the map stands.
            Dot(-9.5f, 1.6f, 24f, new Color(0.85f, 0.15f, 0.2f));
            Text(Loc.T("YOU ARE HERE"), -9.5f, 1.6f, 16f, new Color(0.8f, 0.12f, 0.18f), new Vector2(0f, -26f));

            // The note scratched into the plastic, along the bottom of the plaza (wraps, never leaves the map).
            Vector2 noteAt = Map(-3f, -6.1f);
            TextMeshProUGUI note = RuntimeUI.Text("Note", b, "'" + Loc.T("They gave up in order. Light their lamps in the same order.") + "'", 17f,
                new Color(0.45f, 0.35f, 0.75f), TextAlignmentOptions.Center);
            note.fontStyle = FontStyles.Italic | FontStyles.Bold;
            note.textWrappingMode = TextWrappingModes.Normal;
            note.enableAutoSizing = true;
            note.fontSizeMin = 11f;
            note.fontSizeMax = 17f;
            RuntimeUI.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), noteAt, new Vector2(560f, 46f));
            note.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);

            // Legend.
            float ly = -H + 22f;
            Image lampKey = RuntimeUI.Image("LegendLamp", b, new Color(1f, 0.75f, 0.3f));
            lampKey.sprite = Hacking.HackSprites.Get(Hacking.HackSprites.Shape.Circle);
            RuntimeUI.Place(lampKey.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(W - 300f, ly + 22f), new Vector2(14f, 14f));
            Label(b, Loc.T("LAMPS"), 15, Ink, new Vector2(W - 288f, ly + 32f), new Vector2(110f, 22f), TextAlignmentOptions.Left, true);
            Image benchKey = RuntimeUI.Image("LegendBench", b, new Color(0.5f, 0.4f, 0.32f));
            RuntimeUI.Place(benchKey.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(W - 170f, ly + 22f), new Vector2(20f, 9f));
            Label(b, Loc.T("BENCHES"), 15, Ink, new Vector2(W - 154f, ly + 32f), new Vector2(120f, 22f), TextAlignmentOptions.Left, true);
        }

        private enum HackSpritesShape { Circle, Diamond }
    }
}
