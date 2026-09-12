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
    /// The objective is resolved in priority order — an active boss, then whatever
    /// ObjectiveChain says is next, then a nearest-checkpoint fallback for a level
    /// built without a chain — so it always names the one thing the player can
    /// currently reach.
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
            var holder = new GameObject("ObjectiveCompass", typeof(RectTransform));
            holder.transform.SetParent(transform, false);

            var holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = new Vector2(0.5f, 1f);
            holderRect.anchorMax = new Vector2(0.5f, 1f);
            holderRect.pivot = new Vector2(0.5f, 1f);
            holderRect.anchoredPosition = new Vector2(0f, -topMargin);
            holderRect.sizeDelta = new Vector2(barWidth, 104f);

            _group = holder.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // ---- plate --------------------------------------------------------
            // A backdrop so the heading stays readable over a bright sky or a pale
            // cliff face; the bare bar vanished against both.
            var plate = new GameObject("Plate", typeof(RectTransform));
            plate.transform.SetParent(holder.transform, false);

            var plateRect = (RectTransform)plate.transform;
            plateRect.anchorMin = new Vector2(0.5f, 1f);
            plateRect.anchorMax = new Vector2(0.5f, 1f);
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.anchoredPosition = Vector2.zero;
            plateRect.sizeDelta = new Vector2(barWidth * 0.62f, 104f);

            var plateImage = plate.AddComponent<Image>();
            plateImage.sprite = UiTheme.Panel;
            plateImage.type = Image.Type.Sliced;
            plateImage.color = new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, 0.72f);
            plateImage.raycastTarget = false;

            // ---- track --------------------------------------------------------
            var track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(holder.transform, false);

            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0.5f, 1f);
            trackRect.anchorMax = new Vector2(0.5f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, -16f);
            trackRect.sizeDelta = new Vector2(barWidth, 6f);

            var trackImage = track.AddComponent<Image>();
            trackImage.sprite = UiTheme.Pill;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(1f, 1f, 1f, 0.16f);
            trackImage.raycastTarget = false;

            // A tick at dead centre, so "straight ahead" is readable at a glance
            // rather than something the player has to estimate.
            var centreTick = new GameObject("CentreTick", typeof(RectTransform));
            centreTick.transform.SetParent(holder.transform, false);

            var centreRect = (RectTransform)centreTick.transform;
            centreRect.anchorMin = new Vector2(0.5f, 1f);
            centreRect.anchorMax = new Vector2(0.5f, 1f);
            centreRect.pivot = new Vector2(0.5f, 1f);
            centreRect.anchoredPosition = new Vector2(0f, -10f);
            centreRect.sizeDelta = new Vector2(2f, 18f);

            var centreImage = centreTick.AddComponent<Image>();
            centreImage.color = new Color(1f, 1f, 1f, 0.45f);
            centreImage.raycastTarget = false;

            // ---- marker -------------------------------------------------------
            var marker = new GameObject("Marker", typeof(RectTransform));
            marker.transform.SetParent(holder.transform, false);

            _marker = (RectTransform)marker.transform;
            _marker.anchorMin = new Vector2(0.5f, 1f);
            _marker.anchorMax = new Vector2(0.5f, 1f);
            _marker.pivot = new Vector2(0.5f, 1f);
            _marker.anchoredPosition = new Vector2(0f, -4f);
            _marker.sizeDelta = new Vector2(44f, 44f);

            // A glyph rather than a sprite, because it can be swapped to point round
            // when the objective is behind the player.
            _markerIcon = UiTheme.Label(marker.transform, "Icon", "\u25B2", 34, UiTheme.Gold);
            UiTheme.Stretch(_markerIcon.gameObject);

            // ---- caption ------------------------------------------------------
            _label = UiTheme.Label(holder.transform, "Label", string.Empty,
                                   UiTheme.BodySize, UiTheme.TextPrimary);

            var labelRect = (RectTransform)_label.transform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -46f);
            labelRect.sizeDelta = new Vector2(barWidth, 34f);

            _distance = UiTheme.Label(holder.transform, "Distance", string.Empty,
                                      UiTheme.CaptionSize, UiTheme.TextMuted);

            var distanceRect = (RectTransform)_distance.transform;
            distanceRect.anchorMin = new Vector2(0.5f, 1f);
            distanceRect.anchorMax = new Vector2(0.5f, 1f);
            distanceRect.pivot = new Vector2(0.5f, 1f);
            distanceRect.anchoredPosition = new Vector2(0f, -76f);
            distanceRect.sizeDelta = new Vector2(barWidth, 30f);
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
            _markerIcon.color = behind ? UiTheme.Danger : UiTheme.Gold;

            _label.text = caption;
            _distance.text = $"{Mathf.RoundToInt(distance)} m";
        }

        /// <summary>
        /// Priority order: the guardian you are fighting, then whatever the chain says
        /// is next, then a nearest-checkpoint fallback for a level with no chain.
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

            // The chain is authoritative when there is one: checkpoints are a locked
            // sequence, so pointing at the nearest would send the player back to one
            // they already claimed the moment they wandered.
            ObjectiveChain chain = ObjectiveChain.Current;

            if (chain != null)
            {
                Transform next = chain.CurrentTarget;

                if (next != null)
                {
                    target = next;
                    caption = chain.CurrentLabel;
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
