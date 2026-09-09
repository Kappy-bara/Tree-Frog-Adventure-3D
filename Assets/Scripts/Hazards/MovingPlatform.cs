using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// A platform that travels between waypoints and carries whatever stands on it.
    ///
    /// The carrying is the whole point. A CharacterController does NOT inherit motion
    /// from the surface under it — without this the player would slide off the moment
    /// the platform moved. Riders are found with an overlap test above the platform
    /// each frame, so no trigger child is needed and it works for enemies too.
    ///
    /// Runs in LateUpdate so riders are moved after they have finished their own
    /// movement for the frame; doing it in Update fights the player's controller and
    /// produces jitter.
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Points travelled in order, looping. World space.")]
        [SerializeField] private Transform[] waypoints;

        [SerializeField] private float speed = 2.5f;

        [Tooltip("Seconds paused at each waypoint.")]
        [SerializeField] private float waitTime = 0.6f;

        [Tooltip("Ping-pong back along the path instead of looping to the first point.")]
        [SerializeField] private bool pingPong = true;

        [Header("Riders")]
        [Tooltip("How far above the platform surface counts as standing on it.")]
        [SerializeField] private float riderCheckHeight = 0.6f;

        [SerializeField] private LayerMask riderLayers = ~0;

        private int _index;
        private int _direction = 1;
        private float _waitTimer;
        private Vector3 _lastPosition;
        private Collider _collider;

        private static readonly Collider[] RiderBuffer = new Collider[16];

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            Vector3 before = transform.position;

            Advance(Time.deltaTime);

            Vector3 delta = transform.position - before;
            if (delta.sqrMagnitude > 1e-10f)
            {
                CarryRiders(delta);
            }

            _lastPosition = transform.position;
        }

        private void Advance(float deltaTime)
        {
            if (waypoints == null || waypoints.Length < 2) return;

            if (_waitTimer > 0f)
            {
                _waitTimer -= deltaTime;
                return;
            }

            Transform target = waypoints[_index];
            if (target == null) return;

            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * deltaTime);

            if (Vector3.Distance(transform.position, target.position) < 0.01f)
            {
                _waitTimer = waitTime;
                StepIndex();
            }
        }

        private void StepIndex()
        {
            if (pingPong)
            {
                if (_index + _direction >= waypoints.Length || _index + _direction < 0)
                {
                    _direction = -_direction;
                }
                _index += _direction;
            }
            else
            {
                _index = (_index + 1) % waypoints.Length;
            }
        }

        private void CarryRiders(Vector3 delta)
        {
            if (_collider == null) return;

            Bounds b = _collider.bounds;
            Vector3 centre = new Vector3(b.center.x, b.max.y + riderCheckHeight * 0.5f, b.center.z);
            Vector3 half = new Vector3(b.extents.x, riderCheckHeight * 0.5f, b.extents.z);

            int count = Physics.OverlapBoxNonAlloc(centre, half, RiderBuffer, transform.rotation,
                                                   riderLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider rider = RiderBuffer[i];
                if (rider == null || rider.transform.IsChildOf(transform)) continue;

                var cc = rider.GetComponentInParent<CharacterController>();
                if (cc != null && cc.enabled)
                {
                    cc.Move(delta);
                    continue;
                }

                var rb = rider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) rb.position += delta;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null) return;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);

                Transform next = waypoints[(i + 1) % waypoints.Length];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
