using TMPro;
using TreeFrogAdventure.Player;
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

        private Button CreateButton(string name, string label, Transform parent, float width, float height)
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
            if (_font != null) text.font = _font;
            text.fontSize = Mathf.Min(26f, height * 0.45f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private Slider BuildSlider(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350, 20);

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

        private Toggle BuildToggle(GameObject go)
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

            var resumeButton = CreateButton("ResumeButton", "Resume", buttonContainer.transform, 360, 65);
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
            contentRect.sizeDelta = new Vector2(800, 480);
            contentRect.anchoredPosition = new Vector2(0, -30);

            var vlg = contentContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 25;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var soundHeader = CreateText("Sound", contentContainer.transform, 32, TextAlignmentOptions.Left);
            soundHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;

            var volumeRow = new GameObject("VolumeRow", typeof(RectTransform));
            volumeRow.transform.SetParent(contentContainer.transform, false);
            volumeRow.AddComponent<LayoutElement>().preferredHeight = 45;
            var volumeRowHlg = volumeRow.AddComponent<HorizontalLayoutGroup>();
            volumeRowHlg.spacing = 20;
            volumeRowHlg.childAlignment = TextAnchor.MiddleLeft;
            volumeRowHlg.childControlWidth = true;
            volumeRowHlg.childControlHeight = true;
            volumeRowHlg.childForceExpandWidth = false;
            volumeRowHlg.childForceExpandHeight = true;

            var volumeLabel = CreateText("Master Volume", volumeRow.transform, 24, TextAlignmentOptions.Left);
            volumeLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;

            var sliderGO = new GameObject("VolumeSlider", typeof(RectTransform));
            sliderGO.transform.SetParent(volumeRow.transform, false);
            sliderGO.AddComponent<LayoutElement>().preferredWidth = 350;
            var slider = BuildSlider(sliderGO);

            var muteRow = new GameObject("MuteRow", typeof(RectTransform));
            muteRow.transform.SetParent(contentContainer.transform, false);
            muteRow.AddComponent<LayoutElement>().preferredHeight = 45;
            var muteRowHlg = muteRow.AddComponent<HorizontalLayoutGroup>();
            muteRowHlg.spacing = 20;
            muteRowHlg.childAlignment = TextAnchor.MiddleLeft;
            muteRowHlg.childControlWidth = true;
            muteRowHlg.childControlHeight = true;
            muteRowHlg.childForceExpandWidth = false;
            muteRowHlg.childForceExpandHeight = true;

            var muteLabel = CreateText("Mute", muteRow.transform, 24, TextAlignmentOptions.Left);
            muteLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 220;

            var toggleGO = new GameObject("MuteToggle", typeof(RectTransform));
            toggleGO.transform.SetParent(muteRow.transform, false);
            toggleGO.AddComponent<LayoutElement>().preferredWidth = 45;
            var toggle = BuildToggle(toggleGO);

            var controlsHeader = CreateText("Controls", contentContainer.transform, 32, TextAlignmentOptions.Left);
            controlsHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;

            const string controlsText =
                "Move — WASD / Arrow Keys / Gamepad Left Stick\n" +
                "Look — Mouse / Gamepad Right Stick\n" +
                "Jump — Space / Gamepad South Button\n" +
                "Sprint — Left Shift / Gamepad Left Stick Press\n" +
                "Attack — Left Mouse Button / Enter / Gamepad West Button\n" +
                "Interact (Hold) — E / Gamepad North Button\n" +
                "Tongue — G";

            var controlsBody = CreateText(controlsText, contentContainer.transform, 20, TextAlignmentOptions.Left);
            controlsBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;

            var backButton = CreateButton("BackButton", "Back", panel.transform, 200, 55);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0, 60);
            backButton.onClick.AddListener(OnBackFromOptions);

            var optionsPanelComponent = panel.AddComponent<OptionsPanel>();
            optionsPanelComponent.volumeSlider = slider;
            optionsPanelComponent.muteToggle = toggle;

            return panel;
        }
    }
}
