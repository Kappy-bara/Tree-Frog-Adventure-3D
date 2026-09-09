using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Marker component for anything that should damage the player on contact
    /// (enemies, traps, hazards). Detected by PlayerHealth via trigger or
    /// CharacterController collision.
    ///
    /// Extended rather than replaced by a separate Hazard type: PlayerHealth already
    /// reads this three ways (OnTriggerEnter / OnTriggerStay / OnControllerColliderHit)
    /// and Bunny.prefab already carries one, so a parallel system would mean two code
    /// paths for the same thing.
    /// </summary>
    public class DamageSource : MonoBehaviour
    {
        [Tooltip("Hit points removed per contact.")]
        public int damageAmount = 1;

        [Tooltip("Ignore health entirely and kill outright — lava, bottomless pits, " +
                 "crushers. Costs a life and sends the player to the last checkpoint.")]
        public bool instantKill;

        [Tooltip("Impulse pushing the player away from this source on contact. " +
                 "0 = no knockback.")]
        public float knockbackForce;

        [Tooltip("Extra upward push added to knockback so the player is lifted clear " +
                 "instead of being scraped along the ground.")]
        public float knockbackUpForce = 4f;
    }
}
