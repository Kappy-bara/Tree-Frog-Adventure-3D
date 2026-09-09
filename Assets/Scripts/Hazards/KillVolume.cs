using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// Instant death on entry. Stretched under the map to catch falls, or used as a
    /// lava/void volume.
    ///
    /// Kills enemies too, so one that wanders off a ledge is cleaned up instead of
    /// falling forever and leaving a live AI chasing from below the world.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillVolume : MonoBehaviour
    {
        [SerializeField] private bool killsPlayer = true;
        [SerializeField] private bool killsEnemies = true;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (killsPlayer)
            {
                var player = other.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.Kill();
                    return;
                }
            }

            if (killsEnemies)
            {
                var enemy = other.GetComponentInParent<EnemyHealth>();
                if (enemy != null) enemy.Kill(DamageKind.Environment);
            }
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.18f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
