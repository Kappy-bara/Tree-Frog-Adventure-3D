using System.Collections.Generic;
using System.Linq;
using TMPro;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// One-time generator: run from the Unity menu to build and save the Main Menu scene
    /// (Play/Options/Quit, 20-level select grid, sound + camera + controls options) using real
    /// UnityEngine.UI/TMPro APIs rather than hand-authored scene YAML. Safe to re-run.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        private const string CoreSystemsGuid = "511cd4a52bad1364dbaccea691f042d9";
        private const string TmpFontGuid = "8f586378b4e144a9851e7b34d9b748ee";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

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
            UiTheme.Stretch(go);

            // A solid ground for every screen. Without it the menu floated on the
            // camera's clear colour and read as unfinished.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(go.transform, false);
            UiTheme.Stretch(backdrop);

            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.035f, 0.075f, 0.06f, 1f);
            backdropImage.raycastTarget = false;

            // A soft pool of light behind the content, so the middle of the screen is
            // not as flat as the edges.
            var glow = new GameObject("Glow", typeof(RectTransform));
            glow.transform.SetParent(go.transform, false);

            var glowRect = (RectTransform)glow.transform;
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(1700f, 1300f);
            glowRect.anchoredPosition = new Vector2(0f, 40f);

            var glowImage = glow.AddComponent<Image>();
            glowImage.sprite = UiTheme.Glow;
            glowImage.color = new Color(0.16f, 0.42f, 0.28f, 0.55f);
            glowImage.raycastTarget = false;

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

        private static Button CreateButton(string name, string label, Transform parent, TMP_FontAsset font,
                                           float width, float height,
                                           UiTheme.ButtonKind kind = UiTheme.ButtonKind.Secondary)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            // Styled through UiTheme so the menu, the pause screen and the results
            // screen are recognisably the same interface. The old flat 12%-white quad
            // had no hover, no press and no visible disabled state.
            var image = go.AddComponent<Image>();
            image.sprite = UiTheme.Pill;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UiTheme.ButtonColors(kind);

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
            text.fontSize = Mathf.Min(UiTheme.SubheadingSize, height * 0.42f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = kind == UiTheme.ButtonKind.Primary
                ? new Color(0.05f, 0.12f, 0.08f)
                : UiTheme.TextPrimary;
            text.raycastTarget = false;

            return button;
        }

        private static GameObject BuildMainPanel(Transform canvasTransform, TMP_FontAsset font, MainMenuController controller)
        {
            var panel = CreateFullRectPanel("MainPanel", canvasTransform);

            var title = CreateText("TREE FROG", panel.transform, font, 96);
            title.color = UiTheme.Accent;
            title.characterSpacing = 14f;

            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -120);
            titleRect.sizeDelta = new Vector2(1200, 110);

            var subtitle = CreateText("A D V E N T U R E", panel.transform, font, 34);
            subtitle.color = UiTheme.TextMuted;
            subtitle.characterSpacing = 8f;

            var subtitleRect = (RectTransform)subtitle.transform;
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0, -224);
            subtitleRect.sizeDelta = new Vector2(1200, 50);

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

            // Play is the primary action and is coloured as such; the old menu gave
            // all three buttons the same weight, so nothing said where to start.
            var playButton = CreateButton("PlayButton", "Play", buttonContainer.transform, font,
                                          UiTheme.ButtonWidth, UiTheme.ButtonHeight,
                                          UiTheme.ButtonKind.Primary);
            var optionsButton = CreateButton("OptionsButton", "Options", buttonContainer.transform, font,
                                             UiTheme.ButtonWidth, UiTheme.ButtonHeight);
            var quitButton = CreateButton("QuitButton", "Quit", buttonContainer.transform, font,
                                          UiTheme.ButtonWidth, UiTheme.ButtonHeight);

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
                rowGO.AddComponent<LayoutElement>().preferredHeight = 196;

                var rowVlg = rowGO.AddComponent<VerticalLayoutGroup>();
                rowVlg.spacing = 8;
                rowVlg.childAlignment = TextAnchor.UpperCenter;
                rowVlg.childControlWidth = true;
                rowVlg.childControlHeight = true;
                rowVlg.childForceExpandWidth = true;
                rowVlg.childForceExpandHeight = false;

                var worldLabel = CreateText(
                    $"WORLD {world}  \u2014  {LevelProgress.BiomeDisplayNames[world - 1].ToUpperInvariant()}",
                    rowGO.transform, font, 26);
                worldLabel.color = UiTheme.TextMuted;
                worldLabel.characterSpacing = 6f;
                worldLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

                var buttonsRowGO = new GameObject("LevelButtons", typeof(RectTransform));
                buttonsRowGO.transform.SetParent(rowGO.transform, false);
                buttonsRowGO.AddComponent<LayoutElement>().preferredHeight = 128;

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

                    var levelButton = CreateButton($"Level{levelIndex}Button", string.Empty,
                                                   buttonsRowGO.transform, font, 116, 116);

                    // The number sits high so the progress row below it has somewhere
                    // to live inside the tile.
                    TextMeshProUGUI number = levelButton.GetComponentInChildren<TextMeshProUGUI>();
                    number.text = levelIndex.ToString();
                    number.fontSize = 44;
                    number.alignment = TextAlignmentOptions.Top;
                    ((RectTransform)number.transform).offsetMin = new Vector2(0f, 26f);

                    // A tint over a completed level, so a glance at the grid says how
                    // far you have got without reading twenty numbers.
                    var completedTint = new GameObject("CompletedTint", typeof(RectTransform));
                    completedTint.transform.SetParent(levelButton.transform, false);
                    UiTheme.Stretch(completedTint);

                    var tintImage = completedTint.AddComponent<Image>();
                    tintImage.sprite = UiTheme.PillOutline;
                    tintImage.type = Image.Type.Sliced;
                    tintImage.color = UiTheme.Gold;
                    tintImage.raycastTarget = false;
                    completedTint.SetActive(false);

                    // ---- progress row -------------------------------------
                    // SaveSystem has recorded a crystal, a secret count and a best time
                    // per level since the save system was written, and nothing ever
                    // displayed any of it. This is where it surfaces.
                    var progress = new GameObject("Progress", typeof(RectTransform));
                    progress.transform.SetParent(levelButton.transform, false);

                    var progressRect = (RectTransform)progress.transform;
                    progressRect.anchorMin = new Vector2(0f, 0f);
                    progressRect.anchorMax = new Vector2(1f, 0f);
                    progressRect.pivot = new Vector2(0.5f, 0f);
                    progressRect.anchoredPosition = new Vector2(0f, 26f);
                    progressRect.sizeDelta = new Vector2(0f, 22f);

                    var progressRow = progress.AddComponent<HorizontalLayoutGroup>();
                    progressRow.spacing = 6f;
                    progressRow.childAlignment = TextAnchor.MiddleCenter;
                    progressRow.childControlWidth = true;
                    progressRow.childControlHeight = true;
                    progressRow.childForceExpandWidth = false;
                    progressRow.childForceExpandHeight = false;

                    var crystalPip = new GameObject("CrystalPip", typeof(RectTransform));
                    crystalPip.transform.SetParent(progress.transform, false);

                    var pipLayout = crystalPip.AddComponent<LayoutElement>();
                    pipLayout.preferredWidth = 14f;
                    pipLayout.preferredHeight = 14f;

                    var pipImage = crystalPip.AddComponent<Image>();
                    pipImage.sprite = UiTheme.Circle;
                    pipImage.color = UiTheme.Crystal;
                    pipImage.raycastTarget = false;
                    crystalPip.SetActive(false);

                    TextMeshProUGUI secrets = CreateText("0", progress.transform, font, 18,
                                                         TextAlignmentOptions.Center);
                    secrets.color = UiTheme.Accent;
                    secrets.gameObject.name = "Secrets";
                    secrets.gameObject.AddComponent<LayoutElement>().preferredWidth = 24f;
                    secrets.gameObject.SetActive(false);

                    TextMeshProUGUI bestTime = CreateText(string.Empty, levelButton.transform, font, 16,
                                                          TextAlignmentOptions.Center);
                    bestTime.color = UiTheme.TextMuted;
                    bestTime.gameObject.name = "BestTime";

                    var bestRect = (RectTransform)bestTime.transform;
                    bestRect.anchorMin = new Vector2(0f, 0f);
                    bestRect.anchorMax = new Vector2(1f, 0f);
                    bestRect.pivot = new Vector2(0.5f, 0f);
                    bestRect.anchoredPosition = new Vector2(0f, 8f);
                    bestRect.sizeDelta = new Vector2(0f, 20f);
                    bestTime.gameObject.SetActive(false);

                    // ---- locked veil --------------------------------------
                    var lockedOverlay = new GameObject("LockedOverlay", typeof(RectTransform));
                    lockedOverlay.transform.SetParent(levelButton.transform, false);
                    UiTheme.Stretch(lockedOverlay);

                    var overlayImage = lockedOverlay.AddComponent<Image>();
                    overlayImage.sprite = UiTheme.Pill;
                    overlayImage.type = Image.Type.Sliced;
                    overlayImage.color = new Color(0.02f, 0.04f, 0.03f, 0.82f);
                    overlayImage.raycastTarget = false;

                    CreateText("\u2715", lockedOverlay.transform, font, 34,
                               TextAlignmentOptions.Center).color = UiTheme.TextDisabled;

                    var levelButtonUI = levelButton.gameObject.AddComponent<LevelButtonUI>();
                    levelButtonUI.button = levelButton;
                    levelButtonUI.label = number;
                    levelButtonUI.lockedOverlay = lockedOverlay;
                    levelButtonUI.crystalPip = crystalPip;
                    levelButtonUI.secretsLabel = secrets;
                    levelButtonUI.bestTimeLabel = bestTime;
                    levelButtonUI.completedTint = tintImage;

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
            // Taller than it was: the panel now carries a Camera section as well.
            contentRect.sizeDelta = new Vector2(900, 800);
            contentRect.anchoredPosition = new Vector2(0, -40);

            OptionsPanelBuilder.Style style = OptionsPanelBuilder.Style.Menu;
            OptionsPanelBuilder.ApplyContentLayout(contentContainer, style);

            // Rows are built by the shared builder, which PauseMenuController also
            // calls. The two panels used to be separate copies of the same layout.
            OptionsPanelBuilder.Controls controls =
                OptionsPanelBuilder.BuildContent(contentContainer.transform, font, style);

            var optionsPanelComponent = panel.AddComponent<OptionsPanel>();
            OptionsPanelBuilder.Assign(optionsPanelComponent, controls);

            return panel;
        }

        private static void UpdateBuildSettings()
        {
            var existing = EditorBuildSettings.scenes
                .Where(s => !s.path.EndsWith("MainMenu.unity") && !s.path.EndsWith("Level_1_Forest.unity"))
                .ToList();

            // Level_1_Forest is the pre-split 27 MB scene, orphaned when levels moved
            // to the additive environment + gameplay layout. It used to be re-added
            // here on every run, dragging it back into every build; it is now filtered
            // out above and never re-added, so running this tool also cleans it up.
            var newList = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            newList.AddRange(existing);

            EditorBuildSettings.scenes = newList.ToArray();
        }
    }
}
