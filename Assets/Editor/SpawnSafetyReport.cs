using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreeFrogAdventure.Hazards;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Checks that the player can survive the first second of each level.
    ///
    /// Spawning inside something lethal is invisible in the scene view — the kill volume
    /// is an untextured trigger 400 m wide and hazards are small — but it ends the run
    /// before the player has moved. This reports, per level, what is close enough to the
    /// spawn to matter.
    ///
    /// Run: Tools > Tree Frog > Report Spawn Safety
    /// </summary>
    public static class SpawnSafetyReport
    {
        /// <summary>Nothing that damages on contact should be nearer than this.</summary>
        private const float SafeRadius = 8f;

        [MenuItem("Tools/Tree Frog/Report Spawn Safety")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[SpawnSafetyReport] Result:");

            string currentEnv = null;
            Scene envScene = default;
            int problems = 0;

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string levelPath = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(levelPath)) continue;

                string envName = LevelProgress.GetEnvironmentScene(level);
                string envPath = $"Assets/Scenes/Environments/{envName}.unity";

                if (envName != currentEnv)
                {
                    if (!File.Exists(envPath)) continue;
                    envScene = EditorSceneManager.OpenScene(envPath, OpenSceneMode.Single);
                    currentEnv = envName;
                }

                Scene levelScene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Additive);

                string line = Inspect(level, levelScene, envScene, out bool bad);
                if (bad) problems++;

                report.AppendLine("  " + line);
                EditorSceneManager.CloseScene(levelScene, true);
            }

            report.AppendLine();
            report.AppendLine($"SPAWN SAFETY: {problems} level(s) with an unsafe start");

            Debug.Log(report.ToString());
        }

        private static string Inspect(int level, Scene levelScene, Scene envScene, out bool bad)
        {
            bad = false;

            GameObject[] roots = levelScene.GetRootGameObjects();

            Transform spawn = roots
                .Select(r => r.transform.Find("SpawnPoint"))
                .FirstOrDefault(t => t != null);

            if (spawn == null)
            {
                bad = true;
                return $"Level_{level:00}  NO SPAWN POINT";
            }

            Vector3 at = spawn.position;
            var notes = new List<string>();

            // --- ground ------------------------------------------------------
            float ground = SampleTerrain(at, envScene, out bool onTerrain);

            if (!onTerrain)
            {
                notes.Add("spawn is not over any terrain");
                bad = true;
            }
            else
            {
                float above = at.y - ground;

                // A long drop is survivable but disorienting; below ground is not.
                if (above < -0.5f) { notes.Add($"spawn {-above:F1} m BELOW ground"); bad = true; }
                else if (above > 6f) { notes.Add($"spawn {above:F1} m above ground"); }
            }

            // --- kill volume --------------------------------------------------
            foreach (KillVolume volume in roots.SelectMany(r => r.GetComponentsInChildren<KillVolume>(true)))
            {
                var col = volume.GetComponent<Collider>();
                if (col == null) continue;

                Bounds bounds = col.bounds;

                // Grown a little: the player capsule has height, so merely being above
                // the top face by centimetres is still a death on the first step.
                bounds.Expand(new Vector3(0f, 2f, 0f));

                if (bounds.Contains(at))
                {
                    notes.Add($"spawn is INSIDE the kill volume (top y={col.bounds.max.y:F1}, spawn y={at.y:F1})");
                    bad = true;
                }
                else if (at.y < col.bounds.max.y + 4f && at.y > col.bounds.min.y)
                {
                    notes.Add($"spawn only {at.y - col.bounds.max.y:F1} m above the kill volume");
                    bad = true;
                }
            }

            // --- anything that hurts on contact -------------------------------
            DamageSource nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (DamageSource source in roots.SelectMany(r => r.GetComponentsInChildren<DamageSource>(true)))
            {
                float d = Vector3.Distance(source.transform.position, at);
                if (d >= nearestDistance) continue;

                nearestDistance = d;
                nearest = source;
            }

            if (nearest != null && nearestDistance < SafeRadius)
            {
                notes.Add($"{nearest.name} is {nearestDistance:F1} m from spawn");
                bad = true;
            }

            string detail = notes.Count > 0 ? string.Join("; ", notes) : "ok";
            string distance = nearest != null ? $"nearest hazard {nearestDistance:F0} m" : "no hazards";

            return $"Level_{level:00}  spawn=({at.x,6:F1},{at.y,6:F1},{at.z,6:F1})  {distance,-22}  {detail}";
        }

        /// <summary>Highest terrain under the point, and whether any terrain covers it.</summary>
        private static float SampleTerrain(Vector3 at, Scene envScene, out bool found)
        {
            float best = 0f;
            found = false;

            foreach (GameObject root in envScene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    Vector3 tp = terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;

                    if (at.x < tp.x || at.x > tp.x + size.x) continue;
                    if (at.z < tp.z || at.z > tp.z + size.z) continue;

                    float h = terrain.SampleHeight(new Vector3(at.x, 0f, at.z)) + tp.y;
                    if (!found || h > best) { best = h; found = true; }
                }
            }

            return best;
        }
    }
}
