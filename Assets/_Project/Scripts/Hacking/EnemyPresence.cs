using Subnet.Core;
using TMPro;
using UnityEngine;

namespace Subnet.Hacking
{
    /// <summary>
    /// Makes a hackable obstacle read as an enemy in the overworld: a glowing glyph ring on the floor,
    /// shards orbiting it, a glitching name tag (COMMUTER_0451.exe), a flickering light and a body that twitches
    /// every few seconds. Everything grows tenser as Leo gets close, and folds away once the obstacle is resolved.
    /// </summary>
    public class EnemyPresence : MonoBehaviour
    {
        [SerializeField] private HackableObstacle obstacle;
        [Tooltip("The visible body that twitches (the paper wall, the statue...).")]
        [SerializeField] private Transform body;
        [SerializeField] private Material auraMaterial;
        [SerializeField] private Material innerMaterial;
        [SerializeField] private Color color = new Color(1f, 0.31f, 0.85f);
        [SerializeField] private Vector3 center = Vector3.zero;
        [SerializeField] private float radius = 1.4f;
        [SerializeField] private float tagHeight = 3.2f;
        [SerializeField] private TMP_FontAsset font;

        private Transform root;
        private Transform ring;
        private Transform[] shards;
        private TextMeshPro nameTag;
        private Light glow;
        private Transform player;
        private Vector3 bodyBase;
        private float twitchTimer;
        private float twitch;
        private float fold = 1f;
        private string tagText;

        private void Start()
        {
            if (obstacle == null) obstacle = GetComponent<HackableObstacle>();
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            if (body != null) bodyBase = body.localPosition;
            twitchTimer = Random.Range(1.5f, 3.5f);

            root = new GameObject("EnemyPresence_FX").transform;
            root.SetParent(transform, false);
            root.localPosition = center;

            // Floor ring: a flat disc with a darker inner disc (reads as a glyph circle from the iso camera).
            // The ring is an empty pivot: the discs are its children, so pulsing it never stretches them
            // (scaling the disc itself turned it into a 2 m tall cylinder hiding the enemy).
            ring = new GameObject("Ring").transform;
            ring.SetParent(root, false);
            Disc("Outer", radius, 0.02f, auraMaterial).transform.SetParent(ring, false);
            Disc("Inner", radius * 0.82f, 0.025f, innerMaterial != null ? innerMaterial : auraMaterial).transform.SetParent(ring, false);

            shards = new Transform[6];
            for (int i = 0; i < shards.Length; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(s.GetComponent<Collider>());
                s.name = "Shard";
                s.transform.SetParent(root, false);
                s.transform.localScale = new Vector3(0.08f, 0.22f, 0.08f);
                if (auraMaterial != null) s.GetComponent<Renderer>().sharedMaterial = auraMaterial;
                shards[i] = s.transform;
            }

            var tagGo = new GameObject("NameTag");
            tagGo.transform.SetParent(root, false);
            tagGo.transform.localPosition = new Vector3(0f, tagHeight, 0f);
            nameTag = tagGo.AddComponent<TextMeshPro>();
            if (font != null) nameTag.font = font;
            string title = obstacle != null ? obstacle.encounterTitle : name;
            int cut = title.IndexOf("//", System.StringComparison.Ordinal);
            tagText = (cut > 0 ? title.Substring(0, cut) : title).Trim();
            nameTag.text = tagText;
            nameTag.fontSize = 2.4f;
            nameTag.alignment = TextAlignmentOptions.Center;
            nameTag.color = color;
            nameTag.fontStyle = FontStyles.Bold;
            nameTag.textWrappingMode = TextWrappingModes.NoWrap;
            nameTag.rectTransform.sizeDelta = new Vector2(8f, 1f);
            nameTag.outlineWidth = 0.2f;
            nameTag.outlineColor = new Color32(10, 5, 20, 255);

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = radius * 3f;
            glow.intensity = 1f;
        }

        private GameObject Disc(string discName, float r, float height, Material material)
        {
            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(d.GetComponent<Collider>());
            d.name = discName;
            d.transform.SetParent(root, false);
            d.transform.localPosition = new Vector3(0f, height, 0f);
            d.transform.localScale = new Vector3(r * 2f, 0.005f, r * 2f);
            if (material != null) d.GetComponent<Renderer>().sharedMaterial = material;
            return d;
        }

        private void Update()
        {
            if (root == null) return;
            bool resolved = obstacle != null && obstacle.IsResolved;
            fold = Mathf.MoveTowards(fold, resolved ? 0f : 1f, Time.deltaTime * 1.5f);
            if (fold <= 0f) { root.gameObject.SetActive(false); enabled = false; return; }
            root.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, fold);

            float t = Time.time;
            float near = 0f;
            if (player != null)
            {
                float d = Vector3.Distance(player.position, transform.position + center);
                near = Mathf.Clamp01(1f - (d - 1.5f) / 5f);
            }
            bool fighting = HackingManager.Instance != null && HackingManager.Instance.IsActive;

            ring.localRotation = Quaternion.Euler(0f, t * (20f + near * 60f), 0f);
            float pulse = 1f + Mathf.Sin(t * (2f + near * 4f)) * 0.04f;
            ring.localScale = new Vector3(pulse, 1f, pulse);

            for (int i = 0; i < shards.Length; i++)
            {
                float a = t * (0.8f + near * 1.5f) + i * Mathf.PI * 2f / shards.Length;
                float h = 0.6f + Mathf.Sin(t * 1.7f + i) * 0.25f + (i % 2) * 0.9f;
                shards[i].localPosition = new Vector3(Mathf.Cos(a) * radius * 0.9f, h, Mathf.Sin(a) * radius * 0.9f);
                shards[i].localRotation = Quaternion.Euler(t * 90f + i * 40f, t * 140f, 0f);
            }

            glow.intensity = (0.8f + near * 1.6f) * (Random.value < 0.04f ? 0.3f : 1f);

            // Name tag: always faces the camera; letters glitch now and then, more when Leo is close.
            Camera cam = Camera.main;
            if (cam != null) nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - cam.transform.position);
            nameTag.alpha = Mathf.Lerp(0.55f, 1f, near) * (fighting ? 0f : 1f);
            if (Random.value < 0.02f + near * 0.06f)
            {
                char[] chars = tagText.ToCharArray();
                int k = Random.Range(0, chars.Length);
                if (chars.Length > 0 && chars[k] != ' ') chars[k] = (char)Random.Range(33, 90);
                nameTag.text = new string(chars);
            }
            else if (Random.value < 0.2f) nameTag.text = tagText;

            // The body twitches like a skipping video frame.
            if (body != null && !fighting)
            {
                twitchTimer -= Time.deltaTime;
                if (twitchTimer <= 0f)
                {
                    twitchTimer = Random.Range(1.2f, 3.5f) * (1f - near * 0.6f);
                    twitch = 0.12f;
                }
                if (twitch > 0f)
                {
                    twitch -= Time.deltaTime;
                    body.localPosition = bodyBase + new Vector3(Random.Range(-0.06f, 0.06f), 0f, Random.Range(-0.06f, 0.06f));
                }
                else body.localPosition = bodyBase;
            }
        }
    }
}
