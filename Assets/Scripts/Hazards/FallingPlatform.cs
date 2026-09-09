using System.Collections;
using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// A platform that shakes when stood on, drops, then returns.
    ///
    /// Detects the player by weight from above rather than any collision, so brushing
    /// past the side does not trigger it.
    /// </summary>
    public class FallingPlatform : MonoBehaviour
    {
        [Tooltip("Seconds of warning shake before it drops.")]
        [SerializeField] private float shakeDuration = 0.6f;

        [SerializeField] private float shakeAmount = 0.06f;

        [Tooltip("Seconds falling before it disappears.")]
        [SerializeField] private float fallDuration = 1.2f;

        [SerializeField] private float fallGravity = -18f;

        [Tooltip("Seconds before it reappears at its original spot. 0 = never returns.")]
        [SerializeField] private float respawnDelay = 3f;

        [SerializeField] private LayerMask riderLayers = ~0;

        private Vector3 _homePosition;
        private Collider _collider;
        private Renderer[] _renderers;
        private bool _triggered;

        private static readonly Collider[] Buffer = new Collider[8];

        private void Awake()
        {
            _homePosition = transform.position;
            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            if (_triggered || _collider == null) return;
            if (!HasRiderOnTop()) return;

            _triggered = true;
            StartCoroutine(FallRoutine());
        }

        private bool HasRiderOnTop()
        {
            Bounds b = _collider.bounds;
            Vector3 centre = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
            Vector3 half = new Vector3(b.extents.x, 0.3f, b.extents.z);

            int count = Physics.OverlapBoxNonAlloc(centre, half, Buffer, transform.rotation,
                                                   riderLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (Buffer[i] != null && !Buffer[i].transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        private IEnumerator FallRoutine()
        {
            // Shake in place as a fair warning.
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                transform.position = _homePosition + Random.insideUnitSphere * shakeAmount;
                yield return null;
            }

            transform.position = _homePosition;

            // Drop. The collider stays on briefly so a rider falls with it rather than
            // being left standing on nothing.
            float velocity = 0f;
            float fallen = 0f;
            while (fallen < fallDuration)
            {
                fallen += Time.deltaTime;
                velocity += fallGravity * Time.deltaTime;
                transform.position += Vector3.up * (velocity * Time.deltaTime);
                yield return null;
            }

            SetVisible(false);

            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);

                transform.position = _homePosition;
                SetVisible(true);
                _triggered = false;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_collider != null) _collider.enabled = visible;
            foreach (Renderer r in _renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }
    }
}
