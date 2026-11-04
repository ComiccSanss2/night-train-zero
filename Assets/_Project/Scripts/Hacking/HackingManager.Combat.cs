using System;
using System.Collections;
using System.Collections.Generic;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Shape = Subnet.Hacking.HackSprites.Shape;
using Random = UnityEngine.Random;

namespace Subnet.Hacking
{
    /// <summary>
    /// The combat half of the hacking fights: bullets with behaviours (sine, homing, gravity, splitting,
    /// bouncing, orbiting, delayed dashes), telegraphed lasers, particles, Leo's HP bar, potions (Q),
    /// the wave banner and the token reward. Every pattern changes halfway through ("phase 2").
    /// </summary>
    public partial class HackingManager
    {
        // ================================================================== Data

        private class Bullet
        {
            public RectTransform rect;
            public Image image;
            public Image glow;
            public bool active;
            public bool grazed;
            public Vector2 position;   // what collides (base + sine offset)
            public Vector2 basePos;
            public Vector2 velocity;
            public float radius;
            public Vector2 box;        // > 0: rectangular hitbox (half extents)
            public float spin;
            public bool align;
            public float age;
            public float life;
            public float delay;        // grows in place before moving (telegraph)
            public bool aimOnLaunch;
            public float launchSpeed;
            public float accel;
            public float minSpeed;
            public Vector2 gravity;
            public float sineAmp, sineFreq, sinePhase;
            public float homing, turnRate;
            public float turn;
            public int bounces;
            public float splitAt;
            public int splitCount;
            public float splitSpeed;
            public bool orbit;
            public float orbitR, orbitA, orbitW, orbitDR;
            public float trailTimer;
            public bool trail;
            public int damage;
            public Vector2 size;
            public Color color;
            public Shape shape;
        }

        private class Laser
        {
            public RectTransform rect;
            public Image core;
            public Image glow;
            public bool active;
            public Vector2 center;
            public float angle;
            public float angularSpeed;
            public float length;
            public float thickness;
            public bool fromPivot;
            public float warn;
            public float fire;
            public float age;
            public bool harmless;
            public Color color;
            public bool fired;
        }

        private class Particle
        {
            public RectTransform rect;
            public Image image;
            public bool active;
            public Vector2 position;
            public Vector2 velocity;
            public float life, maxLife;
            public float startSize, endSize;
            public Color color;
        }

        private class FloatText
        {
            public TextMeshProUGUI text;
            public bool active;
            public float life;
            public Vector2 position;
        }

        private readonly List<Bullet> bullets = new List<Bullet>();
        private readonly List<Laser> lasers = new List<Laser>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<FloatText> floats = new List<FloatText>();
        private readonly List<KeyValuePair<float, Action>> scheduled = new List<KeyValuePair<float, Action>>();

        // Pattern state
        private float wavePhase;
        private float secondaryTimer;
        private float spinDirection = 1f;
        private float serpentY, serpentY2;
        private float gapY;
        private bool wallsFromLeft;
        private Vector2 orbitAnchor;
        private int burstCount;

        // Session state
        private int reconnects;
        private int grazes;
        private float potionCooldown;

        // Combat UI
        private RectTransform hpFill;
        private RectTransform hpGhost;
        private Image hpFillImage;
        private TextMeshProUGUI hpNumbers;
        private TextMeshProUGUI itemHint;
        private RectTransform progressFill;
        private TextMeshProUGUI progressLabel;
        private TextMeshProUGUI bannerText;
        private RawImage arenaGrid;
        private RawImage backdropLines;
        private Image arenaGlow;
        private readonly List<Image> brackets = new List<Image>();
        private float ghostValue = 1f;
        private float fillValue = 1f;
        private const float HpBarWidth = 230f;

        // ================================================================== Session

        private void BeginCombatSession()
        {
            reconnects = 0;
            grazes = 0;
            if (Inventory.HP <= 0) Inventory.SetHP(Inventory.MaxHP);
            RefreshHP(true);
            RefreshItemHint();
        }

        private void BeginWavePattern(HackWave wave)
        {
            ClearBullets();
            secondaryTimer = 1.2f;
            spinDirection = Random.value < 0.5f ? 1f : -1f;
            serpentY = Random.Range(-arenaHalf.y * 0.5f, arenaHalf.y * 0.5f);
            serpentY2 = -serpentY;
            gapY = 0f;
            wallsFromLeft = false;
            orbitAnchor = Vector2.zero;
            burstCount = 0;
            wavePhase = 0f;
            RefreshItemHint();
            if (wave == null) return;
            Color accent = PatternColor(wave.pattern);
            foreach (Image b in brackets) b.color = accent;
            if (arenaGlow != null) arenaGlow.color = new Color(accent.r, accent.g, accent.b, 0.18f);
        }

        /// <summary>First fight only: how hacking works, in five short lines.</summary>
        private IEnumerator MaybeTutorial()
        {
            if (StoryFlags.Has(TutorialFlag) || DialogueManager.Instance == null) yield break;
            StoryFlags.Set(TutorialFlag);
            bool done = false;
            DialogueManager.Instance.PlayLines(new[]
            {
                "* <color=#33FFF2>HOW TO HACK SOMEONE.</color> {p}It's not a fight. It's a conversation with bullets.",
                "* <color=#FFD84F>1. DODGE.</color> Move the heart. Every hit costs HP (the yellow bar under the arena is yours). Brushing past bullets without touching them earns bonus tokens.",
                "* <color=#FFD84F>2. LISTEN.</color> They talk the whole time: the speech bubble, the taunts, the line after each wave. Every word is a clue about what hurts.",
                "* <color=#FFD84F>3. UNDERSTAND.</color> After each wave, pick the memory fragment that answers them with kindness and honesty. Never the cold one, never the comfortable lie.",
                "* Each right answer breaks one block of their <color=#FF4FD8>FIREWALL</color> (top left). Not sure? Pick <color=#B9A7FF>[Listen closer]</color> for a hint. A wrong pick just means one more wave, and that fragment disappears.",
                "* Low on HP? Press <color=#FFD84F>F</color> to drink a potion."
            }, null, () => done = true);
            while (!done) yield return null;
        }

        private IEnumerator GiveReward(HackableObstacle target)
        {
            foreach (HackWave w in target.waves)
            {
                if (w.surrender) yield break; // no coins for grief.
            }
            int reward = 6 + (reconnects == 0 ? 3 : 0) + Mathf.Min(5, grazes / 8);
            statusText.text = reconnects == 0
                ? Loc.F("FLAWLESS LINK. REWARD: +{0} TOKENS", reward)
                : Loc.F("REWARD: +{0} TOKENS", reward);
            Inventory.AddTokens(reward);
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                EmitParticle(Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(160f, 320f), new Color(1f, 0.85f, 0.3f), 14f, 2f, 0.9f, Shape.Diamond);
            }
            yield return Wait(1.1f);
        }

        // ================================================================== Patterns

        private Vector2 SoulPos => soulPosition;
        private bool Phase2 => wavePhase > 0.5f;

        private void SpawnPattern(float dt)
        {
            float k = currentWave.intensity;
            secondaryTimer -= dt;
            spawnTimer -= dt;
            if (spawnTimer > 0f && secondaryTimer > 0f) return;
            bool main = spawnTimer <= 0f;
            bool second = secondaryTimer <= 0f;

            switch (currentWave.pattern)
            {
                case HackPattern.PaperRain: PaperRain(k, main, second); break;
                case HackPattern.ClockHands: ClockHands(k, main, second); break;
                case HackPattern.Crossfire: Crossfire(k, main, second); break;
                case HackPattern.Spiral: Spiral(k, main, second); break;
                case HackPattern.Aimed: Aimed(k, main, second); break;
                case HackPattern.Ripple: Ripple(k, main, second); break;
                case HackPattern.Lasers: Lasers(k, main, second); break;
                case HackPattern.Bloom: Bloom(k, main, second); break;
                case HackPattern.Serpent: Serpent(k, main, second); break;
                case HackPattern.Fireworks: Fireworks(k, main, second); break;
                case HackPattern.Walls: Walls(k, main, second); break;
                case HackPattern.Homing: Homing(k, main, second); break;
                case HackPattern.Orbit: Orbit(k, main, second); break;
                case HackPattern.TrainRush: TrainRush(k, main, second); break;
            }
            if (spawnTimer <= 0f) spawnTimer = 0.5f;
            if (secondaryTimer <= 0f) secondaryTimer = 99f;
        }

