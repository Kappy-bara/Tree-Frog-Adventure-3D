using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Builds the body of the options screen.
    ///
    /// MainMenuSceneBuilder and PauseMenuController were building byte-identical panels
    /// from two separate copies of the same 90 lines, so every new row had to be added
    /// twice and the two could silently drift apart. This is the single copy.
    ///
    /// Lives in runtime code rather than under Editor/ precisely so both can call it —
    /// the main menu builds its panel at edit time and the pause menu at runtime.
    /// </summary>
    public static class OptionsPanelBuilder
    {
        /// <summary>The controls the caller has to hand to an <see cref="OptionsPanel"/>.</summary>
        public struct Controls
        {
            public Slider Volume;
            public Toggle Mute;
            public Slider MouseSensitivity;
            public Slider GamepadSensitivity;
            public Toggle InvertY;
        }

        /// <summary>
        /// Sizes differ between the two panels (the main menu has more room than a
        /// pause overlay), so the layout is parameterised rather than duplicated.
        /// </summary>
        public struct Style
        {
            public int HeaderSize;
            public int LabelSize;
            public int BodySize;
            public float RowHeight;
            public float LabelWidth;
            public float SliderWidth;
            public float ToggleWidth;
            public float Spacing;

            public static Style Menu => new Style
            {
                HeaderSize = 36, LabelSize = 28, BodySize = 24,
                RowHeight = 50, LabelWidth = 300, SliderWidth = 380,
                ToggleWidth = 50, Spacing = 22,
            };

            public static Style Pause => new Style
            {
                HeaderSize = 32, LabelSize = 24, BodySize = 20,
                RowHeight = 45, LabelWidth = 260, SliderWidth = 330,
                ToggleWidth = 45, Spacing = 18,
            };
        }

        public const string ControlsText =
            "Move — WASD / Arrow Keys / Gamepad Left Stick\n" +
            "Look — Mouse / Gamepad Right Stick\n" +
            "Jump — Space / Gamepad South Button\n" +
            "Double Jump — Space again in mid-air\n" +
            "Wall Climb — Hold Left Shift against a wall\n" +
            "Swim — Move in water, Space to surface\n" +
            "Attack — Left Mouse Button / Enter / Gamepad West Button\n" +
            "Interact (Hold) — E / Gamepad North Button\n" +
            "Tongue — G";

        /// <summary>
        /// Fills <paramref name="content"/> with every options row and returns the
        /// controls. The caller owns the container, its layout group and the Back button.
        /// </summary>
        public static Controls BuildContent(Transform content, TMP_FontAsset font, Style style)
        {
            var controls = new Controls();

            Header("Sound", content, font, style);

            controls.Volume = SliderRow("Master Volume", "VolumeRow", content, font, style);
            controls.Mute = ToggleRow("Mute", "MuteRow", content, font, style);

            Header("Camera", content, font, style);

            controls.MouseSensitivity =
                SliderRow("Mouse Sensitivity", "MouseSensitivityRow", content, font, style);
            controls.GamepadSensitivity =
                SliderRow("Gamepad Sensitivity", "GamepadSensitivityRow", content, font, style);
            controls.InvertY = ToggleRow("Invert Look Y", "InvertYRow", content, font, style);

            Header("Controls", content, font, style);

            TextMeshProUGUI body = CreateText(ControlsText, content, font, style.BodySize,
                                              TextAlignmentOptions.Left);
            body.gameObject.AddComponent<LayoutElement>().preferredHeight = style.BodySize * 11f;

            return controls;
        }

        /// <summary>Applies the shared vertical layout to an options content container.</summary>
        public static VerticalLayoutGroup ApplyContentLayout(GameObject content, Style style)
        {
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = style.Spacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        // ----------------------------------------------------------------- rows --

        private static void Header(string text, Transform parent, TMP_FontAsset font, Style style)
        {
            TextMeshProUGUI header = CreateText(text, parent, font, style.HeaderSize,
                                                TextAlignmentOptions.Left);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = style.RowHeight;
        }

        private static GameObject Row(string name, Transform parent, Style style)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = style.RowHeight;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            return row;
        }

        private static Slider SliderRow(string label, string name, Transform parent,
                                        TMP_FontAsset font, Style style)
        {
            GameObject row = Row(name, parent, style);

            TextMeshProUGUI text = CreateText(label, row.transform, font, style.LabelSize,
                                              TextAlignmentOptions.Left);
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = style.LabelWidth;

            var sliderGO = new GameObject("Slider", typeof(RectTransform));
            sliderGO.transform.SetParent(row.transform, false);
            sliderGO.AddComponent<LayoutElement>().preferredWidth = style.SliderWidth;

            return BuildSlider(sliderGO, style.SliderWidth);
        }

        private static Toggle ToggleRow(string label, string name, Transform parent,
                                        TMP_FontAsset font, Style style)
        {
            GameObject row = Row(name, parent, style);

            TextMeshProUGUI text = CreateText(label, row.transform, font, style.LabelSize,
                                              TextAlignmentOptions.Left);
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = style.LabelWidth;

            var toggleGO = new GameObject("Toggle", typeof(RectTransform));
            toggleGO.transform.SetParent(row.transform, false);
            toggleGO.AddComponent<LayoutElement>().preferredWidth = style.ToggleWidth;

            return BuildToggle(toggleGO, style.ToggleWidth);
        }

        // ------------------------------------------------------------- widgets --

        public static TextMeshProUGUI CreateText(string content, Transform parent, TMP_FontAsset font,
                                                 int fontSize,
                                                 TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public static Slider BuildSlider(GameObject go, float width)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 20);

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = (RectTransform)fillAreaGO.transform;
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

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
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handleGO = new GameObject("Handle", typeof(RectTransform));
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = (RectTransform)handleGO.transform;
            handleRect.sizeDelta = new Vector2(20f, 20f);
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

        public static Toggle BuildToggle(GameObject go, float size)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);

            var bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.15f);

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

        /// <summary>Hands the built controls to the panel component.</summary>
        public static void Assign(OptionsPanel panel, Controls controls)
        {
            panel.volumeSlider = controls.Volume;
            panel.muteToggle = controls.Mute;
            panel.mouseSensitivitySlider = controls.MouseSensitivity;
            panel.gamepadSensitivitySlider = controls.GamepadSensitivity;
            panel.invertYToggle = controls.InvertY;
        }
    }
}
