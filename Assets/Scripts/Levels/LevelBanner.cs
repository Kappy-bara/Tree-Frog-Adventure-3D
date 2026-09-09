using System.Collections;
using TMPro;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// The "World 2 — Level 7" card on entry and the completion card on finish.
    ///
    /// Built in code rather than as a prefab to match how every other screen in this
    /// project is made (PauseMenuController, PlayerHealth's game-over screen,
    /// MainMenuSceneBuilder), so there is one idiom rather than two.
    /// </summary>
    public class LevelBanner : MonoBehaviour
    {
        [SerializeField] private float introHold = 1.6f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private bool showIntroOnStart = true;

        private TMP_FontAsset _font;

        private void Start()
        {
            if (!showIntroOnStart) return;

            int level = LevelContext.Current != null ? LevelContext.Current.LevelNumber : 0;
            if (level <= 0) return;

            var (world, levelInWorld) = LevelProgress.GetWorldAndLevel(level);
            StartCoroutine(ShowCard(
                LevelProgress.GetBiomeName(level),
                $"Level {levelInWorld} of {LevelProgress.LevelsPerWorld}",
                introHold,
                world));
        }

        /// <summary>Called by LevelGoal before it advances.</summary>
        public void ShowComplete(float hold)
        {
            StartCoroutine(ShowCard("Level Complete", $"Coins {GameSession.Coins}", hold, 0));
        }

        private IEnumerator ShowCard(string title, string subtitle, float hold, int world)
        {
            _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var canvasGO = new GameObject("LevelBannerCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var group = canvasGO.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;   // never eat input; this is decoration
            group.interactable = false;
            group.alpha = 0f;

            CreateLabel(canvasGO.transform, title, 90, new Vector2(0f, 60f));
            if (!string.IsNullOrEmpty(subtitle))
            {
                CreateLabel(canvasGO.transform, subtitle, 44, new Vector2(0f, -30f));
            }

            yield return Fade(group, 0f, 1f, fadeDuration);
            yield return new WaitForSeconds(hold);
            yield return Fade(group, 1f, 0f, fadeDuration);

            Destroy(canvasGO);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                // Unscaled: the completion card can overlap a paused timescale.
                t += Time.unscaledDeltaTime;
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        private void CreateLabel(Transform parent, string text, int size, Vector2 offset)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(1400f, size * 1.6f);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 200);
        }
    }
}
