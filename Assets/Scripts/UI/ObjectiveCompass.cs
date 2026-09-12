using System.Collections.Generic;
using TMPro;
using TreeFrogAdventure.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.UI
{
    /// <summary>
    /// A compass strip across the top of the screen pointing at the next objective.
    ///
    /// The five levels of a world all share one 100x100 map, so a level is a long
    /// traversal across open terrain with no corridor to funnel the player. Without a
    /// heading, finding the next checkpoint is guesswork.
    ///
    /// The objective is resolved in priority order — an active boss, then the nearest
    /// checkpoint not yet reached, then the exit — so it always names the thing the
    /// player should be walking towards right now.
    ///
    /// Builds its own children, matching how every other screen in this project is made.
    /// </summary>
    public class ObjectiveCompass : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float barWidth = 900f;
        [SerializeField] private float topMargin = 26f;

        [Tooltip("Bearing, in degrees either side of centre, that the bar spans. The " +
                 "marker pins to an edge beyond this and the arrow turns to point round.")]
        [SerializeField] private float halfSpanDegrees = 90f;

        [Header("Refresh")]
        [Tooltip("Seconds between re-scans for objectives. Checkpoints are activated " +
                 "and guardians die mid-level, so the list cannot be cached once.")]
        [SerializeField] private float rescanInterval = 1f;

        private readonly List<Checkpoint> _checkpoints = new();

        private Transform _player;
        private Camera _camera;
        private LevelGoal _goal;
        private BossArena _arena;

        private RectTransform _marker;
        private TMP_Text _markerIcon;
        private TMP_Text _label;
        private TMP_Text _distance;
        private CanvasGroup _group;

        private float _rescanTimer;
        private float _markerX;

        private void Start()
        {
            Build();
            Rescan();
        }

        // ---------------------------------------------------------------- build --

        private void Build()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var holder = new GameObject("ObjectiveCompass", typeof(RectTransform));
            holder.transform.SetParent(transform, false);

            var holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = new Vector2(0.5f, 1f);
            holderRect.anchorMax = new Vector2(0.5f, 1f);
            holderRect.pivot = new Vector2(0.5f, 1f);
            holderRect.anchoredPosition = new Vector2(0f, -topMargin);
            holderRect.sizeDelta = new Vector2(barWidth, 96f);

            _group = holder.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // --- track --------------------------------------------------------
            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(holder.transform, false);

            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0.5f, 1f);
            trackRect.anchorMax = new Vector2(0.5f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, 0f);
            trackRect.sizeDelta = new Vector2(barWidth, 6f);

            var trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.18f);
            trackImage.raycastTarget = false;

            // A tick at dead centre, so "straight ahead" is readable at a glance
            // rather than something the player has to estimate.
            var centreTick = new GameObject("CentreTick", typeof(RectTransform));
            centreTick.transform.SetParent(holder.transform, false);

            var centreRect = (RectTransform)centreTick.transform;
            centreRect.anchorMin = new Vector2(0.5f, 1f);
            centreRect.anchorMax = new Vector2(0.5f, 1f);
            centreRect.pivot = new Vector2(0.5f, 1f);
            centreRect.anchoredPosition = new Vector2(0f, 4f);
            centreRect.sizeDelta = new Vector2(2f, 18f);

            var centreImage = centreTick.AddComponent<Image>();
            centreImage.color = new Color(1f, 1f, 1f, 0.5f);
            centreImage.raycastTarget = false;

            // --- marker -------------------------------------------------------
            var marker = new GameObject("Marker", typeof(RectTransform));
            marker.transform.SetParent(holder.transform, false);

            _marker = (RectTransform)marker.transform;
            _marker.anchorMin = new Vector2(0.5f, 1f);
            _marker.anchorMax = new Vector2(0.5f, 1f);
            _marker.pivot = new Vector2(0.5f, 1f);
            _marker.anchoredPosition = new Vector2(0f, -6f);
            _marker.sizeDelta = new Vector2(48f, 48f);

            // A glyph rather than a sprite: nothing in this project ships UI sprites,
            // and a text arrow can be rotated to mean "keep turning" for free.
            _markerIcon = MakeText(marker.transform, "Icon", "▲", 40, font,
                                   new Color(1f, 0.85f, 0.3f));
            _markerIcon.alignment = TextAlignmentOptions.Center;

            var iconRect = (RectTransform)_markerIcon.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            // --- caption ------------------------------------------------------
            _label = MakeText(holder.transform, "Label", "", 30, font, Color.white);
            var labelRect = (RectTransform)_label.transform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -52f);
            labelRect.sizeDelta = new Vector2(barWidth, 36f);
            _label.alignment = TextAlignmentOptions.Center;

            _distance = MakeText(holder.transform, "Distance", "", 26, font,
                                 new Color(0.75f, 0.85f, 0.8f));
            var distanceRect = (RectTransform)_distance.transform;
            distanceRect.anchorMin = new Vector2(0.5f, 1f);
            distanceRect.anchorMax = new Vector2(0.5f, 1f);
            distanceRect.pivot = new Vector2(0.5f, 1f);
            distanceRect.anchoredPosition = new Vector2(0f, -84f);
            distanceRect.sizeDelta = new Vector2(barWidth, 32f);
            _distance.alignment = TextAlignmentOptions.Center;
        }

        private static TMP_Text MakeText(Transform parent, string name, string content, int size,
                                         TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            return text;
        }

        // --------------------------------------------------------------- update --

        private void LateUpdate()
        {
            _rescanTimer -= Time.unscaledDeltaTime;
            if (_rescanTimer <= 0f) Rescan();

            if (!ResolveTarget(out Transform target, out string caption))
            {
                _group.alpha = 0f;
                return;
            }

            _group.alpha = 1f;

            Vector3 playerPosition = _player.position;
            Vector3 toTarget = target.position - playerPosition;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;

            Vector3 forward = _camera != null ? _camera.transform.forward : _player.forward;
            forward.y = 0f;

            // Degenerate when the camera looks straight down; fall back to the body.
            if (forward.sqrMagnitude < 1e-4f) forward = _player.forward;

            float bearing = toTarget.sqrMagnitude < 1e-4f
                ? 0f
                : Vector3.SignedAngle(forward, toTarget, Vector3.up);

            float normalised = Mathf.Clamp(bearing / Mathf.Max(1f, halfSpanDegrees), -1f, 1f);
            float targetX = normalised * (barWidth * 0.5f);

            // Smoothed, or the marker jitters with every small camera correction.
            _markerX = Mathf.Lerp(_markerX, targetX, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            _marker.anchoredPosition = new Vector2(_markerX, -6f);

            bool behind = Mathf.Abs(bearing) > halfSpanDegrees;

            // Pinned at an edge, the up arrow would be a lie — turn it to point the
            // way round instead.
            _markerIcon.text = behind ? (bearing > 0f ? "▶" : "◀") : "▲";
            _markerIcon.color = behind
                ? new Color(1f, 0.6f, 0.3f)
                : new Color(1f, 0.85f, 0.3f);

            _label.text = caption;
            _distance.text = $"{Mathf.RoundToInt(distance)} m";
        }

        /// <summary>
        /// Priority order: the guardian you are fighting, then the nearest checkpoint
        /// you have not reached, then the exit.
        /// </summary>
        private bool ResolveTarget(out Transform target, out string caption)
        {
            target = null;
            caption = string.Empty;

            if (_player == null) return false;

            if (_arena != null)
            {
                Transform boss = _arena.BossTarget;

                if (boss != null)
                {
                    // Before the fight starts, point at the arena entrance; the guardian
                    // itself may be the far side of it.
                    target = _arena.AwaitingPlayer ? _arena.transform : boss;
                    caption = _arena.AwaitingPlayer ? "Guardian's Arena" : "Defeat the Guardian";
                    return true;
                }
            }

            Checkpoint nearest = null;
            float best = float.MaxValue;

            foreach (Checkpoint checkpoint in _checkpoints)
            {
                if (checkpoint == null || checkpoint.IsActivated) continue;

                float d = (checkpoint.Marker.position - _player.position).sqrMagnitude;
                if (d >= best) continue;

                best = d;
                nearest = checkpoint;
            }

            if (nearest != null)
            {
                target = nearest.Marker;
                caption = "Checkpoint";
                return true;
            }

            if (_goal != null)
            {
                target = _goal.transform;
                caption = "Level Exit";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Re-finds the objectives. Cheap enough at one scan a second, and necessary
        /// because the player rig, the goal and the arena all arrive from different
        /// scenes under additive loading and may not exist on the first frame.
        /// </summary>
        private void Rescan()
        {
            _rescanTimer = Mathf.Max(0.25f, rescanInterval);

            if (_player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null) _player = found.transform;
            }

            if (_camera == null) _camera = Camera.main;

            if (_goal == null) _goal = FindAnyObjectByType<LevelGoal>();
            if (_arena == null) _arena = FindAnyObjectByType<BossArena>();

            _checkpoints.Clear();
            _checkpoints.AddRange(FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
        }
    }
}
