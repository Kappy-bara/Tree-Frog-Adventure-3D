using System.Text.RegularExpressions;
using UnityEngine;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Player;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// Identifies which level the containing gameplay scene is, and where the
    /// player starts in it.
    ///
    /// The level number is derived from the scene's own name (Level_07 -> 7), so
    /// there is nothing to wire up per scene and nothing to forget when you
    /// duplicate one. Set levelNumberOverride only if you rename scenes away from
    /// that convention.
    ///
    /// Lives in the additively-loaded gameplay scene, never in the shared
    /// environment scene.
    /// </summary>
    public class LevelContext : MonoBehaviour
    {
        [Tooltip("Leave at 0 to derive the level number from the scene name.")]
        [SerializeField] private int levelNumberOverride;

        [Tooltip("Optional. If set, the player is moved here on load. Move this " +
                 "marker to give each level a different starting point on the shared map.")]
        [SerializeField] private Transform spawnPoint;

        /// <summary>The LevelContext of the currently loaded gameplay scene.</summary>
        public static LevelContext Current { get; private set; }

        public int LevelNumber { get; private set; }

        /// <summary>Where the level starts. Used as the respawn fallback when the
        /// player dies before reaching any checkpoint.</summary>
        public Transform SpawnPoint => spawnPoint;

        private void Awake()
        {
            Current = this;
            LevelNumber = levelNumberOverride > 0
                ? levelNumberOverride
                : ParseLevelNumber(gameObject.scene.name);

            if (LevelNumber <= 0)
            {
                Debug.LogWarning($"[LevelContext] Could not determine a level number from scene " +
                                 $"'{gameObject.scene.name}'. Set levelNumberOverride.", this);
            }
        }

        private void Start()
        {
            MovePlayerToSpawn();
            InitialiseSession();
        }

        /// <summary>
        /// Clears the previous level's checkpoint and tops fly ammo back up, so every
        /// level is startable even if you arrived with none left.
        /// </summary>
        private void InitialiseSession()
        {
            int startingFlies = 5;

            var player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null && controller.Data != null)
                {
                    startingFlies = controller.Data.startingFlies;
                }
            }

            GameSession.ResetForLevel(startingFlies);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void MovePlayerToSpawn()
        {
            if (spawnPoint == null) return;

            var player = FindAnyObjectByType<PlayerHealth>();
            if (player == null) return;

            // A CharacterController overrides direct transform writes, so it has
            // to be disabled across the teleport or the move is silently undone.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            if (controller != null) controller.enabled = wasEnabled;
        }

        /// <summary>"Level_07" -> 7. Returns 0 if the name has no digits.</summary>
        public static int ParseLevelNumber(string sceneName)
        {
            Match m = Regex.Match(sceneName ?? string.Empty, @"(\d+)");
            return m.Success && int.TryParse(m.Value, out int n) ? n : 0;
        }
    }
}
