#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Core;
using Subnet.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Subnet.EditorTools
{
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== 00_MainMenu

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera cam = CreateCamera("Main Camera", new Color(0.027f, 0.031f, 0.043f));
            cam.transform.position = new Vector3(0f, 0f, -10f);

            VolumeProfile profile = CreateProfile("PP_MainMenu", p =>
            {
                p.Add<Bloom>(true).intensity.value = 0.8f;
                p.Add<ChromaticAberration>(true).intensity.value = 0.12f;
                p.Add<Vignette>(true).intensity.value = 0.35f;
                p.Add<FilmGrain>(true).intensity.value = 0.25f;
            });
            Volume volume = CreateVolume(profile);
            CreateEventSystem();

            var audioRoot = new GameObject("Audio").transform;
            AudioSource uiSource = CreateAudio("UI_Source", audioRoot, false);
            AudioSource humSource = CreateAudio("Hum_Source", audioRoot, true);
            AudioSource rainSource = CreateAudio("Rain_Source", audioRoot, true);

            // Synthesized rain + subway hum behind the menu.
            var ambienceGo = new GameObject("ProceduralAmbience");
            ambienceGo.transform.SetParent(audioRoot, false);
            ambienceGo.AddComponent<AudioSource>().playOnAwake = false;
            var ambience = ambienceGo.AddComponent<ProceduralAmbience>();
            ambience.masterVolume = 0.45f;
            ambience.rain = 0.55f;
            ambience.hum = 0.35f;
            ambience.humFrequency = 41f;

            AddSceneMusic(false);
            Canvas canvas = CreateCanvas("MenuCanvas", 0, cam);
            RectTransform screenRoot = Rect(UI("ScreenRoot", canvas.transform));
            Stretch(screenRoot);

            Image bg = Img(screenRoot, "Background", new Color(0.035f, 0.047f, 0.063f));
            Stretch(bg.rectTransform);
            Image inner = Img(screenRoot, "ScreenPanel", Panel);
            Stretch(inner.rectTransform, 60, 60, 40, 40);
            inner.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 1f, 0.95f, 0.35f);

            TextMeshProUGUI osName = Text(screenRoot, "OSName", "LEO-OS  //  CENTRAL_STATION_PUBLIC", 24, Dim, TextAlignmentOptions.Left);
            Place(osName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -70), new Vector2(1000, 40));
            TextMeshProUGUI clock = Text(screenRoot, "Clock", "00:00", 24, Dim, TextAlignmentOptions.Right);
            Place(clock.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -70), new Vector2(300, 40));

            TextMeshProUGUI bootLog = Text(screenRoot, "BootLog", "", 24, Cyan, TextAlignmentOptions.TopLeft);
            Place(bootLog.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -130), new Vector2(1300, 380));

            CanvasGroup mainPanel = MenuPanel(screenRoot, "MainPanel");
            TextMeshProUGUI title = Text(mainPanel.transform, "Title", "NIGHT TRAIN ZERO", 72, Magenta, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            RetroMenuButton boot = MenuButton(mainPanel.transform, "Btn_Boot", "BOOT SYSTEM");
            RetroMenuButton memories = MenuButton(mainPanel.transform, "Btn_Memories", "CORRUPTED MEMORIES");
            RetroMenuButton config = MenuButton(mainPanel.transform, "Btn_Config", "SYSTEM CONFIG");
            RetroMenuButton shutdown = MenuButton(mainPanel.transform, "Btn_Shutdown", "SHUT DOWN");

            CanvasGroup memoriesPanel = MenuPanel(screenRoot, "MemoriesPanel");
            TextMeshProUGUI memTitle = Text(memoriesPanel.transform, "Header", "/memories  (damaged sectors detected)", 32, Dim, TextAlignmentOptions.Left);
            memTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            Localize(memTitle);
            RetroMenuButton entryTemplate = MenuButton(memoriesPanel.transform, "Entry_Template", "MEM_00", 34);
            entryTemplate.gameObject.SetActive(false);
            RetroMenuButton memBack = MenuButton(memoriesPanel.transform, "Btn_Back", "RETURN", 34);

            CanvasGroup configPanel = MenuPanel(screenRoot, "ConfigPanel");
            TextMeshProUGUI cfgTitle = Text(configPanel.transform, "Header", "/system/config", 32, Dim, TextAlignmentOptions.Left);
            cfgTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            Slider master = SliderRow(configPanel.transform, "MASTER", out TextMeshProUGUI masterValue);
            Slider music = SliderRow(configPanel.transform, "MUSIC", out TextMeshProUGUI musicValue);
            Slider sfx = SliderRow(configPanel.transform, "SFX", out TextMeshProUGUI sfxValue);
            Toggle shake = ToggleRow(configPanel.transform, "SCREEN SHAKE");
            Toggle fullscreen = ToggleRow(configPanel.transform, "FULLSCREEN");
            RetroMenuButton language = MenuButton(configPanel.transform, "Btn_Language", "LANGUAGE", 34);
            RetroMenuButton quality = MenuButton(configPanel.transform, "Btn_Graphics", "GRAPHICS", 34);
            RetroMenuButton cfgBack = MenuButton(configPanel.transform, "Btn_Back", "SAVE AND RETURN", 34);

            TextMeshProUGUI status = Text(screenRoot, "StatusText", "", 22, Dim, TextAlignmentOptions.Left);
            Place(status.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 60), new Vector2(1600, 40));

            Image crt = Img(screenRoot, "CRT_Overlay", Color.white);
            crt.material = crtMaterial;
            crt.raycastTarget = false;
            Stretch(crt.rectTransform);

            ScreenFader fader = CreateFader(0f);

            var controller = new GameObject("MainMenu").AddComponent<MainMenuController>();
            Set(controller, "newGameScene", TenementScene);
            Set(controller, "screenRoot", screenRoot);
            Set(controller, "mainPanel", mainPanel);
            Set(controller, "memoriesPanel", memoriesPanel);
            Set(controller, "configPanel", configPanel);
            Set(controller, "bootButton", boot);
            Set(controller, "memoriesButton", memories);
            Set(controller, "configButton", config);
            Set(controller, "shutdownButton", shutdown);
            Set(controller, "memoriesListRoot", (RectTransform)memoriesPanel.transform);
            Set(controller, "memoryEntryPrefab", entryTemplate);
            Set(controller, "memoriesBackButton", memBack);
            Set(controller, "masterSlider", master);
            Set(controller, "musicSlider", music);
            Set(controller, "sfxSlider", sfx);
            Set(controller, "masterValueText", masterValue);
            Set(controller, "musicValueText", musicValue);
            Set(controller, "sfxValueText", sfxValue);
            Set(controller, "screenShakeToggle", shake);
            Set(controller, "fullscreenToggle", fullscreen);
            Set(controller, "configBackButton", cfgBack);
            Set(controller, "languageButton", language);
            Set(controller, "qualityButton", quality);
            Set(controller, "bootLogText", bootLog);
            Set(controller, "clockText", clock);
            Set(controller, "statusText", status);
            Set(controller, "crtOverlay", crt);
            Set(controller, "postProcessVolume", volume);
            Set(controller, "uiSource", uiSource);
            Set(controller, "humSource", humSource);
            Set(controller, "rainSource", rainSource);
            Set(controller, "hoverClip", Clip("tick_001"));
            Set(controller, "confirmClip", Clip("confirmation_002"));
            Set(controller, "backClip", Clip("back_002"));
            Set(controller, "errorClip", Clip("error_006"));
            Set(controller, "openPanelClip", Clip("maximize_008"));
            Set(controller, "closePanelClip", Clip("minimize_008"));
            Set(controller, "toggleClip", Clip("switch_002"));
            Set(controller, "sliderTickClip", Clip("tick_002"));
            Set(controller, "bootBlipClip", Clip("click_002"));
            Set(controller, "powerOffClip", Clip("minimize_005"));
            SetArray(controller, "glitchClips", new Object[] { Clip("glitch_001"), Clip("glitch_002"), Clip("glitch_003"), Clip("glitch_004") });
            Set(controller, "fader", fader);

            var so = new SerializedObject(controller);
            SerializedProperty chapters = so.FindProperty("chapters");
            string[,] chapterData =
            {
                { "MEM_01 // THE MOVING BOXES", TenementScene, "" },
                { "MEM_02 // CENTRAL STATION", StationScene, "ch2" },
                { "MEM_03 // SUBNET: COMMERCIAL DISTRICT", SubnetScene, "ch3" },
                { "MEM_04 // THE CONCRETE PARK", ParkScene, "ch4" },
                { "MEM_05 // THE GHOST LINE", GhostLineScene, "ch5" },
                { "MEM_06 // THE CENTRAL CORE", CoreScene, "ch6" },
            };
            chapters.arraySize = chapterData.GetLength(0);
            for (int i = 0; i < chapters.arraySize; i++)
            {
                SerializedProperty element = chapters.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("title").stringValue = chapterData[i, 0];
                element.FindPropertyRelative("sceneName").stringValue = chapterData[i, 1];
                element.FindPropertyRelative("unlockKey").stringValue = chapterData[i, 2];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ambience);
            EditorSceneManager.SaveScene(scene, ScenesFolder + MenuScene + ".unity");
        }

        private static CanvasGroup MenuPanel(Transform parent, string name)
        {
            GameObject go = UI(name, parent);
            Place(Rect(go), new Vector2(0, 0), new Vector2(0, 0), new Vector2(140, 130), new Vector2(1100, 560));
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            return go.AddComponent<CanvasGroup>();
        }

        private static RetroMenuButton MenuButton(Transform parent, string name, string label, float fontSize = 44)
        {
            GameObject go = UI(name, parent);
            Rect(go).sizeDelta = new Vector2(1000, 64);
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            go.AddComponent<Button>();
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = fontSize + 20;
            element.preferredWidth = 1000;

            TextMeshProUGUI text = Text(go.transform, "Label", label, fontSize, Cyan, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform);

            var button = go.AddComponent<RetroMenuButton>();
            Set(button, "label", text);
            Set(button, "labelText", label);
            return button;
        }

        private static Slider SliderRow(Transform parent, string label, out TextMeshProUGUI valueText)
        {
            GameObject row = UI("Row_" + label, parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = 56;

            TextMeshProUGUI name = Text(row.transform, "Label", label, 34, Cyan, TextAlignmentOptions.Left);
            var nameElement = name.gameObject.AddComponent<LayoutElement>();
            nameElement.preferredWidth = 260;
            nameElement.preferredHeight = 50;
            Localize(name);

            GameObject sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.transform.SetParent(row.transform, false);
            var sliderElement = sliderGo.AddComponent<LayoutElement>();
            sliderElement.preferredWidth = 380;
            sliderElement.preferredHeight = 24;
            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            StyleSelectable(slider);
            TintChild(sliderGo.transform, "Background", new Color(0.12f, 0.16f, 0.2f));
            TintChild(sliderGo.transform, "Fill Area/Fill", Cyan);
            TintChild(sliderGo.transform, "Handle Slide Area/Handle", Color.white);

            valueText = Text(row.transform, "Value", "", 28, Dim, TextAlignmentOptions.Left);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            var valueElement = valueText.gameObject.AddComponent<LayoutElement>();
            valueElement.preferredWidth = 300;
            valueElement.preferredHeight = 50;
            return slider;
        }

        private static Toggle ToggleRow(Transform parent, string label)
        {
            GameObject toggleGo = DefaultControls.CreateToggle(UiResources());
            toggleGo.name = "Toggle_" + label.Replace(' ', '_');
            toggleGo.transform.SetParent(parent, false);
            var element = toggleGo.AddComponent<LayoutElement>();
            element.preferredHeight = 56;
            element.preferredWidth = 700;

            Transform legacyLabel = toggleGo.transform.Find("Label");
            if (legacyLabel != null) Object.DestroyImmediate(legacyLabel.gameObject);

            var background = (RectTransform)toggleGo.transform.Find("Background");
            background.anchorMin = background.anchorMax = new Vector2(0, 0.5f);
            background.pivot = new Vector2(0, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(36, 36);
            background.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.2f);
            var check = (RectTransform)background.Find("Checkmark");
            check.anchorMin = Vector2.zero;
            check.anchorMax = Vector2.one;
            check.offsetMin = new Vector2(4, 4);
            check.offsetMax = new Vector2(-4, -4);
            check.GetComponent<Image>().color = Magenta;

            TextMeshProUGUI text = Text(toggleGo.transform, "Label", label, 34, Cyan, TextAlignmentOptions.Left);
            Stretch(text.rectTransform, 60, 0, 0, 0);
            Localize(text);

            Toggle toggle = toggleGo.GetComponent<Toggle>();
            StyleSelectable(toggle);
            return toggle;
        }

        private static void StyleSelectable(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.normalColor = new Color(0.75f, 0.75f, 0.8f);
            colors.highlightedColor = Magenta;
            colors.selectedColor = Magenta;
            colors.pressedColor = Cyan;
            selectable.colors = colors;
        }

        private static void TintChild(Transform root, string path, Color color)
        {
            Transform child = root.Find(path);
            if (child != null && child.TryGetComponent(out Image image)) image.color = color;
        }

        private static DefaultControls.Resources UiResources()
        {
            return new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            };
        }
    }
}
#endif
