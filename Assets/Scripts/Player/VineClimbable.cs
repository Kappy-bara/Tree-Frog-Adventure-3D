using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Marks an object as a climbable vine. Requires a trigger Collider that the
    /// player's VineClimb component can detect via OnTriggerEnter/Exit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VineClimbable : MonoBehaviour
    {
        [Tooltip("Optional explicit endpoints. If left unassigned, the trigger Collider's world bounds are used instead.")]
        public Transform bottomPoint;
        public Transform topPoint;

        private Collider _collider;

        public Vector3 VineUp => (topPoint != null && bottomPoint != null)
            ? (topPoint.position - bottomPoint.position).normalized
            : transform.up;

        public Vector3 BottomWorld => bottomPoint != null ? bottomPoint.position : _collider.bounds.min;
        public Vector3 TopWorld => topPoint != null ? topPoint.position : _collider.bounds.max;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void Start()
        {
            _collider.isTrigger = true;
        }

        /// <summary>
        /// Projects a world position onto the vine's segment and clamps it between the endpoints.
        /// </summary>
        public Vector3 GetClampedPoint(Vector3 worldPos)
        {
            Vector3 bottom = BottomWorld;
            Vector3 top = TopWorld;
            Vector3 segment = top - bottom;
            float segmentLength = segment.magnitude;
            if (segmentLength < 0.0001f) return bottom;

            Vector3 segmentDir = segment / segmentLength;
            float t = Mathf.Clamp(Vector3.Dot(worldPos - bottom, segmentDir), 0f, segmentLength);
            return bottom + segmentDir * t;
        }

        /// <summary>
        /// True when a clamped point sits within a small margin of either endpoint.
        /// </summary>
        public bool IsAtExtent(Vector3 worldPos, float margin = 0.05f)
        {
            Vector3 clamped = GetClampedPoint(worldPos);
            return Vector3.Distance(clamped, BottomWorld) <= margin || Vector3.Distance(clamped, TopWorld) <= margin;
        }
    }
}
