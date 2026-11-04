using System.Collections;
using System.Collections.Generic;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Interaction;
using Subnet.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subnet.UI
{
    /// <summary>
    /// A shop counter. Talking to the keeper opens a menu: items with prices in TOKENS, what Leo already has,
    /// a description, and the keeper's reactions. Up/Down choose, Confirm buys, Cancel leaves.
    /// The first visit always leaves Leo able to afford one potion (the keeper "welcomes" new customers).
    /// </summary>
    public class ShopCounter : MonoBehaviour, IInteractable
    {
        [SerializeField] private string shopName = "NEON MART  //  OPEN 24/7";
        [SerializeField] private string[] stock = { "potion", "megapotion", "ramen" };
        [SerializeField] private DialogueSpeaker keeper;
        [TextArea(1, 3)] [SerializeField] private string[] firstGreeting = { };
        [TextArea(1, 3)] [SerializeField] private string[] greeting = { };
        [TextArea(1, 3)] [SerializeField] private string welcomeGiftLine = "First time? Have a welcome bonus. {p}{small}Corporate makes me say that. I mean it, though.{/small}";
        [SerializeField] private string visitedFlag = "shop_visited";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip moveClip;
        [SerializeField] private AudioClip buyClip;
        [SerializeField] private AudioClip errorClip;

        private static readonly string[] Thanks =
        {
            "Pleasure doing business. {p}{small}Don't drink it all at once.{/small}",
            "Good choice. {p}I'd say that to anyone, but this time I mean it.",
            "Ka-ching. {p}{small}I'm legally required to say ka-ching.{/small}",
            "Stay hydrated out there. {p}It's a dangerous city for feelings."
        };
        private static readonly string[] Broke =
        {
            "You're a little short. {p}{small}Happens to the best of us. Happens to me every month.{/small}",
            "Not enough tokens. {p}Win a few fights. Check under couch cushions."
        };

        private Collider cachedCollider;
        private Canvas canvas;
        private TextMeshProUGUI title;
        private TextMeshProUGUI list;
        private TextMeshProUGUI detail;
        private TextMeshProUGUI keeperLine;
        private TextMeshProUGUI wallet;
        private TextMeshProUGUI hint;
        private RectTransform panel;
        private bool open;
        private int cursor;
        private float navTimer;
        private int openedFrame;
        private string reaction;

        public bool CanInteract => isActiveAndEnabled && !open;

        public Vector3 PromptAnchor => cachedCollider != null
            ? new Vector3(cachedCollider.bounds.center.x, cachedCollider.bounds.max.y + 0.3f, cachedCollider.bounds.center.z)
            : transform.position + Vector3.up * 1.6f;

        public void SetFocused(bool focused) { }

        private void Awake()
        {
            cachedCollider = GetComponentInChildren<Collider>();
            BuildUI();
            canvas.enabled = false;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (open) return;
            DialogueManager dialogue = DialogueManager.Instance;
            bool first = !StoryFlags.Has(visitedFlag);
            var lines = new List<string>(first ? firstGreeting : greeting);
            int cheapestPotion = Inventory.Find("potion") != null ? Inventory.Find("potion").price : 5;
            int gift = first ? Mathf.Max(0, cheapestPotion - Inventory.Tokens) : 0;
            if (gift > 0 && !string.IsNullOrEmpty(welcomeGiftLine)) lines.Add(welcomeGiftLine);
            StoryFlags.Set(visitedFlag);

            void Open()
            {
                if (gift > 0) Inventory.AddTokens(gift);
                StartCoroutine(OpenMenu());
            }

            if (dialogue != null && lines.Count > 0) dialogue.PlayLines(lines.ToArray(), keeper, Open);
            else Open();
        }

        private IEnumerator OpenMenu()
        {
            yield return null;
            open = true;
            openedFrame = Time.frameCount;
            InputLock.Acquire(this);
            canvas.enabled = true;
            cursor = 0;
            reaction = "What'll it be?";
            Refresh();
            for (float t = 0f; t < 0.2f; t += GameTime.UnscaledDelta)
            {
                panel.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, t / 0.2f);
                yield return null;
            }
            panel.localScale = Vector3.one;
        }

        private void Close()
        {
            open = false;
            canvas.enabled = false;
            InputLock.Release(this);
            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue != null && keeper != null) dialogue.PlayLines(new[] { "Come back anytime. {p}{small}I'm always here. That's kind of the problem.{/small}" }, keeper);
        }

        private void Update()
        {
            if (!open || GameTime.Paused || Time.frameCount == openedFrame) return;
            int count = stock.Length + 1; // + LEAVE
            Vector2 move = GameInput.MoveVector;
            navTimer -= Time.unscaledDeltaTime;
            if (Mathf.Abs(move.y) < 0.5f) navTimer = 0f;
            else if (navTimer <= 0f)
            {
                navTimer = 0.2f;
                cursor = (cursor + (move.y > 0f ? -1 : 1) + count) % count;
                Play(moveClip, 0.6f);
                Refresh();
            }

            if (GameInput.CancelPressed) { Close(); return; }
            if (!GameInput.SubmitPressed) return;
            if (cursor >= stock.Length) { Close(); return; }

            Inventory.ItemDef item = Inventory.Find(stock[cursor]);
            if (item == null) return;
            if (Inventory.Spend(item.price))
            {
                Inventory.Add(item.id);
                reaction = Thanks[Random.Range(0, Thanks.Length)];
                Play(buyClip, 0.8f);
                StartCoroutine(Punch(wallet.rectTransform));
            }
            else
            {
                reaction = Broke[Random.Range(0, Broke.Length)];
                Play(errorClip, 0.7f);
                StartCoroutine(Punch(list.rectTransform));
            }
            Refresh();
        }

        private void Refresh()
        {
            title.text = Loc.T(shopName);
            wallet.text = "<color=#FFD84F>◊ " + Inventory.Tokens + "</color>  " + Loc.T("TOKENS") + "      HP " + Inventory.HP + "/" + Inventory.MaxHP;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < stock.Length; i++)
            {
                Inventory.ItemDef item = Inventory.Find(stock[i]);
                if (item == null) continue;
                bool sel = i == cursor;
                bool afford = Inventory.Tokens >= item.price;
                sb.Append(sel ? "<color=#FF4FD8>> </color>" : "  ")
                  .Append(sel ? "<color=#FFFFFF>" : "<color=#C9C3E6>").Append(Loc.T(item.name)).Append("</color>")
                  .Append("<pos=62%>").Append(afford ? "<color=#FFD84F>" : "<color=#8A5560>").Append("◊ ").Append(item.price).Append("</color>")
                  .Append("<pos=80%><color=#8A8F99>").Append(Loc.T("OWNED")).Append(' ').Append(Inventory.Count(item.id)).Append("</color>\n");
            }
            sb.Append('\n').Append(cursor >= stock.Length ? "<color=#FF4FD8>> </color><color=#FFFFFF>" : "  <color=#C9C3E6>").Append(Loc.T("LEAVE")).Append("</color>");
            list.text = sb.ToString();
            detail.text = cursor < stock.Length && Inventory.Find(stock[cursor]) != null ? Loc.T(Inventory.Find(stock[cursor]).description) : Loc.T("Back to the street.");
            TextFXAnimator fx = keeperLine.GetComponent<TextFXAnimator>();
            if (fx == null) fx = keeperLine.gameObject.AddComponent<TextFXAnimator>();
            fx.SetText(Loc.T(reaction), true, TextEffect.None);
            fx.RevealAll();
            hint.text = Loc.T("UP/DOWN: CHOOSE   CONFIRM: BUY   CANCEL: LEAVE");
        }

        private IEnumerator Punch(RectTransform target)
        {
            for (float t = 0f; t < 0.2f; t += GameTime.UnscaledDelta)
            {
                target.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.2f * Mathf.PI) * 0.06f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private void BuildUI()
        {
            canvas = RuntimeUI.CreateCanvas("ShopCanvas", transform, 80);
            Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.35f));
            RuntimeUI.Stretch(dim.rectTransform);

            Image frame = RuntimeUI.Image("Panel", canvas.transform, new Color(0.06f, 0.03f, 0.1f, 0.97f));
            panel = frame.rectTransform;
            RuntimeUI.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1100f, 600f));
            Outline outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.31f, 0.85f);
            outline.effectDistance = new Vector2(4, -4);
            Image stripe = RuntimeUI.Image("Stripe", panel, new Color(0.2f, 1f, 0.95f, 0.9f));
            RuntimeUI.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1040f, 3f));

            title = RuntimeUI.Text("Title", panel, "", 36, RuntimeUI.Cyan, TextAlignmentOptions.TopLeft);
            title.fontStyle = FontStyles.Bold;
            RuntimeUI.Stretch(title.rectTransform, 30, 30, 22, 520);
            wallet = RuntimeUI.Text("Wallet", panel, "", 24, Color.white, TextAlignmentOptions.TopRight);
            RuntimeUI.Stretch(wallet.rectTransform, 30, 30, 30, 520);

            list = RuntimeUI.Text("List", panel, "", 30, Color.white, TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(list.rectTransform, 40, 40, 100, 250);
            list.textWrappingMode = TextWrappingModes.NoWrap;

            detail = RuntimeUI.Text("Detail", panel, "", 22, RuntimeUI.Lilac, TextAlignmentOptions.TopLeft);
            RuntimeUI.Stretch(detail.rectTransform, 40, 40, 360, 170);

            Image speech = RuntimeUI.Image("Speech", panel, new Color(1f, 1f, 1f, 0.06f));
            RuntimeUI.Stretch(speech.rectTransform, 30, 30, 440, 50);
            keeperLine = RuntimeUI.Text("Keeper", speech.transform, "", 24, new Color(1f, 0.92f, 0.7f), TextAlignmentOptions.MidlineLeft);
            RuntimeUI.Stretch(keeperLine.rectTransform, 20, 20, 6, 6);

            hint = RuntimeUI.Text("Hint", panel, "", 18, RuntimeUI.Dim, TextAlignmentOptions.BottomLeft);
            RuntimeUI.Stretch(hint.rectTransform, 30, 30, 560, 14);
        }

        private void OnDestroy()
        {
            if (open) InputLock.Release(this);
        }
    }
}
