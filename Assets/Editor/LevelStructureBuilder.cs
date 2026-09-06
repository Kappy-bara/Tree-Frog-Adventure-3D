using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Splits the four big biome scenes into a shared environment scene per biome
    /// plus 20 small per-level gameplay scenes.
    ///
    /// Why: the five levels of a world use the same environment. Copying the whole
    /// 34 MB scene five times would mean ~680 MB of near-identical files, and every
    /// environment fix would have to be repeated five times. After this split there
    /// are 4 environment scenes and 20 gameplay scenes of a few KB each.
    ///
    /// The environment scene is loaded as the PRIMARY scene at runtime so its fog,
    /// ambient, skybox and occlusion data apply; the gameplay scene goes on top
    /// additively. See LevelLoader.
    ///
    /// Run: Tools > Tree Frog > Build Level Structure
    ///
    /// Non-destructive: it never overwrites a scene that already exists, so once you
    /// start designing levels this is safe to re-run. The original biome scenes are
    /// left on disk untouched (delete them yourself once you are happy - git has them).
    /// </summary>
    public static class LevelStructureBuilder
    {
        private const string EnvFolder = "Assets/Scenes/Environments";
        private const string LevelFolder = "Assets/Scenes/Levels";
        private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";

        private const int TotalLevels = 20;
        private const int LevelsPerWorld = 5;

        // Environment scene name -> the biome scene it is carved out of.
        // Emerald Forest comes first because it is the hand-built level, so its
        // gameplay objects become the template every other level starts from.
        private static readonly (string envName, string sourceScene)[] Biomes =
        {
            ("Env_EmeraldForest",     "Assets/Scenes/Level_1_Forest.unity"),
            ("Env_CanopyHeights",     "Assets/Scenes/Level_6_CanopyHeights.unity"),
            ("Env_MistySwamp",        "Assets/Scenes/Level_11_MistySwamp.unity"),
            ("Env_VolcanicHighlands", "Assets/Scenes/Level_16_VolcanicHighlands.unity"),
        };

        [MenuItem("Tools/Tree Frog/Build Level Structure")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Directory.CreateDirectory(EnvFolder);
            Directory.CreateDirectory(LevelFolder);
            AssetDatabase.Refresh();

            var report = new StringBuilder();
            report.AppendLine("[LevelStructureBuilder] Result:");

            string templatePath = $"{LevelFolder}/Level_01.unity";
            bool templateExists = File.Exists(templatePath);

            foreach ((string envName, string sourceScene) in Biomes)
            {
                string envPath = $"{EnvFolder}/{envName}.unity";

                if (File.Exists(envPath))
                {
                    report.AppendLine($"  SKIP (exists): {envPath}");
                    continue;
                }

                if (!File.Exists(sourceScene))
                {
                    report.AppendLine($"  SKIP (source missing): {sourceScene}");
                    continue;
                }

                if (!AssetDatabase.CopyAsset(sourceScene, envPath))
                {
                    report.AppendLine($"  FAILED to copy {sourceScene} -> {envPath}");
                    continue;
                }
                AssetDatabase.Refresh();

                Scene env = EditorSceneManager.OpenScene(envPath, OpenSceneMode.Single);
                GameObject[] roots = env.GetRootGameObjects();

                int kept = roots.Count(IsEnvironmentRoot);
                int moved = 0;

                // The first biome donates its gameplay objects as the template
                // for all 20 levels. Every other biome just discards its copies.
                bool extract = !templateExists && envName == Biomes[0].envName;

                if (extract)
                {
                    Scene gameplay = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                    foreach (GameObject root in roots)
                    {
                        if (IsEnvironmentRoot(root)) continue;
                        SceneManager.MoveGameObjectToScene(root, gameplay);
                        moved++;
                    }

                    EditorSceneManager.SaveScene(gameplay, templatePath);
                    EditorSceneManager.CloseScene(gameplay, true);
                    templateExists = true;

                    report.AppendLine($"  {templatePath}: extracted {moved} gameplay root(s)");
                }
                else
                {
                    foreach (GameObject root in roots)
                    {
                        if (IsEnvironmentRoot(root)) continue;
                        Object.DestroyImmediate(root);
                        moved++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(env);
                EditorSceneManager.SaveScene(env, envPath);

                report.AppendLine($"  {envPath}: kept {kept} environment root(s), removed {moved} gameplay root(s)");
            }

            report.AppendLine(CreateLevelScenes(templatePath));
            report.AppendLine(RebuildBuildSettings());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Environment is everything from the Ultimate Nature pack plus the
        /// terrains. Everything else - player, camera, UI, waypoints, walls,
        /// platforms - is per-level gameplay.
        /// </summary>
        private static bool IsEnvironmentRoot(GameObject go)
        {
            string n = go.name;
            return n.StartsWith("UNS_") || n.StartsWith("Terrain");
        }

        private static string CreateLevelScenes(string templatePath)
        {
            if (!File.Exists(templatePath))
            {
                return "  No level template - skipped creating Level_02..Level_20.";
            }

            int created = 0, skipped = 0;

            for (int level = 2; level <= TotalLevels; level++)
            {
                string path = $"{LevelFolder}/Level_{level:00}.unity";

                if (File.Exists(path))
                {
                    skipped++;   // never clobber a level you have started designing
                    continue;
                }

                if (AssetDatabase.CopyAsset(templatePath, path))
                {
                    created++;
                }
            }

            AssetDatabase.Refresh();
            return $"  Level scenes: {created} created, {skipped} left alone (already existed)";
        }

        private static string RebuildBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            // Index 0 must be the main menu - it is what the game boots into.
            if (File.Exists(MainMenuScene))
            {
                scenes.Add(new EditorBuildSettingsScene(MainMenuScene, true));
            }

            foreach ((string envName, string _) in Biomes)
            {
                string path = $"{EnvFolder}/{envName}.unity";
                if (File.Exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            for (int level = 1; level <= TotalLevels; level++)
            {
                string path = $"{LevelFolder}/Level_{level:00}.unity";
                if (File.Exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            return $"  Build Settings rebuilt: {scenes.Count} scene(s) registered";
        }
    }
}
