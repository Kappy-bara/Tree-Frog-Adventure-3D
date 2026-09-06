using UnityEngine;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Static save/progress API for level unlocks. Persisted via PlayerPrefs.
    /// </summary>
    public static class LevelProgress
    {
        public const int TotalWorlds = 5;
        public const int LevelsPerWorld = 5;
        public const int TotalLevels = TotalWorlds * LevelsPerWorld;

        private const string HighestUnlockedKey = "LevelProgress_HighestUnlocked";

        public static int HighestUnlockedLevel =>
            Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedKey, 1), 1, TotalLevels);

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
                PlayerPrefs.SetInt(HighestUnlockedKey, levelNumber + 1);
                PlayerPrefs.Save();
            }
        }

        public static (int world, int levelInWorld) GetWorldAndLevel(int levelNumber)
        {
            int world = (levelNumber - 1) / LevelsPerWorld + 1;
            int levelInWorld = (levelNumber - 1) % LevelsPerWorld + 1;
            return (world, levelInWorld);
        }

        /// <summary>
        /// Level 1 maps to the real scene. Levels 2-25 use a placeholder naming
        /// scheme until those scenes are built.
        /// </summary>
        public static string GetSceneName(int levelNumber)
        {
            if (levelNumber == 1) return "Level_1_Forest";

            var (world, levelInWorld) = GetWorldAndLevel(levelNumber);
            return $"World{world}_Level{levelInWorld}";
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(HighestUnlockedKey);
        }
    }
}
