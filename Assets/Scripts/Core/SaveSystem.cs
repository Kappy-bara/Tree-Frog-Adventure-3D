using System;
using System.IO;
using UnityEngine;

namespace TreeFrogAdventure.Core
{
    /// <summary>
    /// JSON save file in <c>Application.persistentDataPath</c>.
    ///
    /// Replaces the single PlayerPrefs int that was the entire save system
    /// (`LevelProgress_HighestUnlocked`). Coins, crystals, secrets and completion times
    /// were previously wiped on every launch.
    ///
    /// Writes are ATOMIC — to a temp file, then a replace. A crash or a pulled plug
    /// mid-write leaves the previous save intact rather than a truncated file that
    /// fails to parse and silently resets the player's progress.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "treefrog_save.json";
        private const string LegacyPrefsKey = "LevelProgress_HighestUnlocked";

        private static SaveData _data;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The loaded save, loading it on first access.</summary>
        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        /// <summary>Dropped between play sessions when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay() => _data = null;

        // ------------------------------------------------------------------ io --

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    _data = JsonUtility.FromJson<SaveData>(json);

                    if (_data != null)
                    {
                        Migrate(_data);
                        return _data;
                    }

                    Debug.LogWarning("[SaveSystem] Save file did not parse; starting fresh.");
                }
            }
            catch (Exception e)
            {
                // A corrupt save must not hard-fail the game into an unplayable state.
                Debug.LogError($"[SaveSystem] Failed to read save, starting fresh: {e.Message}");
            }

            _data = new SaveData();
            MigrateFromPlayerPrefs(_data);
            return _data;
        }

        public static void Save()
        {
            if (_data == null) return;

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);

                string json = JsonUtility.ToJson(_data, prettyPrint: true);
                string temp = SavePath + ".tmp";

                File.WriteAllText(temp, json);

                // Replace rather than delete-then-write: the window where no valid save
                // exists is what corrupts progress on a crash.
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, null);
                else File.Move(temp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save: {e.Message}");
            }
        }

        // ----------------------------------------------------------- migration --

        private static void Migrate(SaveData data)
        {
            if (data.version == SaveData.CurrentVersion) return;

            // No older formats exist yet; this is the hook for when one does.
            Debug.Log($"[SaveSystem] Migrating save v{data.version} -> v{SaveData.CurrentVersion}");
            data.version = SaveData.CurrentVersion;
            Save();
        }

        /// <summary>
        /// Carries the old PlayerPrefs unlock frontier into the new file once, so
        /// existing players do not get reset to level 1 by this change.
        /// </summary>
        private static void MigrateFromPlayerPrefs(SaveData data)
        {
            if (!PlayerPrefs.HasKey(LegacyPrefsKey)) return;

            data.highestUnlockedLevel = Mathf.Max(1, PlayerPrefs.GetInt(LegacyPrefsKey, 1));
            Debug.Log($"[SaveSystem] Migrated unlock progress from PlayerPrefs " +
                      $"(level {data.highestUnlockedLevel}).");
            Save();
        }

        // -------------------------------------------------------------- records --

        public static LevelRecord GetLevel(int level) => Data.Find(level);

        public static void SetHighestUnlocked(int level)
        {
            if (level <= Data.highestUnlockedLevel) return;

            Data.highestUnlockedLevel = level;
            Save();
        }

        public static void RecordCrystal(int level)
        {
            if (level <= 0) return;

            LevelRecord record = Data.GetOrCreate(level);
            if (record.crystalFound) return;

            record.crystalFound = true;
            Save();
        }

        public static void RecordSecrets(int level, int count)
        {
            if (level <= 0 || count <= 0) return;

            LevelRecord record = Data.GetOrCreate(level);
            if (count <= record.secretsFound) return;

            record.secretsFound = count;
            Save();
        }

        public static void RecordCompletion(int level, float timeSeconds, int coinsCollected)
        {
            if (level <= 0) return;

            LevelRecord record = Data.GetOrCreate(level);
            record.completed = true;

            if (record.bestTimeSeconds <= 0f || timeSeconds < record.bestTimeSeconds)
            {
                record.bestTimeSeconds = timeSeconds;
            }

            Data.totalCoins = Mathf.Max(Data.totalCoins, coinsCollected);
            Save();
        }

        public static void ResetAll()
        {
            _data = new SaveData();

            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save: {e.Message}");
            }

            PlayerPrefs.DeleteKey(LegacyPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
