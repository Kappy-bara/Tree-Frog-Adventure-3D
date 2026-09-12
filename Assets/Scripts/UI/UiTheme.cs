using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.UI
{
    /// <summary>
    /// One definition of what this game's interface looks like, and the widget factory
    /// every screen builds from.
    ///
    /// Six screens are assembled in code — main menu, pause, options, game over, level
    /// complete, HUD — and each had grown its own copy of "make a label", "make a
    /// button", each with slightly different sizes and its own hand-picked greys. The
    /// result read as six interfaces rather than one game.
    ///
    /// Everything here is null-safe: with the sprites not yet generated the widgets fall
    /// back to plain quads, which is exactly how the UI looked before, rather than
    /// throwing on a missing Resources load.
    /// </summary>
    public static class UiTheme
    {
        // ---------------------------------------------------------- palette --
        // A damp-forest palette: dark green-greys for surfaces, frog green for the
        // interactive accent, amber for rewards, and a muted red reserved for danger.

        public static readonly Color Backdrop = new(0.03f, 0.06f, 0.05f, 0.86f);
        public static readonly Color Surface = new(0.08f, 0.13f, 0.11f, 0.96f);
        public static readonly Color SurfaceRaised = new(0.13f, 0.20f, 0.17f, 1f);
        public static readonly Color Hairline = new(0.35f, 0.55f, 0.44f, 0.55f);

        public static readonly Color Accent = new(0.40f, 0.82f, 0.47f);
        public static readonly Color AccentDim = new(0.22f, 0.45f, 0.28f);
        public static readonly Color Gold = new(1f, 0.80f, 0.33f);
        public static readonly Color Crystal = new(0.45f, 0.95f, 0.78f);
        public static readonly Color Danger = new(0.91f, 0.36f, 0.33f);

        public static readonly Color TextPrimary = new(0.95f, 0.98f, 0.95f);
        public static readonly Color TextMuted = new(0.60f, 0.71f, 0.65f);
        public static readonly Color TextDisabled = new(0.40f, 0.46f, 0.43f);

        // ------------------------------------------------------------ scale --

        public const int TitleSize = 72;
        public const int HeadingSize = 40;
        public const int SubheadingSize = 30;
        public const int BodySize = 26;
        public const int CaptionSize = 22;

        public const float ButtonHeight = 68f;
        public const float ButtonWidth = 340f;
        public const float Gap = 16f;
        public const float PanelPadding = 32f;

        // ----------------------------------------------------------- assets --

        private static TMP_FontAsset _font;
        private static bool _fontLoaded;

        public static TMP_FontAsset Font
        {
            get
            {
                if (!_fontLoaded)
                {
                    _fontLoaded = true;
                    _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                }
                return _font;
            }
        }

        public static Sprite Panel => Load("UI/Panel", ref _panel, ref _panelLoaded);
        public static Sprite PanelOutline => Load("UI/PanelOutlined", ref _panelOutline, ref _panelOutlineLoaded);
        public static Sprite Pill => Load("UI/Pill", ref _pill, ref _pillLoaded);
        public static Sprite PillOutline => Load("UI/PillOutlined", ref _pillOutline, ref _pillOutlineLoaded);
        public static Sprite Circle => Load("UI/Circle", ref _circle, ref _circleLoaded);
        public static Sprite Ring => Load("UI/Ring", ref _ring, ref _ringLoaded);
        public static Sprite Glow => Load("UI/Glow", ref _glow, ref _glowLoaded);

        private static Sprite _panel, _panelOutline, _pill, _pillOutline, _circle, _ring, _glow;
        private static bool _panelLoaded, _panelOutlineLoaded, _pillLoaded, _pillOutlineLoaded;
        private static bool _circleLoaded, _ringLoaded, _glowLoaded;

        private static Sprite Load(string path, ref Sprite cache, ref bool loaded)
        {
            if (loaded) return cache;

            loaded = true;
            cache = Resources.Load<Sprite>(path);
            return cache;
        }

        /// <summary>
        /// Cached sprites and fonts are destroyed between play sessions when domain
        /// reload is disabled, leaving the caches holding dead objects.
        /// </summary>
        /// <summary>
        /// Drops the cached lookups.
        ///
        /// Needed by the editor tools as well as by play mode: a tool that generates the
        /// sprites and then builds a prefab in the same session would otherwise bake in
        /// the nulls cached before the sprites existed.
        /// </summary>
        public static void ClearCache()
        {
            ResetStaticsOnPlay();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            _font = null;
            _fontLoaded = false;
            _panelLoaded = _panelOutlineLoaded = _pillLoaded = _pillOutlineLoaded = false;
            _circleLoaded = _ringLoaded = _glowLoaded = false;
        }

        // ---------------------------------------------------------- widgets --

        public enum ButtonKind { Primary, Secondary, Danger }

        /// <summary>A full-screen child that stretches to its parent.</summary>
        public static RectTransform Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect == null) rect = go.AddComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>The dimmed backdrop every full-screen overlay sits on.</summary>
        public static Image Dim(Transform parent, Color? color = null)
        {
            var go = new GameObject("Dim", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go);

            var image = go.AddComponent<Image>();
            image.color = color ?? Backdrop;
            return image;
        }

        /// <summary>A rounded surface. Returns the panel's RectTransform to fill.</summary>
        public static RectTransform PanelBox(Transform parent, string name, Vector2 size,
                                             Vector2 position, Color? fill = null, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = go.AddComponent<Image>();
            image.sprite = Panel;
            image.type = Image.Type.Sliced;
            image.color = fill ?? Surface;
            image.raycastTarget = false;

            if (outline) Outline(rect);

            return rect;
        }

        /// <summary>A hairline border drawn over a panel, as its own stretched child.</summary>
        public static Image Outline(RectTransform parent, Color? color = null)
        {
            var go = new GameObject("Outline", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go);

            var image = go.AddComponent<Image>();
            image.sprite = PanelOutline;
            image.type = Image.Type.Sliced;
            image.color = color ?? Hairline;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, int size,
                                            Color? color = null,
                                            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;

            label.text = text;
            label.fontSize = size;
            label.color = color ?? TextPrimary;
            label.alignment = alignment;
            label.raycastTarget = false;

            // Every screen sits over gameplay, so text needs to survive a bright
            // background without a solid plate behind it.
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(0, 0, 0, 215);

            return label;
        }

        /// <summary>
        /// A button with real interaction states.
        ///
        /// The originals used the default ColorBlock over a flat white-alpha quad, so
        /// hover and press were nearly invisible and a disabled button looked identical
        /// to an enabled one — which matters most on the level-select grid, where locked
        /// and unlocked levels sat side by side.
        /// </summary>
        public static Button Button(Transform parent, string name, string text,
                                    float width, float height, ButtonKind kind = ButtonKind.Primary)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            var image = go.AddComponent<Image>();
            image.sprite = Pill;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors(kind);

            var label = Label(go.transform, "Label", text, SubheadingSize,
                              kind == ButtonKind.Primary ? new Color(0.05f, 0.12f, 0.08f) : TextPrimary);
            Stretch(label.gameObject);

            return button;
        }

        /// <summary>
        /// Interaction colours for a button kind. Public because the main menu is built
        /// by an editor tool with its own widget helpers, and it must not drift from the
        /// runtime screens.
        /// </summary>
        public static ColorBlock ButtonColors(ButtonKind kind)
        {
            Color baseColor = kind switch
            {
                ButtonKind.Primary => Accent,
                ButtonKind.Danger => Danger,
                _ => SurfaceRaised,
            };

            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = Brighten(baseColor, 0.16f),
                pressedColor = Brighten(baseColor, -0.18f),
                selectedColor = Brighten(baseColor, 0.10f),

                // Clearly dead rather than merely dimmer: a locked level must not look
                // like one you simply have not hovered.
                disabledColor = new Color(baseColor.r * 0.3f, baseColor.g * 0.3f,
                                          baseColor.b * 0.3f, 0.45f),

                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static Color Brighten(Color color, float amount)
        {
            return new Color(Mathf.Clamp01(color.r + amount),
                             Mathf.Clamp01(color.g + amount),
                             Mathf.Clamp01(color.b + amount),
                             color.a);
        }

        /// <summary>
        /// A horizontal meter. Returns the fill, whose width is driven by anchorMax.x
        /// through <see cref="SetFill"/> — the sprites exist now, but anchors keep
        /// working whether or not a sprite is assigned.
        /// </summary>
        public static RectTransform Meter(Transform parent, string name, Vector2 size,
                                          Vector2 position, Color fillColor, out Image fillImage)
        {
            RectTransform track = PanelBox(parent, name, size, position,
                                           new Color(0f, 0f, 0f, 0.55f), outline: false);

            var go = new GameObject("Fill", typeof(RectTransform));
            go.transform.SetParent(track, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);

            fillImage = go.AddComponent<Image>();
            fillImage.sprite = Pill;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            return rect;
        }

        /// <summary>Sets a meter fill to a 0..1 fraction of its track.</summary>
        public static void SetFill(RectTransform fill, float fraction)
        {
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            fill.anchorMax = max;

            // Insets must be re-applied after moving the anchor, or the bar creeps
            // wider by the inset every frame.
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
        }

        public static VerticalLayoutGroup Column(GameObject go, float spacing = Gap,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Row(GameObject go, float spacing = Gap,
                                                TextAnchor align = TextAnchor.MiddleCenter)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }
    }
}
