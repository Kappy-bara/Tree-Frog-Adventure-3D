using TreeFrogAdventure.Core;
using TreeFrogAdventure.Menu;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Save-file shortcuts for testing.
    ///
    /// Level select only offers levels the save says are unlocked, which means reaching
    /// level 17 to test level 17. These write the save directly rather than adding a
    /// cheat path into the game, so nothing in shipping code has to know about them and
    /// there is no debug flag to accidentally leave switched on.
    ///
    /// The editor and a local build share Application.persistentDataPath, so unlocking
    /// here also unlocks in a build made on this machine.
    /// </summary>
    public static class SaveDevTools
    {
        [MenuItem("Tools/Tree Frog/Save/Unlock All Levels", priority = 200)]
        public static void UnlockAll()
        {
            SaveSystem.SetHighestUnlocked(LevelProgress.TotalLevels);

            Debug.Log($"[SaveDevTools] All {LevelProgress.TotalLevels} levels unlocked.\n" +
                      $"Save: {SaveSystem.SavePath}");
        }

        [MenuItem("Tools/Tree Frog/Save/Reset Progress", priority = 201)]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset progress?",
                    "Deletes the save file: unlocks, crystals, secrets and best times.\n\n" +
                    SaveSystem.SavePath,
                    "Delete", "Cancel"))
            {
                return;
            }

            SaveSystem.ResetAll();
            Debug.Log("[SaveDevTools] Progress reset.");
        }

        [MenuItem("Tools/Tree Frog/Save/Show Save File", priority = 202)]
        public static void ShowSave()
        {
            SaveData data = SaveSystem.Data;

            Debug.Log($"[SaveDevTools] {SaveSystem.SavePath}\n" +
                      $"  highest unlocked : {data.highestUnlockedLevel}\n" +
                      $"  total coins      : {data.totalCoins}\n" +
                      $"  levels recorded  : {data.levels.Count}\n" +
                      $"  crystals found   : {data.levels.FindAll(l => l.crystalFound).Count}");

            EditorUtility.RevealInFinder(SaveSystem.SavePath);
        }
    }
}
