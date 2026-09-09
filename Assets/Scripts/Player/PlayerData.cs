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

        // Default is Default(0) + Barrier(15). Layer 9 is empty in this project and
        // every Wall object sits on Barrier, so the old 1 << 9 made wall jumping
        // silently impossible.
        [Tooltip("Layers considered as walls")]
        public LayerMask wallLayers = (1 << 0) | (1 << 15);

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

        // ──────────────────────────────── Combat ───────────────────────────────────

        [Header("Stomping")]
        [Tooltip("Upward velocity given to the player after stomping an enemy")]
        public float stompBounceForce = 9f;

        [Tooltip("How flat the contact must be to count as a stomp rather than a side hit. " +
                 "1 = perfectly on top. Below this, the enemy damages the player instead.")]
        [Range(0.1f, 0.95f)]
        public float stompNormalThreshold = 0.4f;

        [Tooltip("Player must be falling at least this fast for a stomp to register")]
        public float stompMinFallSpeed = 0.5f;

        [Header("Throwable Flies")]
        [Tooltip("Launch speed of a thrown fly")]
        public float flyThrowForce = 22f;

        [Tooltip("Seconds between throws")]
        public float flyCooldown = 0.35f;

        [Tooltip("Maximum flies the player can carry")]
        public int maxFlies = 10;

        [Tooltip("Flies the player starts a level with")]
        public int startingFlies = 5;

        [Tooltip("Damage a single fly deals")]
        public int flyDamage = 1;

        [Tooltip("Seconds before an un-hit fly despawns")]
        public float flyLifetime = 3f;

        [Tooltip("Layers a thrown fly collides with (enemies + world)")]
        public LayerMask flyHitLayers = (1 << 0) | (1 << 11) | (1 << 15);

        // ───────────────────────────── Wall Climbing ───────────────────────────────
        // Distinct from the climbSpeed above, which belongs to VINE climbing.

        [Header("Wall Climbing")]
        [Tooltip("Upward speed while climbing a wall. Replaces the wall-slide fall.")]
        public float wallClimbSpeed = 3.2f;

        [Tooltip("Seconds of continuous climbing before the frog tires and starts sliding.")]
        public float wallClimbStamina = 1.8f;

        [Tooltip("Stamina recovered per second while grounded.")]
        public float wallClimbRegenRate = 1.2f;

        // ─────────────────────────────── Swimming ──────────────────────────────────

        [Header("Swimming")]
        [Tooltip("Horizontal swim speed. Usually slower than running.")]
        public float swimSpeed = 4.5f;

        [Tooltip("Upward speed while holding Jump underwater.")]
        public float swimUpSpeed = 4f;

        [Tooltip("Gravity while submerged. Much weaker than air gravity.")]
        public float waterGravity = -3f;

        [Tooltip("Velocity damping per second in water — this is what makes water feel heavy.")]
        [Range(0f, 10f)]
        public float waterDrag = 3f;

        [Tooltip("Upward push applied when below the surface, scaled by depth. " +
                 "Above ~1 the frog bobs back up on its own.")]
        public float buoyancy = 6f;

        [Tooltip("How far the frog's centre sits below the surface when floating at rest.")]
        public float swimFloatDepth = 0.35f;

        [Tooltip("Upward velocity when jumping out of water at the surface.")]
        public float waterExitJumpForce = 7f;

        [Tooltip("Layers treated as swimmable water. Layer 4 by default.")]
        public LayerMask waterLayers = 1 << 4;

        // ─────────────────────────────── Power-Ups ─────────────────────────────────
        // Magnitudes only. These are never written to at runtime — PlayerData is a
        // ScriptableObject, so a runtime write persists into the asset between play
        // sessions. Multipliers live on PlayerController instead.

        [Header("Power-Ups")]
        [Tooltip("Run speed multiplier while Speed Boost is active.")]
        public float speedBoostMultiplier = 1.6f;

        [Tooltip("Seconds a Speed Boost lasts.")]
        public float speedBoostDuration = 8f;

        [Tooltip("Jump force multiplier while Jump Boost is active.")]
        public float jumpBoostMultiplier = 1.45f;

        [Tooltip("Seconds a Jump Boost lasts.")]
        public float jumpBoostDuration = 8f;

        [Tooltip("Seconds an Invincibility power-up lasts. Separate from the short " +
                 "post-hit invincibility window on PlayerHealth.")]
        public float invincibilityPowerUpDuration = 10f;
    }
}
