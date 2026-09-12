using System.Collections.Generic;
using System.Linq;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Levels;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Fills a level with enemies, traps, pickups, secrets and — every fifth level — a
    /// boss arena.
    ///
    /// Before this, all twenty gameplay scenes were byte-identical copies of the same
    /// greybox template holding one un-killable bunny, and not one of the six enemy
    /// prefabs or eight pickup prefabs appeared in any scene.
    ///
    /// Two properties matter more than the specific numbers:
    ///
    /// DETERMINISTIC — the seed is derived from the level number, so re-running the
    /// builder reproduces the same layout rather than reshuffling a level you have
    /// already hand-tuned around.
    ///
    /// CONTAINED — everything generated lives under a single "LevelContent" root that
    /// is destroyed and rebuilt each run. Anything you place by hand outside it is
    /// never touched, so hand-tuning and regeneration can coexist.
    /// </summary>
    public static class LevelPopulator
    {
        public const string ContentRoot = "LevelContent";

        private const string EnemyFolder = "Assets/Prefabs/Enemies";
        private const string HazardFolder = "Assets/Prefabs/Hazards";
        private const string PickupFolder = "Assets/Prefabs/Pickups";
        private const string BossFolder = "Assets/Prefabs/Bosses";
        private const string CheckpointPrefab = "Assets/Prefabs/Level/Checkpoint.prefab";
        private const string CrystalPrefab = PickupFolder + "/Pickup_NatureCrystal.prefab";
        private const string LegacyBunnyPrefab = EnemyFolder + "/Bunny.prefab";

        // The playable area is the CENTER terrain tile; the surrounding four are visual
        // borders with no gameplay on them.
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.6f);
        private const float PlayableHalfExtent = 48f;

        /// <summary>
        /// Placement attempts per object before giving up. A single attempt was not
        /// enough: the swamp is largely water and the volcanic map is largely cliff, so
        /// a first pick is rejected often and levels came out with a fraction of their
        /// intended content — one came out with no coins at all.
        /// </summary>
        private const int PlacementAttempts = 24;

        private const int WaterLayer = 4;

        /// <summary>
        /// Intermediate points the route bends through. Three turns the crossing into a
        /// journey without folding the path back over itself.
        /// </summary>
        private const int RouteWaypoints = 3;

        // ------------------------------------------------------------- recipe --

        /// <summary>What a level is made of, derived purely from its number.</summary>
        private struct Recipe
        {
            public int Enemies;
            public int Traps;
            public int Coins;
            public int PowerUps;
            public int Secrets;
            public int Checkpoints;
            public bool Boss;
        }

        private static Recipe RecipeFor(int level)
        {
            var (world, levelInWorld) = LevelProgress.GetWorldAndLevel(level);
            bool boss = levelInWorld == LevelProgress.LevelsPerWorld;

            return new Recipe
            {
                // Ramps within a world and again between worlds, so level 6 is a step
                // up from level 5 rather than a reset to the world-1 difficulty.
                Enemies = boss ? 3 + world : 6 + world * 3 + levelInWorld,
                Traps = boss ? world : 2 + world + levelInWorld / 2,

                // The route now crosses the whole map, so a trail sized for the old
                // short corridor would leave long stretches with nothing in them.
                Coins = 18 + world * 6,
                PowerUps = 1 + (level >= 6 ? 1 : 0) + (level >= 14 ? 1 : 0),

                // A boss level's crystal is the fight's reward, so it needs no secret
                // to hide one in.
                Secrets = boss ? 1 : 2 + (levelInWorld >= 3 ? 1 : 0),
                Checkpoints = boss ? 2 : 3,
                Boss = boss,
            };
        }

        /// <summary>
        /// Which enemies exist by this level. The roster opens gradually so the player
        /// meets one new threat at a time rather than all six in world 1.
        /// </summary>
        private static readonly (string Prefab, int FromLevel, int Weight)[] EnemyRoster =
        {
            ("Enemy_Bunny_Rigged", 1,  3),
            ("Enemy_Chicken",      1,  3),
            ("Enemy_Bee",          3,  2),
            ("Enemy_Pig",          6,  2),
            ("Enemy_Bat",          8,  2),
            ("Enemy_Rhino",       11,  2),
        };

        private static readonly (string Prefab, int FromLevel, int Weight)[] TrapRoster =
        {
            ("Hazard_Spikes",          1, 3),
            ("Hazard_MovingPlatform",  2, 2),
            ("Hazard_FallingPlatform", 4, 2),
            ("Hazard_Crusher",         9, 2),
            ("Hazard_LavaPool",       16, 3),
        };

        private static readonly (string Prefab, int FromLevel, int Weight)[] PowerUpRoster =
        {
            ("Pickup_Health",        1, 3),
            ("Pickup_Fly",           1, 3),
            ("Pickup_JumpBoost",     2, 2),
            ("Pickup_SpeedBoost",    4, 2),
            ("Pickup_Invincibility", 7, 1),
            ("Pickup_ExtraLife",     9, 1),
        };

        private static readonly string[] BossPrefabByWorld =
        {
            "Boss_ForestGuardian",
            "Boss_CanopyGuardian",
            "Boss_SwampGuardian",
            "Boss_VolcanoGuardian",
        };

        // ------------------------------------------------------------ populate --

        public static string Populate(int level, Scene levelScene, Scene envScene,
                                      Transform spawn, Transform goal)
        {
            // Seeded from the level number: re-running must reproduce the same layout,
            // not reshuffle a level that has already been tuned around.
            Random.InitState(level * 7919);

            Recipe recipe = RecipeFor(level);

            GameObject root = ResetContentRoot(levelScene);

            Vector3 from = spawn != null ? spawn.position : Vector3.zero;
            Vector3 to = goal != null ? goal.position : from + Vector3.forward * 30f;

            // A winding polyline rather than the straight line between the endpoints.
            // Content used to be scattered along that straight line with a few metres
            // of lateral spread, which turned a 96 m map into a narrow corridor.
            List<Vector3> route = BuildRoute(from, to, envScene);

            var counts = new List<string>();
            var shortfalls = new List<string>();

            int cp = PlaceCheckpoints(root, recipe, envScene, route);
            int en = PlaceEnemies(root, recipe, level, envScene, route);
            int tr = PlaceTraps(root, recipe, level, envScene, route);
            int co = PlaceCoins(root, recipe, envScene, route);
            int pu = PlacePowerUps(root, recipe, level, envScene, route);
            int se = PlaceSecrets(root, recipe, envScene, route, includeCrystal: !recipe.Boss);

            counts.Add($"{cp}cp");
            counts.Add($"{en}e");
            counts.Add($"{tr}t");
            counts.Add($"{co}c");
            counts.Add($"{pu}p");
            counts.Add($"{se}s");

            // Report anything the terrain refused to take. Silence here is how a level
            // ends up with no coins without anybody noticing.
            Shortfall(shortfalls, "checkpoints", cp, recipe.Checkpoints);
            Shortfall(shortfalls, "enemies", en, recipe.Enemies);
            Shortfall(shortfalls, "traps", tr, recipe.Traps);
            Shortfall(shortfalls, "powerups", pu, recipe.PowerUps);
            Shortfall(shortfalls, "secrets", se, recipe.Secrets);
            if (co == 0) shortfalls.Add("coins 0");

            string bossNote = recipe.Boss
                ? PlaceBoss(root, level, envScene, goal)
                : string.Empty;

            // Every level must hold exactly one Nature Crystal — it is the story
            // collectible and the save records it per level. On boss levels the arena
            // spawns it on defeat; everywhere else it lives in the first secret, and if
            // no secret could be placed it still has to exist somewhere reachable.
            string crystalNote = recipe.Boss
                ? string.Empty
                : EnsureCrystal(root, levelScene, envScene, route);

            int vines = EnableDormantVines(levelScene);
            int legacy = RemoveLegacyEnemies(levelScene);

            return $"content[{string.Join(" ", counts)}]"
                   + (vines > 0 ? $" +{vines}vine" : string.Empty)
                   + (legacy > 0 ? $" -{legacy}legacy" : string.Empty)
                   + (string.IsNullOrEmpty(bossNote) ? string.Empty : " " + bossNote)
                   + (string.IsNullOrEmpty(crystalNote) ? string.Empty : " " + crystalNote)
                   + (shortfalls.Count > 0 ? $"  SHORT[{string.Join(", ", shortfalls)}]" : string.Empty);
        }

        private static void Shortfall(List<string> into, string label, int placed, int wanted)
        {
            if (placed < wanted) into.Add($"{label} {placed}/{wanted}");
        }

        /// <summary>
        /// Guarantees the level's Nature Crystal exists. Walks outward from the middle
        /// of the path looking for any valid ground, because a level without a crystal
        /// is one the player can never fully complete.
        /// </summary>
        private static string EnsureCrystal(GameObject root, Scene levelScene, Scene envScene,
                                            List<Vector3> route)
        {
            var crystal = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefab);
            if (crystal == null) return "CRYSTAL PREFAB MISSING";

            if (SceneHasCrystal(levelScene)) return string.Empty;

            for (int i = 0; i < 40; i++)
            {
                // Alternate above and below the midpoint, widening each step.
                float t = 0.5f + (i % 2 == 0 ? 1f : -1f) * (i / 80f);
                float lateral = (i % 4) * 4f;

                if (!TryPoint(route, Mathf.Clamp01(t), lateral, envScene, out Vector3 point)) continue;

                GameObject group = Group(root, "Crystal");
                Instantiate(crystal, group, point + Vector3.up * 1.2f, "Pickup_NatureCrystal");
                return "crystal=fallback";
            }

            return "CRYSTAL UNPLACED";
        }

        private static bool SceneHasCrystal(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Collectible c in rootObject.GetComponentsInChildren<Collectible>(true))
                {
                    var so = new SerializedObject(c);
                    if (so.FindProperty("kind").enumValueIndex == (int)CollectibleKind.NatureCrystal)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Everything generated is destroyed and rebuilt. Hand-placed objects live
        /// outside this root and survive untouched.
        /// </summary>
        private static GameObject ResetContentRoot(Scene scene)
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ContentRoot);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(ContentRoot);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        // ------------------------------------------------------------- placing --

        private static int PlaceCheckpoints(GameObject root, Recipe recipe, Scene env,
                                            List<Vector3> route)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            if (prefab == null) return 0;

            var group = Group(root, "Checkpoints");
            int placed = 0;

            for (int i = 1; i <= recipe.Checkpoints; i++)
            {
                float t = i / (float)(recipe.Checkpoints + 1);

                // Directly on the path, not scattered: a checkpoint the player can walk
                // past without touching is worse than none at all.
                if (!TryPoint(route, t, 0f, env, out Vector3 point)) continue;

                Instantiate(prefab, group, point, $"Checkpoint_{i}");
                placed++;
            }

            return placed;
        }

        private static int PlaceEnemies(GameObject root, Recipe recipe, int level, Scene env,
                                        List<Vector3> route)
        {
            var group = Group(root, "Enemies");
            int placed = 0;

            for (int i = 0; i < recipe.Enemies; i++)
            {
                string name = PickWeighted(EnemyRoster, level);
                if (name == null) continue;

                var prefab = LoadEnemy(name);
                if (prefab == null) continue;

                // Never within the first tenth of the path: spawning on top of the
                // player is a hit they cannot avoid.
                if (!TryPlace(route, env, 0.12f, 0.96f, -16f, 16f, out Vector3 point)) continue;

                GameObject instance = Instantiate(prefab, group, point + Vector3.up * 0.1f, null);
                instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                placed++;
            }

            return placed;
        }

        private static int PlaceTraps(GameObject root, Recipe recipe, int level, Scene env,
                                      List<Vector3> route)
        {
            var group = Group(root, "Traps");
            int placed = 0;

            for (int i = 0; i < recipe.Traps; i++)
            {
                string name = PickWeighted(TrapRoster, level);
                if (name == null) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{HazardFolder}/{name}.prefab");
                if (prefab == null) continue;

                if (!TryPlace(route, env, 0.15f, 0.92f, -10f, 10f, out Vector3 point)) continue;

                // Platforms hang above the ground; spikes and lava sit on it.
                bool airborne = name.Contains("Platform");
                Vector3 position = point + Vector3.up * (airborne ? 2.5f : 0.05f);

                GameObject instance = Instantiate(prefab, group, position, null);
                instance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                placed++;
            }

            return placed;
        }

        private static int PlaceCoins(GameObject root, Recipe recipe, Scene env,
                                      List<Vector3> route)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PickupFolder}/Pickup_Coin.prefab");
            if (prefab == null) return 0;

            var group = Group(root, "Coins");
            int placed = 0;

            // Coins run in short arcs rather than being scattered singly: a trail is a
            // signpost, and following one is how a player is taught where to go.
            int trails = Mathf.Max(3, recipe.Coins / 6);
            int perTrail = Mathf.Max(4, recipe.Coins / trails);

            for (int trail = 0; trail < trails; trail++)
            {
                // Spread the trails evenly along the route instead of picking each
                // start at random: random starts cluster, and on a route this long
                // that leaves whole stretches with nothing to follow.
                float t = (trail + 0.5f) / trails * 0.86f + 0.07f;
                float lateral = Random.Range(-11f, 11f);
                float step = Random.value < 0.5f ? 1.8f : -1.8f;

                for (int i = 0; i < perTrail; i++)
                {
                    if (!TryPoint(route, t, lateral + i * step, env, out Vector3 point)) continue;

                    Instantiate(prefab, group, point + Vector3.up * 1f, null);
                    placed++;
                }
            }

            return placed;
        }

        private static int PlacePowerUps(GameObject root, Recipe recipe, int level, Scene env,
                                         List<Vector3> route)
        {
            var group = Group(root, "PowerUps");
            int placed = 0;

            for (int i = 0; i < recipe.PowerUps; i++)
            {
                string name = PickWeighted(PowerUpRoster, level);
                if (name == null) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PickupFolder}/{name}.prefab");
                if (prefab == null) continue;

                if (!TryPlace(route, env, 0.2f, 0.9f, -14f, 14f, out Vector3 point)) continue;

                Instantiate(prefab, group, point + Vector3.up * 1f, null);
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// A secret is a concealing wall with a reward alcove behind it. On non-boss
        /// levels the first one holds the Nature Crystal, which is what makes finding
        /// secrets matter rather than being optional decoration.
        /// </summary>
        private static int PlaceSecrets(GameObject root, Recipe recipe, Scene env,
                                        List<Vector3> route, bool includeCrystal)
        {
            var group = Group(root, "Secrets");
            var coin = AssetDatabase.LoadAssetAtPath<GameObject>($"{PickupFolder}/Pickup_Coin.prefab");
            var crystal = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefab);

            int placed = 0;

            for (int i = 0; i < recipe.Secrets; i++)
            {
                // Well off the path — a secret found by walking the critical route is
                // not a secret. Falls back to a tighter offset rather than being
                // dropped, because a missing secret can mean a missing Nature Crystal.
                if (!TryPlace(route, env, 0.15f, 0.9f, 22f, 38f, out Vector3 point)
                    && !TryPlace(route, env, 0.15f, 0.9f, 10f, 22f, out point))
                {
                    continue;
                }

                var secret = new GameObject($"Secret_{i + 1}");
                secret.transform.SetParent(group.transform, false);
                secret.transform.position = point;

                // Collider before SecretArea: [RequireComponent(typeof(Collider))]
                // cannot be satisfied automatically because Collider is abstract.
                var trigger = secret.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(6f, 5f, 6f);
                trigger.center = new Vector3(0f, 2.5f, 0f);

                var area = secret.AddComponent<SecretArea>();

                GameObject concealer = BuildConcealer(secret.transform);
                GameObject reveal = BuildReveal(secret.transform, coin,
                                                includeCrystal && i == 0 ? crystal : null);

                var so = new SerializedObject(area);
                so.FindProperty("concealer").objectReferenceValue = concealer;
                so.FindProperty("reveal").objectReferenceValue = reveal;
                so.ApplyModifiedPropertiesWithoutUndo();

                placed++;
            }

            return placed;
        }

        private static GameObject BuildConcealer(Transform parent)
        {
            Material mat = PlaceholderFactory.GetMaterial("PH_SecretWall", new Color(0.32f, 0.30f, 0.26f));

            var concealer = new GameObject("Concealer");
            concealer.transform.SetParent(parent, false);

            GameObject slab = PlaceholderFactory.AddPart(concealer.transform, PrimitiveType.Cube, "Wall",
                new Vector3(0f, 1.6f, 2.6f), new Vector3(5f, 3.2f, 0.4f), mat);

            // The visual parts have their colliders stripped by AddPart, so the wall
            // needs one of its own or the player walks straight through the "solid" face.
            var col = slab.AddComponent<BoxCollider>();
            col.isTrigger = false;

            return concealer;
        }

        private static GameObject BuildReveal(Transform parent, GameObject coin, GameObject crystal)
        {
            var reveal = new GameObject("Reveal");
            reveal.transform.SetParent(parent, false);

            if (crystal != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(crystal, reveal.transform);
                instance.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            }

            if (coin != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(coin, reveal.transform);
                    instance.transform.localPosition = new Vector3((i - 1) * 1.3f, 1f, -1.4f);
                }
            }

            return reveal;
        }

        // ---------------------------------------------------------------- boss --

        private static string PlaceBoss(GameObject root, int level, Scene env, Transform goal)
        {
            var (world, _) = LevelProgress.GetWorldAndLevel(level);
            string prefabName = BossPrefabByWorld[Mathf.Clamp(world - 1, 0, BossPrefabByWorld.Length - 1)];

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BossFolder}/{prefabName}.prefab");
            if (prefab == null) return $"BOSS MISSING ({prefabName})";

            var group = Group(root, "Boss");

            Vector3 centre = goal != null ? goal.position : Vector3.zero;
            centre = GroundAt(new Vector2(centre.x, centre.z), env, 0f);

            GameObject boss = Instantiate(prefab, group, centre + Vector3.up * 1f, null);

            // ---- arena trigger ----------------------------------------------
            var arena = new GameObject("BossArena");
            arena.transform.SetParent(group.transform, false);
            arena.transform.position = centre;

            var trigger = arena.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(26f, 12f, 26f);
            trigger.center = new Vector3(0f, 6f, 0f);

            var component = arena.AddComponent<BossArena>();

            GameObject barriers = BuildArenaBarriers(arena.transform);

            var so = new SerializedObject(component);
            so.FindProperty("boss").objectReferenceValue = boss.GetComponent<BossBase>();
            so.FindProperty("barriers").objectReferenceValue = barriers;
            so.FindProperty("crystalPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefab);

            // The goal is switched off in BossArena.Awake and back on when the guardian
            // falls, so the exit cannot be walked past the fight.
            so.FindProperty("levelGoal").objectReferenceValue = goal != null ? goal.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            return $"boss={prefabName.Replace("Boss_", "")}";
        }

        /// <summary>
        /// A ring of invisible boxes on the Barrier layer — the same idiom the level
        /// scenes already use for their outer walls.
        /// </summary>
        private static GameObject BuildArenaBarriers(Transform parent)
        {
            int barrierLayer = PlaceholderFactory.Layer("Barrier");

            var barriers = new GameObject("Barriers");
            barriers.transform.SetParent(parent, false);
            barriers.transform.localPosition = Vector3.zero;

            const int segments = 12;
            const float radius = 15f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var wall = new GameObject($"Wall_{i}");
                wall.transform.SetParent(barriers.transform, false);

                wall.transform.localPosition =
                    new Vector3(Mathf.Cos(angle) * radius, 6f, Mathf.Sin(angle) * radius);
                wall.transform.localRotation =
                    Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));

                var col = wall.AddComponent<BoxCollider>();
                col.size = new Vector3(2f * Mathf.PI * radius / segments + 1f, 14f, 0.6f);

                wall.layer = barrierLayer;
            }

            barriers.layer = barrierLayer;
            return barriers;
        }

        // ---------------------------------------------------------------- vines --

        /// <summary>
        /// The template every level was copied from carried two vine cubes left
        /// inactive, so vine climbing existed in code and appeared in no level.
        /// </summary>
        private static int EnableDormantVines(Scene scene)
        {
            int enabled = 0;

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (VineClimbable vine in rootObject.GetComponentsInChildren<VineClimbable>(true))
                {
                    if (vine.gameObject.activeSelf) continue;

                    vine.gameObject.SetActive(true);
                    enabled++;
                }
            }

            return enabled;
        }

        // -------------------------------------------------------------- helpers --

        /// <summary>
        /// The path the level is built along: spawn, a few waypoints swung alternately
        /// off the straight line, then the exit.
        ///
        /// The swing is what makes a level feel like a journey across the map. A
        /// straight run between two points on opposite sides is long but featureless,
        /// and it only ever visits one narrow band of terrain.
        /// </summary>
        private static List<Vector3> BuildRoute(Vector3 from, Vector3 to, Scene env)
        {
            var route = new List<Vector3> { from };

            Vector2 a = new Vector2(from.x, from.z);
            Vector2 b = new Vector2(to.x, to.z);

            Vector2 along = b - a;
            if (along.sqrMagnitude < 1e-4f) along = Vector2.up;

            Vector2 dir = along.normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);

            for (int i = 1; i <= RouteWaypoints; i++)
            {
                float t = i / (float)(RouteWaypoints + 1);
                float swing = (i % 2 == 0 ? -1f : 1f) * Random.Range(16f, 30f);

                // Pull the swing in until the waypoint is somewhere the player can
                // actually stand, rather than dropping the route into a lake.
                Vector3 point = default;
                bool found = false;

                for (int attempt = 0; attempt < 6 && !found; attempt++)
                {
                    float scale = 1f - attempt * 0.18f;
                    found = TryGround(Vector2.Lerp(a, b, t) + side * (swing * scale), env, out point);
                }

                route.Add(found ? point : GroundAt(Vector2.Lerp(a, b, t), env, 0f));
            }

            route.Add(to);
            return route;
        }

        /// <summary>
        /// Repeatedly picks a point in the given band until one lands somewhere valid.
        /// Without retrying, every rejection silently cost the level one object.
        /// </summary>
        private static bool TryPlace(List<Vector3> route, Scene env,
                                     float tMin, float tMax, float latMin, float latMax,
                                     out Vector3 result)
        {
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                float t = Random.Range(tMin, tMax);
                float lateral = Random.Range(latMin, latMax);

                // Half the attempts mirror to the other side, so a level whose route
                // hugs one edge of the map still uses the space on the other.
                if (attempt % 2 == 1) lateral = -lateral;

                if (TryPoint(route, t, lateral, env, out result)) return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// A point at normalised distance <paramref name="t"/> along the route, offset
        /// sideways from the local heading and dropped onto the terrain. Returns false
        /// when the result is off the playable tile or in water.
        /// </summary>
        private static bool TryPoint(List<Vector3> route, float t, float lateral,
                                     Scene env, out Vector3 result)
        {
            result = default;

            Vector3 centre = RoutePosition(route, t);
            Vector3 tangent = RouteTangent(route, t);

            var side = new Vector2(-tangent.z, tangent.x);
            var xz = new Vector2(centre.x, centre.z) + side * lateral;

            return TryGround(xz, env, out result);
        }

        /// <summary>Bounds and water test, shared by every placement path.</summary>
        private static bool TryGround(Vector2 xz, Scene env, out Vector3 result)
        {
            result = default;

            if (Mathf.Abs(xz.x - MapCenter.x) > PlayableHalfExtent) return false;
            if (Mathf.Abs(xz.y - MapCenter.y) > PlayableHalfExtent) return false;

            Vector3 ground = GroundAt(xz, env, 0f);

            // Water colliders became triggers when swimming was added, so this needs
            // QueryTriggerInteraction.Collide to see them at all.
            if (Physics.CheckSphere(ground + Vector3.up * 1.5f, 1.5f, 1 << WaterLayer,
                                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            result = ground;
            return true;
        }

        /// <summary>
        /// Interpolates along the polyline by arc length, so t is an even measure of
        /// progress. Interpolating per segment instead would bunch content up on the
        /// short legs and spread it thin on the long ones.
        /// </summary>
        private static Vector3 RoutePosition(List<Vector3> route, float t)
        {
            if (route == null || route.Count == 0) return Vector3.zero;
            if (route.Count == 1) return route[0];

            float total = RouteLength(route);
            if (total <= 1e-4f) return route[0];

            float travelled = Mathf.Clamp01(t) * total;

            for (int i = 1; i < route.Count; i++)
            {
                float segment = Vector3.Distance(route[i - 1], route[i]);

                if (travelled <= segment || i == route.Count - 1)
                {
                    float local = segment <= 1e-4f ? 0f : travelled / segment;
                    return Vector3.Lerp(route[i - 1], route[i], Mathf.Clamp01(local));
                }

                travelled -= segment;
            }

            return route[route.Count - 1];
        }

        /// <summary>The route's flat heading at t. Used as the lateral reference.</summary>
        private static Vector3 RouteTangent(List<Vector3> route, float t)
        {
            Vector3 ahead = RoutePosition(route, Mathf.Clamp01(t + 0.02f));
            Vector3 behind = RoutePosition(route, Mathf.Clamp01(t - 0.02f));

            Vector3 tangent = ahead - behind;
            tangent.y = 0f;

            return tangent.sqrMagnitude < 1e-4f ? Vector3.forward : tangent.normalized;
        }

        private static float RouteLength(List<Vector3> route)
        {
            float total = 0f;
            for (int i = 1; i < route.Count; i++) total += Vector3.Distance(route[i - 1], route[i]);
            return total;
        }

        /// <summary>
        /// Deletes instances of the legacy Bunny.prefab.
        ///
        /// It carries no EnemyHealth, so nothing in the game can kill it — not a stomp,
        /// not a thrown fly, not a hazard — and it moves by writing transform.position
        /// on a fixed Y, so it floats over the sloped terrain. One was copied into all
        /// twenty scenes as part of the shared template, outside LevelContent, which is
        /// why regenerating content never removed it.
        ///
        /// Deleted rather than swapped in place: a swap would be non-deterministic, as
        /// the second run would find nothing left to swap and produce a different scene.
        /// The populator places the rigged replacement among the level enemies instead.
        /// </summary>
        private static int RemoveLegacyEnemies(Scene scene)
        {
            var doomed = new List<GameObject>();

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;

                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == LegacyBunnyPrefab)
                    {
                        doomed.Add(go);
                    }
                }
            }

            // Collected first: destroying while walking the hierarchy invalidates it.
            foreach (GameObject go in doomed)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            return doomed.Count;
        }

        /// <summary>Validates and grounds an already-chosen world point.</summary>
        private static bool Ground(Vector3 candidate, Scene env, out Vector3 result)
        {
            result = default;

            if (Mathf.Abs(candidate.x - MapCenter.x) > PlayableHalfExtent) return false;
            if (Mathf.Abs(candidate.z - MapCenter.y) > PlayableHalfExtent) return false;

            Vector3 ground = GroundAt(new Vector2(candidate.x, candidate.z), env, 0f);

            if (Physics.CheckSphere(ground + Vector3.up * 1.5f, 1.5f, 1 << WaterLayer,
                                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            result = ground;
            return true;
        }

        private static GameObject LoadEnemy(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/{name}.prefab");

            // The rigged bunny is produced by LegacyPrefabFixer; fall back to the
            // primitive one if that tool has not been run.
            if (prefab == null && name == "Enemy_Bunny_Rigged")
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyFolder}/Enemy_Bunny.prefab");
            }

            return prefab;
        }

        private static string PickWeighted((string Prefab, int FromLevel, int Weight)[] roster, int level)
        {
            int total = 0;
            foreach (var entry in roster)
            {
                if (entry.FromLevel <= level) total += entry.Weight;
            }

            if (total <= 0) return null;

            int roll = Random.Range(0, total);

            foreach (var entry in roster)
            {
                if (entry.FromLevel > level) continue;

                roll -= entry.Weight;
                if (roll < 0) return entry.Prefab;
            }

            return null;
        }

        private static GameObject Group(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private static GameObject Instantiate(GameObject prefab, GameObject parent,
                                              Vector3 position, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.transform.position = position;
            if (!string.IsNullOrEmpty(name)) instance.name = name;
            return instance;
        }

        private static Vector3 GroundAt(Vector2 xz, Scene env, float offset) =>
            LevelContentBuilder.GroundAt(xz, env, offset);
    }
}
