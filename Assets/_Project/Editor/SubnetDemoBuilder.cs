#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Subnet.Audio;
using Subnet.CameraSystem;
using Subnet.Cinematics;
using Subnet.Core;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Hacking;
using Subnet.Interaction;
using Subnet.Player;
using Subnet.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Tools → Subnet → Build Full Game.
    /// Generates every scene of NIGHT TRAIN ZERO from the runtime scripts and the Kenney kits:
    ///   00_MainMenu, 01_Tenement, 02_Station, 03_Subnet_Commercial, 04_ConcretePark, 05_CentralCore.
    /// Re-running rebuilds those scenes from scratch (other scenes are untouched).
    /// This file holds the entry point and shared helpers; each act lives in its own partial file.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenesFolder = Root + "/Scenes/";

        public const string MenuScene = "00_MainMenu";
        public const string TenementScene = "01_Tenement";
        public const string StationScene = "02_Station";
        public const string SubnetScene = "03_Subnet_Commercial";
        public const string ParkScene = "04_ConcretePark";
        public const string CoreScene = "05_CentralCore";

        private static readonly Color Cyan = new Color(0.2f, 1f, 0.95f);
        private static readonly Color Magenta = new Color(1f, 0.31f, 0.85f);
        private static readonly Color Lilac = new Color(0.73f, 0.65f, 1f);
        private static readonly Color Dim = new Color(0.54f, 0.56f, 0.6f);
        private static readonly Color Panel = new Color(0.043f, 0.059f, 0.078f);
        private static readonly Color Warm = new Color(1f, 0.75f, 0.45f);

        private static TMP_FontAsset font;
        private static Material crtMaterial;

        // Speakers (created once per build).
        private static DialogueSpeaker spkLeoInner, spkLeo, spkByte, spkKira, spkAlgorithm, spkObject, spkCommuter, spkStatue, spkUnknown, spkMia, spkConductor;

        // ====================================================================== Entry

        [MenuItem("Tools/Subnet/Build Full Game", priority = 0)]
        public static void Build()
        {
            if (!EnsureTextMeshPro()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SubnetProjectSetup.Setup();
            controllers.Clear();
            clipCache.Clear();
            choicePrefabCache = null;
            ConfigureMusicImport("firewall");
            ConfigureMusicImport("scrapeyard-aster");
            ConfigureMusicImport("freedom");
            ConfigureMusicImport("funkygameost");
            ConfigureMusicImport("truth");
            foreach (string stem in StemNames) ConfigureStemImport(stem);

            try
            {
                Progress("Shared assets...", 0.05f);
                crtMaterial = CreateCrtMaterial();
                CreateSpeakers();

                var failures = new List<string>();
                Step("Main menu...", 0.1f, BuildMainMenuScene, failures);
                Step("Act I: the tenement...", 0.25f, BuildTenementScene, failures);
                Step("Act I: Central Station...", 0.4f, BuildStationScene, failures);
                Step("Act II: Subnet...", 0.55f, BuildSubnetScene, failures);
                Step("Act III: Concrete Park...", 0.7f, BuildParkScene, failures);
                Step("Act IV: the Ghost Line...", 0.8f, BuildGhostLineScene, failures);
                Step("Act V: Central Core...", 0.9f, BuildCoreScene, failures);
                if (failures.Count > 0)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Night Train Zero", "Some scenes failed to build:\n\n" + string.Join("\n", failures) +
                        "\n\nThe other scenes are fine. Send the red Console error to fix it.", "OK");
                }

                EditorBuildSettings.scenes = new[] { MenuScene, TenementScene, StationScene, SubnetScene, ParkScene, GhostLineScene, CoreScene }
                    .Where(s => File.Exists(ScenesFolder + s + ".unity"))
                    .Select(s => new EditorBuildSettingsScene(ScenesFolder + s + ".unity", true))
                    .ToArray();

                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(ScenesFolder + MenuScene + ".unity", OpenSceneMode.Single);
                Debug.Log("[Subnet] Full game built. Press Play in 00_MainMenu.");
                EditorUtility.DisplayDialog("Night Train Zero",
                    "Full game built (6 scenes).\n\nPress Play in 00_MainMenu.\n\n" +
                    "Move: WASD / arrows\nInteract / advance: E, Enter, Space\nBack: X / Esc (hold to skip cutscenes)\n\n" +
                    "No sound? Make sure the speaker icon in the Game view toolbar isn't muted, pick your Mac's output device, then restart Unity.",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Subnet/Build Playable Demo", priority = 50)]
        public static void BuildLegacyName() => Build();

        private static void Step(string label, float progress, Action build, List<string> failures)
        {
            Progress(label, progress);
            try
            {
                build();
            }
            catch (Exception e)
            {
                failures.Add(label + " " + e.GetType().Name + ": " + e.Message);
                Debug.LogException(e);
            }
        }

        private static void Progress(string message, float value) => EditorUtility.DisplayProgressBar("Night Train Zero", message, value);

        // ====================================================================== TextMeshPro

        private static bool EnsureTextMeshPro()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null && TMP_Settings.defaultFontAsset != null)
            {
                font = TMP_Settings.defaultFontAsset;
                return true;
            }

            foreach (string id in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + id);
                if (info == null) continue;
                string package = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (!File.Exists(package)) continue;

                AssetDatabase.importPackageCompleted += OnTmpImported;
                AssetDatabase.ImportPackage(package, false);
                Debug.Log("[Subnet] Importing TMP Essential Resources... the build will continue automatically.");
                return false;
            }

            EditorUtility.DisplayDialog("TextMeshPro missing",
                "Run Window → TextMeshPro → Import TMP Essential Resources, then Tools → Subnet → Build Full Game again.", "OK");
            return false;
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }

        // ====================================================================== Speakers

        private static void CreateSpeakers()
        {
            spkLeoInner = CreateSpeaker("Speaker_LeoInner", "", "tick_002", new Vector2(0.92f, 1f), 1f, Cyan,
                SpeakerStyle.Thought, new Color(0.86f, 0.84f, 1f), TextEffect.Float);
            spkLeo = CreateSpeaker("Speaker_Leo", "LEO", "click_002", new Vector2(0.95f, 1.05f), 1f, Cyan,
                SpeakerStyle.Leo, new Color(0.88f, 1f, 0.98f));
            spkByte = CreateSpeaker("Speaker_Byte", "BYTE", "click_004", new Vector2(1.35f, 1.55f), 1.25f, new Color(0.65f, 1f, 0.78f),
                SpeakerStyle.Byte, new Color(0.78f, 1f, 0.86f));
            spkKira = CreateSpeaker("Speaker_Kira", "KIRA", "pluck_002", new Vector2(1.1f, 1.25f), 1.3f, new Color(1f, 0.7f, 0.3f),
                SpeakerStyle.Kira, new Color(1f, 0.9f, 0.76f));
            spkAlgorithm = CreateSpeaker("Speaker_Algorithm", "THE ALGORITHM", "glitch_002", new Vector2(0.5f, 0.6f), 0.65f, Magenta,
                SpeakerStyle.Algorithm, new Color(1f, 0.5f, 0.66f), TextEffect.Glitch);
            spkObject = CreateSpeaker("Speaker_Object", "", "drop_002", new Vector2(0.8f, 0.9f), 0.85f, Dim,
                SpeakerStyle.Object, new Color(0.82f, 0.82f, 0.88f));
            spkCommuter = CreateSpeaker("Speaker_Commuter", "COMMUTER", "tick_004", new Vector2(0.75f, 0.85f), 0.9f, new Color(0.8f, 0.8f, 0.75f),
                SpeakerStyle.Enemy, new Color(0.9f, 0.88f, 0.82f));
            spkStatue = CreateSpeaker("Speaker_Statue", "STATUE", "drop_004", new Vector2(0.6f, 0.7f), 0.7f, new Color(0.7f, 0.7f, 0.72f),
                SpeakerStyle.Enemy, new Color(0.84f, 0.86f, 0.92f));
            spkUnknown = CreateSpeaker("Speaker_Unknown", "???", "glitch_001", new Vector2(0.9f, 1.3f), 0.9f, Lilac,
                SpeakerStyle.Unknown, new Color(0.82f, 0.75f, 1f), TextEffect.Glitch);
            spkConductor = CreateSpeaker("Speaker_Conductor", "CONDUCTOR", "drop_002", new Vector2(0.62f, 0.7f), 0.75f, new Color(0.95f, 0.85f, 0.55f),
                SpeakerStyle.Object, new Color(0.95f, 0.92f, 0.85f));
            // Mia lives in Resources so the handheld menu can replay her voicemails from any scene.
            EnsureFolder(Root + "/Resources");
            spkMia = CreateSpeaker("../Resources/Speaker_Mia", "MIA", "tick_002", new Vector2(1.08f, 1.16f), 0.85f, new Color(1f, 0.75f, 0.85f),
                SpeakerStyle.Default, new Color(1f, 0.92f, 0.95f), TextEffect.None);
            EnsureFolder(Root + "/Resources/Fonts");
            BuildFontLibrary();
        }

        /// <summary>Collects every font in the project into Resources/DialogueFontLibrary (per-character typefaces).</summary>
        private static void BuildFontLibrary()
        {
            string path = Root + "/Resources/DialogueFontLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<DialogueFontLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<DialogueFontLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }
            var fonts = new List<Font>();
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { "Assets" }))
            {
                string fontPath = AssetDatabase.GUIDToAssetPath(guid);
                if (fontPath.Contains("TextMesh Pro")) continue;
                if (AssetImporter.GetAtPath(fontPath) is TrueTypeFontImporter importer && !importer.includeFontData)
                {
                    importer.includeFontData = true;
                    importer.SaveAndReimport();
                }
                Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font != null) fonts.Add(font);
            }
            library.fonts = fonts.ToArray();
            EditorUtility.SetDirty(library);
            Debug.Log($"[Subnet] Dialogue fonts found: {string.Join(", ", fonts.ConvertAll(f => f.name))}");
        }

        private static DialogueSpeaker CreateSpeaker(string assetName, string displayName, string blip, Vector2 pitch, float speed, Color color,
            SpeakerStyle style = SpeakerStyle.Default, Color textColor = default, TextEffect signature = TextEffect.None)
        {
            string path = assetName.StartsWith("../Resources/") ? Root + "/Resources/" + assetName.Substring(13) + ".asset" : Root + "/Data/Speakers/" + assetName + ".asset";
            var speaker = AssetDatabase.LoadAssetAtPath<DialogueSpeaker>(path);
            if (speaker == null)
            {
                speaker = ScriptableObject.CreateInstance<DialogueSpeaker>();
                AssetDatabase.CreateAsset(speaker, path);
            }
            speaker.displayName = displayName;
            speaker.voiceBlip = Clip(blip);
            speaker.pitchRange = pitch;
            speaker.typingSpeedMultiplier = speed;
            speaker.nameColor = color;
            speaker.style = style;
            speaker.textColor = textColor.a > 0f ? textColor : Color.white;
            speaker.signatureEffect = signature;
            EditorUtility.SetDirty(speaker);
            return speaker;
        }

        // ====================================================================== Gameplay scene scaffold

        private class Ctx
        {
            public Scene scene;
            public Camera cam;
            public IsometricCameraRig rig;
            public GameObject leo;
            public CinematicDirector director;
            public ScreenFader fader;
            public ProceduralAmbience ambience;
            public AudioSource sfx;
            public Transform env;
            public Transform props;
            public Transform story;
            public int interactableLayer;
            public int groundLayer;
            /// <summary>Scenes with city music drop the synthesized ambience (it clashes with the music).</summary>
            public bool removeAmbience;
        }

        private static Ctx BeginGameplayScene(Vector3 spawn, float spawnYaw, VolumeProfile profile,
            Color ambient, Color sunColor, float sunIntensity, bool hasIntroCinematic)
        {
            var ctx = new Ctx
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
                interactableLayer = LayerOr("Interactable", 0),
                groundLayer = LayerOr("Ground", 0),
            };

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Hard; // cheaper than soft shadows, same look at this scale
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            CreateVolume(profile);
            CreateEventSystem();

            ctx.env = new GameObject("Environment").transform;
            ctx.props = new GameObject("Props").transform;
            ctx.story = new GameObject("Story").transform;

            ctx.cam = CreateCamera("Main Camera", new Color(0.02f, 0.02f, 0.03f));
            ctx.cam.fieldOfView = 28f;

            ctx.leo = CreateLeo(spawn, spawnYaw, ctx.cam, out PlayerInteractor interactor, out AudioSource leoAudio);

            ctx.rig = ctx.cam.gameObject.AddComponent<IsometricCameraRig>();
            Set(ctx.rig, "target", ctx.leo.transform);
            Set(ctx.rig, "distance", 13f);
            Set(ctx.rig, "fieldOfView", 28f);
            ctx.rig.SnapToTarget();

            // HUD prompt: one pill "Z  Front Door" that sizes itself to the text.
            Canvas hud = CreateCanvas("HUDCanvas", 0, null);
            GameObject prompt = UI("Prompt", hud.transform);
            Place(Rect(prompt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(200, 48));
            Image promptBg = prompt.AddComponent<Image>();
            promptBg.color = new Color(0.04f, 0.06f, 0.08f, 0.92f);
            promptBg.raycastTarget = false;
            prompt.AddComponent<Outline>().effectColor = Cyan;
            var promptLayout = prompt.AddComponent<HorizontalLayoutGroup>();
            promptLayout.padding = new RectOffset(16, 16, 6, 6);
            promptLayout.childAlignment = TextAnchor.MiddleCenter;
            promptLayout.childControlWidth = true;
            promptLayout.childControlHeight = true;
            promptLayout.childForceExpandWidth = false;
            promptLayout.childForceExpandHeight = false;
            var promptFitter = prompt.AddComponent<ContentSizeFitter>();
            promptFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            promptFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI promptText = Text(prompt.transform, "Label", "<b>E</b>  Look", 26, Cyan, TextAlignmentOptions.Center);
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            Set(interactor, "promptLabel", promptText);

            Set(interactor, "interactableMask", 1 << ctx.interactableLayer);
            Set(interactor, "promptUI", Rect(prompt));
            Set(interactor, "promptCanvas", hud);
            Set(interactor, "audioSource", leoAudio);
            Set(interactor, "interactClip", Clip("select_001"));

            BuildDialogueUI();

            // Cinematic canvas
            Canvas cinematicCanvas = CreateCanvas("CinematicCanvas", 50, null);
            Image top = Img(cinematicCanvas.transform, "LetterboxTop", Color.black);
            SetBar(top.rectTransform, true);
            Image bottom = Img(cinematicCanvas.transform, "LetterboxBottom", Color.black);
            SetBar(bottom.rectTransform, false);
            GameObject skipHint = UI("SkipHint", cinematicCanvas.transform);
            Place(Rect(skipHint), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 30), new Vector2(420, 60));
            CanvasGroup skipGroup = skipHint.AddComponent<CanvasGroup>();
            TextMeshProUGUI skipText = Text(skipHint.transform, "Label", "HOLD [X] TO SKIP", 26, Dim, TextAlignmentOptions.Right);
            Localize(skipText);
            Stretch(skipText.rectTransform, 0, 70, 0, 0);
            Image skipFill = Img(skipHint.transform, "Progress", Cyan);
            skipFill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Radial360;
            skipFill.fillAmount = 0f;
            Place(skipFill.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(48, 48));

            ctx.fader = CreateFader(hasIntroCinematic ? 0f : 1.4f);

            var directorGo = new GameObject("CinematicDirector");
            ctx.director = directorGo.AddComponent<CinematicDirector>();
            AudioSource cineAmbience = CreateAudio("Ambience", directorGo.transform, true);
            ctx.sfx = CreateAudio("SFX", directorGo.transform, false);
            Set(ctx.director, "targetCamera", ctx.cam);
            Set(ctx.director, "gameplayRig", ctx.rig);
            Set(ctx.director, "fader", ctx.fader);
            Set(ctx.director, "topBar", top.rectTransform);
            Set(ctx.director, "bottomBar", bottom.rectTransform);
            Set(ctx.director, "skipHintGroup", skipGroup);
            Set(ctx.director, "skipProgressFill", skipFill);
            Set(ctx.director, "ambienceSource", cineAmbience);
            Set(ctx.director, "sfxSource", ctx.sfx);
            Set(ctx.director, "playIntroOnStart", false);

            // Obstacle Hacking
            var hackingGo = new GameObject("HackingManager");
            AudioSource hackAudio = hackingGo.AddComponent<AudioSource>();
            hackAudio.playOnAwake = false;
            var hacking = hackingGo.AddComponent<HackingManager>();
            Set(hacking, "audioSource", hackAudio);
            Set(hacking, "crtMaterial", crtMaterial);
            Set(hacking, "connectClip", Clip("maximize_004"));
            Set(hacking, "waveStartClip", Clip("switch_005"));
            Set(hacking, "hitClip", Clip("error_004"));
            Set(hacking, "grazeClip", Clip("tick_001"));
            Set(hacking, "tickClip", Clip("tick_004"));
            Set(hacking, "firewallBreakClip", Clip("glass_002"));
            Set(hacking, "wrongClip", Clip("error_006"));
            Set(hacking, "resolvedClip", Clip("confirmation_004"));
            Set(hacking, "disconnectClip", Clip("minimize_004"));
            Set(hacking, "alertClip", Clip("question_001"));
            Set(hacking, "transitionClip", Clip("glitch_003"));
            Set(hacking, "soulBlinkClip", Clip("tick_002"));
            Set(hacking, "transitionOutClip", Clip("maximize_002"));

            // SMS on the handheld
            var phoneGo = new GameObject("PhoneNotifier");
            AudioSource phoneAudio = phoneGo.AddComponent<AudioSource>();
            phoneAudio.playOnAwake = false;
            var phone = phoneGo.AddComponent<PhoneNotifier>();
            Set(phone, "audioSource", phoneAudio);
            Set(phone, "messageClip", Clip("question_002"));

            // Pause menu (Esc)
            var pauseGo = new GameObject("PauseMenu");
            AudioSource pauseAudio = pauseGo.AddComponent<AudioSource>();
            pauseAudio.playOnAwake = false;
            var pause = pauseGo.AddComponent<PauseMenu>();
            Set(pause, "audioSource", pauseAudio);
            Set(pause, "openClip", Clip("open_003"));
            Set(pause, "closeClip", Clip("close_003"));
            Set(pause, "moveClip", Clip("tick_001"));
            Set(pause, "confirmClip", Clip("confirmation_001"));
            Set(pause, "errorClip", Clip("error_006"));

            // Synth ambience
            var ambienceGo = new GameObject("ProceduralAmbience");
            ambienceGo.AddComponent<AudioSource>().playOnAwake = false;
            ctx.ambience = ambienceGo.AddComponent<ProceduralAmbience>();

            return ctx;
        }

        private static void EndGameplayScene(Ctx ctx, string sceneName)
        {
            if (ctx.removeAmbience) Object.DestroyImmediate(ctx.ambience.gameObject);
            else EditorUtility.SetDirty(ctx.ambience);
            EditorSceneManager.SaveScene(ctx.scene, ScenesFolder + sceneName + ".unity");
        }

        private static void SetIntro(Ctx ctx, CinematicSequence intro)
        {
            Set(ctx.director, "introSequence", intro);
            Set(ctx.director, "playIntroOnStart", true);
        }

        private static GameObject CreateLeo(Vector3 spawn, float yaw, Camera cam, out PlayerInteractor interactor, out AudioSource audio)
        {
            var leo = new GameObject("Leo");
            leo.tag = "Player";
            leo.layer = LayerOr("Player", 0);
            leo.transform.position = spawn + Vector3.up * 0.02f;
            leo.transform.rotation = Quaternion.Euler(0, yaw, 0);

            var cc = leo.AddComponent<CharacterController>();
            cc.height = 1.0f;
            cc.radius = 0.22f;
            cc.center = new Vector3(0, 0.5f, 0);
            cc.stepOffset = 0.2f;
            cc.skinWidth = 0.02f;

            var movement = leo.AddComponent<IsometricPlayerController>();
            interactor = leo.AddComponent<PlayerInteractor>();
            audio = leo.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            GameObject leoModel = SpawnCharacterModel("character-male-b", "mini-characters", leo.transform, 1.0f, out Animator animator);
            leoModel.AddComponent<ProceduralLean>();
            SetLayerRecursive(leo, LayerOr("Player", 0));
            Set(movement, "cameraTransform", cam.transform);
            Set(movement, "animator", animator);
            return leo;
        }

        // ====================================================================== Characters

        private static readonly Dictionary<string, RuntimeAnimatorController> controllers = new Dictionary<string, RuntimeAnimatorController>();

        private static GameObject SpawnCharacterModel(string model, string hint, Transform parent, float height, out Animator animator)
        {
            animator = null;
            GameObject asset = Model(model, hint);
            if (asset == null)
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
                capsule.name = "Model";
                capsule.transform.SetParent(parent, false);
                capsule.transform.localScale = new Vector3(0.45f, height * 0.5f, 0.45f);
                capsule.transform.localPosition = new Vector3(0, height * 0.5f, 0);
                return capsule;
            }

            string key = AssetDatabase.GetAssetPath(asset);
            if (!controllers.TryGetValue(key, out RuntimeAnimatorController controller))
            {
                controller = CreateLocomotionController(asset, "AC_" + model);
                controllers[key] = controller;
                asset = Model(model, hint); // reload after a possible reimport
            }

            GameObject instance = SpawnModel(asset, parent, height, false, 0f);
            instance.name = "Model";
            animator = instance.GetComponentInChildren<Animator>();
            if (controller != null)
            {
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            return instance;
        }

        private static GameObject Character(Ctx ctx, string key, string model, string hint, Vector3 position, float yaw, float height,
            Material overrideMaterial = null, bool solid = true, Transform parent = null)
        {
            var root = new GameObject("NPC_" + key);
            root.transform.SetParent(parent != null ? parent : ctx.props, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            root.layer = ctx.interactableLayer;

            GameObject visual = SpawnCharacterModel(model, hint, root.transform, height, out _);
            if (overrideMaterial != null) TintModel(visual, overrideMaterial);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, height * 0.5f, 0);
            capsule.height = height;
            capsule.radius = Mathf.Max(0.25f, height * 0.25f);
            capsule.isTrigger = !solid;
            return root;
        }

        private static void TintModel(GameObject model, Material material)
        {
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
        }

        private static RuntimeAnimatorController CreateLocomotionController(GameObject modelAsset, string controllerName)
        {
            string modelPath = AssetDatabase.GetAssetPath(modelAsset);

            if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
            {
                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                bool changed = false;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    string n = clip.name.ToLowerInvariant();
                    if ((n.Contains("idle") || n.Contains("walk") || n.Contains("sprint") || n.Contains("run")) && !clip.loopTime)
                    {
                        clip.loopTime = true;
                        changed = true;
                    }
                }
                if (changed)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            AnimationClip[] all = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
            AnimationClip idle = all.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
            AnimationClip walk = all.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("walk"));
            if (idle == null && walk == null) return null;

            string path = Root + "/Animation/Controllers/" + controllerName + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 1f);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ====================================================================== Props

        private static GameObject Prop(Ctx ctx, string key, string model, string hint, Vector3 position, float yaw, float size, bool byLength,
            string[] first, string[] repeat = null, string flag = null, bool solid = true, DialogueSpeaker speaker = null, Transform parent = null)
        {
            var root = new GameObject("Prop_" + key);
            root.transform.SetParent(parent != null ? parent : ctx.props, false);
            root.transform.position = position;

            GameObject asset = Model(model, hint);
            GameObject visual;
            if (asset != null)
            {
                visual = SpawnModel(asset, root.transform, size, byLength, yaw);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(size * 0.6f, size, size * 0.6f);
                visual.transform.localPosition = new Vector3(0, size * 0.5f, 0);
            }
            visual.name = "Model";

            bool inspectable = first != null && first.Length > 0;
            if (solid || inspectable)
            {
                Bounds bounds = WorldBounds(visual);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center - position;
                collider.size = new Vector3(Mathf.Max(bounds.size.x, 0.15f), Mathf.Max(bounds.size.y, 0.15f), Mathf.Max(bounds.size.z, 0.15f));
                collider.isTrigger = !solid;
            }

            if (inspectable)
            {
                root.layer = ctx.interactableLayer;
                Inspectable(root, key, first, repeat, flag, speaker, null, visual.transform);
            }
            return root;
        }

        private static InspectableObject Inspectable(GameObject go, string key, string[] first, string[] repeat = null, string flag = null,
            DialogueSpeaker speaker = null, DialogueSequence sequence = null, Transform punch = null)
        {
            go.tag = "Inspectable";
            InspectableObject inspectable = go.GetComponent<InspectableObject>();
            if (inspectable == null) inspectable = go.AddComponent<InspectableObject>();
            Set(inspectable, "inspectionId", key);
            SetStrings(inspectable, "firstInspection", first ?? new string[0]);
            SetStrings(inspectable, "repeatInspection", repeat ?? new string[0]);
            Set(inspectable, "flagOnInspect", flag ?? string.Empty);
            Set(inspectable, "punchTarget", punch != null ? punch : go.transform);
            Set(inspectable, "speaker", speaker);
            Set(inspectable, "sequenceOverride", sequence);
            // Characters (capsule roots) turn to look at Leo while talking.
            Set(inspectable, "faceInteractor", go.GetComponent<CapsuleCollider>() != null);

            // Characters with a voice get a body the DialogueDirector can stage (turn, walk over, bob while talking).
            if (go.GetComponent<CapsuleCollider>() != null && speaker != null)
            {
                DialogueActor actor = go.GetComponent<DialogueActor>();
                if (actor == null) actor = go.AddComponent<DialogueActor>();
                actor.speaker = speaker;
                actor.canApproach = speaker == spkByte || speaker == spkKira;
                actor.actorId = speaker == spkByte ? "byte" : speaker == spkKira ? "kira" : string.Empty;
                actor.turnToFace = speaker != spkStatue; // statues don't turn their heads
                EditorUtility.SetDirty(actor);
            }
            return inspectable;
        }

        private static GameObject SpawnModel(GameObject asset, Transform parent, float size, bool byLength, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one;

            Bounds bounds = WorldBounds(go);
            float measure = byLength ? Mathf.Max(bounds.size.x, bounds.size.z) : bounds.size.y;
            if (measure > 0.0001f) go.transform.localScale = Vector3.one * (size / measure);

            bounds = WorldBounds(go);
            Vector3 anchor = parent.position;
            go.transform.position += new Vector3(anchor.x - bounds.center.x, anchor.y - bounds.min.y, anchor.z - bounds.center.z);
            return go;
        }

        /// <summary>
        /// Lines up buildings along +X without overlaps: each facade starts at <paramref name="minZ"/> (behind the
        /// walkable area), footprints deeper than <paramref name="maxDepth"/> are scaled down, and every building
        /// starts where the previous one ended (+gap). Returns the furthest Z reached by the row.
        /// </summary>
        private static float BuildingRow(Transform parent, string[] models, string hint, float startX, float endX, float minZ,
            float baseHeight, float heightStep, float gap, float maxDepth, float yaw, int count = -1)
        {
            float cursor = startX;
            float furthestZ = minZ;
            int total = count > 0 ? count : models.Length;
            for (int i = 0; i < total; i++)
            {
                GameObject asset = Model(models[i % models.Length], hint);
                if (asset == null) continue;

                var holder = new GameObject("Building_" + models[i % models.Length] + "_" + i);
                holder.transform.SetParent(parent, false);
                holder.transform.position = new Vector3(cursor, 0f, minZ + 5f);
                GameObject building = SpawnModel(asset, holder.transform, baseHeight + (i % 3) * heightStep, false, yaw);

                Bounds b = WorldBounds(building);
                if (b.size.z > maxDepth)
                {
                    float shrink = maxDepth / b.size.z;
                    building.transform.localScale *= shrink;
                    b = WorldBounds(building);
                }

                // Snap: left edge at the cursor, facade (min Z) exactly on the boundary line.
                building.transform.position += new Vector3(cursor - b.min.x, -b.min.y, minZ - b.min.z);
                b = WorldBounds(building);
                if (b.max.x > endX)
                {
                    Object.DestroyImmediate(holder);
                    break;
                }

                cursor = b.max.x + gap;
                furthestZ = Mathf.Max(furthestZ, b.max.z);
            }
            return furthestZ;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static float Top(GameObject go)
        {
            Transform model = go.transform.Find("Model");
            return WorldBounds(model != null ? model.gameObject : go).max.y;
        }

        private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material, int layer)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.layer = layer;
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            else Object.DestroyImmediate(renderer);
            return go;
        }

        /// <summary>Visual-only cube (no collider).</summary>
        private static GameObject Deco(string name, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 euler = default)
        {
            GameObject go = Block(name, parent, position, size, material, 0);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        private static Light PointLight(Transform parent, Vector3 worldPosition, Color color, float intensity, float range)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = worldPosition;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        private static Transform CinematicPoint(string name, Transform parent, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject(name);
            go.tag = "CinematicPoint";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            return go.transform;
        }

        private static CinematicSequence Cinematic(string name, Transform parent, params CinematicShot[] shots)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sequence = go.AddComponent<CinematicSequence>();
            sequence.shots = new List<CinematicShot>(shots);
            return sequence;
        }

        private static CinematicShot Shot(string label, Transform point, float travel, float hold, float fov, string[] lines = null,
            DialogueSpeaker speaker = null, bool cut = false, AudioClip sound = null, float shake = 0f)
        {
            return new CinematicShot
            {
                label = label,
                cameraPoint = point,
                travelDuration = travel,
                holdDuration = hold,
                fieldOfView = fov,
                narrationLines = lines ?? new string[0],
                narrationSpeaker = speaker,
                cut = cut,
                startSound = sound,
                shakeOnStart = shake
            };
        }

        /// <summary>Simple falling-pixel particle emitter (weeping statues, static).</summary>
        private static ParticleSystem PixelTears(Transform parent, Vector3 position, Material material, float rate = 14f)
        {
            var go = new GameObject("PixelTears");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.2f;
            main.startSize = 0.07f;
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        // ====================================================================== Story helpers

        private static SceneExit Exit(GameObject go, string targetScene, string unlockKey, string requiredFlag, string[] lockedLines,
            string[] leavingLines = null, CinematicSequence cinematic = null, bool triggerOnEnter = false, DialogueSpeaker speaker = null,
            AudioSource source = null, AudioClip clip = null)
        {
            var exit = go.AddComponent<SceneExit>();
            Set(exit, "targetScene", targetScene);
            Set(exit, "unlockChapterKey", unlockKey ?? string.Empty);
            Set(exit, "requiredFlag", requiredFlag ?? string.Empty);
            SetStrings(exit, "lockedLines", lockedLines ?? new string[0]);
            SetStrings(exit, "leavingLines", leavingLines ?? new string[0]);
            Set(exit, "cinematicBeforeLeaving", cinematic);
            Set(exit, "triggerOnEnter", triggerOnEnter);
            Set(exit, "speaker", speaker);
            Set(exit, "audioSource", source);
            Set(exit, "openClip", clip);
            if (triggerOnEnter && go.TryGetComponent(out Collider c)) c.isTrigger = true;
            return exit;
        }

        private static SceneExit ExitZone(Ctx ctx, string name, Vector3 center, Vector3 size, string targetScene, string unlockKey, string requiredFlag, string[] lockedLines)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.story, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            return Exit(go, targetScene, unlockKey, requiredFlag, lockedLines, null, null, true);
        }

        private static StoryEvent Event(Ctx ctx, string name, StoryEvent.TriggerMode mode, string requiredFlag, float delay,
            string sender, string[] sms, string[] lines, DialogueSpeaker speaker, string flagToSet,
            Vector3 zoneCenter = default, Vector3 zoneSize = default, DialogueSequence sequence = null)
        {
            var go = new GameObject("Event_" + name);
            go.transform.SetParent(ctx.story, false);
            if (mode == StoryEvent.TriggerMode.OnPlayerEnter)
            {
                go.transform.position = zoneCenter;
                var box = go.AddComponent<BoxCollider>();
                box.size = zoneSize == default ? new Vector3(3, 2, 3) : zoneSize;
                box.isTrigger = true;
            }
            var ev = go.AddComponent<StoryEvent>();
            var so = new SerializedObject(ev);
            so.FindProperty("trigger").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(ev, "requiredFlag", requiredFlag ?? string.Empty);
            Set(ev, "delay", delay);
            Set(ev, "phoneSender", sender ?? "BYTE");
            SetStrings(ev, "phoneMessages", sms ?? new string[0]);
            SetStrings(ev, "lines", lines ?? new string[0]);
            Set(ev, "speaker", speaker);
            Set(ev, "sequence", sequence);
            Set(ev, "flagToSet", flagToSet ?? string.Empty);
            return ev;
        }

        private static HackableObstacle Hackable(GameObject root, string title, DialogueSpeaker speaker, string[] intro,
            HackWave[] waves, HackPuzzle[] puzzles, string[] resolution, string requiredFlag, string[] lockedLines,
            string flagOnResolved, GameObject[] hide, GameObject[] show = null)
        {
            var obstacle = root.AddComponent<HackableObstacle>();
            obstacle.encounterTitle = title;
            obstacle.speaker = speaker;
            obstacle.introLines = intro;
            obstacle.waves = new List<HackWave>(waves);
            obstacle.puzzles = new List<HackPuzzle>(puzzles);
            obstacle.resolutionLines = resolution;
            obstacle.requiredFlag = requiredFlag ?? string.Empty;
            obstacle.lockedLines = lockedLines ?? new string[0];
            obstacle.flagOnResolved = flagOnResolved;
            obstacle.hideOnResolved = hide ?? new GameObject[0];
            obstacle.showOnResolved = show ?? new GameObject[0];
            obstacle.dissolveColor = Warm;
            EditorUtility.SetDirty(obstacle);
            return obstacle;
        }

        private static CompanionFollower Follower(GameObject go, string followFlag, float side)
        {
            int npcLayer = LayerOr("NPC", 0);
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, npcLayer);
            Transform model = go.transform.Find("Model");
            if (model != null && model.GetComponent<ProceduralLean>() == null) model.gameObject.AddComponent<ProceduralLean>();
            var follower = go.AddComponent<CompanionFollower>();
            Set(follower, "followFlag", followFlag ?? string.Empty);
            Set(follower, "sideOffset", side);
            return follower;
        }

        private static DialogueSequence SeqAsset(string name, params DialogueNode[] nodes)
        {
            string path = Root + "/Data/Dialogue/" + name + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<DialogueSequence>(path) != null) AssetDatabase.DeleteAsset(path);
            var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            sequence.nodes = new List<DialogueNode>(nodes);
            AssetDatabase.CreateAsset(sequence, path);
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static DialogueNode Say(DialogueSpeaker speaker, string text, string id = "", string next = "", string flag = "", TextEffect effect = TextEffect.None)
        {
            return new DialogueNode { speaker = speaker, text = text, id = id, nextNodeId = next, flagToSet = flag, effect = effect };
        }

        private static DialogueNode Ask(DialogueSpeaker speaker, string text, params DialogueChoice[] choices)
        {
            var node = new DialogueNode { speaker = speaker, text = text };
            node.choices.AddRange(choices);
            return node;
        }

        private static DialogueChoice Opt(string text, string next, string flag = "")
        {
            return new DialogueChoice { text = text, nextNodeId = next, flagToSet = flag };
        }

        private static string[] L(params string[] lines) => lines;

        // ====================================================================== Shared scene pieces

        private static Camera CreateCamera(string name, Color background)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            go.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }

        private static Volume CreateVolume(VolumeProfile profile)
        {
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return volume;
        }

        private static VolumeProfile CreateProfile(string name, Action<VolumeProfile> fill)
        {
            string path = Root + "/Settings/PostProcessing/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            fill(profile);
            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static VolumeProfile LookProfile(string name, float saturation, float bloom, float vignette, float grain, float chromatic, Color tint)
        {
            return CreateProfile(name, p =>
            {
                var bloomFx = p.Add<Bloom>(true);
                bloomFx.intensity.value = bloom * 0.45f;
                bloomFx.threshold.value = 1.1f;
                p.Add<Vignette>(true).intensity.value = vignette;
                var color = p.Add<ColorAdjustments>(true);
                color.saturation.value = saturation;
                color.colorFilter.value = tint;
                p.Add<FilmGrain>(true).intensity.value = grain;
                p.Add<ChromaticAberration>(true).intensity.value = chromatic;
            });
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            // Use the project's own action asset (it contains the "UI" map). Never call
            // AssignDefaultActions() in the editor: it throws for actions outside an asset.
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions != null && actions.FindActionMap("UI") != null)
            {
                try { module.actionsAsset = actions; }
                catch (System.Exception e) { Debug.LogWarning("[Subnet] Could not assign UI actions, the module will use its defaults: " + e.Message); }
            }
        }

        private static AudioSource CreateAudio(string name, Transform parent, bool loop)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static Canvas CreateCanvas(string name, int sortOrder, Camera cameraForScreenSpace)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            Canvas canvas = go.AddComponent<Canvas>();
            if (cameraForScreenSpace != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cameraForScreenSpace;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static ScreenFader CreateFader(float fadeInOnStart)
        {
            Canvas canvas = CreateCanvas("FadeCanvas", 100, null);
            Image black = Img(canvas.transform, "Fader", Color.black);
            Stretch(black.rectTransform);
            black.raycastTarget = false;
            black.gameObject.AddComponent<CanvasGroup>();
            var fader = black.gameObject.AddComponent<ScreenFader>();
            Set(fader, "fadeInOnStart", fadeInOnStart);
            return fader;
        }

        private static void SetBar(RectTransform rect, bool top)
        {
            rect.anchorMin = new Vector2(0, top ? 1 : 0);
            rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 0);
            rect.GetComponent<Image>().raycastTarget = false;
        }

        private static void BuildDialogueUI()
        {
            Canvas canvas = CreateCanvas("DialogueCanvas", 10, null);
            GameObject rootGo = UI("DialogueRoot", canvas.transform);
            Stretch(Rect(rootGo));
            CanvasGroup group = rootGo.AddComponent<CanvasGroup>();
            AudioSource voice = rootGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            AudioSource ui = rootGo.AddComponent<AudioSource>();
            ui.playOnAwake = false;

            Image box = Img(rootGo.transform, "Box", new Color(0.035f, 0.05f, 0.065f, 0.94f));
            RectTransform boxRect = box.rectTransform;
            boxRect.anchorMin = new Vector2(0, 0);
            boxRect.anchorMax = new Vector2(1, 0);
            boxRect.pivot = new Vector2(0.5f, 0f);
            boxRect.offsetMin = new Vector2(180, 40);
            boxRect.offsetMax = new Vector2(-180, 290);
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = Cyan;
            outline.effectDistance = new Vector2(3, -3);

            Image portrait = Img(box.transform, "Portrait", Color.white);
            Place(portrait.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(25, 0), new Vector2(200, 200));
            portrait.gameObject.SetActive(false);

            Image namePlate = Img(box.transform, "NamePlate", new Color(0.1f, 0.05f, 0.12f, 1f));
            Place(namePlate.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(30, -6), new Vector2(380, 54));
            namePlate.gameObject.AddComponent<Outline>().effectColor = Magenta;
            TextMeshProUGUI nameText = Text(namePlate.transform, "NameText", "NAME", 30, Cyan, TextAlignmentOptions.Center);
            nameText.fontStyle = FontStyles.Bold;
            Stretch(nameText.rectTransform);
            namePlate.gameObject.SetActive(false);

            TextMeshProUGUI body = Text(box.transform, "BodyText", "", 34, new Color(0.92f, 0.94f, 0.96f), TextAlignmentOptions.TopLeft);
            body.lineSpacing = 10;
            Stretch(body.rectTransform, 50, 50, 40, 40);

            TextMeshProUGUI indicator = Text(box.transform, "AdvanceIndicator", "v", 36, Magenta, TextAlignmentOptions.Center);
            Place(indicator.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 18), new Vector2(40, 40));

            GameObject choices = UI("Choices", rootGo.transform);
            Place(Rect(choices), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 310), new Vector2(860, 300));
            var layout = choices.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerRight;
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            if (choicePrefabCache == null) choicePrefabCache = CreateChoicePrefab();
            DialogueChoiceButton choicePrefab = choicePrefabCache;

            var manager = rootGo.AddComponent<DialogueManager>();
            Set(manager, "rootGroup", group);
            Set(manager, "boxTransform", boxRect);
            Set(manager, "bodyText", body);
            Set(manager, "namePlate", namePlate.gameObject);
            Set(manager, "nameText", nameText);
            Set(manager, "portraitImage", portrait);
            Set(manager, "advanceIndicator", indicator.rectTransform);
            Set(manager, "choiceContainer", Rect(choices));
            Set(manager, "choiceButtonPrefab", choicePrefab);
            Set(manager, "voiceSource", voice);
            Set(manager, "uiSource", ui);
            Set(manager, "openClip", Clip("open_001"));
            Set(manager, "closeClip", Clip("close_001"));
            Set(manager, "advanceClip", Clip("click_001"));
            Set(manager, "choiceMoveClip", Clip("tick_001"));
            Set(manager, "choiceConfirmClip", Clip("confirmation_001"));
            Set(manager, "defaultSpeaker", spkLeoInner);
        }

        private static DialogueChoiceButton choicePrefabCache;

        private static DialogueChoiceButton CreateChoicePrefab()
        {
            const string path = Root + "/Prefabs/UI/DialogueChoice.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);

            var go = new GameObject("DialogueChoice", typeof(RectTransform));
            go.layer = 5;
            ((RectTransform)go.transform).sizeDelta = new Vector2(860, 56);
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(0.035f, 0.05f, 0.065f, 0.92f);
            go.AddComponent<LayoutElement>().preferredHeight = 56;

            TextMeshProUGUI cursor = Text(go.transform, "Cursor", ">", 32, Magenta, TextAlignmentOptions.Center);
            Place(cursor.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(40, 50));
            TextMeshProUGUI label = Text(go.transform, "Label", "...", 26, Color.white, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(label.rectTransform, 56, 10, 0, 0);

            var button = go.AddComponent<DialogueChoiceButton>();
            Set(button, "label", label);
            Set(button, "cursor", cursor.gameObject);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<DialogueChoiceButton>();
        }

        private static Material CreateCrtMaterial()
        {
            const string path = Root + "/Art/Materials/M_CRTOverlay.mat";
            Shader shader = Shader.Find("Subnet/UI/CRTOverlay");
            bool fallback = shader == null;
            if (fallback) shader = Shader.Find("UI/Default");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0, 0, 0, fallback ? 0f : 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Material LitMaterial(string name, Color color, float smoothness = 0.15f)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        private const float NeonStrength = 0.4f;

        private static Material EmissiveMaterial(string name, Color baseColor, Color emission)
        {
            Material material = LitMaterial(name, baseColor);
            material.EnableKeyword("_EMISSION");
            // Playtest feedback: the neon was hurting people's eyes. Every glow in the game is toned down here.
            material.SetColor("_EmissionColor", emission * NeonStrength);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ParticleMaterial(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ====================================================================== UI helpers

        private static GameObject UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rect(GameObject go) => (RectTransform)go.transform;
        private static RectTransform Rect(Component c) => (RectTransform)c.transform;

        private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = UI(name, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Image Img(Transform parent, string name, Color color)
        {
            GameObject go = UI(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        // ====================================================================== Asset lookup

        private static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        private static AudioClip Clip(string name)
        {
            if (clipCache.TryGetValue(name, out AudioClip cached) && cached != null) return cached;
            foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                clipCache[name] = clip;
                return clip;
            }
            Debug.LogWarning("[Subnet] Audio clip not found: " + name);
            return null;
        }

        private static GameObject Model(string name, string folderHint)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                if (!path.Contains(folderHint)) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.LogWarning($"[Subnet] Model not found: {name} ({folderHint}). Using a placeholder.");
            return null;
        }

        /// <summary>Makes a static label translate itself (its English text is the key).</summary>
        private static void Localize(TMP_Text text)
        {
            if (text != null && text.GetComponent<LocalizedText>() == null) text.gameObject.AddComponent<LocalizedText>();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static void ConfigureStemImport(string stem)
        {
            AudioClip clip = StemClip(stem);
            if (clip == null) return;
            if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer)) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        /// <summary>Long music files: stream from disk (no 20 MB in memory), Vorbis compressed.</summary>
        private static void ConfigureMusicImport(string clipName)
        {
            AudioClip clip = Clip(clipName);
            if (clip == null) return;
            string path = AssetDatabase.GetAssetPath(clip);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis) return;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.75f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
            clipCache.Remove(clipName);
        }

        /// <summary>City track (continues across scenes) or silence; also registers the fight track.</summary>
        private static readonly string[] StemNames = { "chords", "bass", "drums", "lead", "Arp", "Cello", "White Noise" };

        /// <summary>A clip from the Scrapeyard stems folder (names like "bass" are too generic to search everywhere).</summary>
        private static AudioClip StemClip(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Scrapeyard stems") || Path.GetFileNameWithoutExtension(path) != name) continue;
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            Debug.LogWarning("[Subnet] Stem not found: " + name + " (Assets/Assets/Scrapeyard stems). Using the full track.");
            return null;
        }

        private static void AddSceneMusic(bool cityMusic, StemMix mix = StemMix.Street)
        {
            var go = new GameObject("SceneMusic");
            var music = go.AddComponent<SceneMusic>();
            music.mode = cityMusic ? SceneMusic.Mode.City : SceneMusic.Mode.Silence;
            music.cityTrack = Clip("scrapeyard-aster");
            music.fightTrack = Clip("firewall");
            var stems = new AudioClip[StemNames.Length];
            bool all = true;
            for (int i = 0; i < stems.Length; i++) { stems[i] = StemClip(StemNames[i]); all &= stems[i] != null; }
            music.cityStems = all ? stems : new AudioClip[0];
            music.stemMix = mix;
            EditorUtility.SetDirty(music);
        }

        /// <summary>A zone inside the scene that rebalances the stems while Leo is in it.</summary>
        private static void AddStemZone(Ctx ctx, string name, Vector3 center, Vector3 size, StemMix mix)
        {
            var go = new GameObject("StemZone_" + name);
            go.transform.SetParent(ctx.story, false);
            go.transform.position = center;
            var zone = go.AddComponent<StemZone>();
            Set(zone, "mix", (int)mix);
            Set(zone, "size", size);
        }

        private static int LayerOr(string name, int fallback)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : fallback;
        }

        // ====================================================================== Serialized field wiring

        private static SerializedProperty Find(SerializedObject so, Object target, string field)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) Debug.LogError($"[Subnet] Field '{field}' not found on {target.GetType().Name}.");
            return property;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStrings(Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, target, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
