using System;
using System.Collections;
using System.Collections.Generic;
using Subnet.Audio;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Shape = Subnet.Hacking.HackSprites.Shape;
using Random = UnityEngine.Random;

namespace Subnet.Hacking
{
    /// <summary>
    /// The "understand" half of a fight, drawn inside the battle screen instead of the normal dialogue box:
    /// the memory fragments are cards, Leo's heart is the cursor, wrong cards crack and vanish, the right one
    /// lights up and flies into the firewall. Also the GAME OVER screen.
    /// </summary>
    public partial class HackingManager
    {
        private bool fragmentsOpen;
        private bool gameOverOpen;

        private RectTransform fragPanel;
        private TextMeshProUGUI fragHeader;
        private TextMeshProUGUI fragPrompt;
        private TextMeshProUGUI fragFooter;
        private RectTransform fragList;
        private readonly List<FragmentCard> fragCards = new List<FragmentCard>();

        private class FragmentCard
        {
            public RectTransform rect;
            public Image background;
            public Outline outline;
            public TextMeshProUGUI index;
            public TextMeshProUGUI label;
            public Image heart;
            public int option;      // index in puzzle.options, -1 = [Listen closer]
            public Vector2 home;
            public float slide;
        }

        private static readonly Color CardIdle = new Color(0.09f, 0.06f, 0.14f, 0.95f);
        private static readonly Color CardHot = new Color(0.2f, 0.07f, 0.2f, 0.98f);

        // ================================================================== Fragment picker

        private void BuildFragmentPanel()
        {
            if (fragPanel != null) return;
            RectTransform contentRect = (RectTransform)content.transform;
            Image panel = RuntimeUI.Image("Fragments", contentRect, new Color(0.035f, 0.02f, 0.07f, 0.97f));
            fragPanel = panel.rectTransform;
            RuntimeUI.Place(fragPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, arenaCenterY - 10f), new Vector2(920f, 400f));
            Outline o = panel.gameObject.AddComponent<Outline>();
            o.effectColor = RuntimeUI.Magenta;
            o.effectDistance = new Vector2(4, -4);
            Image stripe = RuntimeUI.Image("Stripe", fragPanel, new Color(0.2f, 1f, 0.95f, 0.85f));
            RuntimeUI.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(880f, 2f));

