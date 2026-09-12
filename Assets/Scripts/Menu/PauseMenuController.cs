using TMPro;
using TreeFrogAdventure.Player;
using TreeFrogAdventure.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Self-bootstrapping pause menu. Spawns itself into every gameplay scene (skips
    /// MainMenu) with no manual placement needed, builds its own UI at runtime, and
    /// freezes the game via Time.timeScale while open.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        private GameObject _pauseRoot;
        private GameObject _pausePanel;
        private GameObject _pauseOptionsPanel;
        private TMP_FontAsset _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => SpawnIfNeeded(scene);
            SpawnIfNeeded(SceneManager.GetActiveScene());
        }

        private static void SpawnIfNeeded(Scene scene)
        {
            if (scene.name == "MainMenu") return;
            if (FindFirstObjectByType<PauseMenuController>() != null) return;

            var go = new GameObject("PauseMenu");
            go.AddComponent<PauseMenuController>();
        }

        private void Awake()
        {
            IsPaused = false;
            Time.timeScale = 1f;

            EnsureEventSystem();

            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            BuildUI();
        }

        private void Update()
        {
            if (PlayerHealth.IsDead) return;

            // The results screen freezes the timescale itself; pausing over it and then
            // resuming would unfreeze the game underneath it.
            if (Levels.LevelCompleteScreen.IsShowing) return;

            bool pausePressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

            if (pausePressed)
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            var uiModule = esGO.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
        }

        private void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _pauseRoot.SetActive(true);
            _pausePanel.SetActive(true);
            _pauseOptionsPanel.SetActive(false);
        }

        private void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _pauseRoot.SetActive(false);
        }

        private void OnOptionsButton()
        {
            _pausePanel.SetActive(false);
            _pauseOptionsPanel.SetActive(true);
        }

        private void OnBackFromOptions()
        {
            _pausePanel.SetActive(true);
            _pauseOptionsPanel.SetActive(false);
        }

        private void OnMainMenuButton()
        {
            // Don't call Resume() here — it re-locks the cursor for gameplay, but we're
            // heading to a menu that needs the cursor free. MainMenuController takes
            // ownership of cursor state for its own scene on Start().
            IsPaused = false;
            Time.timeScale = 1f;
            LevelLoader.LoadMainMenu();
        }

        private void OnQuitButton()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------
        // UI construction
        // ---------------------------------------------------------------

        private void BuildUI()
        {
            var canvasGO = new GameObject("PauseCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            _pauseRoot = new GameObject("PauseRoot", typeof(RectTransform));
            _pauseRoot.transform.SetParent(canvasGO.transform, false);
            var rootRect = (RectTransform)_pauseRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            CreateFullRectImage("Dim", _pauseRoot.transform, new Color(0f, 0f, 0f, 0.6f));

            _pausePanel = BuildPausePanel(_pauseRoot.transform);
            _pauseOptionsPanel = BuildOptionsPanel(_pauseRoot.transform);

            _pauseRoot.SetActive(false);
        }

        private GameObject CreateFullRectImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return go;
        }

        private TextMeshProUGUI CreateText(string content, Transform parent, int fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, string label, Transform parent, float width, float height,
                                    UiTheme.ButtonKind kind = UiTheme.ButtonKind.Secondary)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            // Styled from UiTheme so this screen matches the rest of the game. The
            // flat 12%-white quad it replaced had no visible hover or press.
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
            if (_font != null) text.font = _font;
            text.fontSize = Mathf.Min(26f, height * 0.45f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private GameObject BuildPausePanel(Transform parent)
        {
            var panel = new GameObject("PausePanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var title = CreateText("Paused", panel.transform, 56);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 200);
            titleRect.sizeDelta = new Vector2(600, 80);

            var buttonContainer = new GameObject("Buttons", typeof(RectTransform));
            buttonContainer.transform.SetParent(panel.transform, false);
            var containerRect = (RectTransform)buttonContainer.transform;
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(400, 340);
            containerRect.anchoredPosition = new Vector2(0, -30);

            var vlg = buttonContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var resumeButton = CreateButton("ResumeButton", "Resume", buttonContainer.transform, 360, 65,
                                            UiTheme.ButtonKind.Primary);
            resumeButton.onClick.AddListener(Resume);

            var optionsButton = CreateButton("OptionsButton", "Options", buttonContainer.transform, 360, 65);
            optionsButton.onClick.AddListener(OnOptionsButton);

            var mainMenuButton = CreateButton("MainMenuButton", "Main Menu", buttonContainer.transform, 360, 65);
            mainMenuButton.onClick.AddListener(OnMainMenuButton);

            var quitButton = CreateButton("QuitButton", "Quit", buttonContainer.transform, 360, 65);
            quitButton.onClick.AddListener(OnQuitButton);

            return panel;
        }

        private GameObject BuildOptionsPanel(Transform parent)
        {
            var panel = new GameObject("PauseOptionsPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var title = CreateText("Options", panel.transform, 48);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 280);
            titleRect.sizeDelta = new Vector2(600, 70);

            var contentContainer = new GameObject("Content", typeof(RectTransform));
            contentContainer.transform.SetParent(panel.transform, false);
            var contentRect = (RectTransform)contentContainer.transform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(800, 700);
            contentRect.anchoredPosition = new Vector2(0, -60);

            OptionsPanelBuilder.Style style = OptionsPanelBuilder.Style.Pause;
            OptionsPanelBuilder.ApplyContentLayout(contentContainer, style);

            // Shared with the main-menu options screen; these were two separate copies
            // of the same layout and had to be edited in lockstep.
            OptionsPanelBuilder.Controls controls =
                OptionsPanelBuilder.BuildContent(contentContainer.transform, _font, style);

            var backButton = CreateButton("BackButton", "Back", panel.transform, 200, 55);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0, 60);
            backButton.onClick.AddListener(OnBackFromOptions);

            var optionsPanelComponent = panel.AddComponent<OptionsPanel>();
            OptionsPanelBuilder.Assign(optionsPanelComponent, controls);

            return panel;
        }
    }
}
