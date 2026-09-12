using System;
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

        /// <summary>
        /// Claiming is ONE-WAY. Once reached, a checkpoint is reached for the rest of
        /// the level and can never be re-triggered.
        ///
        /// This used to be a single flag that also tracked which checkpoint currently
        /// owned the respawn point, and a static pointer to that one cleared the flag on
        /// whichever it replaced. So reaching B un-claimed A: walking back re-triggered
        /// A, which un-claimed B, and the compass — which asks for the nearest
        /// un-reached checkpoint — sent the player back and forth between the two
        /// forever.
        /// </summary>
        private bool _claimed;

        /// <summary>True once the player has passed through. Never goes back to false.</summary>
        public bool IsActivated => _claimed;

        /// <summary>Where the compass should point. Never null once awake.</summary>
        public Transform Marker => respawnPoint != null ? respawnPoint : transform;

        /// <summary>
        /// Raised the first time the player reaches this checkpoint. ObjectiveChain
        /// listens so it can reveal the next one — checkpoints are a locked sequence,
        /// not a set of optional respawn points scattered around the map.
        /// </summary>
        public event Action<Checkpoint> OnActivated;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            if (respawnPoint == null) respawnPoint = transform;
            RefreshVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_claimed) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null && !other.CompareTag("Player")) return;

            Activate(player);
        }

        private void Activate(PlayerHealth player)
        {
            _claimed = true;
            RefreshVisual();

            // Last one claimed wins the respawn. Earlier checkpoints stay lit but are
            // inert, so they cannot steal it back.
            GameSession.SetCheckpoint(respawnPoint.position, respawnPoint.rotation);

            OnActivated?.Invoke(this);

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

        /// <summary>
        /// Lit once claimed, and it stays lit — the trail of claimed checkpoints behind
        /// the player is what shows how far through the level they are.
        /// </summary>
        private void RefreshVisual()
        {
            if (activeVisual != null) activeVisual.SetActive(_claimed);
            if (inactiveVisual != null) inactiveVisual.SetActive(!_claimed);
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