            fragHeader = RuntimeUI.Text("Header", fragPanel, "", 20, RuntimeUI.Cyan, TextAlignmentOptions.MidlineLeft);
            fragHeader.characterSpacing = 6f;
            RuntimeUI.Place(fragHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -8f), new Vector2(860f, 30f));

            fragPrompt = RuntimeUI.Text("Prompt", fragPanel, "", 25, new Color(0.95f, 0.93f, 1f), TextAlignmentOptions.TopLeft);
            fragPrompt.enableAutoSizing = true;
            fragPrompt.fontSizeMin = 15f;
            fragPrompt.fontSizeMax = 25f;
            RuntimeUI.Place(fragPrompt.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -52f), new Vector2(868f, 118f));

            fragList = RuntimeUI.Rect("Cards", fragPanel);
            RuntimeUI.Place(fragList, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(860f, 196f));

            fragFooter = RuntimeUI.Text("Footer", fragPanel, "", 16, RuntimeUI.Dim, TextAlignmentOptions.BottomRight);
            RuntimeUI.Place(fragFooter.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 8f), new Vector2(600f, 22f));

            fragPanel.gameObject.SetActive(false);
        }

        private FragmentCard MakeCard(int slot, int option, string text, int count)
        {
            Image bg = RuntimeUI.Image("Card", fragList, CardIdle);
            var card = new FragmentCard { rect = bg.rectTransform, background = bg, option = option };
            float height = Mathf.Min(46f, (196f - (count - 1) * 8f) / count);
            card.home = new Vector2(0f, -slot * (height + 8f));
            RuntimeUI.Place(card.rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), card.home + new Vector2(600f, 0f), new Vector2(820f, height));
            card.outline = bg.gameObject.AddComponent<Outline>();
            card.outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
            card.outline.effectDistance = new Vector2(2, -2);

            card.heart = RuntimeUI.Image("Heart", card.rect, RuntimeUI.Magenta);
            card.heart.sprite = HackSprites.Get(Shape.Heart);
            RuntimeUI.Place(card.heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(22f, 22f));

            card.index = RuntimeUI.Text("Index", card.rect, option < 0 ? "?" : (slot + 1).ToString("00"), 18, RuntimeUI.Dim, TextAlignmentOptions.MidlineLeft);
            RuntimeUI.Place(card.index.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(40f, height));

            card.label = RuntimeUI.Text("Label", card.rect, text, 22, new Color(0.85f, 0.83f, 0.92f), TextAlignmentOptions.MidlineLeft);
            card.label.enableAutoSizing = true;
            card.label.fontSizeMin = 13f;
            card.label.fontSizeMax = 22f;
            card.label.textWrappingMode = TextWrappingModes.Normal;
            RuntimeUI.Place(card.label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(720f, height - 4f));
            if (option < 0)
            {
                card.label.fontStyle = FontStyles.Italic;
                card.index.color = RuntimeUI.Lilac;
            }
            return card;
        }

        private void HighlightCards(int selected)
        {
            for (int i = 0; i < fragCards.Count; i++)
            {
                bool on = i == selected;
                FragmentCard c = fragCards[i];
                c.background.color = on ? CardHot : CardIdle;
                c.outline.effectColor = on ? RuntimeUI.Magenta : new Color(1f, 1f, 1f, 0.12f);
                c.label.color = on ? Color.white : (c.option < 0 ? RuntimeUI.Lilac : new Color(0.78f, 0.76f, 0.86f));
                c.heart.enabled = on;
            }
        }

        private IEnumerator TypeInto(TextMeshProUGUI text, string raw, TextEffect effect = TextEffect.None)
        {
            TextFXAnimator fx = text.GetComponent<TextFXAnimator>();
            if (fx == null) fx = text.gameObject.AddComponent<TextFXAnimator>();
            fx.SetText(raw, false, effect);
            int total = text.textInfo.characterCount;
            float budget = 1f;
            int frame = Time.frameCount;
            for (int visible = 0; visible < total;)
            {
                budget += GameTime.UnscaledDelta * 55f;
                while (budget >= 1f && visible < total)
                {
                    budget -= 1f;
                    fx.Reveal(visible);
                    visible++;
                    text.maxVisibleCharacters = visible;
                    if (visible % 3 == 0) Play(tickClip, 0.12f);
                }
                if (Time.frameCount != frame && GameInput.SubmitPressed) visible = total;
                text.maxVisibleCharacters = visible;
                yield return null;
            }
            fx.RevealAll();
            text.maxVisibleCharacters = 99999;
        }

        private IEnumerator WaitForConfirm()
        {
            yield return null;
            while (!GameInput.SubmitPressed) yield return null;
            Play(tickClip, 0.5f);
            yield return null;
        }

        private IEnumerator RunFragments(HackPuzzle puzzle, HashSet<int> eliminated, Action<bool> result)
        {
            BuildFragmentPanel();
            statusText.text = Loc.T("EMOTIONAL FIREWALL DETECTED. SELECT A MEMORY FRAGMENT.");
            SetProgress(0f, string.Empty);
            soul.gameObject.SetActive(false);
            fragmentsOpen = true;

            fragPanel.gameObject.SetActive(true);
            fragPanel.SetAsLastSibling();
            fragHeader.text = "◊  " + Loc.T("MEMORY FRAGMENTS");
            fragFooter.text = Loc.T("↑↓ choose     E confirm");
            foreach (FragmentCard c in fragCards) Destroy(c.rect.gameObject);
            fragCards.Clear();
            fragPrompt.text = string.Empty;

            // Panel opens like a CRT.
            for (float t = 0f; t < 0.22f; t += GameTime.UnscaledDelta)
            {
                fragPanel.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, Easing.Evaluate(EaseType.EaseOutBack, t / 0.22f)), 1f);
                yield return null;
            }
            fragPanel.localScale = Vector3.one;

            string hint = string.IsNullOrEmpty(puzzle.hint)
                ? "Everything they said during the fight is a clue. Which answer would actually help them, not just shut them up?"
                : puzzle.hint;
            string promptText = Loc.T(puzzle.prompt);
            yield return TypeInto(fragPrompt, promptText);

            var options = new List<int>();
            for (int i = 0; i < puzzle.options.Length; i++) if (!eliminated.Contains(i)) options.Add(i);
            int count = options.Count + 1;
            for (int s = 0; s < options.Count; s++) fragCards.Add(MakeCard(s, options[s], TextFX.Strip(Loc.T(puzzle.options[options[s]])), count));
            fragCards.Add(MakeCard(options.Count, -1, Loc.T("[Listen closer]"), count));

            // Cards slide in one after the other.
            for (float t = 0f; t < 0.45f; t += GameTime.UnscaledDelta)
            {
                for (int i = 0; i < fragCards.Count; i++)
                {
                    float n = Mathf.Clamp01((t - i * 0.06f) / 0.25f);
                    fragCards[i].rect.anchoredPosition = fragCards[i].home + new Vector2(Mathf.Lerp(600f, 0f, Easing.Evaluate(EaseType.EaseOutCubic, n)), 0f);
                }
                yield return null;
            }

            int selected = 0;
            bool hintShown = false;
            float nav = 0f;
            HighlightCards(selected);
            yield return null;

            int picked = -1;
            while (picked < 0)
            {
                float y = GameInput.MoveVector.y;
                nav -= GameTime.UnscaledDelta;
                if (Mathf.Abs(y) < 0.5f) nav = 0f;
                else if (nav <= 0f)
                {
                    nav = 0.2f;
                    selected = (selected + (y > 0f ? -1 : 1) + fragCards.Count) % fragCards.Count;
                    HighlightCards(selected);
                    Play(tickClip, 0.35f);
                }

                // Selected card leans out; the heart beats.
                float t = GameTime.UnscaledTime;
                for (int i = 0; i < fragCards.Count; i++)
                {
                    FragmentCard c = fragCards[i];
                    c.slide = Mathf.Lerp(c.slide, i == selected ? 16f : 0f, 1f - Mathf.Exp(-16f * GameTime.UnscaledDelta));
                    c.rect.anchoredPosition = c.home + new Vector2(c.slide, 0f);
                    if (i == selected) c.heart.rectTransform.localScale = Vector3.one * (1f + Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 3.2f)), 6f) * 0.25f);
                }

                if (GameInput.SubmitPressed)
                {
                    FragmentCard c = fragCards[selected];
                    if (c.option < 0)
                    {
                        Play(waveStartClip, 0.4f);
                        if (!hintShown)
                        {
                            hintShown = true;
                            yield return TypeInto(fragPrompt, promptText + "\n<color=#33FFF2><i>" + Loc.T(hint) + "</i></color>");
                        }
                    }
                    else picked = c.option;
                }
                yield return null;
            }

            FragmentCard chosen = fragCards.Find(c => c.option == picked);
            bool correct = picked == puzzle.correctIndex || picked == puzzle.altCorrectIndex;
            string line = picked == puzzle.altCorrectIndex && !string.IsNullOrEmpty(puzzle.altCorrectLine) ? puzzle.altCorrectLine : correct ? puzzle.correctLine : puzzle.wrongLine;

            if (correct)
            {
                Play(firewallBreakClip, 0.6f);
                for (float t = 0f; t < 0.5f; t += GameTime.UnscaledDelta)
                {
                    float n = t / 0.5f;
                    chosen.background.color = Color.Lerp(Color.white, CardHot, n);
                    chosen.rect.localScale = Vector3.one * (1f + Mathf.Sin(n * Mathf.PI) * 0.06f);
                    yield return null;
                }
                foreach (FragmentCard c in fragCards) if (c != chosen) c.rect.gameObject.SetActive(false);
                // The card flies up toward the enemy's firewall.
                Vector2 from = chosen.rect.anchoredPosition;
                for (float t = 0f; t < 0.35f; t += GameTime.UnscaledDelta)
                {
                    float n = Easing.Evaluate(EaseType.EaseInCubic, t / 0.35f);
                    chosen.rect.anchoredPosition = from + new Vector2(0f, n * 420f);
                    chosen.rect.localScale = Vector3.one * (1f - n * 0.7f);
                    yield return null;
                }
                chosen.rect.gameObject.SetActive(false);
            }
            else
            {
                Play(wrongClip);
                Shake(0.25f);
                Vector2 home = chosen.rect.anchoredPosition;
                for (float t = 0f; t < 0.45f; t += GameTime.UnscaledDelta)
                {
                    chosen.background.color = Color.Lerp(new Color(0.7f, 0.1f, 0.2f), CardIdle, t / 0.45f);
                    chosen.rect.anchoredPosition = home + new Vector2(Mathf.Sin(t * 70f) * 10f * (1f - t / 0.45f), 0f);
                    yield return null;
                }
                foreach (FragmentCard c in fragCards) c.rect.gameObject.SetActive(false);
                eliminated.Add(picked);
            }

            fragHeader.text = correct ? "◊  " + Loc.T("FRAGMENT ACCEPTED") : "◊  " + Loc.T("FRAGMENT REJECTED");
            fragHeader.color = correct ? RuntimeUI.Cyan : new Color(1f, 0.4f, 0.45f);
            string message = Loc.T(line);
            if (!correct) message += "\n\n<color=#8A8F99><size=80%>" + Loc.T("(That fragment is gone now. One more wave, then try again.)") + "</size></color>";
            fragFooter.text = Loc.T("E  continue");
            yield return TypeInto(fragPrompt, message, correct ? TextEffect.Wave : TextEffect.Shake);
            yield return WaitForConfirm();

            for (float t = 0f; t < 0.15f; t += GameTime.UnscaledDelta)
            {
                fragPanel.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.02f, t / 0.15f), 1f);
                yield return null;
            }
            fragPanel.gameObject.SetActive(false);
            fragPanel.localScale = Vector3.one;
            fragHeader.color = RuntimeUI.Cyan;
            fragmentsOpen = false;
            result(correct);
        }

        // ================================================================== Game over

        private static readonly string[] GameOverLines =
        {
            "They're still in there. Waiting to be understood.",
            "Breathe. Listen. Try again.",
            "The static is loud. You are louder.",
            "Nobody gets it right the first time. Not even the people who love you."
        };

        private IEnumerator GameOver()
        {
            gameOverOpen = true;
            HideBubbleImmediate();
            ClearBullets();
            statusText.text = string.Empty;

            // 1) The heart cracks...
            Play(hitClip);
            Shake(0.6f);
            soul.gameObject.SetActive(true);
            soulImage.enabled = true;
            soulImage.color = Color.white;
            yield return Wait(0.5f);
            var halves = new List<RectTransform>();
            for (int i = 0; i < 2; i++)
            {
                Image half = RuntimeUI.Image("HalfHeart", arena, RuntimeUI.Magenta);
                half.sprite = HackSprites.Get(Shape.Heart);
                half.type = Image.Type.Filled;
                half.fillMethod = Image.FillMethod.Horizontal;
                half.fillOrigin = i;
                half.fillAmount = 0.5f;
                half.rectTransform.anchorMin = half.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                half.rectTransform.sizeDelta = soul.sizeDelta;
                half.rectTransform.anchoredPosition = soulPosition;
                halves.Add(half.rectTransform);
            }
            soul.gameObject.SetActive(false);
            Play(firewallBreakClip, 0.6f);
            for (float t = 0f; t < 0.5f; t += GameTime.UnscaledDelta)
            {
                for (int i = 0; i < 2; i++)
                {
                    halves[i].anchoredPosition = soulPosition + new Vector2((i == 0 ? -1f : 1f) * t * 10f, 0f);
                    halves[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * t * 30f);
                }
                yield return null;
            }

            // 2) ...and shatters.
            foreach (RectTransform h in halves) Destroy(h.gameObject);
            Play(disconnectClip);
            MusicDirector.Ensure().ExitFight();
            for (int i = 0; i < 10; i++)
            {
                EmitParticle(soulPosition, Random.insideUnitCircle.normalized * Random.Range(120f, 260f) + Vector2.up * 80f, RuntimeUI.Magenta, 10f, 6f, 1.4f, Shape.Shard);
            }
            for (float t = 0f; t < 1.2f; t += GameTime.UnscaledDelta)
            {
                UpdateParticles(GameTime.UnscaledDelta);
                yield return null;
            }

            // 3) GAME OVER.
            RectTransform overlay = RuntimeUI.Rect("GameOver", canvas.transform);
            RuntimeUI.Stretch(overlay);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Image black = RuntimeUI.Image("Black", overlay, new Color(0.01f, 0f, 0.02f, 0.96f));
            RuntimeUI.Stretch(black.rectTransform);
            TextMeshProUGUI title = RuntimeUI.Text("Title", overlay, "", 120, Color.white, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 18f;
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color32(255, 79, 216, 255);
            RuntimeUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1600f, 160f));
            TextMeshProUGUI sub = RuntimeUI.Text("Sub", overlay, "", 30, new Color(0.85f, 0.85f, 0.95f), TextAlignmentOptions.Center);
            RuntimeUI.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1400f, 90f));
            TextMeshProUGUI prompt = RuntimeUI.Text("Prompt", overlay, "", 26, RuntimeUI.Cyan, TextAlignmentOptions.Center);
            RuntimeUI.Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1000f, 40f));
            overlay.SetAsLastSibling();

            for (float t = 0f; t < 0.8f; t += GameTime.UnscaledDelta)
            {
                group.alpha = t / 0.8f;
                yield return null;
            }
            group.alpha = 1f;

            // Letters drop in one by one, glitching.
            string word = Loc.T("GAME OVER");
            for (int i = 1; i <= word.Length; i++)
            {
                title.text = word.Substring(0, i) + (i < word.Length ? "<color=#FF4FD8>" + (char)Random.Range(33, 90) + "</color>" : string.Empty);
                if (word[i - 1] != ' ') Play(tickClip, 0.6f);
                yield return Wait(0.08f);
            }
            title.text = word;
            yield return Wait(0.4f);
            yield return TypeInto(sub, Loc.T(GameOverLines[Random.Range(0, GameOverLines.Length)]));
            yield return Wait(0.3f);

            yield return null;
            float blink = 0f;
            while (!GameInput.SubmitPressed)
            {
                blink += GameTime.UnscaledDelta;
                prompt.text = Mathf.Repeat(blink, 1f) < 0.65f ? "[E]  " + Loc.T("TRY AGAIN") : string.Empty;
                float g = Mathf.PerlinNoise(blink * 3f, 0f);
                title.rectTransform.anchoredPosition = new Vector2(g > 0.8f ? Random.Range(-6f, 6f) : 0f, 90f);
                yield return null;
            }
            Play(connectClip);

            // 4) Back to the street, in front of them. They're still there.
            for (float t = 0f; t < 0.4f; t += GameTime.UnscaledDelta)
            {
                group.alpha = 1f - t / 0.4f;
                yield return null;
            }
            Destroy(overlay.gameObject);
            Inventory.SetHP(Inventory.MaxHP);
            if (DialogueManager.Instance != null) DialogueManager.Instance.SetBattleSkin(false);
            yield return TransitionOut();
            ClearBullets();
            soulImage.color = RuntimeUI.Magenta;
            gameOverOpen = false;
            defeated = false;
            obstacle = null;
            IsActive = false;
            InputLock.Release(this);
        }
    }
}
