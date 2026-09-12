using TMPro;
using TreeFrogAdventure.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.UI
{
    /// <summary>
    /// The guardian's health bar across the top of the screen.
    ///
    /// Built in code and spawned by BossArena rather than living in the HUD prefab,
    /// because it only exists during four fights out of twenty levels and a prefab
    /// would mean carrying a disabled canvas through every other level.
    ///
    /// Anchored to the BOTTOM of the screen. The objective compass owns the top strip,
    /// and the two were drawn on top of each other.
    ///
    /// Phase boundaries are drawn as ticks, so the player can see the next stun coming
    /// instead of being surprised by it.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        private const float DrainSpeed = 1.4f;

        private BossBase _boss;
        private EnemyHealth _health;

        private RectTransform _fill;
        private RectTransform _chip;
        private Image _fillImage;
        private TMP_Text _nameLabel;
        private CanvasGroup _group;

        private float _displayed = 1f;
        private float _chipValue = 1f;
        private float _flashUntil;

        /// <summary>Creates and shows a bar for this guardian. Returns null if it cannot.</summary>
        public static BossHealthBar Show(BossBase boss)
        {
            if (boss == null) return null;

            var go = new GameObject("BossHealthBar");
            var bar = go.AddComponent<BossHealthBar>();
            bar.Build(boss);
            return bar;
        }

        private void Build(BossBase boss)
        {
            _boss = boss;
            _health = boss.GetComponent<EnemyHealth>();

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Below the pause menu and the death screen, above the HUD.
            canvas.sortingOrder = 140;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // ---- name -------------------------------------------------------
            var nameGO = new GameObject("Name", typeof(RectTransform));
            nameGO.transform.SetParent(transform, false);

            var nameRect = (RectTransform)nameGO.transform;
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 104f);
            nameRect.sizeDelta = new Vector2(1200f, 54f);

            _nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
            if (font != null) _nameLabel.font = font;
            _nameLabel.text = boss.BossName;
            _nameLabel.fontSize = UiTheme.HeadingSize;
            _nameLabel.alignment = TextAlignmentOptions.Center;
            _nameLabel.color = UiTheme.TextPrimary;
            _nameLabel.outlineWidth = 0.2f;
            _nameLabel.outlineColor = new Color32(0, 0, 0, 220);

            // ---- track ------------------------------------------------------
            RectTransform trackRect = CreateBar("Track", new Color(0f, 0f, 0f, 0.65f), 62f);

            // The chip sits behind the fill and drains a beat later, so a hit reads as
            // a chunk being taken rather than the bar simply being shorter.
            _chip = CreateChild(trackRect, "Chip", new Color(0.95f, 0.75f, 0.35f, 0.9f), out _);
            _fill = CreateChild(trackRect, "Fill", new Color(0.85f, 0.22f, 0.20f, 1f), out _fillImage);

            BuildPhaseTicks(trackRect, boss.PhaseCount);

            if (_health != null) _health.OnDamaged += HandleDamaged;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDamaged -= HandleDamaged;
        }

        private RectTransform CreateBar(string name, Color color, float y)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(1100f, 28f);

            var image = go.AddComponent<Image>();
            image.sprite = UiTheme.Pill;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        /// <summary>
        /// Width is driven by anchorMax.x, not Image.fillAmount. A Filled Image with no
        /// sprite falls through to a plain quad and ignores fillAmount entirely, and
        /// nothing in this project assigns UI sprites — the bar would never move.
        /// </summary>
        private static RectTransform CreateChild(RectTransform parent, string name, Color color,
                                                 out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            rect.pivot = new Vector2(0f, 0.5f);

            image = go.AddComponent<Image>();
            image.sprite = UiTheme.Pill;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        /// <summary>Sets a bar child to a 0..1 fraction of the track width.</summary>
        private static void SetFraction(RectTransform rect, float fraction)
        {
            Vector2 max = rect.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            rect.anchorMax = max;

            // offsetMax.x is the right inset and must be re-applied after moving the
            // anchor, or the bar creeps 3px wider every frame.
            rect.offsetMax = new Vector2(-3f, -3f);
            rect.offsetMin = new Vector2(3f, 3f);
        }

        private static void BuildPhaseTicks(RectTransform parent, int phaseCount)
        {
            for (int i = 1; i < phaseCount; i++)
            {
                float x = i / (float)phaseCount;

                var go = new GameObject($"Tick{i}", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(x, 0f);
                rect.anchorMax = new Vector2(x, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(3f, 0f);
                rect.anchoredPosition = Vector2.zero;

                var image = go.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.8f);
                image.raycastTarget = false;
            }
        }

        private void HandleDamaged(EnemyHealth health, int amount, DamageKind kind)
        {
            _flashUntil = Time.unscaledTime + 0.1f;
        }

        private void Update()
        {
            if (_boss == null)
            {
                // The guardian was destroyed — fade out rather than leaving a full bar
                // hanging over an empty arena.
                _group.alpha -= Time.unscaledDeltaTime * 2f;
                if (_group.alpha <= 0f) Destroy(gameObject);
                return;
            }

            float target = _boss.HealthFraction;

            _displayed = target;
            _chipValue = Mathf.MoveTowards(_chipValue, target, DrainSpeed * Time.unscaledDeltaTime);

            SetFraction(_fill, _displayed);
            SetFraction(_chip, Mathf.Max(_chipValue, _displayed));

            _fillImage.color = Time.unscaledTime < _flashUntil
                ? Color.white
                : _boss.IsVulnerable
                    ? UiTheme.Gold          // the stun window, made obvious
                    : UiTheme.Danger;

            _nameLabel.text = _boss.IsVulnerable
                ? $"{_boss.BossName} — STUNNED"
                : _boss.BossName;
        }

        /// <summary>Called by BossArena once the fight is over.</summary>
        public void Dismiss()
        {
            _boss = null;
        }
    }
}
