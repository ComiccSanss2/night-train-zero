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
            { "M_Water", new Color(0.05f, 0.28f, 0.42f) * 0.9f },
        };

        /// <summary>
        /// Spawns an enemy model standing at <paramref name="worldPos"/>, <paramref name="height"/> metres tall,
        /// rotated by <paramref name="yaw"/> (the models face +Z; -90 faces Leo, who arrives from -X).
        /// Returns a holder object (safe to scale/twitch), or null if the FBX isn't there yet.
        /// </summary>
        private static GameObject EnemyModel(string name, Transform parent, Vector3 worldPos, float height, float yaw)
            => BlenderModel(EnemyFolder, name, parent, worldPos, height, yaw, 30f);

        private const string PropFolder = Root + "/Art/Models/Props";

        /// <summary>
        /// A hand-made Blender prop (Art/Models/Props), same rules as the enemies: looping baked animation, URP
        /// materials, "FacePoint" turned toward <paramref name="yaw"/> (no camera nudge).
        /// </summary>
        private static GameObject PropModel(string name, Transform parent, Vector3 worldPos, float height, float yaw)
            => BlenderModel(PropFolder, name, parent, worldPos, height, yaw, 0f);

        private static GameObject BlenderModel(string folder, string name, Transform parent, Vector3 worldPos, float height, float yaw, float cameraNudge)
        {
            string path = folder + "/" + name + ".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogWarning("[Subnet] Blender model missing: " + path);
                ReportLine("missing blender model: " + path);
                return null;
            }
            ConfigureEnemyImport(importer);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            var holder = new GameObject(name.StartsWith("Enemy_") ? name : "Model_" + name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = worldPos;
            // Keep the FBX root's own rotation (axis conversion): overwriting it laid the Weeper on its side.
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(holder.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = asset.transform.localRotation;
            model.transform.localScale = Vector3.one;
            Bounds mb = WorldBounds(model);
            if (mb.size.y > 0.0001f) model.transform.localScale = Vector3.one * (height / mb.size.y);
            mb = WorldBounds(model);
            model.transform.position += new Vector3(worldPos.x - mb.center.x, worldPos.y - mb.min.y, worldPos.z - mb.center.z);
            if (Vector3.Dot(model.transform.up, Vector3.up) < 0.5f && Vector3.Dot(model.transform.forward, Vector3.up) > 0.5f)
                ReportLine($"enemy {name}: root is tilted (up {model.transform.up})");

            // Each model has a "FacePoint" empty two metres in front of its face. Turn the holder so that point
            // lies in the 'yaw' direction, nudged 30 degrees toward the isometric camera so we see the face, not a profile.
            Transform facePoint = FindDeep(model.transform, "FacePoint");
            if (facePoint != null)
            {
                Vector3 current = facePoint.position - holder.transform.position;
                current.y = 0f;
                Vector3 wanted = Quaternion.Euler(0f, yaw - cameraNudge, 0f) * Vector3.forward;
                if (current.sqrMagnitude > 0.0001f)
                    holder.transform.rotation = Quaternion.Euler(0f, Vector3.SignedAngle(current, wanted, Vector3.up), 0f) * holder.transform.rotation;
            }
            else holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

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

        /// <summary>
        /// Freezes a character in one frame of one of its own animations (a statue): samples the clip onto the
        /// model, then switches the Animator off so the pose is saved in the scene.
        /// </summary>
        private static void PoseStatue(GameObject characterRoot, string modelName, string hint, string clipName, float at = 0.5f)
        {
            GameObject asset = Model(modelName, hint);
            if (asset == null) return;
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.ToLowerInvariant() == clipName);
            Animator animator = characterRoot.GetComponentInChildren<Animator>();
            if (clip == null || animator == null) { ReportLine($"statue pose: no clip '{clipName}' on {modelName}"); return; }
            // A one-state controller frozen on one frame of the clip (speed 0): the pose holds at runtime.
            string folder = Root + "/Art/Animations/Enemies";
            EnsureFolder(folder);
            string path = folder + "/AC_Statue_" + modelName + "_" + clipName + ".controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            AnimatorState state = controller.layers[0].stateMachine.defaultState;
            if (state != null)
            {
                state.speed = 0f;
                state.cycleOffset = at;
            }
            animator.runtimeAnimatorController = controller;
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clip.SampleAnimation(animator.gameObject, clip.length * at); // also right in the editor view
        }

        /// <summary>
        /// The Weeper is the commuter herself, turned to stone (the same woman you free afterwards): the hand-made
        /// statue figure is swapped for her model in stone, kneeling on the Blender pedestal among the falling tears.
        /// </summary>
        private static void WeeperIsHer(Ctx ctx, GameObject weeper, Material stone, Material tearMat)
        {
            if (weeper == null) return;
            Transform figure = FindDeep(weeper.transform, "Figure");
            if (figure != null) figure.gameObject.SetActive(false);
            Transform top = FindDeep(weeper.transform, "PedestalTop");
            Renderer topR = top != null ? top.GetComponentInChildren<Renderer>() : null;
            Bounds all = WorldBounds(weeper);
            float topY = topR != null ? topR.bounds.max.y : all.min.y + 0.45f;
            Vector3 c = topR != null ? topR.bounds.center : all.center;
            GameObject her = Character(ctx, "WeeperStone", "character-female-d", "mini-characters", new Vector3(c.x, topY - 0.02f, c.z), -120f, 1.25f, stone, false, weeper.transform);
            Object.DestroyImmediate(her.GetComponent<Collider>());
            her.transform.rotation = Quaternion.Euler(0f, -120f, 0f);
            PoseStatue(her, "character-female-d", "mini-characters", "crouch", 0.6f);
            Bounds hb = WorldBounds(her);
            PixelTears(weeper.transform, new Vector3(hb.center.x, hb.max.y - hb.size.y * 0.3f, hb.center.z), tearMat, 26f);
        }

        private static Transform FindDeep(Transform t, string childName)
        {
            if (t.name == childName) return t;
            foreach (Transform c in t)
            {
                Transform found = FindDeep(c, childName);
                if (found != null) return found;
            }
            return null;
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
