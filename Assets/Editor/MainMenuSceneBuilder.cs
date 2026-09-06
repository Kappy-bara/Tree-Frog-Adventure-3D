using System.Collections.Generic;
using System.Linq;
using TMPro;
using TreeFrogAdventure.Menu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// One-time generator: run from the Unity menu to build and save the Main Menu scene
    /// (Play/Options/Quit, 25-level select grid, sound + controls options) using real
    /// UnityEngine.UI/TMPro APIs rather than hand-authored scene YAML. Safe to re-run.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string CoreSystemsGuid = "511cd4a52bad1364dbaccea691f042d9";
        private const string TmpFontGuid = "8f586378b4e144a9851e7b34d9b748ee";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level_1_Forest.unity";

        [MenuItem("Tools/Tree Frog/Build Main Menu Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            InstantiateCoreSystems();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TmpFontGuid));
            if (font == null)
            {
                Debug.LogWarning("MainMenuSceneBuilder: could not resolve the TMP font asset; text will use TMP's fallback default.");
            }

            var canvasGO = CreateCanvas();
            var controller = canvasGO.AddComponent<MainMenuController>();

            var mainPanel = BuildMainPanel(canvasGO.transform, font, controller);
            var playPanel = BuildPlayPanel(canvasGO.transform, font, controller);
            var optionsPanel = BuildOptionsPanel(canvasGO.transform, font, controller);

            controller.mainPanel = mainPanel;
            controller.playPanel = playPanel;
            controller.optionsPanel = optionsPanel;

            mainPanel.SetActive(true);
            playPanel.SetActive(false);
            optionsPanel.SetActive(false);

            Canvas.ForceUpdateCanvases();

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();

            EditorUtility.DisplayDialog("Main Menu Scene", "MainMenu.unity created and added to Build Settings (index 0).", "OK");
        }

        private static void CreateCamera()
        {
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";

            var cam = cameraGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1A, 0x1A, 0x26, 255);

            cameraGO.AddComponent<AudioListener>();
        }

        private static void InstantiateCoreSystems()
        {
            string path = AssetDatabase.GUIDToAssetPath(CoreSystemsGuid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogWarning("MainMenuSceneBuilder: CoreSystems prefab not found; add an EventSystem manually or UI input will not work.");
            }
        }

        private static GameObject CreateCanvas()
        {
            var canvasGO = new GameObject("MainMenuCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();
            return canvasGO;
        }

        private static GameObject CreateFullRectPanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI CreateText(string content, Transform parent, TMP_FontAsset font, int fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, string label, Transform parent, TMP_FontAsset font, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGO.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = font;
            text.fontSize = Mathf.Min(28f, height * 0.5f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private static Slider BuildSlider(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 20);

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = (RectTransform)fillAreaGO.transform;
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.75f, 0.4f, 1f);

            var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGO.transform.SetParent(go.transform, false);
            var handleAreaRect = (RectTransform)handleAreaGO.transform;
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGO = new GameObject("Handle", typeof(RectTransform));
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = (RectTransform)handleGO.transform;
            handleRect.sizeDelta = new Vector2(20, 20);
            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = Color.white;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        private static Toggle BuildToggle(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(40, 40);

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(1, 1, 1, 0.15f);

            var checkGO = new GameObject("Checkmark", typeof(RectTransform));
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRect = (RectTransform)checkGO.transform;
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            var checkImage = checkGO.AddComponent<Image>();
            checkImage.color = new Color(0.3f, 0.75f, 0.4f, 1f);

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;

            return toggle;
        }

        private static GameObject BuildMainPanel(Transform canvasTransform, TMP_FontAsset font, MainMenuController controller)
        {
            var panel = CreateFullRectPanel("MainPanel", canvasTransform);

            var title = CreateText("Tree Frog Adventure", panel.transform, font, 64);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -100);
            titleRect.sizeDelta = new Vector2(1200, 100);

            var buttonContainer = new GameObject("Buttons", typeof(RectTransform));
            buttonContainer.transform.SetParent(panel.transform, false);
            var containerRect = (RectTransform)buttonContainer.transform;
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(400, 260);

            var vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var playButton = CreateButton("PlayButton", "Play", buttonContainer.transform, font, 360, 70);
            var optionsButton = CreateButton("OptionsButton", "Options", buttonContainer.transform, font, 360, 70);
            var quitButton = CreateButton("QuitButton", "Quit", buttonContainer.transform, font, 360, 70);

            UnityEventTools.AddPersistentListener(playButton.onClick, controller.OnPlayButton);
            UnityEventTools.AddPersistentListener(optionsButton.onClick, controller.OnOptionsButton);
            UnityEventTools.AddPersistentListener(quitButton.onClick, controller.OnQuitButton);

            return panel;
        }

        private static Button CreateBackButton(Transform parent, TMP_FontAsset font, MainMenuController controller)
        {
            var backButton = CreateButton("BackButton", "Back", parent, font, 200, 60);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0, 30);
            UnityEventTools.AddPersistentListener(backButton.onClick, controller.OnBackToMain);
            return backButton;
        }

        private static GameObject BuildPlayPanel(Transform canvasTransform, TMP_FontAsset font, MainMenuController controller)
        {
            var panel = CreateFullRectPanel("PlayPanel", canvasTransform);

            var header = CreateText("Select Level", panel.transform, font, 48);
            var headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -40);
            headerRect.sizeDelta = new Vector2(800, 80);

            CreateBackButton(panel.transform, font, controller);

            var scrollGO = new GameObject("LevelScrollView", typeof(RectTransform));
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRectTransform = (RectTransform)scrollGO.transform;
            scrollRectTransform.anchorMin = new Vector2(0.1f, 0f);
            scrollRectTransform.anchorMax = new Vector2(0.9f, 1f);
            scrollRectTransform.offsetMin = new Vector2(0, 120);
            scrollRectTransform.offsetMax = new Vector2(0, -140);

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = (RectTransform)viewportGO.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.05f);
            var mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 30;
            contentVlg.padding = new RectOffset(10, 10, 20, 20);
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            var levelButtons = new LevelButtonUI[LevelProgress.TotalLevels];
            int levelIndex = 0;

            for (int world = 1; world <= LevelProgress.TotalWorlds; world++)
            {
                var rowGO = new GameObject($"World{world}Row", typeof(RectTransform));
                rowGO.transform.SetParent(contentGO.transform, false);
                rowGO.AddComponent<LayoutElement>().preferredHeight = 160;

                var rowVlg = rowGO.AddComponent<VerticalLayoutGroup>();
                rowVlg.spacing = 8;
                rowVlg.childAlignment = TextAnchor.UpperCenter;
                rowVlg.childControlWidth = true;
                rowVlg.childControlHeight = true;
                rowVlg.childForceExpandWidth = true;
                rowVlg.childForceExpandHeight = false;

                var worldLabel = CreateText($"World {world}", rowGO.transform, font, 32);
                worldLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

                var buttonsRowGO = new GameObject("LevelButtons", typeof(RectTransform));
                buttonsRowGO.transform.SetParent(rowGO.transform, false);
                buttonsRowGO.AddComponent<LayoutElement>().preferredHeight = 100;

                var hlg = buttonsRowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 20;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                for (int levelInWorld = 1; levelInWorld <= LevelProgress.LevelsPerWorld; levelInWorld++)
                {
                    levelIndex++;
                    var levelButton = CreateButton($"Level{levelIndex}Button", levelIndex.ToString(), buttonsRowGO.transform, font, 90, 90);

                    var lockedOverlay = new GameObject("LockedOverlay", typeof(RectTransform));
                    lockedOverlay.transform.SetParent(levelButton.transform, false);
                    var overlayRect = (RectTransform)lockedOverlay.transform;
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;
                    var overlayImage = lockedOverlay.AddComponent<Image>();
                    overlayImage.color = new Color(0f, 0f, 0f, 0.75f);
                    overlayImage.raycastTarget = false;

                    var levelButtonUI = levelButton.gameObject.AddComponent<LevelButtonUI>();
                    levelButtonUI.button = levelButton;
                    levelButtonUI.label = levelButton.GetComponentInChildren<TextMeshProUGUI>();
                    levelButtonUI.lockedOverlay = lockedOverlay;

                    levelButtons[levelIndex - 1] = levelButtonUI;
                }
            }

            var levelSelectPanel = panel.AddComponent<LevelSelectPanel>();
            levelSelectPanel.levelButtons = levelButtons;

            return panel;
        }

        private static GameObject BuildOptionsPanel(Transform canvasTransform, TMP_FontAsset font, MainMenuController controller)
        {
            var panel = CreateFullRectPanel("OptionsPanel", canvasTransform);

            var header = CreateText("Options", panel.transform, font, 48);
            var headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0, -40);
            headerRect.sizeDelta = new Vector2(800, 80);

            CreateBackButton(panel.transform, font, controller);

            var contentContainer = new GameObject("Content", typeof(RectTransform));
            contentContainer.transform.SetParent(panel.transform, false);
            var contentRect = (RectTransform)contentContainer.transform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(900, 620);
            contentRect.anchoredPosition = new Vector2(0, -20);

            var vlg = contentContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 30;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var soundHeader = CreateText("Sound", contentContainer.transform, font, 36, TextAlignmentOptions.Left);
            soundHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            var volumeRow = new GameObject("VolumeRow", typeof(RectTransform));
            volumeRow.transform.SetParent(contentContainer.transform, false);
            volumeRow.AddComponent<LayoutElement>().preferredHeight = 50;
            var volumeRowHlg = volumeRow.AddComponent<HorizontalLayoutGroup>();
            volumeRowHlg.spacing = 20;
            volumeRowHlg.childAlignment = TextAnchor.MiddleLeft;
            volumeRowHlg.childControlWidth = true;
            volumeRowHlg.childControlHeight = true;
            volumeRowHlg.childForceExpandWidth = false;
            volumeRowHlg.childForceExpandHeight = true;

            var volumeLabel = CreateText("Master Volume", volumeRow.transform, font, 28, TextAlignmentOptions.Left);
            volumeLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 260;

            var sliderGO = new GameObject("VolumeSlider", typeof(RectTransform));
            sliderGO.transform.SetParent(volumeRow.transform, false);
            sliderGO.AddComponent<LayoutElement>().preferredWidth = 400;
            var slider = BuildSlider(sliderGO);

            var muteRow = new GameObject("MuteRow", typeof(RectTransform));
            muteRow.transform.SetParent(contentContainer.transform, false);
            muteRow.AddComponent<LayoutElement>().preferredHeight = 50;
            var muteRowHlg = muteRow.AddComponent<HorizontalLayoutGroup>();
            muteRowHlg.spacing = 20;
            muteRowHlg.childAlignment = TextAnchor.MiddleLeft;
            muteRowHlg.childControlWidth = true;
            muteRowHlg.childControlHeight = true;
            muteRowHlg.childForceExpandWidth = false;
            muteRowHlg.childForceExpandHeight = true;

            var muteLabel = CreateText("Mute", muteRow.transform, font, 28, TextAlignmentOptions.Left);
            muteLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 260;

            var toggleGO = new GameObject("MuteToggle", typeof(RectTransform));
            toggleGO.transform.SetParent(muteRow.transform, false);
            toggleGO.AddComponent<LayoutElement>().preferredWidth = 50;
            var toggle = BuildToggle(toggleGO);

            var controlsHeader = CreateText("Controls", contentContainer.transform, font, 36, TextAlignmentOptions.Left);
            controlsHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;

            const string controlsText =
                "Move — WASD / Arrow Keys / Gamepad Left Stick\n" +
                "Look — Mouse / Gamepad Right Stick\n" +
                "Jump — Space / Gamepad South Button\n" +
                "Sprint — Left Shift / Gamepad Left Stick Press\n" +
                "Attack — Left Mouse Button / Enter / Gamepad West Button\n" +
                "Interact (Hold) — E / Gamepad North Button\n" +
                "Tongue — G";

            var controlsBody = CreateText(controlsText, contentContainer.transform, font, 24, TextAlignmentOptions.Left);
            controlsBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 240;

            var optionsPanelComponent = panel.AddComponent<OptionsPanel>();
            optionsPanelComponent.volumeSlider = slider;
            optionsPanelComponent.muteToggle = toggle;

            return panel;
        }

        private static void UpdateBuildSettings()
        {
            var existing = EditorBuildSettings.scenes
                .Where(s => !s.path.EndsWith("MainMenu.unity") && !s.path.EndsWith("Level_1_Forest.unity"))
                .ToList();

            var newList = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true)
            };
            newList.AddRange(existing);

            EditorBuildSettings.scenes = newList.ToArray();
        }
    }
}
