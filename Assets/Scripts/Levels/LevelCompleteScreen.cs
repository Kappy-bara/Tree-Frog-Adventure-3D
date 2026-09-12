using TMPro;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// The results screen shown when a level is finished.
    ///
    /// Replaces a one-second decorative fade that auto-advanced to the next level: the
    /// save already recorded a completion time, a crystal and a secret count, and none
    /// of it was ever shown to anyone. This is where that data surfaces, and it is also
    /// the first point in the game where finishing a level is a decision rather than a
    /// cutscene — Next, Retry, or stop.
    ///
    /// Built in code following PlayerHealth's game-over screen, so the project has one
    /// way of making a full-screen menu rather than a prefab for this one and code for
    /// the other two.
    /// </summary>
    public class LevelCompleteScreen : MonoBehaviour
    {
        /// <summary>
        /// True while the results screen is up. PauseMenuController reads this so
        /// Escape cannot open the pause menu on top of it and set the timescale back
        /// to 1 underneath a screen that expects to be frozen.
        /// </summary>
        public static bool IsShowing { get; private set; }

        /// <summary>Everything the screen reports. Gathered by LevelGoal.</summary>
        public struct Result
        {
            public int Level;
            public float TimeSeconds;

            /// <summary>Best time BEFORE this run. 0 when there was none.</summary>
            public float PreviousBestSeconds;

            public int Coins;
            public int SecretsFound;
            public int SecretsInLevel;
            public bool CrystalFound;
        }

        private TMP_FontAsset _font;
        private Result _result;

        public static LevelCompleteScreen Show(Result result)
        {
            var go = new GameObject("LevelCompleteScreen");
            var screen = go.AddComponent<LevelCompleteScreen>();
            screen._result = result;
            screen.Build();
            return screen;
        }

        private void Build()
        {
            EnsureEventSystem();
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the HUD and the boss bar, below the pause menu — pausing is
            // disabled here anyway, but the ordering should still make sense.
            canvas.sortingOrder = 190;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            gameObject.AddComponent<GraphicRaycaster>();

            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(transform, false);
            var dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dim.AddComponent<Image>().color = new Color(0f, 0.08f, 0.05f, 0.78f);

            var (world, levelInWorld) = LevelProgress.GetWorldAndLevel(_result.Level);

            var title = CreateText("Level Complete", transform, 74);
            Place((RectTransform)title.transform, new Vector2(0f, 330f), new Vector2(1000f, 100f));

            var subtitle = CreateText(
                $"{LevelProgress.GetBiomeName(_result.Level)} — Level {levelInWorld} of {LevelProgress.LevelsPerWorld}",
                transform, 34);
            subtitle.color = new Color(0.75f, 0.9f, 0.8f);
            Place((RectTransform)subtitle.transform, new Vector2(0f, 262f), new Vector2(1000f, 50f));

            BuildStats();
            BuildButtons();

            // Frozen while the screen is up, exactly as the game-over screen does. Every
            // button restores it, including the one that leaves the scene.
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            IsShowing = true;
        }

        private void OnDestroy() => IsShowing = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay() => IsShowing = false;

        // --------------------------------------------------------------- stats --

        private void BuildStats()
        {
            var container = new GameObject("Stats", typeof(RectTransform));
            container.transform.SetParent(transform, false);
            Place((RectTransform)container.transform, new Vector2(0f, 60f), new Vector2(720f, 300f));

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            bool isBest = _result.PreviousBestSeconds <= 0f
                          || _result.TimeSeconds < _result.PreviousBestSeconds;

            AddRow(container.transform, "Time", FormatTime(_result.TimeSeconds),
                   isBest ? new Color(1f, 0.9f, 0.4f) : Color.white);

            AddRow(container.transform, "Best",
                   _result.PreviousBestSeconds > 0f
                       ? FormatTime(Mathf.Min(_result.PreviousBestSeconds, _result.TimeSeconds))
                       : FormatTime(_result.TimeSeconds),
                   new Color(0.7f, 0.75f, 0.8f));

            AddRow(container.transform, "Coins", _result.Coins.ToString(), Color.white);

            AddRow(container.transform, "Secrets",
                   _result.SecretsInLevel > 0
                       ? $"{_result.SecretsFound} / {_result.SecretsInLevel}"
                       : _result.SecretsFound.ToString(),
                   _result.SecretsInLevel > 0 && _result.SecretsFound >= _result.SecretsInLevel
                       ? new Color(0.5f, 1f, 0.6f)
                       : Color.white);

            AddRow(container.transform, "Nature Crystal",
                   _result.CrystalFound ? "Recovered" : "Missed",
                   _result.CrystalFound ? new Color(0.5f, 1f, 0.6f) : new Color(0.8f, 0.5f, 0.5f));

            if (isBest && _result.PreviousBestSeconds > 0f)
            {
                var best = CreateText("New best time!", container.transform, 30);
                best.color = new Color(1f, 0.9f, 0.4f);
                best.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            }
        }

        private void AddRow(Transform parent, string label, string value, Color valueColor)
        {
            var row = new GameObject($"{label}Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 42;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var labelText = CreateText(label, row.transform, 32);
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = new Color(0.7f, 0.78f, 0.74f);
            labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 360;

            var valueText = CreateText(value, row.transform, 32);
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.color = valueColor;
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
        }

        // ------------------------------------------------------------- buttons --

        private void BuildButtons()
        {
            var container = new GameObject("Buttons", typeof(RectTransform));
            container.transform.SetParent(transform, false);
            Place((RectTransform)container.transform, new Vector2(0f, -290f), new Vector2(1000f, 90f));

            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            bool hasNext = _result.Level < LevelProgress.TotalLevels
                           && LevelProgress.IsLevelUnlocked(_result.Level + 1);

            if (hasNext)
            {
                CreateButton("NextButton", "Next Level", container.transform, 300f, 78f)
                    .onClick.AddListener(OnNext);
            }

            CreateButton("RetryButton", "Retry", container.transform, 240f, 78f)
                .onClick.AddListener(OnRetry);

            CreateButton("MenuButton", "Main Menu", container.transform, 300f, 78f)
                .onClick.AddListener(OnMainMenu);
        }

        private void OnNext()
        {
            IsShowing = false;
            Time.timeScale = 1f;
            LevelLoader.LoadLevel(_result.Level + 1);
        }

        private void OnRetry()
        {
            IsShowing = false;
            Time.timeScale = 1f;

            // Clears the checkpoint and the secret count for the level being replayed.
            // Coins deliberately survive: they are a running total across the whole run,
            // so clearing them here would delete what earlier levels earned.
            GameSession.ResetForLevel(0);
            LevelLoader.ReloadCurrentLevel();
        }

        private void OnMainMenu()
        {
            IsShowing = false;
            Time.timeScale = 1f;
            LevelLoader.LoadMainMenu();
        }

        // ------------------------------------------------------------- helpers --

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "—";

            int whole = Mathf.FloorToInt(seconds);
            int minutes = whole / 60;
            int secs = whole % 60;
            int hundredths = Mathf.FloorToInt((seconds - whole) * 100f);

            return $"{minutes:0}:{secs:00}.{hundredths:00}";
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private TextMeshProUGUI CreateText(string content, Transform parent, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
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
            text.fontSize = 30;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
