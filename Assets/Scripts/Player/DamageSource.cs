using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Marker component for anything that should damage the player on contact
    /// (enemies, traps, hazards). Detected by PlayerHealth via trigger or
    /// CharacterController collision.
    /// </summary>
    public class DamageSource : MonoBehaviour
    {
        public int damageAmount = 1;
    }
}
