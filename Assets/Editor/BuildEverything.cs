using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Runs every generator in dependency order, in one editor session.
    ///
    /// The order is not cosmetic. Bosses reference Enemy_Bee as a summoned add and
    /// Hazard_LavaPool as a charge trail; the level populator references the bosses and
    /// the rigged bunny. Running these by hand in the wrong order silently produces
    /// prefabs with null references rather than an error, so the order lives here once.
    ///
    /// Run: Tools > Tree Frog > Build Everything
    /// </summary>
    public static class BuildEverything
    {
        [MenuItem("Tools/Tree Frog/Build Everything", priority = 0)]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine("[BuildEverything] Running generators in dependency order:");

            // UI sprites come first: PlayerAndHudBuilder bakes sprite references into
            // the HUD prefab, so generating them afterwards would leave it holding nulls.
            Step(report, "UI sprites", () =>
            {
                UiSpriteBuilder.Build();
                UI.UiTheme.ClearCache();
            });

            Step(report, "Enemy prefabs", EnemyPrefabBuilder.Build);
            Step(report, "FX prefabs + catalog", FxPrefabBuilder.Build);
            Step(report, "Legacy prefab fixes", LegacyPrefabFixer.Fix);
            Step(report, "Boss prefabs", BossPrefabBuilder.Build);
            Step(report, "Hazard + pickup prefabs", HazardPrefabBuilder.Build);
            Step(report, "Player + HUD", PlayerAndHudBuilder.Build);
            Step(report, "Level content", LevelContentBuilder.Build);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// One failing generator must not stop the rest: in batch mode the log is the
        /// only feedback, and a half-run with a clear report beats an early exit that
        /// leaves you guessing which step died.
        /// </summary>
        private static void Step(StringBuilder report, string label, Action action)
        {
            try
            {
                action();
                report.AppendLine($"  OK   {label}");
            }
            catch (Exception e)
            {
                report.AppendLine($"  FAIL {label}: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
            }
        }
    }
}
