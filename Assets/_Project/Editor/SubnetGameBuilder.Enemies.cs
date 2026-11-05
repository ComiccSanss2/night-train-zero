using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    /// <summary>
    /// The four hand-made enemy models (Blender, Art/Models/Enemies): Commuter, Weeper, Purge, Algorithm.
    /// Each FBX carries one baked 4-second loop. Here we make the clip loop, give it a one-state animator that keeps
    /// playing during fights (unscaled time), and swap the Blender materials for URP ones (glows become emissive).
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        private const string EnemyFolder = Root + "/Art/Models/Enemies";

        // Blender material name → emission colour (base colours are read from the imported material).
        private static readonly Dictionary<string, Color> EnemyGlows = new Dictionary<string, Color>
        {
            { "M_Glow_Cyan", new Color(0.25f, 1f, 0.95f) * 3f },
            { "M_Glow_Magenta", new Color(1f, 0.25f, 0.85f) * 3.5f },
            { "M_Glow_White", new Color(0.95f, 0.97f, 1f) * 3f },
            { "M_Glow_Red", new Color(1f, 0.15f, 0.2f) * 3f },
            { "M_Glow_Amber", new Color(1f, 0.65f, 0.2f) * 3f },
            { "M_Glow_Screen", new Color(0.05f, 0.35f, 0.4f) * 1.5f },
            { "M_Static", new Color(0.7f, 0.7f, 0.75f) * 0.8f },
        };

        /// <summary>
        /// Spawns an enemy model standing at <paramref name="worldPos"/>, <paramref name="height"/> metres tall,
        /// rotated by <paramref name="yaw"/> (the models face +Z; -90 faces Leo, who arrives from -X).
        /// Returns a holder object (safe to scale/twitch), or null if the FBX isn't there yet.
        /// </summary>
        private static GameObject EnemyModel(string name, Transform parent, Vector3 worldPos, float height, float yaw)
        {
            string path = EnemyFolder + "/" + name + ".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogWarning("[Subnet] Enemy model missing: " + path);
                return null;
            }
            ConfigureEnemyImport(importer);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            var holder = new GameObject("Enemy_" + name.Replace("Enemy_", string.Empty));
            holder.transform.SetParent(parent, false);
            holder.transform.position = worldPos;
            // The FBX comes out of Blender facing backwards (+180 puts the face where 'yaw' says).
            GameObject model = SpawnModel(asset, holder.transform, height, false, yaw + 180f);

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = EnemyMaterial(name, mats[i]);
                r.sharedMaterials = mats;
            }

            if (clip != null)
            {
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = EnemyController(name, clip);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime; // keeps moving while the fight pauses the world
            }
            return holder;
        }

        private static void ConfigureEnemyImport(ModelImporter importer)
        {
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.importCameras || importer.importLights)
            {
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool loopChanged = false;
            foreach (ModelImporterClipAnimation c in clips)
            {
                if (c.loopTime) continue;
                c.loopTime = true; // the Blender loop is already seamless
                loopChanged = true;
            }
            if (loopChanged || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static RuntimeAnimatorController EnemyController(string name, AnimationClip clip)
        {
            string folder = Root + "/Art/Animations/Enemies";
            EnsureFolder(folder);
            string path = folder + "/AC_" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        private static Material EnemyMaterial(string model, Material source)
        {
            if (source == null) return null;
            string key = source.name.Replace(" (Instance)", string.Empty);
            int dot = key.IndexOf('.');
            if (dot > 0) key = key.Substring(0, dot); // "M_Glow_Cyan.001" → "M_Glow_Cyan"

            Color baseColor = Color.gray;
            if (source.HasProperty("_BaseColor")) baseColor = source.GetColor("_BaseColor");
            else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");
            baseColor.a = 1f;

            string matName = "Enemies/" + model + "_" + key;
            if (EnemyGlows.TryGetValue(key, out Color glow))
                return EmissiveMaterial(matName, Color.Lerp(baseColor, Color.black, 0.3f), glow);

            bool shiny = key.Contains("Metal") || key.Contains("Crystal") || key.Contains("Obsidian") || key.Contains("Tape");
            Material material = LitMaterial(matName, baseColor, shiny ? 0.65f : 0.2f);
            if (key.Contains("Metal")) material.SetFloat("_Metallic", 0.7f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
