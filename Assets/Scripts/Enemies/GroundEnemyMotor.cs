using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Gravity, slope handling and collision for ground enemies, via CharacterController.
    ///
    /// This is what fixes the original problem: DirectWaypointPatrol wrote
    /// transform.position directly with no gravity and kept a fixed Y, so enemies
    /// floated above or sank into the sloped terrain and walked through geometry.
    /// CharacterController is the same idiom the player already uses, so slopes, steps
    /// and walls come for free without baking a NavMesh.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GroundEnemyMotor : MonoBehaviour
    {
        [SerializeField] private float gravity = -25f;

        [Tooltip("Downward velocity held while grounded. A small negative value keeps " +
                 "the controller pressed into slopes instead of stair-stepping down them.")]
        [SerializeField] private float groundedStick = -2f;

        [Tooltip("Layers treated as solid ground for the ledge probe.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        private CharacterController _cc;
        private Vector3 _velocity;

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public Vector3 Velocity => _velocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        /// <summary>Move horizontally; gravity is applied automatically.</summary>
        public void Move(Vector3 horizontalVelocity, float deltaTime)
        {
            if (_cc == null || !_cc.enabled) return;

            if (_cc.isGrounded && _velocity.y < 0f)
            {
                _velocity.y = groundedStick;
            }
            else
            {
                _velocity.y += gravity * deltaTime;
            }

            Vector3 motion = horizontalVelocity;
            motion.y = _velocity.y;

            _cc.Move(motion * deltaTime);
        }

        /// <summary>Stand still but keep falling.</summary>
        public void Idle(float deltaTime) => Move(Vector3.zero, deltaTime);

        public void Jump(float upwardVelocity)
        {
            if (_cc == null || !_cc.enabled || !_cc.isGrounded) return;
            _velocity.y = upwardVelocity;
        }

        /// <summary>
        /// True if there is ground to stand on ahead. Used by patrollers so they turn
        /// around at cliff edges instead of walking off them.
        /// </summary>
        public bool HasGroundAhead(Vector3 direction, float lookAhead = 1.2f, float maxDrop = 2f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return true;

            Vector3 origin = transform.position
                             + Vector3.up * 0.2f
                             + direction.normalized * lookAhead;

            return Physics.Raycast(origin, Vector3.down, maxDrop + 0.2f,
                                   groundLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>True if something solid blocks movement in this direction.</summary>
        public bool IsBlocked(Vector3 direction, float distance = 0.6f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return false;

            Vector3 origin = transform.position + Vector3.up * (_cc != null ? _cc.height * 0.5f : 0.5f);

            return Physics.Raycast(origin, direction.normalized, distance,
                                   groundLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