        // Falling paperwork that sways; phase 2 drops whole stacks in columns.
        private void PaperRain(float k, bool main, bool second)
        {
            Color paper = new Color(0.97f, 0.95f, 0.9f);
            if (main)
            {
                spawnTimer = 0.16f / k;
                Bullet b = Spawn(new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), arenaHalf.y + 20f),
                    new Vector2(Random.Range(-20f, 20f), -Random.Range(130f, 200f) * k), new Vector2(16, 21), 8f, paper, Shape.Square);
                b.sineAmp = Random.Range(14f, 34f);
                b.sineFreq = Random.Range(2f, 4f);
                b.sinePhase = Random.Range(0f, 6f);
                b.spin = Random.Range(-160f, 160f);
                b.glow.enabled = false;
                AttackPulse(0.4f);
            }
            if (second)
            {
                secondaryTimer = Phase2 ? 1.0f / k : 2.2f / k;
                float x = Mathf.Clamp(SoulPos.x + Random.Range(-60f, 60f), -arenaHalf.x + 20f, arenaHalf.x - 20f);
                Warn(new Vector2(x, 0f), 90f, arenaSize.y + 40f, 28f, 0.55f, new Color(1f, 1f, 1f));
                int count = Phase2 ? 6 : 4;
                Schedule(0.55f, () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        Bullet b = Spawn(new Vector2(x, arenaHalf.y + 20f + i * 24f), new Vector2(0f, -360f * k), new Vector2(26, 18), 10f, new Color(1f, 0.97f, 0.85f), Shape.Square);
                        b.glow.enabled = false;
                        b.damage = 4;
                    }
                    Play(tickClip, 0.4f);
                    AttackPulse(1f);
                });
            }
        }

        // Clock hands shoot out from the center; a real minute-hand laser sweeps the dial (two hands in phase 2).
        private void ClockHands(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = 0.55f / k;
                patternAngle += 27f * spinDirection;
                for (int i = 0; i < 3; i++)
                {
                    float a = (patternAngle + i * 120f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Bullet b = Spawn(dir * 20f, dir * (170f * k), new Vector2(10, 28), 7f, RuntimeUI.Cyan, Shape.Shard);
                    b.align = true;
                    b.accel = 0.35f;
                }
                Play(tickClip, 0.35f);
                AttackPulse(0.6f);
            }
            if (second)
            {
                secondaryTimer = 5.2f / k;
                float start = Random.Range(0f, 360f);
                SweepHand(start, 55f * k * spinDirection, arenaHalf.x * 1.3f, RuntimeUI.Cyan);
                if (Phase2) SweepHand(start + 180f, -35f * k * spinDirection, arenaHalf.y * 0.9f, new Color(1f, 0.85f, 0.4f));
                spinDirection = -spinDirection;
            }
        }

        private void SweepHand(float startAngle, float speed, float length, Color color)
        {
            Laser l = CreateLaser(Vector2.zero, startAngle, length, 16f, 0.9f, 2.8f, color, true);
            l.angularSpeed = speed;
        }

        // Notifications zip in from the sides, wobbling; phase 2 adds "ping" bombs from above that burst.
        private void Crossfire(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = 0.3f / k;
                bool fromLeft = Random.value < 0.5f;
                float y = Random.Range(-arenaHalf.y + 14f, arenaHalf.y - 14f);
                Color c = fromLeft ? RuntimeUI.Magenta : RuntimeUI.Cyan;
                Bullet b = Spawn(new Vector2(fromLeft ? -arenaHalf.x - 20f : arenaHalf.x + 20f, y), new Vector2(fromLeft ? 240f : -240f, 0f) * k,
                    new Vector2(30, 14), 7f, c, Shape.Rounded);
                b.sineAmp = 16f;
                b.sineFreq = 7f;
                AttackPulse(0.5f);
            }
            if (second && Phase2)
            {
                secondaryTimer = 1.1f / k;
                float x = Random.Range(-arenaHalf.x * 0.8f, arenaHalf.x * 0.8f);
                Bullet b = Spawn(new Vector2(x, arenaHalf.y + 16f), new Vector2(0f, -170f * k), new Vector2(26, 26), 10f, new Color(1f, 0.4f, 0.45f), Shape.Circle);
                b.splitAt = Random.Range(0.9f, 1.4f);
                b.splitCount = 8;
                b.splitSpeed = 150f * k;
            }
            else if (second) secondaryTimer = 0.5f;
        }

        // A looping thought; phase 2 reverses, adds a third arm and accelerates.
        private void Spiral(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 0.075f : 0.085f) / k;
            int arms = Phase2 ? 3 : 2;
            float dirSign = Phase2 ? -spinDirection : spinDirection;
            patternAngle += 17f * dirSign;
            for (int arm = 0; arm < arms; arm++)
            {
                float a = (patternAngle + arm * 360f / arms) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float hue = Mathf.Repeat(patternAngle / 360f, 1f);
                Color c = Color.Lerp(RuntimeUI.Lilac, RuntimeUI.Magenta, Mathf.PingPong(hue * 2f, 1f));
                Bullet b = Spawn(dir * 10f, dir * ((Phase2 ? 110f : 145f) * k), new Vector2(13, 13), 6f, c, Shape.Diamond);
                b.spin = 120f;
                if (Phase2) { b.accel = 0.9f; b.turn = 25f * dirSign; }
            }
            AttackPulse(0.35f);
        }

        // Spread shots at you; phase 2 surrounds you with doubts that freeze, then dash.
        private void Aimed(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = 0.62f / k;
                Vector2 p = RandomEdgePoint();
                Vector2 dir = (SoulPos - p).normalized;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 d = Rotate(dir, i * 14f);
                    Spawn(p, d * (240f * k), new Vector2(15, 15), 7f, new Color(1f, 0.35f, 0.4f), Shape.Circle);
                }
                AttackPulse(1f);
            }
            if (second)
            {
                secondaryTimer = Phase2 ? 1.8f / k : 3f / k;
                int n = Phase2 ? 8 : 5;
                float offset = Random.Range(0f, 360f);
                for (int i = 0; i < n; i++)
                {
                    float a = (offset + i * 360f / n) * Mathf.Deg2Rad;
                    Vector2 p = SoulPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 150f;
                    p.x = Mathf.Clamp(p.x, -arenaHalf.x + 10f, arenaHalf.x - 10f);
                    p.y = Mathf.Clamp(p.y, -arenaHalf.y + 10f, arenaHalf.y - 10f);
                    Bullet b = Spawn(p, Vector2.zero, new Vector2(16, 16), 7f, new Color(1f, 0.55f, 0.3f), Shape.Star);
                    b.delay = 0.75f + i * 0.04f;
                    b.aimOnLaunch = true;
                    b.launchSpeed = 300f * k;
                    b.spin = 300f;
                }
                Play(alertClip, 0.35f);
            }
        }

        // Rings of tears with a gap; they slow down as they spread. Phase 2: two rings at once.
        private void Ripple(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 0.95f : 1.15f) / k;
            int rings = Phase2 ? 2 : 1;
            bool grief = currentWave.surrender;
            for (int r = 0; r < rings; r++)
            {
                Vector2 origin = Vector2.zero;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    origin = new Vector2(Random.Range(-arenaHalf.x * 0.8f, arenaHalf.x * 0.8f), Random.Range(-arenaHalf.y * 0.8f, arenaHalf.y * 0.8f));
                    if ((origin - SoulPos).magnitude > 170f) break;
                }
                const int count = 16;
                int gapStart = Random.Range(0, count);
                float offset = Random.Range(0f, 30f);
                Color c = grief ? new Color(0.55f, 0.6f, 0.7f) : new Color(0.45f, 0.75f, 1f);
                for (int i = 0; i < count; i++)
                {
                    if (!grief && (i - gapStart + count) % count < 3) continue;
                    float a = (offset + i * 360f / count) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Bullet b = Spawn(origin, dir * (175f * k), new Vector2(14, 14), 6f, c, Shape.Ring);
                    b.accel = -0.55f;
                    b.minSpeed = 55f * k;
                }
                EmitRing(origin, c, 10);
            }
            Play(tickClip, 0.25f);
            AttackPulse(1f);
        }

        // Scan lines: beams light up as warnings, then fire. Phase 2 adds diagonals and a slow rotating X.
        private void Lasers(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = (Phase2 ? 1.35f : 1.6f) / k;
                bool horizontal = (burstCount++ % 2) == 0;
                int count = Phase2 ? 3 : 2;
                float span = horizontal ? arenaHalf.y : arenaHalf.x;
                // Pick lanes that leave a safe gap near (but not on) Leo.
                for (int i = 0; i < count; i++)
                {
                    float pos = Random.Range(-span * 0.85f, span * 0.85f);
                    float soulAxis = horizontal ? SoulPos.y : SoulPos.x;
                    if (i == 0) pos = Mathf.Clamp(soulAxis + Random.Range(-20f, 20f), -span * 0.85f, span * 0.85f); // one always threatens you
                    Vector2 c = horizontal ? new Vector2(0f, pos) : new Vector2(pos, 0f);
                    CreateLaser(c, horizontal ? 0f : 90f, horizontal ? arenaSize.x + 60f : arenaSize.y + 60f, 26f, 0.85f / Mathf.Sqrt(k), 0.4f, new Color(1f, 0.3f, 0.6f), false);
                }
            }
            if (second && Phase2)
            {
                secondaryTimer = 4f / k;
                Laser a = CreateLaser(Vector2.zero, 45f, arenaSize.x * 1.4f, 18f, 1.1f, 2.6f, RuntimeUI.Cyan, false);
                a.angularSpeed = 22f * spinDirection;
                Laser b = CreateLaser(Vector2.zero, 135f, arenaSize.x * 1.4f, 18f, 1.1f, 2.6f, RuntimeUI.Cyan, false);
                b.angularSpeed = 22f * spinDirection;
            }
            else if (second) secondaryTimer = 0.5f;
        }

        // A flower opens, slows, then each petal bursts into three. Phase 2 petals spin while they fly.
        private void Bloom(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 1.05f : 1.3f) / k;
            Vector2 origin = Vector2.zero;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                origin = new Vector2(Random.Range(-arenaHalf.x * 0.75f, arenaHalf.x * 0.75f), Random.Range(-arenaHalf.y * 0.6f, arenaHalf.y * 0.7f));
                if ((origin - SoulPos).magnitude > 160f) break;
            }
            int petals = 6;
            float offset = Random.Range(0f, 60f);
            for (int i = 0; i < petals; i++)
            {
                float a = (offset + i * 360f / petals) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Bullet b = Spawn(origin, dir * (200f * k), new Vector2(20, 20), 9f, new Color(1f, 0.55f, 0.8f), Shape.Circle);
                b.accel = -1.6f;
                b.minSpeed = 8f;
                b.splitAt = 0.85f;
                b.splitCount = 3;
                b.splitSpeed = 140f * k;
                if (Phase2) b.turn = 160f * spinDirection;
            }
            Bullet core = Spawn(origin, Vector2.zero, new Vector2(30, 30), 12f, new Color(1f, 0.9f, 0.4f), Shape.Star);
            core.life = 0.9f;
            core.spin = 200f;
            Play(waveStartClip, 0.25f);
            AttackPulse(1f);
        }

        // Two sine-wave serpents swim across; the head is bigger. Phase 2 adds one from the other side.
        private void Serpent(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = 0.065f / k;
                float t = GameTime.UnscaledTime;
                SerpentSegment(new Vector2(-arenaHalf.x - 20f, serpentY), new Vector2(210f * k, 0f), 70f, t, new Color(0.3f, 1f, 0.6f));
                if (Phase2) SerpentSegment(new Vector2(arenaHalf.x + 20f, serpentY2), new Vector2(-190f * k, 0f), 60f, t + 1.5f, new Color(0.4f, 0.8f, 1f));
            }
            if (second)
            {
                secondaryTimer = 2.4f / k;
                serpentY = Mathf.Clamp(SoulPos.y + Random.Range(-90f, 90f), -arenaHalf.y * 0.6f, arenaHalf.y * 0.6f);
                serpentY2 = Random.Range(-arenaHalf.y * 0.6f, arenaHalf.y * 0.6f);
            }
        }

        private void SerpentSegment(Vector2 pos, Vector2 velocity, float amp, float t, Color color)
        {
            bool head = Mathf.Repeat(t, 2.4f) < 0.07f;
            Bullet b = Spawn(pos, velocity, head ? new Vector2(24, 24) : new Vector2(13, 13), head ? 10f : 6f, head ? Color.white : color, head ? Shape.Diamond : Shape.Circle);
            b.sineAmp = amp;
            b.sineFreq = 3.2f;
            b.sinePhase = 0f;
            b.align = head;
        }

        // Shells rise, burst into falling sparks. Phase 2: two shells, rainbow sparks.
        private void Fireworks(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 0.8f : 1.0f) / k;
            int shells = Phase2 ? 2 : 1;
            for (int s = 0; s < shells; s++)
            {
                float x = Random.Range(-arenaHalf.x * 0.8f, arenaHalf.x * 0.8f);
                Bullet shell = Spawn(new Vector2(x, -arenaHalf.y - 10f), new Vector2(Random.Range(-30f, 30f), Random.Range(400f, 470f)), new Vector2(12, 18), 6f, new Color(1f, 0.95f, 0.7f), Shape.Shard);
                shell.gravity = new Vector2(0f, -330f);
                shell.align = true;
                shell.trail = true;
                shell.splitAt = Random.Range(0.85f, 1.05f);
                shell.splitCount = Phase2 ? 14 : 12;
                shell.splitSpeed = 150f * k;
                shell.damage = 3;
            }
            Play(tickClip, 0.2f);
            AttackPulse(0.8f);
        }

        // Closing doors: wall pairs with a gap slide across. Phase 2: alternating sides and a gap that drifts.
        private void Walls(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 1.0f : 1.25f) / k;
            bool fromLeft = Phase2 && (wallsFromLeft = !wallsFromLeft);
            gapY = Mathf.Clamp(gapY + Random.Range(-110f, 110f), -arenaHalf.y + 60f, arenaHalf.y - 60f);
            float gap = Phase2 ? 92f : 108f;
            float x = fromLeft ? -arenaHalf.x - 20f : arenaHalf.x + 20f;
            float speed = (fromLeft ? 1f : -1f) * 165f * k;
            Color c = new Color(0.95f, 0.75f, 0.3f);
            float topHeight = arenaHalf.y - (gapY + gap * 0.5f);
            float bottomHeight = (gapY - gap * 0.5f) + arenaHalf.y;
            if (topHeight > 4f) WallPiece(new Vector2(x, arenaHalf.y - topHeight * 0.5f), speed, topHeight, c);
            if (bottomHeight > 4f) WallPiece(new Vector2(x, -arenaHalf.y + bottomHeight * 0.5f), speed, bottomHeight, c);
            if (Phase2)
            {
                foreach (Bullet b in bullets)
                {
                    if (b.active && b.box.x > 0f && b.age < 0.01f) { b.sineAmp = 40f; b.sineFreq = 1.8f; }
                }
            }
            Play(tickClip, 0.3f);
            AttackPulse(0.7f);
        }

        private void WallPiece(Vector2 center, float speed, float height, Color color)
        {
            Bullet b = Spawn(center, new Vector2(speed, 0f), new Vector2(22, height), 0f, color, Shape.Square);
            b.box = new Vector2(11f, height * 0.5f);
            b.glow.enabled = false;
            b.damage = 3;
            b.sinePhase = Mathf.PI * 0.5f; // vertical sine (perpendicular to motion)
        }

        // Seekers chase you for a while, leaving trails. Phase 2: they split when they give up.
        private void Homing(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = (Phase2 ? 1.05f : 1.35f) / k;
                Vector2 p = RandomEdgePoint();
                Vector2 dir = (SoulPos - p).normalized;
                Bullet b = Spawn(p, dir * (165f * k), new Vector2(18, 18), 8f, new Color(1f, 0.3f, 0.3f), Shape.Diamond);
                b.homing = 1.9f;
                b.turnRate = 150f;
                b.trail = true;
                b.align = true;
                if (Phase2) { b.splitAt = 2.0f; b.splitCount = 3; b.splitSpeed = 180f * k; }
                AttackPulse(0.8f);
            }
            if (second)
            {
                secondaryTimer = 0.45f / k;
                Vector2 p = new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), arenaHalf.y + 12f);
                Spawn(p, new Vector2(0f, -120f * k), new Vector2(10, 10), 5f, new Color(1f, 0.6f, 0.6f), Shape.Circle);
            }
        }

        // Rings circle Leo, close in, then explode outward. Phase 2: two rings turning opposite ways.
        private void Orbit(float k, bool main, bool second)
        {
            if (!main) { if (second) secondaryTimer = 0.5f; return; }
            spawnTimer = (Phase2 ? 2.6f : 3.2f) / k;
            int rings = Phase2 ? 2 : 1;
            for (int r = 0; r < rings; r++)
            {
                int count = 12;
                int gap = Random.Range(0, count);
                float dir = (r == 0 ? 1f : -1f) * spinDirection;
                float radius = 230f + r * 50f;
                Color c = r == 0 ? new Color(0.75f, 0.6f, 1f) : new Color(0.5f, 1f, 0.9f);
                for (int i = 0; i < count; i++)
                {
                    if (i == gap || i == (gap + 1) % count) continue;
                    Bullet b = Spawn(orbitAnchor, Vector2.zero, new Vector2(16, 16), 7f, c, Shape.Star);
                    b.orbit = true;
                    b.orbitR = radius;
                    b.orbitA = i * 360f / count;
                    b.orbitW = 60f * dir * k;
                    b.orbitDR = -62f * k;
                    b.spin = 180f;
                    b.delay = 0.35f;
                    b.life = 9f;
                }
            }
            spinDirection = -spinDirection;
            Play(waveStartClip, 0.2f);
            AttackPulse(1f);
        }

        // Rush hour: a lane flashes, then a train tears through. Phase 2: two lanes, debris.
        private void TrainRush(float k, bool main, bool second)
        {
            if (main)
            {
                spawnTimer = (Phase2 ? 1.25f : 1.55f) / k;
                int lanes = Phase2 ? 2 : 1;
                float laneHeight = arenaSize.y / 4f;
                var used = new List<int>();
                for (int i = 0; i < lanes; i++)
                {
                    int lane;
                    if (i == 0) lane = Mathf.Clamp(Mathf.FloorToInt((SoulPos.y + arenaHalf.y) / laneHeight), 0, 3);
                    else { do lane = Random.Range(0, 4); while (used.Contains(lane)); }
                    used.Add(lane);
                    float y = -arenaHalf.y + laneHeight * (lane + 0.5f);
                    bool fromLeft = Random.value < 0.5f;
                    Laser warn = CreateLaser(new Vector2(0f, y), 0f, arenaSize.x + 40f, laneHeight - 8f, 0.9f / Mathf.Sqrt(k), 0f, new Color(1f, 0.85f, 0.2f), false);
                    warn.harmless = true;
                    float trainY = y;
                    Schedule(0.9f / Mathf.Sqrt(k), () =>
                    {
                        float x = fromLeft ? -arenaHalf.x - 160f : arenaHalf.x + 160f;
                        Bullet train = Spawn(new Vector2(x, trainY), new Vector2(fromLeft ? 760f : -760f, 0f), new Vector2(280, laneHeight - 14f), 0f, new Color(0.85f, 0.9f, 1f), Shape.Rounded);
                        train.box = new Vector2(135f, (laneHeight - 18f) * 0.5f);
                        train.damage = 6;
                        train.trail = true;
                        Shake(0.25f);
                        Play(transitionClip, 0.35f);
                        AttackPulse(1f);
                        if (Phase2)
                        {
                            for (int d = 0; d < 6; d++)
                            {
                                Bullet debris = Spawn(new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), trainY), new Vector2(Random.Range(-60f, 60f), Random.Range(120f, 220f) * (Random.value < 0.5f ? 1f : -1f)), new Vector2(10, 10), 5f, new Color(1f, 0.8f, 0.4f), Shape.Square);
                                debris.delay = 0.25f;
                                debris.spin = 400f;
                                debris.gravity = new Vector2(0f, -160f);
                            }
                        }
                    });
                }
            }
            if (second)
            {
                secondaryTimer = 0.9f / k;
                // Sparks from the rails.
                Vector2 p = new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), -arenaHalf.y - 8f);
                Bullet spark = Spawn(p, new Vector2(Random.Range(-50f, 50f), 260f), new Vector2(8, 8), 4f, new Color(1f, 0.75f, 0.3f), Shape.Diamond);
                spark.gravity = new Vector2(0f, -320f);
            }
        }

        // ================================================================== Bullets

        private Bullet Spawn(Vector2 position, Vector2 velocity, Vector2 size, float radius, Color color, Shape shape)
        {
            Bullet bullet = null;
            foreach (Bullet b in bullets)
            {
                if (!b.active) { bullet = b; break; }
            }
            if (bullet == null)
            {
                Image image = RuntimeUI.Image("Thought", arena, Color.white);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                Image glow = RuntimeUI.Image("Glow", image.rectTransform, Color.white);
                glow.sprite = HackSprites.Get(Shape.Glow);
                glow.rectTransform.anchorMin = Vector2.zero;
                glow.rectTransform.anchorMax = Vector2.one;
                glow.rectTransform.offsetMin = new Vector2(-12f, -12f);
                glow.rectTransform.offsetMax = new Vector2(12f, 12f);
                bullet = new Bullet { rect = image.rectTransform, image = image, glow = glow };
                bullets.Add(bullet);
            }

            bullet.active = true;
            bullet.grazed = false;
            bullet.basePos = bullet.position = position;
            bullet.velocity = velocity;
            bullet.radius = radius;
            bullet.box = Vector2.zero;
            bullet.spin = 0f;
            bullet.align = false;
            bullet.age = 0f;
            bullet.life = 0f;
            bullet.delay = 0f;
            bullet.aimOnLaunch = false;
            bullet.launchSpeed = 0f;
            bullet.accel = 0f;
            bullet.minSpeed = 0f;
            bullet.gravity = Vector2.zero;
            bullet.sineAmp = bullet.sineFreq = bullet.sinePhase = 0f;
            bullet.homing = bullet.turnRate = bullet.turn = 0f;
            bullet.bounces = 0;
            bullet.splitAt = 0f;
            bullet.splitCount = 0;
            bullet.splitSpeed = 0f;
            bullet.orbit = false;
            bullet.trail = false;
            bullet.trailTimer = 0f;
            bullet.damage = 3;
            bullet.size = size;
            bullet.color = color;
            bullet.shape = shape;

            bullet.image.sprite = HackSprites.Get(shape);
            bullet.image.color = Color.Lerp(color, Color.white, 0.35f);
            bullet.glow.enabled = true;
            bullet.glow.color = new Color(color.r, color.g, color.b, 0.45f);
            bullet.rect.sizeDelta = size;
            bullet.rect.localRotation = Quaternion.identity;
            bullet.rect.localScale = Vector3.one;
            bullet.rect.anchoredPosition = position;
            bullet.rect.gameObject.SetActive(true);
            soul.SetAsLastSibling();
            return bullet;
        }

        private void UpdateBullets(float dt)
        {
            const float margin = 200f;
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet b = bullets[i];
                if (!b.active) continue;
                b.age += dt;

                // Telegraph: grow in place, then (optionally) dash at where Leo is now.
                if (b.age < b.delay)
                {
                    float n = b.age / b.delay;
                    b.rect.localScale = Vector3.one * Easing.Evaluate(EaseType.EaseOutBack, Mathf.Clamp01(n * 1.4f));
                    b.image.color = Color.Lerp(Color.white, Color.Lerp(b.color, Color.white, 0.35f), Mathf.PingPong(b.age * 8f, 1f));
                    b.rect.Rotate(0f, 0f, b.spin * dt);
                    if (b.orbit) UpdateOrbit(b, dt, false);
                    continue;
                }
                if (b.delay > 0f && b.aimOnLaunch)
                {
                    b.aimOnLaunch = false;
                    b.velocity = (SoulPos - b.basePos).normalized * b.launchSpeed;
                    b.rect.localScale = Vector3.one;
                    b.trail = true;
                }

                if (b.orbit)
                {
                    UpdateOrbit(b, dt, true);
                }
                else
                {
                    if (b.homing > 0f)
                    {
                        b.homing -= dt;
                        Vector2 desired = (SoulPos - b.position).normalized * b.velocity.magnitude;
                        b.velocity = Vector3.RotateTowards(b.velocity, desired, b.turnRate * Mathf.Deg2Rad * dt, 0f);
                    }
                    if (b.turn != 0f) b.velocity = Rotate(b.velocity, b.turn * dt);
                    b.velocity += b.gravity * dt;
                    if (b.accel != 0f)
                    {
                        float speed = b.velocity.magnitude;
                        float newSpeed = Mathf.Max(b.minSpeed, speed * (1f + b.accel * dt));
                        if (speed > 0.001f) b.velocity *= newSpeed / speed;
                    }
                    b.basePos += b.velocity * dt;
                    Vector2 offset = Vector2.zero;
                    if (b.sineAmp != 0f)
                    {
                        Vector2 forward = b.velocity.sqrMagnitude > 0.01f ? b.velocity.normalized : Vector2.right;
                        Vector2 perp = new Vector2(-forward.y, forward.x);
                        offset = perp * (Mathf.Sin(b.age * b.sineFreq * Mathf.PI * 2f * 0.5f + (b.box.x > 0f ? 0f : b.sinePhase)) * b.sineAmp);
                    }
                    b.position = b.basePos + offset;
                }

                if (b.bounces > 0)
                {
                    if (Mathf.Abs(b.basePos.x) > arenaHalf.x) { b.velocity.x = -b.velocity.x; b.basePos.x = Mathf.Sign(b.basePos.x) * arenaHalf.x; b.bounces--; }
                    if (Mathf.Abs(b.basePos.y) > arenaHalf.y) { b.velocity.y = -b.velocity.y; b.basePos.y = Mathf.Sign(b.basePos.y) * arenaHalf.y; b.bounces--; }
                }

                b.rect.anchoredPosition = b.position;
                if (b.align && b.velocity.sqrMagnitude > 1f) b.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.velocity.y, b.velocity.x) * Mathf.Rad2Deg - 90f);
                else if (b.spin != 0f) b.rect.Rotate(0f, 0f, b.spin * dt);

                if (b.trail)
                {
                    b.trailTimer -= dt;
                    if (b.trailTimer <= 0f)
                    {
                        b.trailTimer = 0.035f;
                        float s = b.box.x > 0f ? b.size.y * 0.6f : Mathf.Max(b.size.x, b.size.y) * 0.7f;
                        EmitParticle(b.position, -b.velocity * 0.05f, b.color, s, 0f, 0.3f, Shape.Glow);
                    }
                }

                if (b.splitAt > 0f && b.age >= b.splitAt)
                {
                    Split(b);
                    continue;
                }
                if (b.life > 0f && b.age >= b.life)
                {
                    EmitParticle(b.position, Vector2.zero, b.color, b.size.x * 2f, 0f, 0.25f, Shape.Glow);
                    Deactivate(b);
                    continue;
                }

                if (Mathf.Abs(b.position.x) > arenaHalf.x + margin || Mathf.Abs(b.position.y) > arenaHalf.y + margin)
                {
                    Deactivate(b);
                    continue;
                }
                if (b.box.x <= 0f && (Mathf.Abs(b.position.x) > arenaHalf.x + 10f || Mathf.Abs(b.position.y) > arenaHalf.y + 10f)) continue;

                bool hit;
                float near;
                if (b.box.x > 0f)
                {
                    Vector2 d = new Vector2(Mathf.Abs(b.position.x - SoulPos.x), Mathf.Abs(b.position.y - SoulPos.y));
                    hit = d.x < b.box.x + soulRadius * 0.7f && d.y < b.box.y + soulRadius * 0.7f;
                    near = Mathf.Max(d.x - b.box.x, d.y - b.box.y);
                }
                else
                {
                    float distance = (b.position - SoulPos).magnitude;
                    hit = distance < b.radius + soulRadius;
                    near = distance - b.radius;
                }
                if (hit)
                {
                    if (invulnerable <= 0f) Hit(b);
                }
                else if (!b.grazed && near < soulRadius + grazeDistance)
                {
                    b.grazed = true;
                    grazes++;
                    Play(grazeClip, 0.25f);
                    Shake(0.05f);
                    EmitParticle(SoulPos, (SoulPos - b.position).normalized * 90f, Color.white, 10f, 2f, 0.3f, Shape.Diamond);
                }
            }
        }

        private void UpdateOrbit(Bullet b, float dt, bool move)
        {
            if (move)
            {
                b.orbitA += b.orbitW * dt;
                b.orbitR += b.orbitDR * dt;
                if (b.orbitR < 52f && b.orbitDR < 0f)
                {
                    b.orbitDR = 260f; // close in... then burst outward
                    b.orbitW *= -1.6f;
                }
                if (b.orbitR > 520f) { Deactivate(b); return; }
            }
            float a = b.orbitA * Mathf.Deg2Rad;
            Vector2 previous = b.position;
            b.position = b.basePos = orbitAnchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * b.orbitR;
            if (dt > 0f) b.velocity = (b.position - previous) / dt;
            b.rect.anchoredPosition = b.position;
        }

        private void Split(Bullet parent)
        {
            int n = Mathf.Max(1, parent.splitCount);
            float offset = Random.Range(0f, 360f);
            Vector2 at = parent.position;
            Color c = parent.color;
            bool rainbow = currentWave != null && currentWave.pattern == HackPattern.Fireworks && Phase2;
            bool fireworks = currentWave != null && currentWave.pattern == HackPattern.Fireworks;
            float speed = parent.splitSpeed > 0f ? parent.splitSpeed : 150f;
            Deactivate(parent);
            EmitParticle(at, Vector2.zero, c, 70f, 10f, 0.3f, Shape.Glow);
            for (int i = 0; i < n; i++)
            {
                float a = (offset + i * 360f / n) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Color cc = rainbow ? Color.HSVToRGB((float)i / n, 0.6f, 1f) : (fireworks ? new Color(1f, 0.8f, 0.45f) : c);
                Bullet child = Spawn(at, dir * speed, new Vector2(11, 11), 5f, cc, fireworks ? Shape.Star : Shape.Circle);
                if (fireworks) { child.gravity = new Vector2(0f, -150f); child.spin = 240f; child.accel = -0.4f; child.minSpeed = 40f; }
            }
            Play(tickClip, 0.3f);
        }

        private void Hit(Bullet b)
        {
            if (waveActive && currentWave != null && currentWave.surrender)
            {
                // The grief wave doesn't hurt. It just goes through you.
                if (b.box.x <= 0f) Deactivate(b);
                StartCoroutine(Flash(new Color(0.75f, 0.9f, 1f), 0.12f));
                return;
            }
            if (b.box.x <= 0f) Deactivate(b);
            Damage(b.damage);
        }

        private void Damage(int amount)
        {
            if (waveActive && currentWave != null && currentWave.surrender) return;
            Inventory.SetHP(Inventory.HP - amount);
            invulnerable = invulnerabilitySeconds;
            RefreshHP(false);
            Play(hitClip);
            Shake(0.35f);
            StartCoroutine(Flash(new Color(1f, 0.2f, 0.35f), 0.25f));
            ShowFloat(SoulPos + new Vector2(0f, 26f), "-" + amount, new Color(1f, 0.35f, 0.4f));
            for (int i = 0; i < 8; i++)
            {
                EmitParticle(SoulPos, Random.insideUnitCircle.normalized * Random.Range(80f, 200f), RuntimeUI.Magenta, 8f, 1f, 0.4f, Shape.Heart);
            }
        }

        private static void Deactivate(Bullet b)
        {
            b.active = false;
            b.rect.gameObject.SetActive(false);
        }

        private void ClearBullets()
        {
            foreach (Bullet b in bullets) Deactivate(b);
            foreach (Laser l in lasers) { l.active = false; l.rect.gameObject.SetActive(false); }
            foreach (Particle p in particles) { p.active = false; p.rect.gameObject.SetActive(false); }
            foreach (FloatText f in floats) { f.active = false; f.text.gameObject.SetActive(false); }
            scheduled.Clear();
        }

        private Vector2 RandomEdgePoint()
        {
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), arenaHalf.y + 15f);
                case 1: return new Vector2(Random.Range(-arenaHalf.x, arenaHalf.x), -arenaHalf.y - 15f);
                case 2: return new Vector2(-arenaHalf.x - 15f, Random.Range(-arenaHalf.y, arenaHalf.y));
                default: return new Vector2(arenaHalf.x + 15f, Random.Range(-arenaHalf.y, arenaHalf.y));
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private void Schedule(float delay, Action action) => scheduled.Add(new KeyValuePair<float, Action>(delay, action));

        private void UpdateScheduled(float dt)
        {
            for (int i = scheduled.Count - 1; i >= 0; i--)
            {
                float left = scheduled[i].Key - dt;
                if (left > 0f) { scheduled[i] = new KeyValuePair<float, Action>(left, scheduled[i].Value); continue; }
                Action action = scheduled[i].Value;
                scheduled.RemoveAt(i);
                action?.Invoke();
            }
        }

        // ================================================================== Lasers

        /// <summary>A beam: warns (thin blinking line + faint danger zone), fires (bright, deadly), fades.</summary>
        private Laser CreateLaser(Vector2 center, float angle, float length, float thickness, float warn, float fire, Color color, bool fromPivot)
        {
            Laser laser = null;
            foreach (Laser l in lasers)
            {
                if (!l.active) { laser = l; break; }
            }
            if (laser == null)
            {
                Image glow = RuntimeUI.Image("Laser", arena, Color.white);
                glow.sprite = HackSprites.Get(Shape.Rounded);
                Image core = RuntimeUI.Image("Core", glow.rectTransform, Color.white);
                core.rectTransform.anchorMin = new Vector2(0f, 0.3f);
                core.rectTransform.anchorMax = new Vector2(1f, 0.7f);
                core.rectTransform.offsetMin = core.rectTransform.offsetMax = Vector2.zero;
                laser = new Laser { rect = glow.rectTransform, glow = glow, core = core };
                lasers.Add(laser);
            }
            laser.active = true;
            laser.center = center;
            laser.angle = angle;
            laser.angularSpeed = 0f;
            laser.length = length;
            laser.thickness = thickness;
            laser.warn = warn;
            laser.fire = fire;
            laser.age = 0f;
            laser.color = color;
            laser.fromPivot = fromPivot;
            laser.harmless = false;
            laser.fired = false;
            laser.rect.anchorMin = laser.rect.anchorMax = new Vector2(0.5f, 0.5f);
            laser.rect.pivot = fromPivot ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            laser.rect.anchoredPosition = center;
            laser.rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            laser.rect.gameObject.SetActive(true);
            soul.SetAsLastSibling();
            return laser;
        }

        /// <summary>Harmless column/row highlight (for falling stacks etc.).</summary>
        private void Warn(Vector2 center, float angle, float length, float thickness, float duration, Color color)
        {
            Laser l = CreateLaser(center, angle, length, thickness, duration, 0f, color, false);
            l.harmless = true;
        }

        private void UpdateLasers(float dt)
        {
            foreach (Laser l in lasers)
            {
                if (!l.active) continue;
                l.age += dt;
                l.angle += l.angularSpeed * dt * (l.age > l.warn ? 1f : 0.25f);
                l.rect.localRotation = Quaternion.Euler(0f, 0f, l.angle);
                float fadeTime = 0.18f;
                float end = l.warn + l.fire + fadeTime;
                if (l.age >= end || (l.harmless && l.age >= l.warn)) { l.active = false; l.rect.gameObject.SetActive(false); continue; }

                if (l.age < l.warn)
                {
                    // Warning: faint zone + blinking thin core.
                    float blink = Mathf.PingPong(l.age * (l.age > l.warn * 0.6f ? 16f : 8f), 1f);
                    l.rect.sizeDelta = new Vector2(l.length, l.thickness);
                    l.glow.color = new Color(l.color.r, l.color.g, l.color.b, 0.10f + 0.08f * blink);
                    l.core.color = new Color(l.color.r, l.color.g, l.color.b, 0.35f + 0.5f * blink);
                    l.core.rectTransform.anchorMin = new Vector2(0f, 0.47f);
                    l.core.rectTransform.anchorMax = new Vector2(1f, 0.53f);
                    continue;
                }
                if (!l.fired)
                {
                    l.fired = true;
                    Play(firewallBreakClip, 0.3f);
                    Shake(0.18f);
                    AttackPulse(1f);
                }
                float fireT = l.age - l.warn;
                float width = fireT < l.fire ? 1f : 1f - (fireT - l.fire) / fadeTime;
                float pulse = 1f + Mathf.Sin(l.age * 50f) * 0.08f;
                l.rect.sizeDelta = new Vector2(l.length, l.thickness * 1.6f * width * pulse);
                l.glow.color = new Color(l.color.r, l.color.g, l.color.b, 0.55f);
                l.core.color = Color.white;
                l.core.rectTransform.anchorMin = new Vector2(0f, 0.3f);
                l.core.rectTransform.anchorMax = new Vector2(1f, 0.7f);

                if (fireT >= l.fire || invulnerable > 0f) continue;
                Vector2 dir = new Vector2(Mathf.Cos(l.angle * Mathf.Deg2Rad), Mathf.Sin(l.angle * Mathf.Deg2Rad));
                Vector2 rel = SoulPos - l.center;
                float along = Vector2.Dot(rel, dir);
                float across = Mathf.Abs(dir.x * rel.y - dir.y * rel.x);
                bool inside = l.fromPivot ? along > 0f && along < l.length : Mathf.Abs(along) < l.length * 0.5f;
                if (inside && across < l.thickness * 0.5f + soulRadius * 0.6f) Damage(4);
            }
        }

        // ================================================================== Particles and floating numbers

        private void EmitParticle(Vector2 position, Vector2 velocity, Color color, float size, float endSize, float life, Shape shape)
        {
            Particle particle = null;
            foreach (Particle p in particles)
            {
                if (!p.active) { particle = p; break; }
            }
            if (particle == null)
            {
                if (particles.Count >= 260) return;
                Image image = RuntimeUI.Image("Fx", arena, Color.white);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                particle = new Particle { rect = image.rectTransform, image = image };
                particles.Add(particle);
            }
            particle.active = true;
            particle.position = position;
            particle.velocity = velocity;
            particle.life = particle.maxLife = Mathf.Max(0.05f, life);
            particle.startSize = size;
            particle.endSize = endSize;
            particle.color = color;
            particle.image.sprite = HackSprites.Get(shape);
            particle.rect.anchoredPosition = position;
            particle.rect.sizeDelta = new Vector2(size, size);
            particle.rect.gameObject.SetActive(true);
            particle.rect.SetAsFirstSibling();
            if (arenaGrid != null) arenaGrid.rectTransform.SetAsFirstSibling();
        }

        private void EmitRing(Vector2 origin, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                EmitParticle(origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 220f, color, 12f, 2f, 0.35f, Shape.Glow);
            }
        }

        private void ShowFloat(Vector2 position, string value, Color color)
        {
            FloatText f = null;
            foreach (FloatText x in floats)
            {
                if (!x.active) { f = x; break; }
            }
            if (f == null)
            {
                TextMeshProUGUI text = RuntimeUI.Text("Float", arena, "", 30, Color.white, TextAlignmentOptions.Center);
                text.fontStyle = FontStyles.Bold;
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(120f, 40f);
                f = new FloatText { text = text };
                floats.Add(f);
            }
            f.active = true;
            f.life = 0.9f;
            f.position = position;
            f.text.text = value;
            f.text.color = color;
            f.text.gameObject.SetActive(true);
            f.text.rectTransform.SetAsLastSibling();
        }

        /// <summary>Particles, floating numbers, potion key. Runs every frame of a wave.</summary>
        private void UpdateCombatExtras(float dt)
        {
            orbitAnchor = Vector2.MoveTowards(orbitAnchor, SoulPos, 55f * dt);
            UpdateParticles(dt);

            potionCooldown -= dt;
            if (potionCooldown <= 0f && PotionPressed()) TryDrinkPotion();
        }

        private void UpdateParticles(float dt)
        {
            foreach (Particle p in particles)
            {
                if (!p.active) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.active = false; p.rect.gameObject.SetActive(false); continue; }
                float n = 1f - p.life / p.maxLife;
                p.position += p.velocity * dt;
                p.velocity *= 1f - 2.5f * dt;
                p.rect.anchoredPosition = p.position;
                float s = Mathf.Lerp(p.startSize, p.endSize, n);
                p.rect.sizeDelta = new Vector2(s, s);
                Color c = p.color;
                c.a = (1f - n) * 0.9f;
                p.image.color = c;
            }
            foreach (FloatText f in floats)
            {
                if (!f.active) continue;
                f.life -= dt;
                if (f.life <= 0f) { f.active = false; f.text.gameObject.SetActive(false); continue; }
                f.position += new Vector2(0f, 60f * dt);
                f.text.rectTransform.anchoredPosition = f.position;
                f.text.alpha = Mathf.Clamp01(f.life * 2.5f);
                f.text.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, f.life - 0.7f) * 2f);
            }
        }

        // ================================================================== Potions

        private static bool PotionPressed()
        {
            if (GameTime.Paused) return false;
            // F sits in the same place on QWERTY and AZERTY keyboards.
            bool key = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
            return key || pad;
        }

        private void TryDrinkPotion()
        {
            potionCooldown = 0.5f;
            Inventory.ItemDef best = Inventory.BestHeal();
            if (best == null)
            {
                ShowFloat(SoulPos + new Vector2(0f, 30f), Loc.T("NO ITEMS"), RuntimeUI.Dim);
                Play(wrongClip, 0.5f);
                return;
            }
            if (Inventory.HP >= Inventory.MaxHP)
            {
                ShowFloat(SoulPos + new Vector2(0f, 30f), Loc.T("HP FULL"), RuntimeUI.Dim);
                return;
            }
            int before = Inventory.HP;
            Inventory.Use(best.id);
            int healed = Inventory.HP - before;
            RefreshHP(false);
            RefreshItemHint();
            Play(resolvedClip, 0.5f);
            ShowFloat(SoulPos + new Vector2(0f, 30f), "+" + healed, new Color(0.45f, 1f, 0.55f));
            statusText.text = Loc.F("* YOU DRINK THE {0}.", Loc.T(best.name).ToUpperInvariant());
            for (int i = 0; i < 14; i++)
            {
                EmitParticle(SoulPos + Random.insideUnitCircle * 20f, new Vector2(Random.Range(-30f, 30f), Random.Range(60f, 160f)), new Color(0.45f, 1f, 0.6f), 10f, 2f, 0.7f, Shape.Glow);
            }
        }

        // ================================================================== HP, progress, banner

        private void RefreshHP(bool instant)
        {
            if (hpFill == null) return;
            fillValue = Inventory.HP / (float)Inventory.MaxHP;
            if (instant) ghostValue = fillValue;
            hpFill.sizeDelta = new Vector2(HpBarWidth * fillValue, hpFill.sizeDelta.y);
            hpFillImage.color = fillValue <= 0.25f ? new Color(1f, 0.3f, 0.35f) : new Color(1f, 0.85f, 0.25f);
            hpNumbers.text = $"{Inventory.HP} / {Inventory.MaxHP}";
            hpNumbers.color = fillValue <= 0.25f ? new Color(1f, 0.45f, 0.5f) : Color.white;
        }

        private void RefreshItemHint()
        {
            if (itemHint == null) return;
            Inventory.ItemDef best = Inventory.BestHeal();
            int total = 0;
            foreach (Inventory.ItemDef item in Inventory.Consumables()) total += Inventory.Count(item.id);
            itemHint.text = best == null
                ? "<color=#8A8F99>" + Loc.T("[F] ITEM — NONE LEFT") + "</color>"
                : Loc.F("[F] {0}", Loc.T(best.name)) + $"  <color=#FFD84F>x{total}</color>";
        }

        private void SetProgress(float value, string label)
        {
            if (progressFill == null) return;
            progressFill.sizeDelta = new Vector2(arenaSize.x * Mathf.Clamp01(value), progressFill.sizeDelta.y);
            progressLabel.text = label;
        }

        private IEnumerator WaveBanner(string name)
        {
            if (bannerText == null) yield break;
            bannerText.text = Loc.T(name);
            bannerText.gameObject.SetActive(true);
            RectTransform r = bannerText.rectTransform;
            Vector2 home = new Vector2(0f, arenaCenterY);
            for (float t = 0f; t < 0.25f; t += GameTime.UnscaledDelta)
            {
                float e = Easing.Evaluate(EaseType.EaseOutBack, t / 0.25f);
                r.anchoredPosition = home + new Vector2(Mathf.Lerp(-500f, 0f, e), 0f);
                bannerText.alpha = t / 0.25f;
                r.localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1f, e), 1f);
                yield return null;
            }
            for (float t = 0f; t < 0.7f; t += GameTime.UnscaledDelta)
            {
                r.anchoredPosition = home + new Vector2(t * 30f, 0f);
                bannerText.characterSpacing = 6f + t * 10f;
                yield return null;
            }
            for (float t = 0f; t < 0.25f; t += GameTime.UnscaledDelta)
            {
                r.anchoredPosition = home + new Vector2(21f + Mathf.Lerp(0f, 600f, t / 0.25f), 0f);
                bannerText.alpha = 1f - t / 0.25f;
                yield return null;
            }
            bannerText.gameObject.SetActive(false);
        }

        /// <summary>Ambient motion of the decor (grid scroll, brackets pulse, HP ghost bar). Called from Update.</summary>
        private void UpdateDecor(float dt)
        {
            float t = GameTime.UnscaledTime;
            if (arenaGrid != null)
            {
                Rect uv = arenaGrid.uvRect;
                uv.x = Mathf.Repeat(uv.x + dt * 0.08f, 1f);
                uv.y = Mathf.Repeat(uv.y + dt * 0.15f, 1f);
                arenaGrid.uvRect = uv;
            }
            if (backdropLines != null)
            {
                Rect uv = backdropLines.uvRect;
                uv.x = Mathf.Repeat(uv.x + dt * 0.25f, 1f);
                backdropLines.uvRect = uv;
                Color c = backdropLines.color;
                c.a = background.color.a * 0.6f;
                backdropLines.color = c;
            }
            float pulse = 0.6f + 0.4f * Mathf.Sin(t * 4f) + attackPulse * 0.4f;
            foreach (Image b in brackets)
            {
                Color c = b.color;
                c.a = Mathf.Clamp01(pulse);
                b.color = c;
            }
            if (arenaGlow != null) arenaGlow.rectTransform.localScale = Vector3.one * (1f + attackPulse * 0.04f);
            if (hpGhost != null)
            {
                ghostValue = Mathf.MoveTowards(ghostValue, fillValue, dt * (ghostValue > fillValue ? 0.5f : 5f));
                hpGhost.sizeDelta = new Vector2(HpBarWidth * Mathf.Max(ghostValue, fillValue), hpGhost.sizeDelta.y);
            }
            if (!waveActive && particles.Count > 0) UpdateParticles(dt);
        }

        // ================================================================== UI construction

        private void BuildArenaDecor(RectTransform contentRect, Image arenaImage)
        {
            // Full-screen speed lines behind everything.
            var linesRect = RuntimeUI.Rect("BackdropLines", canvas.transform);
            RuntimeUI.Stretch(linesRect);
            linesRect.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            backdropLines = linesRect.gameObject.AddComponent<RawImage>();
            backdropLines.texture = HackSprites.Lines();
            backdropLines.uvRect = new Rect(0f, 0f, 30f, 17f);
            backdropLines.color = new Color(RuntimeUI.Magenta.r, RuntimeUI.Magenta.g, RuntimeUI.Magenta.b, 0f);
            backdropLines.raycastTarget = false;

            // Soft glow around the arena.
            arenaGlow = RuntimeUI.Image("ArenaGlow", contentRect, new Color(0.2f, 1f, 0.95f, 0.18f));
            arenaGlow.sprite = HackSprites.Get(Shape.Glow);
            RuntimeUI.Place(arenaGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), arenaBasePosition, arenaSize * 1.45f);
            arenaGlow.rectTransform.SetSiblingIndex(arena.GetSiblingIndex());

            arenaImage.color = new Color(0.025f, 0.035f, 0.065f, 1f);

            // Scrolling grid floor inside the arena.
            var gridRect = RuntimeUI.Rect("Grid", arena);
            RuntimeUI.Stretch(gridRect);
            arenaGrid = gridRect.gameObject.AddComponent<RawImage>();
            arenaGrid.texture = HackSprites.Grid();
            arenaGrid.uvRect = new Rect(0f, 0f, arenaSize.x / 32f, arenaSize.y / 32f);
            arenaGrid.color = new Color(0.4f, 0.9f, 1f, 0.35f);
            arenaGrid.raycastTarget = false;

            // Corner brackets.
            Vector2[] corners = { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
            foreach (Vector2 c in corners)
            {
                for (int i = 0; i < 2; i++)
                {
                    Image bar = RuntimeUI.Image("Bracket", contentRect, RuntimeUI.Cyan);
                    Vector2 size = i == 0 ? new Vector2(34f, 5f) : new Vector2(5f, 34f);
                    Vector2 pos = arenaBasePosition + new Vector2(c.x * (arenaHalf.x + 10f), c.y * (arenaHalf.y + 10f));
                    pos += i == 0 ? new Vector2(-c.x * 14.5f, 0f) : new Vector2(0f, -c.y * 14.5f);
                    RuntimeUI.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
                    brackets.Add(bar);
                }
            }

            // Wave banner.
            bannerText = RuntimeUI.Text("Banner", contentRect, "", 64, Color.white, TextAlignmentOptions.Center);
            bannerText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            bannerText.characterSpacing = 6f;
            bannerText.outlineWidth = 0.25f;
            bannerText.outlineColor = new Color32(255, 79, 216, 255);
            RuntimeUI.Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, arenaCenterY), new Vector2(1400f, 90f));
            bannerText.gameObject.SetActive(false);
        }

        private void BuildCombatUI(RectTransform contentRect, float rowY)
        {
            // HP row (left, under the arena): LEO   HP [██████░░]  14 / 20
            RectTransform row = RuntimeUI.Rect("HPRow", contentRect);
            RuntimeUI.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-arenaHalf.x, rowY), new Vector2(520f, 32f));
            TextMeshProUGUI name = RuntimeUI.Text("Name", row, "LEO", 26, Color.white, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            RuntimeUI.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(70f, 32f));
            TextMeshProUGUI hpLabel = RuntimeUI.Text("HP", row, "HP", 18, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.MidlineLeft);
            RuntimeUI.Place(hpLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(78f, 0f), new Vector2(40f, 32f));
            Image barBack = RuntimeUI.Image("HPBack", row, new Color(0.55f, 0.08f, 0.15f, 1f));
            RuntimeUI.Place(barBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(112f, 0f), new Vector2(HpBarWidth, 22f));
            Outline barOutline = barBack.gameObject.AddComponent<Outline>();
            barOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            barOutline.effectDistance = new Vector2(2, -2);
            Image ghost = RuntimeUI.Image("HPGhost", barBack.transform, new Color(1f, 1f, 1f, 0.85f));
            hpGhost = ghost.rectTransform;
            RuntimeUI.Place(hpGhost, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(HpBarWidth, 22f));
            hpFillImage = RuntimeUI.Image("HPFill", barBack.transform, new Color(1f, 0.85f, 0.25f));
            hpFill = hpFillImage.rectTransform;
            RuntimeUI.Place(hpFill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(HpBarWidth, 22f));
            Image shine = RuntimeUI.Image("Shine", hpFill, new Color(1f, 1f, 1f, 0.25f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0.6f);
            shine.rectTransform.anchorMax = new Vector2(1f, 0.9f);
            shine.rectTransform.offsetMin = shine.rectTransform.offsetMax = Vector2.zero;
            hpNumbers = RuntimeUI.Text("Numbers", row, "", 24, Color.white, TextAlignmentOptions.MidlineLeft);
            RuntimeUI.Place(hpNumbers.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(112f + HpBarWidth + 14f, 0f), new Vector2(140f, 32f));

            // Item hint (right, under the arena).
            itemHint = RuntimeUI.Text("ItemHint", contentRect, "", 22, Color.white, TextAlignmentOptions.MidlineRight);
            RuntimeUI.Place(itemHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(arenaHalf.x, rowY), new Vector2(460f, 32f));

            // Decryption progress: a thin bar on the arena's top edge + label inside the top-right corner.
            Image progressBack = RuntimeUI.Image("ProgressBack", contentRect, new Color(1f, 1f, 1f, 0.08f));
            RuntimeUI.Place(progressBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-arenaHalf.x, arenaCenterY + arenaHalf.y + 6f), new Vector2(arenaSize.x, 6f));
            Image progressImage = RuntimeUI.Image("ProgressFill", progressBack.transform, RuntimeUI.Cyan);
            progressFill = progressImage.rectTransform;
            RuntimeUI.Place(progressFill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 6f));
            progressLabel = RuntimeUI.Text("ProgressLabel", arena, "", 16, new Color(0.6f, 0.95f, 1f, 0.75f), TextAlignmentOptions.TopRight);
            RuntimeUI.Place(progressLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(300f, 22f));
        }

        // ================================================================== Names

        private static Color PatternColor(HackPattern pattern)
        {
            switch (pattern)
            {
                case HackPattern.PaperRain: return new Color(0.95f, 0.95f, 0.9f);
                case HackPattern.ClockHands: return RuntimeUI.Cyan;
                case HackPattern.Crossfire: return RuntimeUI.Magenta;
                case HackPattern.Spiral: return RuntimeUI.Lilac;
                case HackPattern.Aimed: return new Color(1f, 0.4f, 0.4f);
                case HackPattern.Ripple: return new Color(0.45f, 0.75f, 1f);
                case HackPattern.Lasers: return new Color(1f, 0.3f, 0.6f);
                case HackPattern.Bloom: return new Color(1f, 0.6f, 0.85f);
                case HackPattern.Serpent: return new Color(0.3f, 1f, 0.6f);
                case HackPattern.Fireworks: return new Color(1f, 0.8f, 0.4f);
                case HackPattern.Walls: return new Color(0.95f, 0.75f, 0.3f);
                case HackPattern.Homing: return new Color(1f, 0.3f, 0.3f);
                case HackPattern.Orbit: return new Color(0.75f, 0.6f, 1f);
                case HackPattern.TrainRush: return new Color(1f, 0.85f, 0.2f);
                default: return RuntimeUI.Cyan;
            }
        }

        private static string WaveName(HackPattern pattern)
        {
            switch (pattern)
            {
                case HackPattern.PaperRain: return "PAPER STORM";
                case HackPattern.ClockHands: return "OVERTIME CLOCK";
                case HackPattern.Crossfire: return "NOTIFICATION HELL";
                case HackPattern.Spiral: return "THOUGHT LOOP";
                case HackPattern.Aimed: return "SELF-DOUBT";
                case HackPattern.Ripple: return "RIPPLES";
                case HackPattern.Lasers: return "SCAN LINES";
                case HackPattern.Bloom: return "BLOOM";
                case HackPattern.Serpent: return "DATA SERPENTS";
                case HackPattern.Fireworks: return "LAST SUMMER'S FIREWORKS";
                case HackPattern.Walls: return "CLOSING DOORS";
                case HackPattern.Homing: return "SEEKERS";
                case HackPattern.Orbit: return "CLOSING IN";
                case HackPattern.TrainRush: return "RUSH HOUR";
                default: return "INCOMING";
            }
        }

        private static string WaveFlavor(HackPattern pattern)
        {
            switch (pattern)
            {
                case HackPattern.PaperRain: return "INCOMING: UNREAD PAPERWORK. DODGE.";
                case HackPattern.ClockHands: return "INCOMING: TICKING CLOCK HANDS. DON'T LOOK AT THE TIME.";
                case HackPattern.Crossfire: return "INCOMING: NOTIFICATIONS FROM BOTH SIDES.";
                case HackPattern.Spiral: return "INCOMING: A LOOPING THOUGHT. IT KEEPS COMING BACK.";
                case HackPattern.Aimed: return "INCOMING: DOUBTS. THEY KNOW WHERE YOU ARE.";
                case HackPattern.Ripple: return "INCOMING: RIPPLES OF STATIC.";
                case HackPattern.Lasers: return "SCANNING... READ THE WARNING LINES.";
                case HackPattern.Bloom: return "SOMETHING IS OPENING. GIVE IT ROOM.";
                case HackPattern.Serpent: return "DATA STREAMS. FOLLOW THE WAVE, NOT THE HEAD.";
                case HackPattern.Fireworks: return "WHAT GOES UP COMES DOWN IN PIECES.";
                case HackPattern.Walls: return "THE DOORS ARE CLOSING. FIND THE GAP.";
                case HackPattern.Homing: return "THEY FOLLOW YOU. KEEP MOVING.";
                case HackPattern.Orbit: return "IT'S CIRCLING. LOOK FOR THE OPENING.";
                case HackPattern.TrainRush: return "WATCH THE LANES. TRAINS DON'T STOP.";
                default: return "INCOMING.";
            }
        }
    }
}
