using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// ScriptableObject containing all player tuning parameters.
    /// Create via Assets > Create > TreeFrog > Player Data.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerData", menuName = "TreeFrog/Player Data")]
    public class PlayerData : ScriptableObject
    {
        // ────────────────────────────── Movement ──────────────────────────────

        [Header("Basic Movement")]
        [Tooltip("Run speed in units/second")]
        public float runSpeed = 8f;

        [Tooltip("Rotation speed in degrees/second")]
        public float rotationSpeed = 720f;

        // ────────────────────────────── Jumping ──────────────────────────────

        [Header("Jumping")]
        [Tooltip("Initial jump velocity")]
        public float jumpForce = 12f;

        [Tooltip("Double jump velocity (slightly less than first jump)")]
        public float doubleJumpForce = 10f;

        public float tongueRange = 25f;

        public float tonguePullSpeed = 30f;

        public LayerMask grappleLayer;

        // ────────────────────────────── Physics ──────────────────────────────

        [Header("Physics")]
        [Tooltip("Gravity scale")]
        public float gravity = -25f;

        // ────────────────────────────── Wall Jump ──────────────────────────────

        [Header("Wall Jump")]
        [Tooltip("Distance of the side raycasts used to detect a wall")]
        public float wallCheckDistance = 0.45f;

        [Tooltip("Local offset from the CharacterController center the wall raycasts originate from")]
        public Vector3 wallCheckOffset = new Vector3(0f, 0.1f, 0f);

        [Tooltip("Max downward speed while sliding down a wall")]
        public float wallSlideSpeed = -2.5f;

        [Tooltip("Upward velocity applied on a wall jump")]
        public float wallJumpUpForce = 11f;

        [Tooltip("Velocity applied away from the wall on a wall jump")]
        public float wallJumpAwayForce = 9f;

        [Tooltip("Time after a wall jump before the same wall can be re-detected")]
        public float wallJumpLockDuration = 0.15f;

        [Tooltip("Layers considered as walls")]
        public LayerMask wallLayers = 1 << 9;

        [Tooltip("Max degrees a surface may tilt away from true vertical and still count as a wall (rejects slopes/ramps). Keep comfortably below the CharacterController's Slope Limit.")]
        public float wallMaxSurfaceAngle = 35f;

        // ────────────────────────────── Vine Climbing ──────────────────────────────

        [Header("Vine Climbing")]
        [Tooltip("Vertical climb speed along the vine")]
        public float climbSpeed = 3.5f;

        [Tooltip("Horizontal shimmy speed around the vine")]
        public float climbShimmySpeed = 2f;

        [Tooltip("Upward velocity applied when releasing a vine")]
        public float climbExitJumpUpForce = 8f;

        [Tooltip("Velocity applied away from the vine when releasing")]
        public float climbExitJumpAwayForce = 4f;
    }
}
