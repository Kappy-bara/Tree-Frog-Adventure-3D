using System.IO;
using System.Linq;
using System.Text;
using TreeFrogAdventure.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Prints how much of the map each level actually uses.
    ///
    /// All five levels of a world share one 100x100 terrain, so the question that
    /// matters while tuning is not "how many enemies" but "does this level cross the
    /// map or huddle in one corner of it". Measuring the generated content's bounding
    /// box answers that directly; eyeballing the scene view does not, because the
    /// invisible boundary walls are far larger than the playable area and swamp any
    /// visual sense of scale.
    ///
    /// Run: Tools > Tree Frog > Report Level Layout
    /// </summary>
    public static class LevelLayoutReport
    {
        /// <summary>The playable centre terrain tile, for the coverage percentage.</summary>
        private const float PlayableSize = 96f;

        [MenuItem("Tools/Tree Frog/Report Level Layout")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[LevelLayoutReport] Content footprint per level:");
            report.AppendLine("  level     span   footprint      coverage  objects");

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string path = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(path))
                {
                    report.AppendLine($"  Level_{level:00}  missing");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                GameObject content = scene.GetRootGameObjects()
                    .FirstOrDefault(g => g.name == LevelPopulator.ContentRoot);

                if (content == null)
                {
                    report.AppendLine($"  Level_{level:00}  no {LevelPopulator.ContentRoot} root");
                    continue;
                }

                // Only leaf objects: the group transforms sit at the origin and would
                // drag the bounding box back to the middle of the map.
                Transform[] placed = content.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.parent != null && t.parent.parent == content.transform)
                    .ToArray();

                if (placed.Length == 0)
                {
                    report.AppendLine($"  Level_{level:00}  empty");
                    continue;
                }

                float minX = placed.Min(t => t.position.x);
                float maxX = placed.Max(t => t.position.x);
                float minZ = placed.Min(t => t.position.z);
                float maxZ = placed.Max(t => t.position.z);

                float width = maxX - minX;
                float depth = maxZ - minZ;
                float coverage = 100f * (width * depth) / (PlayableSize * PlayableSize);

                Transform spawn = FindSpawn(scene);
                Transform goal = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "LevelGoal")?.transform;

                float span = spawn != null && goal != null
                    ? Vector3.Distance(spawn.position, goal.position)
                    : 0f;

                report.AppendLine(
                    $"  Level_{level:00}  {span,5:F0}m   {width,4:F0} x {depth,-4:F0}   " +
                    $"{coverage,5:F0}%    {placed.Length}");
            }

            Debug.Log(report.ToString());
        }

        private static Transform FindSpawn(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform spawn = root.transform.Find("SpawnPoint");
                if (spawn != null) return spawn;
            }
            return null;
        }
    }
}
