using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Hazards;
using TreeFrogAdventure.Levels;
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
            CheckMovementAndSave();
            CheckBossesFxAndContent();

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

        // ------------------------------------------------------------------
        /// <summary>
        /// Bosses, effects and the generated level content.
        ///
        /// Where section [6] scans the scene files as raw text — and so would pass on
        /// an object that exists but is broken or disabled — this section opens each
        /// level and asserts on loaded components.
        /// </summary>
        private static void CheckBossesFxAndContent()
        {
            _sb.AppendLine();
            _sb.AppendLine("[8] Bosses, FX and level content");

            CheckBossPrefabs();
            CheckEnemyHitboxes();
            CheckFxCatalog();
            CheckLegacyEnemyRetired();
            CheckLevelContent();
        }

        private static void CheckBossPrefabs()
        {
            var expected = new[]
            {
                "Boss_ForestGuardian",
                "Boss_CanopyGuardian",
                "Boss_SwampGuardian",
                "Boss_VolcanoGuardian",
            };

            foreach (string name in expected)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Bosses/{name}.prefab");
                if (prefab == null) { Fail($"missing boss prefab: {name}"); continue; }

                var boss = prefab.GetComponent<BossBase>();
                var health = prefab.GetComponent<EnemyHealth>();

                if (boss == null) { Fail($"{name} has no BossBase"); continue; }
                if (health == null) { Fail($"{name} has no EnemyHealth"); continue; }

                if (health.MaxHealth < 2)
                {
                    Fail($"{name} has {health.MaxHealth} HP, too few for its phases");
                    continue;
                }

                // Authored stompable would let a lucky jump skip the phase structure
                // entirely. BossBase opens the stomp window itself, between phases.
                if (health.Stompable) { Fail($"{name} is authored stompable"); continue; }

                if (prefab.GetComponentInChildren<Fx.EnemyFeedback>(true) == null)
                {
                    Warn($"{name} has no EnemyFeedback, so hits on it are silent");
                }

                Pass($"{name}: {health.MaxHealth} HP, {boss.PhaseCount} phases");
            }

            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Combat/Boss_Projectile.prefab");

            if (projectile == null || projectile.GetComponent<BossProjectile>() == null)
            {
                Fail("Boss_Projectile missing or has no BossProjectile component");
            }
            else
            {
                Pass("Boss_Projectile wired");
            }
        }

        /// <summary>
        /// Enemy hitboxes must be roughly enemy-sized in WORLD space.
        ///
        /// Collider fields are local, so a prefab whose root is scaled — the rigged
        /// bunny model imports at 200x — multiplies them. Authoring that capsule in
        /// metres produced a 52 m radius trigger carrying a DamageSource, which damaged
        /// the player continuously from across the map the moment a level loaded. The
        /// prefab looked correct in the inspector; only the world size gave it away.
        /// </summary>
        private static void CheckEnemyHitboxes()
        {
            const float maxReasonable = 6f;

            string[] folders = { "Assets/Prefabs/Enemies", "Assets/Prefabs/Bosses" };
            int checkedCount = 0;

            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (string path in Directory.GetFiles(folder, "*.prefab"))
                {
                    string assetPath = path.Replace('\\', '/');
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                    if (prefab == null || prefab.GetComponent<EnemyHealth>() == null) continue;

                    checkedCount++;
                    float biggest = 0f;
                    string worst = null;

                    foreach (Collider col in prefab.GetComponentsInChildren<Collider>(true))
                    {
                        float size = WorldExtent(col);
                        if (size <= biggest) continue;

                        biggest = size;
                        worst = col.GetType().Name;
                    }

                    var controller = prefab.GetComponent<CharacterController>();
                    if (controller != null)
                    {
                        Vector3 scale = controller.transform.lossyScale;
                        float lateral = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

                        float size = Mathf.Max(controller.radius * 2f * lateral,
                                               controller.height * Mathf.Abs(scale.y));

                        if (size > biggest) { biggest = size; worst = "CharacterController"; }
                    }

                    string name = Path.GetFileNameWithoutExtension(assetPath);
                    float rootScale = prefab.transform.localScale.x;

                    if (biggest > maxReasonable)
                    {
                        Fail($"{name}: {worst} is {biggest:F1} m across in world space " +
                             $"(root scale {rootScale:F0}x) — collider sizes are local and " +
                             "must be divided by the root scale");
                    }
                    else
                    {
                        Pass($"{name}: largest hitbox {biggest:F2} m" +
                             (Mathf.Abs(rootScale - 1f) > 0.01f ? $" (root scale {rootScale:F0}x)" : string.Empty));
                    }
                }
            }

            if (checkedCount == 0) Warn("no enemy prefabs found to size-check");
        }

        /// <summary>Largest world-space dimension of a collider on a prefab asset.</summary>
        private static float WorldExtent(Collider col)
        {
            // bounds are unreliable on an asset that was never instantiated, so this
            // multiplies the authored local size by the transform's lossy scale.
            Vector3 scale = col.transform.lossyScale;
            float lateral = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            switch (col)
            {
                case SphereCollider sphere:
                    return sphere.radius * 2f * Mathf.Max(lateral, Mathf.Abs(scale.y));

                case CapsuleCollider capsule:
                    return Mathf.Max(capsule.radius * 2f * lateral,
                                     capsule.height * Mathf.Abs(scale.y));

                case BoxCollider box:
                    Vector3 world = Vector3.Scale(box.size, scale);
                    return Mathf.Max(Mathf.Abs(world.x), Mathf.Max(Mathf.Abs(world.y), Mathf.Abs(world.z)));

                default:
                    return 0f;
            }
        }

        private static void CheckFxCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<Fx.FxCatalog>("Assets/Resources/FxCatalog.asset");

            if (catalog == null)
            {
                Fail("no FxCatalog in Assets/Resources; run Tools > Tree Frog > Build FX Prefabs");
                return;
            }

            int missing = 0;
            var kinds = System.Enum.GetValues(typeof(Fx.FxKind));

            // Every enum value must resolve, or that effect silently never plays.
            foreach (Fx.FxKind kind in kinds)
            {
                GameObject prefab = catalog.GetPrefab(kind);

                if (prefab == null) { Fail($"FxCatalog has no prefab for {kind}"); missing++; continue; }

                if (prefab.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    Fail($"FX prefab for {kind} has no ParticleSystem");
                    missing++;
                }
            }

            if (missing == 0) Pass($"FxCatalog resolves all {kinds.Length} effect kinds");
        }

        private static void CheckLegacyEnemyRetired()
        {
            var rigged = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Enemies/Enemy_Bunny_Rigged.prefab");

            if (rigged == null)
            {
                Fail("Enemy_Bunny_Rigged missing; run Tools > Tree Frog > Fix Legacy Prefabs");
            }
            else if (rigged.GetComponent<EnemyHealth>() == null)
            {
                Fail("Enemy_Bunny_Rigged has no EnemyHealth, so it would be unkillable");
            }
            else
            {
                Pass("Enemy_Bunny_Rigged is killable and keeps its animation");
            }

            // The legacy Bunny.prefab has no EnemyHealth at all, so an instance of it in
            // a level is an enemy that nothing in the game can kill.
            string legacyGuid = AssetDatabase.AssetPathToGUID("Assets/Prefabs/Enemies/Bunny.prefab");
            var offenders = new List<string>();

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string path = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(path)) continue;

                if (File.ReadAllText(path).Contains(legacyGuid))
                {
                    offenders.Add($"Level_{level:00}");
                }
            }

            if (offenders.Count > 0)
            {
                Fail($"un-killable legacy Bunny.prefab still instanced in: {string.Join(", ", offenders)}");
            }
            else
            {
                Pass("no level references the un-killable legacy Bunny.prefab");
            }
        }

        private static void CheckLevelContent()
        {
            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string levelPath = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(levelPath)) { Fail($"missing level scene: {levelPath}"); continue; }

                // Only the gameplay scene is opened. It holds every component checked
                // here, and skipping the 44 MB environment keeps this section quick.
                Scene scene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();

                int enemies = roots.Sum(r => r.GetComponentsInChildren<EnemyBase>(true).Length);
                int pickups = roots.Sum(r => r.GetComponentsInChildren<Collectible>(true).Length);
                int secrets = roots.Sum(r => r.GetComponentsInChildren<SecretArea>(true).Length);
                int checkpoints = roots.Sum(r => r.GetComponentsInChildren<Checkpoint>(true).Length);

                int crystals = roots
                    .SelectMany(r => r.GetComponentsInChildren<Collectible>(true))
                    .Count(IsCrystal);

                var (_, levelInWorld) = LevelProgress.GetWorldAndLevel(level);
                bool bossLevel = levelInWorld == LevelProgress.LevelsPerWorld;

                var problems = new List<string>();

                if (enemies == 0) problems.Add("no enemies");
                if (pickups == 0) problems.Add("no pickups");
                if (checkpoints == 0) problems.Add("no checkpoint");
                if (secrets == 0) problems.Add("no secret area");

                if (bossLevel)
                {
                    BossArena arena = roots.Select(r => r.GetComponentInChildren<BossArena>(true))
                                           .FirstOrDefault(a => a != null);
                    BossBase boss = roots.Select(r => r.GetComponentInChildren<BossBase>(true))
                                         .FirstOrDefault(b => b != null);

                    if (arena == null) problems.Add("no BossArena");
                    if (boss == null) problems.Add("no guardian");

                    // A boss level carries no crystal in the scene: the arena spawns it
                    // where the guardian falls.
                }
                else if (crystals != 1)
                {
                    problems.Add($"{crystals} Nature Crystals, expected exactly 1");
                }

                LevelGoal goal = roots.Select(r => r.GetComponentInChildren<LevelGoal>(true))
                                      .FirstOrDefault(g => g != null);

                if (goal == null)
                {
                    problems.Add("no LevelGoal");
                }
                else
                {
                    var so = new SerializedObject(goal);
                    if (so.FindProperty("loadNextAutomatically").boolValue)
                    {
                        problems.Add("LevelGoal auto-advances, skipping the results screen");
                    }
                }

                // The chain is the level structure: without it every checkpoint and
                // the exit are live from the first frame and the route means nothing.
                ObjectiveChain chain = roots.Select(r => r.GetComponentInChildren<ObjectiveChain>(true))
                                            .FirstOrDefault(c => c != null);

                if (chain == null)
                {
                    problems.Add("no ObjectiveChain");
                }
                else
                {
                    var chainSo = new SerializedObject(chain);
                    int wired = chainSo.FindProperty("checkpoints").arraySize;

                    if (wired != checkpoints)
                    {
                        problems.Add($"chain wires {wired} checkpoints but the scene has {checkpoints}");
                    }

                    if (chainSo.FindProperty("finalObjective").objectReferenceValue == null)
                    {
                        problems.Add("chain has no final objective, so the exit never opens");
                    }
                }

                if (problems.Count > 0)
                {
                    Fail($"Level_{level:00}: {string.Join("; ", problems)}");
                }
                else
                {
                    Pass($"Level_{level:00}: {enemies} enemies, {pickups} pickups, " +
                         $"{secrets} secret(s), {checkpoints} checkpoint(s)" +
                         (bossLevel ? ", boss arena" : ", 1 crystal"));
                }
            }
        }

        private static bool IsCrystal(Collectible collectible)
        {
            var so = new SerializedObject(collectible);
            return so.FindProperty("kind").enumValueIndex == (int)CollectibleKind.NatureCrystal;
        }
        private static void Fail(string msg) { _sb.AppendLine("  FAIL  " + msg); _fail++; }
        private static void Warn(string msg) { _sb.AppendLine("  WARN  " + msg); _warn++; }

        // ------------------------------------------------------------------
        private static void CheckMovementAndSave()
        {
            _sb.AppendLine("[7] Movement, power-ups and save");

            // --- water --------------------------------------------------------
            const string waterRoot = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Water/Prefabs";
            int waterLayer = LayerMask.NameToLayer("Water");

            foreach (string name in new[] { "UNS_Water_Flat", "UNS_Water_Detailed" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{waterRoot}/{name}.prefab");
                if (prefab == null) { Fail($"missing water prefab: {name}"); continue; }

                bool onLayer = prefab.layer == waterLayer;
                var volume = prefab.GetComponent<WaterVolume>();
                var col = prefab.GetComponent<Collider>();
                bool isTrigger = col != null && col.isTrigger;
                bool noMesh = prefab.GetComponentInChildren<MeshCollider>(true) == null;

                if (onLayer && volume != null && isTrigger && noMesh)
                    Pass($"{name} is a swimmable trigger volume on the Water layer");
                else
                    Fail($"{name} not swimmable — layer:{onLayer} volume:{volume != null} " +
                         $"trigger:{isTrigger} meshColliderRemoved:{noMesh}");
            }

            // --- player components --------------------------------------------
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player_Camera.prefab");
            if (player == null)
            {
                Fail("Player_Camera.prefab not found");
            }
            else
            {
                if (player.GetComponentInChildren<PlayerSwim>(true) != null) Pass("player has PlayerSwim");
                else Fail("player is missing PlayerSwim");

                if (player.GetComponentInChildren<PlayerBuffs>(true) != null) Pass("player has PlayerBuffs");
                else Fail("player is missing PlayerBuffs");
            }

            // --- PlayerData ----------------------------------------------------
            var data = AssetDatabase.LoadAssetAtPath<PlayerData>("Assets/ScriptableObjects/PlayerData.asset");
            if (data == null)
            {
                Fail("PlayerData.asset not found");
            }
            else
            {
                if ((data.waterLayers.value & (1 << waterLayer)) != 0)
                    Pass("PlayerData.waterLayers includes the Water layer");
                else
                    Fail($"PlayerData.waterLayers ({data.waterLayers.value}) excludes Water — swimming will never trigger");

                if (data.wallClimbSpeed > 0f && data.wallClimbStamina > 0f) Pass("wall climb is tuned");
                else Fail("wallClimbSpeed / wallClimbStamina are zero — climbing does nothing");

                if (data.speedBoostMultiplier > 1f && data.jumpBoostMultiplier > 1f)
                    Pass("power-up multipliers are above 1");
                else
                    Fail("power-up multipliers are <= 1 — the boosts would do nothing");
            }

            // --- pickup prefabs ------------------------------------------------
            foreach (string name in new[]
                     {
                         "Pickup_Coin", "Pickup_Fly", "Pickup_ExtraLife", "Pickup_Health",
                         "Pickup_SpeedBoost", "Pickup_JumpBoost", "Pickup_Invincibility",
                         "Pickup_NatureCrystal",
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Pickups/{name}.prefab");
                if (prefab == null) { Fail($"missing pickup prefab: {name}"); continue; }

                var collectible = prefab.GetComponent<Collectible>();
                var col = prefab.GetComponent<Collider>();

                if (collectible != null && col != null && col.isTrigger) Pass($"{name} wired");
                else Fail($"{name} incomplete — collectible:{collectible != null} trigger:{col != null && col.isTrigger}");
            }

            // --- save round trip ------------------------------------------------
            // Serialisation only, deliberately not touching the real save file:
            // JsonUtility silently drops types it cannot handle, and List<LevelRecord>
            // is the part worth proving.
            var probe = new SaveData { highestUnlockedLevel = 7, totalCoins = 123 };
            LevelRecord record = probe.GetOrCreate(3);
            record.completed = true;
            record.crystalFound = true;
            record.secretsFound = 2;
            record.bestTimeSeconds = 41.5f;

            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(probe));
            LevelRecord back = restored?.Find(3);

            if (restored != null && restored.highestUnlockedLevel == 7 && restored.totalCoins == 123 &&
                back != null && back.completed && back.crystalFound && back.secretsFound == 2)
            {
                Pass("SaveData round-trips through JsonUtility (per-level records included)");
            }
            else
            {
                Fail("SaveData did not round-trip — saved progress would be lost");
            }

            _sb.AppendLine();
        }

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
