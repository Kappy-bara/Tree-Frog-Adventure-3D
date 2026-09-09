using System;
using System.Collections.Generic;

namespace TreeFrogAdventure.Core
{
    /// <summary>Per-level progress. One entry is created lazily the first time a level
    /// records anything.</summary>
    [Serializable]
    public class LevelRecord
    {
        public int level;
        public bool completed;
        public bool crystalFound;
        public int secretsFound;
        public float bestTimeSeconds;
    }

    /// <summary>
    /// The on-disk save. Plain fields with no properties, because JsonUtility only
    /// serialises public fields.
    ///
    /// `version` exists so a future format change can migrate instead of discarding a
    /// player's progress — see SaveSystem.Load.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int highestUnlockedLevel = 1;
        public int totalCoins;
        public List<LevelRecord> levels = new List<LevelRecord>();

        public LevelRecord GetOrCreate(int level)
        {
            foreach (LevelRecord record in levels)
            {
                if (record.level == level) return record;
            }

            var created = new LevelRecord { level = level };
            levels.Add(created);
            return created;
        }

        public LevelRecord Find(int level)
        {
            foreach (LevelRecord record in levels)
            {
                if (record.level == level) return record;
            }
            return null;
        }
    }
}
