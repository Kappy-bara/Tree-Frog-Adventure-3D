using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.Player;
using TreeFrogAdventure.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// End-to-end check of the biome + level setup. Verifies that every level
    /// resolves to a real environment scene and a real gameplay scene, that Build
    /// Settings has no duplicates or dead entries, and that each environment scene
    /// actually uses its own biome's materials.
    ///
    /// Run: Tools > Tree Frog > Validate Setup
    /// </summary>
    public static class BiomeSetupValidator
    {
        private static int _fail;
        private static int _warn;
        private static StringBuilder _sb;

        [MenuItem("Tools/Tree Frog/Validate Setup")]
        public static void Validate()
        {
            _sb = new StringBuilder();
            _fail = 0;
            _warn = 0;

            _sb.AppendLine("=== TREE FROG SETUP VALIDATION ===");
            _sb.AppendLine();

            CheckConstants();
            CheckBuildSettings();
            CheckLevelResolution();
            CheckSceneFiles();
            CheckEnvironmentScenes();
            CheckGameplaySetup();

            _sb.AppendLine();
            _sb.AppendLine($"RESULT: {_fail} failure(s), {_warn} warning(s)");

            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "validation_result.txt"),
                              _sb.ToString());
            Debug.Log(_sb.ToString());

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(_fail > 0 ? 1 : 0);
            }
        }

        private static void Pass(string msg) => _sb.AppendLine("  PASS  " + msg);
        private static void Fail(string msg) { _sb.AppendLine("  FAIL  " + msg); _fail++; }
        private static void Warn(string msg) { _sb.AppendLine("  WARN  " + msg); _warn++; }

        // ------------------------------------------------------------------
        private static void CheckGameplaySetup()
        {
            _sb.AppendLine("[6] Gameplay systems");

            // --- layers ------------------------------------------------------
            foreach (string layer in new[] { "Enemy", "Hazard", "Projectile" })
            {
                if (LayerMask.NameToLayer(layer) >= 0) Pass($"layer '{layer}' exists");
                else Fail($"layer '{layer}' is missing — add it in Tags and Layers");
            }

            // --- PlayerData masks (the wall-jump bug) ------------------------
            var data = AssetDatabase.LoadAssetAtPath<PlayerData>("Assets/ScriptableObjects/PlayerData.asset");
            if (data == null)
            {
                Fail("PlayerData.asset not found");
            }
            else
            {
                int barrier = LayerMask.NameToLayer("Barrier");
                if (barrier >= 0 && (data.wallLayers.value & (1 << barrier)) != 0)
                    Pass("wallLayers includes Barrier — wall jump can actually hit the Wall objects");
                else
                    Fail($"wallLayers ({data.wallLayers.value}) excludes Barrier — wall jump is inert");

                if ((data.grappleLayer.value & (1 << 5)) == 0)
                    Pass("grappleLayer no longer includes the UI layer");
                else
                    Fail("grappleLayer still includes UI (layer 5)");
            }

            // --- enemy prefabs -----------------------------------------------
            string[] enemies =
            {
                "Enemy_Bunny", "Enemy_Rhino", "Enemy_Chicken",
                "Enemy_Pig", "Enemy_Bat", "Enemy_Bee",
            };

            foreach (string name in enemies)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab");
                if (prefab == null) { Fail($"missing enemy prefab: {name}"); continue; }

                bool health = prefab.GetComponent<EnemyHealth>() != null;
                bool ai = prefab.GetComponent<EnemyBase>() != null;
                bool damage = prefab.GetComponentInChildren<DamageSource>(true) != null;
                bool collider = prefab.GetComponentInChildren<Collider>(true) != null;
                bool visual = prefab.GetComponentInChildren<EnemyVisual>(true) != null;

                if (health && ai && damage && collider && visual) Pass($"{name} fully wired");
                else Fail($"{name} incomplete — health:{health} ai:{ai} damage:{damage} " +
                          $"collider:{collider} visual:{visual}");
            }

            // --- player combat components ------------------------------------
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player_Camera.prefab");
            if (player == null)
            {
                Fail("Player_Camera.prefab not found");
            }
            else
            {
                bool stomp = player.GetComponentInChildren<PlayerStomp>(true) != null;
                bool respawn = player.GetComponentInChildren<PlayerRespawn>(true) != null;
                var thrower = player.GetComponentInChildren<FlyThrower>(true);

                if (stomp) Pass("player has PlayerStomp"); else Fail("player is missing PlayerStomp");
                if (respawn) Pass("player has PlayerRespawn"); else Fail("player is missing PlayerRespawn");

                if (thrower == null)
                {
                    Fail("player is missing FlyThrower");
                }
                else
                {
                    var so = new SerializedObject(thrower);
                    bool wired = so.FindProperty("flyPrefab").objectReferenceValue != null;
                    if (wired) Pass("FlyThrower has its projectile prefab");
                    else Fail("FlyThrower.flyPrefab is empty — throwing does nothing");
                }
            }

            // --- level furniture ---------------------------------------------
            int missingHud = 0, missingKill = 0, missingCheckpoint = 0, missingBanner = 0;

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string path = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(path)) continue;

                string text = File.ReadAllText(path);
                if (!text.Contains("GameHUD")) missingHud++;
                if (!text.Contains("KillVolume")) missingKill++;
                if (!text.Contains("Checkpoint")) missingCheckpoint++;
                if (!text.Contains("LevelBanner")) missingBanner++;
            }

            Report("HUD", missingHud);
            Report("KillVolume", missingKill);
            Report("Checkpoint", missingCheckpoint);
            Report("LevelBanner", missingBanner);

            _sb.AppendLine();
        }

        private static void Report(string what, int missing)
        {
            if (missing == 0) Pass($"all {LevelProgress.TotalLevels} level scenes have a {what}");
            else Fail($"{missing} level scene(s) missing a {what}");
        }

        // ------------------------------------------------------------------
        private static void CheckConstants()
        {
            _sb.AppendLine("[1] LevelProgress constants");

            // Read into locals: these are const, so comparing them directly makes
            // the else branches compile-time unreachable (CS0162).
            int worlds = LevelProgress.TotalWorlds;
            int levels = LevelProgress.TotalLevels;

            if (worlds == 4) Pass("TotalWorlds = 4");
            else Fail($"TotalWorlds = {worlds}, expected 4");

            if (levels == 20) Pass("TotalLevels = 20");
            else Fail($"TotalLevels = {levels}, expected 20");

            _sb.AppendLine();
        }

        // ------------------------------------------------------------------
        private static void CheckBuildSettings()
        {
            _sb.AppendLine("[2] Build Settings");

            var scenes = EditorBuildSettings.scenes;
            var paths = scenes.Select(s => s.path).ToList();

            // Duplicates would mean the same scene registered twice.
            var dupes = paths.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count == 0) Pass($"no duplicate entries ({paths.Count} total)");
            else foreach (string d in dupes) Fail($"duplicate Build Settings entry: {d}");

            // Dead entries point at scenes that no longer exist.
            foreach (string p in paths)
            {
                if (!File.Exists(p)) Fail($"Build Settings points at a missing scene: {p}");
            }

            if (paths.Count > 0 && paths[0].EndsWith("MainMenu.unity")) Pass("index 0 is MainMenu");
            else Fail($"index 0 is '{(paths.Count > 0 ? paths[0] : "<empty>")}', expected MainMenu");

            foreach (var s in scenes.Where(s => !s.enabled))
            {
                Warn($"disabled in Build Settings (will not load at runtime): {s.path}");
            }

            _sb.AppendLine();
        }

        // ------------------------------------------------------------------
        private static void CheckLevelResolution()
        {
            _sb.AppendLine("[3] Level -> scene resolution (all 20 levels)");

            var buildPaths = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            var envUsage = new Dictionary<string, List<int>>();

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string env = LevelProgress.GetEnvironmentScene(level);
                string lvl = LevelProgress.GetLevelScene(level);

                if (!envUsage.ContainsKey(env)) envUsage[env] = new List<int>();
                envUsage[env].Add(level);

                if (!buildPaths.Contains(env))
                {
                    Fail($"level {level}: environment '{env}' not in Build Settings");
                }

                if (!buildPaths.Contains(lvl))
                {
                    Fail($"level {level}: gameplay scene '{lvl}' not in Build Settings");
                }
            }

            if (_fail == 0) Pass("every level resolves to a registered environment + gameplay scene");

            _sb.AppendLine();
            _sb.AppendLine("      biome grouping (5 levels sharing 1 environment = no duplication):");
            foreach (var kv in envUsage.OrderBy(k => k.Value[0]))
            {
                string range = $"{kv.Value.Min()}-{kv.Value.Max()}";
                string ok = kv.Value.Count == LevelProgress.LevelsPerWorld ? "OK " : "BAD";
                _sb.AppendLine($"      [{ok}] {kv.Key,-24} levels {range,-6} ({kv.Value.Count} levels)");
                if (kv.Value.Count != LevelProgress.LevelsPerWorld)
                {
                    Fail($"{kv.Key} serves {kv.Value.Count} levels, expected {LevelProgress.LevelsPerWorld}");
                }
            }

            if (envUsage.Count == 4) Pass("exactly 4 distinct environment scenes for 20 levels");
            else Fail($"{envUsage.Count} distinct environment scenes, expected 4");

            _sb.AppendLine();
        }

        // ------------------------------------------------------------------
        private static void CheckSceneFiles()
        {
            _sb.AppendLine("[4] Scene files on disk");

            long envBytes = 0, levelBytes = 0;

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string path = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (File.Exists(path)) levelBytes += new FileInfo(path).Length;
                else Fail($"missing gameplay scene file: {path}");
            }

            foreach (string env in Enumerable.Range(1, 4)
                         .Select(w => LevelProgress.GetEnvironmentScene((w - 1) * 5 + 1)))
            {
                string path = $"Assets/Scenes/Environments/{env}.unity";
                if (File.Exists(path)) envBytes += new FileInfo(path).Length;
                else Fail($"missing environment scene file: {path}");
            }

            Pass($"20 gameplay scenes = {levelBytes / 1024f / 1024f:F1} MB, " +
                 $"4 environment scenes = {envBytes / 1024f / 1024f:F1} MB");

            long ifCopied = (envBytes / 4) * LevelProgress.TotalLevels;
            _sb.AppendLine($"      copying each biome 5x would have been ~{ifCopied / 1024f / 1024f:F0} MB " +
                           $"(saved ~{(ifCopied - envBytes - levelBytes) / 1024f / 1024f:F0} MB)");

            // Pre-split originals are now redundant.
            foreach (string stale in new[]
                     {
                         "Assets/Scenes/Level_6_CanopyHeights.unity",
                         "Assets/Scenes/Level_11_MistySwamp.unity",
                         "Assets/Scenes/Level_16_VolcanicHighlands.unity",
                     })
            {
                if (File.Exists(stale))
                {
                    Warn($"redundant pre-split scene still on disk ({new FileInfo(stale).Length / 1024 / 1024} MB): {stale}");
                }
            }

            _sb.AppendLine();
        }

        // ------------------------------------------------------------------
        private static void CheckEnvironmentScenes()
        {
            _sb.AppendLine("[5] Environment scene contents");

            var prefixes = new Dictionary<string, string>
            {
                { "Env_EmeraldForest", "EF_" },
                { "Env_CanopyHeights", "CH_" },
                { "Env_MistySwamp", "MS_" },
                { "Env_VolcanicHighlands", "VH_" },
            };

            foreach (var kv in prefixes)
            {
                string path = $"Assets/Scenes/Environments/{kv.Key}.unity";
                if (!File.Exists(path)) continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var wrongBiome = new HashSet<string>();
                int biomeMats = 0, renderers = 0, terrains = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        renderers++;
                        foreach (Material m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null) continue;
                            if (!m.shader.name.StartsWith("TreeFrogAdventure/")) continue;

                            biomeMats++;
                            if (!m.name.StartsWith(kv.Value)) wrongBiome.Add(m.name);
                        }
                    }
                    terrains += root.GetComponentsInChildren<Terrain>(true).Length;
                }

                // Gameplay must have been moved out.
                var gameplayRoots = scene.GetRootGameObjects()
                    .Where(g => !g.name.StartsWith("UNS_") && !g.name.StartsWith("Terrain"))
                    .Select(g => g.name).ToList();

                string skybox = RenderSettings.skybox != null ? RenderSettings.skybox.name : "<none>";

                _sb.AppendLine($"      {kv.Key}: {renderers} renderers, {biomeMats} biome-material slots, " +
                               $"{terrains} terrains, skybox '{skybox}'");

                if (wrongBiome.Count == 0) Pass($"{kv.Key}: all biome materials use the {kv.Value}* set");
                else Fail($"{kv.Key}: foreign materials present -> {string.Join(", ", wrongBiome.Take(5))}");

                if (gameplayRoots.Count == 0) Pass($"{kv.Key}: no gameplay objects left in the environment");
                else Fail($"{kv.Key}: gameplay still present -> {string.Join(", ", gameplayRoots)}");

                if (skybox.StartsWith(kv.Value)) Pass($"{kv.Key}: skybox is {skybox}");
                else Fail($"{kv.Key}: skybox is '{skybox}', expected {kv.Value}Skybox");
            }

            _sb.AppendLine();
        }
    }
}
