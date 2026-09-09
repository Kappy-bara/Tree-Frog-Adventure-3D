using TreeFrogAdventure.Core;
using UnityEngine;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Static progress API for level unlocks and biome/scene naming.
    /// Persistence lives in <see cref="SaveSystem"/> (JSON in persistentDataPath).
    /// </summary>
    public static class LevelProgress
    {
        // 4 biomes x 5 levels. Changing TotalWorlds also changes which biome a
        // level belongs to, so keep it in step with BiomeEnvironmentScenes below.
        public const int TotalWorlds = 4;
        public const int LevelsPerWorld = 5;
        public const int TotalLevels = TotalWorlds * LevelsPerWorld;

        /// <summary>One environment scene per world, indexed by world - 1.</summary>
        private static readonly string[] BiomeEnvironmentScenes =
        {
            "Env_EmeraldForest",     // levels 1-5
            "Env_CanopyHeights",     // levels 6-10
            "Env_MistySwamp",        // levels 11-15
            "Env_VolcanicHighlands", // levels 16-20
        };

        public static readonly string[] BiomeDisplayNames =
        {
            "Emerald Forest",
            "Canopy Heights",
            "Misty Swamp",
            "Volcanic Highlands",
        };

        // Backed by SaveSystem's JSON file now, not PlayerPrefs. The old
        // LevelProgress_HighestUnlocked key is migrated across once on first load, so
        // existing players keep their progress.
        public static int HighestUnlockedLevel =>
            Mathf.Clamp(SaveSystem.Data.highestUnlockedLevel, 1, TotalLevels);

        public static bool IsLevelUnlocked(int levelNumber)
        {
            return levelNumber >= 1 && levelNumber <= HighestUnlockedLevel;
        }

        /// <summary>
        /// Unlocks the next level after the given one. Idempotent and safe to call
        /// unconditionally from a level-end trigger (no-ops if not the current frontier).
        /// </summary>
        public static void CompleteLevel(int levelNumber)
        {
            if (levelNumber == HighestUnlockedLevel && levelNumber < TotalLevels)
            {
                SaveSystem.SetHighestUnlocked(levelNumber + 1);
            }
        }

        public static (int world, int levelInWorld) GetWorldAndLevel(int levelNumber)
        {
            int world = (levelNumber - 1) / LevelsPerWorld + 1;
            int levelInWorld = (levelNumber - 1) % LevelsPerWorld + 1;
            return (world, levelInWorld);
        }

        /// <summary>
        /// The shared biome environment scene for this level. Loaded as the
        /// PRIMARY scene so its lighting and occlusion data apply - see LevelLoader.
        /// </summary>
        public static string GetEnvironmentScene(int levelNumber)
        {
            var (world, _) = GetWorldAndLevel(levelNumber);
            int index = Mathf.Clamp(world - 1, 0, BiomeEnvironmentScenes.Length - 1);
            return BiomeEnvironmentScenes[index];
        }

        /// <summary>
        /// The small per-level gameplay scene (player spawn, enemies, puzzles),
        /// loaded additively on top of the environment.
        /// </summary>
        public static string GetLevelScene(int levelNumber)
        {
            return $"Level_{levelNumber:00}";
        }

        public static string GetBiomeName(int levelNumber)
        {
            var (world, _) = GetWorldAndLevel(levelNumber);
            return BiomeDisplayNames[Mathf.Clamp(world - 1, 0, BiomeDisplayNames.Length - 1)];
        }

        /// <summary>
        /// Wipes all saved progress. Previously this deleted the PlayerPrefs key
        /// without calling Save(), unlike CompleteLevel — so a reset could silently
        /// fail to stick if the process ended before Unity flushed prefs.
        /// </summary>
        public static void ResetProgress()
        {
            SaveSystem.ResetAll();
        }
    }
}
