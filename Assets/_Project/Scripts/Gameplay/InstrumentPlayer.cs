using Subnet.CameraSystem;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Interaction;
using Subnet.Player;
using Subnet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Subnet.Gameplay
{
    /// <summary>
    /// One target for a whole instrument (a drum kit, a row of wind chimes) instead of four tiny ones you can
    /// never aim at. Interact to sit down and play: the camera frames the instrument and each piece is a
    /// direction (arrows / WASD / d-pad), shown on screen. X / Esc stops. Hits go to the pads' SequencePuzzle.
    /// </summary>
    public class InstrumentPlayer : MonoBehaviour, IInteractable
    {
        [SerializeField] private PuzzleSwitch[] pads = new PuzzleSwitch[4];   // left, up, right, down
        [SerializeField] private string[] labels = { "Left", "Up", "Right", "Down" };
        [SerializeField] private Color[] colors = { };
        [SerializeField] private Transform cameraPoint;
        [SerializeField] private Transform lookPoint;
        [SerializeField] private float fieldOfView = 40f;
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private string title = "PLAY";

        private bool playing;
        private int openedFrame;
        private Canvas canvas;
        private CanvasGroup group;
        private readonly Image[] keys = new Image[4];
        private readonly float[] flash = new float[4];

        public bool CanInteract => isActiveAndEnabled && !playing && !Solved;
        public Vector3 PromptAnchor => promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.4f;
        public void SetFocused(bool focused) { }

        private SequencePuzzle Puzzle => pads.Length > 0 && pads[0] != null ? pads[0].Owner : null;
        private bool Solved => Puzzle != null && Puzzle.IsSolved;

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor != null) interactor.FaceTowards(transform.position);
            if (Puzzle != null && Puzzle.IsLocked) { Puzzle.SayLocked(); return; }
            Begin();
        }

        private void Begin()
        {
            playing = true;
            openedFrame = Time.frameCount;
            InputLock.Acquire(this);
            if (IsometricCameraRig.Main != null && cameraPoint != null && lookPoint != null)
                IsometricCameraRig.Main.SetShot(cameraPoint.position, lookPoint.position, fieldOfView);
            BuildUI();
            group.alpha = 1f;
            canvas.enabled = true;
        }

        private void End()
        {
            playing = false;
            InputLock.Release(this);
            if (IsometricCameraRig.Main != null) IsometricCameraRig.Main.ClearShot();
            if (canvas != null) canvas.enabled = false;
        }

        private void OnDisable()
        {
            if (playing) End();
        }

        private void Update()
        {
            if (!playing) return;
            for (int i = 0; i < 4; i++)
            {
                flash[i] = Mathf.MoveTowards(flash[i], 0f, Time.unscaledDeltaTime * 4f);
                if (keys[i] != null)
                {
                    Color c = i < colors.Length ? colors[i] : RuntimeUI.Cyan;
                    keys[i].color = Color.Lerp(new Color(0.06f, 0.06f, 0.09f, 0.88f), c, flash[i]);
                    keys[i].rectTransform.localScale = Vector3.one * (1f + 0.12f * flash[i]);
                }
            }

            // A wrong note shows a line in the dialogue box: wait for it.
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
            if (GameTime.Paused) return;
            if (Solved) { End(); return; }
            if (Time.frameCount == openedFrame) return;

            Keyboard kb = Keyboard.current;
            Gamepad gp = Gamepad.current;
            bool stop = (kb != null && (kb.xKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (stop) { End(); return; }

            if (Pressed(kb?.leftArrowKey, kb?.aKey, gp?.dpad.left)) Play(0);
            if (Pressed(kb?.upArrowKey, kb?.wKey, gp?.dpad.up)) Play(1);
            if (Pressed(kb?.rightArrowKey, kb?.dKey, gp?.dpad.right)) Play(2);
            if (Pressed(kb?.downArrowKey, kb?.sKey, gp?.dpad.down)) Play(3);
        }

        private static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl a, UnityEngine.InputSystem.Controls.ButtonControl b, UnityEngine.InputSystem.Controls.ButtonControl c)
        {
            return (a != null && a.wasPressedThisFrame) || (b != null && b.wasPressedThisFrame) || (c != null && c.wasPressedThisFrame);
        }

        private void Play(int i)
        {
            if (i >= pads.Length || pads[i] == null) return;
            flash[i] = 1f;
            pads[i].Hit();
        }

        private void BuildUI()
        {
            if (canvas != null) return;
            canvas = RuntimeUI.CreateCanvas("InstrumentCanvas", transform, 45);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            RectTransform root = RuntimeUI.Rect("Pad", canvas.transform);
            RuntimeUI.Place(root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(520f, 380f));

            TextMeshProUGUI head = RuntimeUI.Text("Title", root, Loc.T(title), 26, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center);
            head.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(500f, 36f));

            Vector2[] slots = { new Vector2(-150f, 0f), new Vector2(0f, 95f), new Vector2(150f, 0f), new Vector2(0f, -95f) };
            string[] letters = { "A", "W", "D", "S" };
            for (int i = 0; i < 4; i++)
            {
                Image key = RuntimeUI.Image("Key" + i, root, new Color(0.06f, 0.06f, 0.09f, 0.88f));
                RuntimeUI.Place(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), slots[i] + new Vector2(0f, -20f), new Vector2(140f, 78f));
                Outline o = key.gameObject.AddComponent<Outline>();
                o.effectColor = i < colors.Length ? colors[i] : RuntimeUI.Cyan;
                o.effectDistance = new Vector2(2f, -2f);
                keys[i] = key;
                TextMeshProUGUI label = RuntimeUI.Text("Label", key.rectTransform, "", 22, Color.white, TextAlignmentOptions.Center);
                RuntimeUI.Stretch(label.rectTransform);
                // The arrow glyphs may not exist in the pixel font: show the WASD letter, the name underneath.
                label.text = "<b>" + letters[i] + "</b>\n<size=80%>" + Loc.T(i < labels.Length ? labels[i] : string.Empty) + "</size>";
            }
            TextMeshProUGUI hint = RuntimeUI.Text("Hint", root, Loc.T("ARROWS / WASD: PLAY    X: STOP"), 18, RuntimeUI.Dim, TextAlignmentOptions.Center);
            RuntimeUI.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(520f, 28f));
            canvas.enabled = false;
        }
    }
}
