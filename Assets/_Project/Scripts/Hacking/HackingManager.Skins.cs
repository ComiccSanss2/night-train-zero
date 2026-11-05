using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shape = Subnet.Hacking.HackSprites.Shape;

namespace Subnet.Hacking
{
    /// <summary>
    /// Hand-made bullet art, rendered in Blender (Resources/HackSkins). Each enemy dresses its own attacks:
    /// the Commuter throws paperwork, late folders, clock hands and a whole subway car; the Weeper cries
    /// tears, petals and stone shards; the Purge fires error icons, erasers, deleted files and walls of VHS tapes;
    /// the Algorithm sends cursors, notifications, data serpents and rockets.
    /// File name: {Enemy}_{Pattern}_{Shape}[W|T]  (W = wide variant, T = tiled wall). Missing file → plain shape.
    /// </summary>
    public partial class HackingManager
    {
        private static readonly Dictionary<string, Sprite> skinCache = new Dictionary<string, Sprite>();
        private string skinPrefix = string.Empty;

        private static string SkinPrefixFor(HackableObstacle target)
        {
            if (target == null) return string.Empty;
            string n = target.gameObject.name;
            if (n.Contains("Overtime") || n.Contains("Commuter_Wall")) return "Commuter";
            if (n.Contains("Weeping")) return "Weeper";
            if (n.Contains("Purge")) return "Purge";
            if (n.Contains("MemoryFile") || n.Contains("Algorithm")) return "Algorithm";
            return string.Empty;
        }

        private static Sprite LoadSkin(string key)
        {
            if (skinCache.TryGetValue(key, out Sprite s)) return s;
            s = Resources.Load<Sprite>("HackSkins/" + key);
            skinCache[key] = s;
            return s;
        }

        /// <summary>Applies the enemy's art to a freshly spawned bullet. Returns true if a skin was found.</summary>
        private bool ApplySkin(Bullet bullet, Shape shape, Vector2 size)
        {
            Image image = bullet.image;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            bullet.skinned = false;
            if (string.IsNullOrEmpty(skinPrefix) || currentWave == null) return false;

            string baseKey = skinPrefix + "_" + currentWave.pattern + "_" + shape;
            Sprite skin = null;
            bool tiled = false;
            if (size.y > size.x * 1.2f) { skin = LoadSkin(baseKey + "T"); tiled = skin != null; }
            if (skin == null && size.x > size.y * 1.25f && size.x < 100f) skin = LoadSkin(baseKey + "W");
            if (skin == null) skin = LoadSkin(baseKey);
            if (skin == null) return false;

            bullet.skinned = true;
            image.sprite = skin;
            image.color = Color.white;
            if (tiled)
            {
                image.type = Image.Type.Tiled;
                image.pixelsPerUnitMultiplier = skin.rect.width / Mathf.Max(1f, size.x);
                bullet.rect.sizeDelta = size;
            }
            else
            {
                image.preserveAspect = true;
                // Small bullets get a little more room so the art reads (hitboxes don't change).
                bullet.rect.sizeDelta = size.x < 60f ? size * 1.3f : size;
            }
            bullet.glow.color = new Color(bullet.color.r, bullet.color.g, bullet.color.b, 0.28f);
            return true;
        }
    }
}
