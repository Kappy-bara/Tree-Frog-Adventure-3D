using TreeFrogAdventure.Core;
using TreeFrogAdventure.Fx;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// Records a respawn point when the player passes through.
    ///
    /// Only the most recent one counts, so walking back through an earlier checkpoint
    /// does not move the player's progress backwards.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Where the player reappears. Defaults to this object's transform. " +
                 "Use a child placed slightly above ground so respawning does not clip.")]
        [SerializeField] private Transform respawnPoint;

        [Tooltip("Shown once this checkpoint is the active one.")]
        [SerializeField] private GameObject activeVisual;

        [Tooltip("Shown while this checkpoint has not been reached.")]
        [SerializeField] private GameObject inactiveVisual;

        [Tooltip("Also top the player's health back up on activation.")]
        [SerializeField] private bool healOnActivate = true;

        /// <summary>The checkpoint currently owning the respawn point, so the previous
        /// one can switch its visual back off.</summary>
        private static Checkpoint _active;

        private bool _isActive;

        /// <summary>
        /// True once the player has passed through. Read by ObjectiveCompass so the
        /// marker moves on to the next objective instead of pointing backwards.
        /// </summary>
        public bool IsActivated => _isActive;

        /// <summary>Where the compass should point. Never null once awake.</summary>
        public Transform Marker => respawnPoint != null ? respawnPoint : transform;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            if (respawnPoint == null) respawnPoint = transform;
            SetVisual(false);
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isActive) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null && !other.CompareTag("Player")) return;

            Activate(player);
        }

        private void Activate(PlayerHealth player)
        {
            if (_active != null && _active != this) _active.SetVisual(false);

            _active = this;
            SetVisual(true);

            GameSession.SetCheckpoint(respawnPoint.position, respawnPoint.rotation);

            // Reaching a checkpoint is silent otherwise — the only feedback was the
            // visual swap, which is easy to miss while running past.
            FxPool.Play(FxKind.Checkpoint, respawnPoint.position);

            if (healOnActivate && player != null)
            {
                // Clamped at 0: PlayerHealth.Heal is deliberately uncapped, so if a
                // pickup pushed HP above startingHealth this difference would be
                // negative and the checkpoint would quietly damage the player.
                int missing = Mathf.Max(0, player.startingHealth - player.CurrentHealth);
                if (missing > 0) player.Heal(missing);
            }
        }

        private void SetVisual(bool active)
        {
            _isActive = active;
            if (activeVisual != null) activeVisual.SetActive(active);
            if (inactiveVisual != null) inactiveVisual.SetActive(!active);
        }

        private void OnDrawGizmos()
        {
            Transform point = respawnPoint != null ? respawnPoint : transform;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(point.position, 0.4f);
            Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
        }
    }
}
