using TreeFrogAdventure.Enemies;
using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Lets the player kill enemies by landing on them.
    ///
    /// PlayerHealth calls into this BEFORE applying contact damage, rather than both
    /// components independently handling the collision. Two components reacting to the
    /// same contact would race — Unity does not define the order of
    /// OnControllerColliderHit between components on one GameObject — and the player
    /// would sometimes take damage from an enemy they successfully stomped.
    ///
    /// Handles trigger contacts too: Bunny.prefab's collider is a trigger, so
    /// OnControllerColliderHit never fires for it and there is no contact normal to
    /// test. In that case the check falls back to "am I falling, and are my feet above
    /// its middle".
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStomp : MonoBehaviour
    {
        private CharacterController _cc;
        private PlayerController _controller;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _controller = GetComponent<PlayerController>();
        }

        /// <summary>
        /// Evaluates a contact as a possible stomp.
        /// Returns true if the enemy was stomped — the caller must then NOT apply
        /// contact damage to the player.
        /// </summary>
        public bool TryStomp(Collider enemyCollider, Vector3 contactNormal, bool hasNormal)
        {
            if (enemyCollider == null) return false;

            var health = enemyCollider.GetComponentInParent<EnemyHealth>();
            if (health == null || health.IsDead || !health.Stompable) return false;

            PlayerData data = _controller.Data;
            if (data == null) return false;

            // Must actually be coming down on it.
            if (_cc.velocity.y > -data.stompMinFallSpeed) return false;

            // Feet must be above the enemy's midpoint, or walking into its side while
            // descending a slope would count as a stomp.
            float feetY = transform.position.y + _cc.center.y - _cc.height * 0.5f;
            if (feetY < enemyCollider.bounds.center.y) return false;

            // Solid contacts give a normal; use it when we have one.
            if (hasNormal && contactNormal.y < data.stompNormalThreshold) return false;

            if (!health.TryStomp()) return false;

            // Reuses the existing external-velocity hook rather than poking the
            // controller's private velocity.
            _controller.ApplyExternalVelocity(Vector3.up * data.stompBounceForce);

            // Refund the air jump so a chain of stomps stays airborne — the same
            // courtesy wall jump and vine release already get.
            _controller.HasUsedDoubleJump = false;

            return true;
        }
    }
}
