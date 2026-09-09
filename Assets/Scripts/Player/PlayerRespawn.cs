using TreeFrogAdventure.Core;
using TreeFrogAdventure.Levels;
using UnityEngine;

namespace TreeFrogAdventure.Player
{
    /// <summary>
    /// Moves the player back to the last checkpoint, or the level's spawn point if no
    /// checkpoint has been reached.
    ///
    /// Uses the same disable-CharacterController-across-the-teleport trick as
    /// LevelContext.MovePlayerToSpawn(): a CharacterController overrides direct
    /// transform writes, so without this the move is silently undone on the next frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerRespawn : MonoBehaviour
    {
        [Tooltip("Fallback if there is no checkpoint AND no LevelContext spawn point.")]
        [SerializeField] private Vector3 fallbackPosition = new Vector3(0f, 20f, 0f);

        private CharacterController _cc;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
        }

        /// <summary>Teleports to the best available respawn point.</summary>
        public void RespawnNow()
        {
            GetRespawnPoint(out Vector3 position, out Quaternion rotation);
            TeleportTo(position, rotation);
        }

        public void GetRespawnPoint(out Vector3 position, out Quaternion rotation)
        {
            if (GameSession.HasCheckpoint)
            {
                position = GameSession.CheckpointPosition;
                rotation = GameSession.CheckpointRotation;
                return;
            }

            LevelContext context = LevelContext.Current;
            if (context != null && context.SpawnPoint != null)
            {
                position = context.SpawnPoint.position;
                rotation = context.SpawnPoint.rotation;
                return;
            }

            // Nothing authored — fall back to where the player started the scene.
            position = _initialPosition != Vector3.zero ? _initialPosition : fallbackPosition;
            rotation = _initialRotation;
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _cc != null && _cc.enabled;
            if (_cc != null) _cc.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (_cc != null) _cc.enabled = wasEnabled;
        }
    }
}
