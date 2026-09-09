using System;
using UnityEngine;

namespace TreeFrogAdventure.Core
{
    /// <summary>
    /// Run state for the current play session: lives, coins, fly ammo and the last
    /// checkpoint.
    ///
    /// Deliberately NOT the same thing as <see cref="TreeFrogAdventure.Menu.LevelProgress"/>.
    /// That persists which levels are unlocked across launches (PlayerPrefs); this is
    /// in-memory state for the current run and is intentionally lost on quit — dying
    /// out of lives should not be recoverable by relaunching.
    ///
    /// Note on the 3-life system: PlayerHealth's `startingHealth = 3` is HIT POINTS,
    /// not lives. Lives live here, and one is spent each time health reaches zero.
    /// </summary>
    public static class GameSession
    {
        public const int StartingLives = 3;
        public const int MaxLives = 9;

        public static int Lives { get; private set; } = StartingLives;
        public static int Coins { get; private set; }
        public static int Flies { get; private set; }

        /// <summary>Nature Crystals recovered — the story collectible, one per level.</summary>
        public static int Crystals { get; private set; }

        /// <summary>Secret areas discovered in the current level.</summary>
        public static int SecretsThisLevel { get; private set; }

        public static bool HasCheckpoint { get; private set; }
        public static Vector3 CheckpointPosition { get; private set; }
        public static Quaternion CheckpointRotation { get; private set; } = Quaternion.identity;

        public static event Action<int> OnLivesChanged;
        public static event Action<int> OnCoinsChanged;
        public static event Action<int> OnFliesChanged;
        public static event Action<int> OnCrystalsChanged;
        public static event Action<int> OnSecretsChanged;

        /// <summary>
        /// With "Enter Play Mode Options" set to skip domain reload, statics survive
        /// between play sessions and you would start a run with the previous run's
        /// lives. This resets before any scene loads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            Lives = StartingLives;
            Coins = 0;
            Flies = 0;
            Crystals = 0;
            SecretsThisLevel = 0;
            ClearCheckpoint();

            // Listeners from the previous session are dead objects.
            OnLivesChanged = null;
            OnCoinsChanged = null;
            OnFliesChanged = null;
            OnCrystalsChanged = null;
            OnSecretsChanged = null;
        }

        // ------------------------------------------------------------ lifecycle --

        /// <summary>Full reset — new game from the main menu, or after a game over.</summary>
        public static void ResetForNewGame()
        {
            Lives = StartingLives;
            Coins = 0;
            Flies = 0;
            Crystals = 0;
            SecretsThisLevel = 0;
            ClearCheckpoint();

            OnCrystalsChanged?.Invoke(Crystals);
            OnSecretsChanged?.Invoke(SecretsThisLevel);
            OnLivesChanged?.Invoke(Lives);
            OnCoinsChanged?.Invoke(Coins);
            OnFliesChanged?.Invoke(Flies);
        }

        /// <summary>
        /// Entering a level. Lives and coins carry across levels; the checkpoint does
        /// not, and fly ammo is topped back up so a level is always startable.
        /// </summary>
        public static void ResetForLevel(int startingFlies)
        {
            ClearCheckpoint();
            Flies = Mathf.Max(Flies, startingFlies);
            SecretsThisLevel = 0;

            OnFliesChanged?.Invoke(Flies);
            OnSecretsChanged?.Invoke(SecretsThisLevel);
        }

        // ---------------------------------------------------------------- lives --

        /// <summary>Spends a life. Returns true if the run continues.</summary>
        public static bool LoseLife()
        {
            Lives = Mathf.Max(0, Lives - 1);
            OnLivesChanged?.Invoke(Lives);
            return Lives > 0;
        }

        public static void AddLife(int amount = 1)
        {
            Lives = Mathf.Clamp(Lives + amount, 0, MaxLives);
            OnLivesChanged?.Invoke(Lives);
        }

        // ---------------------------------------------------------------- coins --

        public static void AddCoins(int amount)
        {
            Coins = Mathf.Max(0, Coins + amount);
            OnCoinsChanged?.Invoke(Coins);
        }

        // ---------------------------------------------------------------- flies --

        public static void AddFlies(int amount, int max)
        {
            Flies = Mathf.Clamp(Flies + amount, 0, Mathf.Max(0, max));
            OnFliesChanged?.Invoke(Flies);
        }

        /// <summary>Spends one fly. Returns false when out of ammo.</summary>
        public static bool ConsumeFly()
        {
            if (Flies <= 0) return false;

            Flies--;
            OnFliesChanged?.Invoke(Flies);
            return true;
        }

        // ------------------------------------------------------------ crystals --

        public static void AddCrystal(int amount = 1)
        {
            Crystals = Mathf.Max(0, Crystals + amount);
            OnCrystalsChanged?.Invoke(Crystals);
        }

        public static void AddSecret(int amount = 1)
        {
            SecretsThisLevel = Mathf.Max(0, SecretsThisLevel + amount);
            OnSecretsChanged?.Invoke(SecretsThisLevel);
        }

        // ----------------------------------------------------------- checkpoint --

        public static void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            CheckpointPosition = position;
            CheckpointRotation = rotation;
            HasCheckpoint = true;
        }

        public static void ClearCheckpoint()
        {
            HasCheckpoint = false;
            CheckpointPosition = Vector3.zero;
            CheckpointRotation = Quaternion.identity;
        }
    }
}
