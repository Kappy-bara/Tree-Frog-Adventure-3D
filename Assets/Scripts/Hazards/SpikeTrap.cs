using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// Spikes that extend and retract on a timer.
    ///
    /// Disables the hazard collider while retracted, so the gap is genuinely safe
    /// rather than merely looking safe.
    /// </summary>
    public class SpikeTrap : MonoBehaviour
    {
        [Tooltip("The part that moves. Defaults to the first child.")]
        [SerializeField] private Transform spikes;

        [Tooltip("Collider that hurts the player. Disabled while retracted.")]
        [SerializeField] private Collider hazardCollider;

        [SerializeField] private float extendDistance = 1f;
        [SerializeField] private float extendSpeed = 14f;
        [SerializeField] private float retractSpeed = 4f;

        [SerializeField] private float timeExtended = 1f;
        [SerializeField] private float timeRetracted = 1.6f;

        [Tooltip("Offsets the cycle so a row of traps fires in a wave.")]
        [SerializeField] private float startDelay;

        private Vector3 _retractedPosition;
        private Vector3 _extendedPosition;
        private bool _extending;
        private float _timer;

        private void Awake()
        {
            if (spikes == null && transform.childCount > 0) spikes = transform.GetChild(0);
            if (spikes == null) spikes = transform;

            if (hazardCollider == null) hazardCollider = GetComponentInChildren<Collider>();

            _retractedPosition = spikes.localPosition;
            _extendedPosition = _retractedPosition + Vector3.up * extendDistance;

            _timer = timeRetracted + startDelay;
            SetHazardActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timer -= dt;

            Vector3 target = _extending ? _extendedPosition : _retractedPosition;
            float speed = _extending ? extendSpeed : retractSpeed;

            spikes.localPosition = Vector3.MoveTowards(spikes.localPosition, target, speed * dt);

            // Only dangerous once actually out.
            bool nearlyExtended = Vector3.Distance(spikes.localPosition, _extendedPosition) < extendDistance * 0.35f;
            SetHazardActive(nearlyExtended);

            if (_timer <= 0f)
            {
                _extending = !_extending;
                _timer = _extending ? timeExtended : timeRetracted;
            }
        }

        private void SetHazardActive(bool active)
        {
            if (hazardCollider != null && hazardCollider.enabled != active)
            {
                hazardCollider.enabled = active;
            }
        }
    }
}
