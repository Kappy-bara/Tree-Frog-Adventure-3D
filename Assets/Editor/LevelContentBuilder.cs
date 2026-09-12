using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TreeFrogAdventure.Hazards;
using TreeFrogAdventure.Levels;
using TreeFrogAdventure.Menu;
using TreeFrogAdventure.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Populates the 20 gameplay scenes with the pieces every level needs:
    /// a LevelContext (level number + spawn marker), a LevelGoal trigger, and a
    /// LevelAtmosphere preset.
    ///
    /// All five levels of a world share one 100x100 map, so the atmosphere preset
    /// is what keeps them from reading as the same place five times: each world
    /// runs dawn / midday / overcast / dusk / night. The presets are computed as
    /// modifiers on the biome's own lighting, so the swamp stays green at night
    /// and the volcano stays red at dawn.
    ///
    /// Opens the biome environment first and the level scene additively on top,
    /// which is both the intended authoring workflow and what makes the terrain
    /// available for sampling ground height under the goal.
    ///
    /// Re-runnable: it reuses objects it created before instead of duplicating.
    ///
    /// Run: Tools > Tree Frog > Build Level Content
    /// </summary>
    public static class LevelContentBuilder
    {
        private const string SystemsRoot = "LevelSystems";
        private const string SpawnName = "SpawnPoint";
        private const string GoalName = "LevelGoal";
        private const string HudName = "GameHUD";
        private const string KillVolumeName = "KillVolume";
        private const string CheckpointName = "Checkpoint_Mid";

        private const string HudPrefab = "Assets/Prefabs/UI/GameHUD.prefab";
        private const string KillVolumePrefab = "Assets/Prefabs/Hazards/Hazard_KillVolume.prefab";
        private const string CheckpointPrefab = "Assets/Prefabs/Level/Checkpoint.prefab";

        // Playable area is the CENTER terrain tile; the other four are borders.
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.6f);

        // ---------------------------------------------------------- presets --
        private struct Mood
        {
            public string Name;
            public float SunElevationMul;
            public float SunAzimuth;      // absolute, degrees
            public float SunIntensityMul;
            public Color SunTint;
            public float FogMul;
            public Color FogTint;
            public float AmbientMul;
            public Color AmbientTint;
        }

        private static readonly Mood[] Moods =
        {
            new Mood { Name = "Dawn",     SunElevationMul = 0.40f, SunAzimuth =  80f, SunIntensityMul = 0.85f,
                       SunTint = new Color(1.15f, 0.92f, 0.75f), FogMul = 1.5f,
                       FogTint = new Color(1.10f, 0.95f, 0.85f), AmbientMul = 0.85f,
                       AmbientTint = new Color(1.05f, 0.95f, 0.85f) },

            new Mood { Name = "Midday",   SunElevationMul = 1.00f, SunAzimuth = -1f,  SunIntensityMul = 1.00f,
                       SunTint = Color.white, FogMul = 1.0f,
                       FogTint = Color.white, AmbientMul = 1.00f, AmbientTint = Color.white },

            new Mood { Name = "Overcast", SunElevationMul = 0.95f, SunAzimuth = -1f,  SunIntensityMul = 0.45f,
                       SunTint = new Color(0.85f, 0.88f, 0.92f), FogMul = 2.2f,
                       FogTint = new Color(0.85f, 0.88f, 0.90f), AmbientMul = 0.80f,
                       AmbientTint = new Color(0.90f, 0.93f, 0.97f) },

            new Mood { Name = "Dusk",     SunElevationMul = 0.30f, SunAzimuth = 280f, SunIntensityMul = 0.75f,
                       SunTint = new Color(1.25f, 0.70f, 0.45f), FogMul = 1.7f,
                       FogTint = new Color(1.15f, 0.80f, 0.65f), AmbientMul = 0.65f,
                       AmbientTint = new Color(1.10f, 0.85f, 0.70f) },

            new Mood { Name = "Night",    SunElevationMul = 0.15f, SunAzimuth = 200f, SunIntensityMul = 0.22f,
                       SunTint = new Color(0.55f, 0.65f, 1.00f), FogMul = 1.9f,
                       FogTint = new Color(0.50f, 0.60f, 0.85f), AmbientMul = 0.35f,
                       AmbientTint = new Color(0.60f, 0.70f, 1.00f) },
        };

        // ---------------------------------------------------------------------
        [MenuItem("Tools/Tree Frog/Build Level Content")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[LevelContentBuilder] Result:");

            string currentEnv = null;
            Scene envScene = default;

            for (int level = 1; level <= LevelProgress.TotalLevels; level++)
            {
                string levelPath = $"Assets/Scenes/Levels/{LevelProgress.GetLevelScene(level)}.unity";
                if (!File.Exists(levelPath))
                {
                    report.AppendLine($"  SKIP (missing): {levelPath}");
                    continue;
                }

                string envName = LevelProgress.GetEnvironmentScene(level);
                string envPath = $"Assets/Scenes/Environments/{envName}.unity";

                // Reload the environment only when the biome changes - these are
                // ~40 MB scenes and reopening one per level would be needlessly slow.
                if (envName != currentEnv)
                {
                    if (!File.Exists(envPath))
                    {
                        report.AppendLine($"  SKIP (missing environment): {envPath}");
                        continue;
                    }
                    envScene = EditorSceneManager.OpenScene(envPath, OpenSceneMode.Single);
                    currentEnv = envName;
                }

                Scene levelScene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Additive);

                string detail = Populate(level, levelScene, envScene);

                EditorSceneManager.MarkSceneDirty(levelScene);
                EditorSceneManager.SaveScene(levelScene, levelPath);
                EditorSceneManager.CloseScene(levelScene, true);

                report.AppendLine($"  Level_{level:00} [{envName.Replace("Env_", "")}] {detail}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------------
        private static string Populate(int level, Scene levelScene, Scene envScene)
        {
            var (world, levelInWorld) = LevelProgress.GetWorldAndLevel(level);
            Mood mood = Moods[Mathf.Clamp(levelInWorld - 1, 0, Moods.Length - 1)];

            GameObject systems = FindOrCreateRoot(levelScene, SystemsRoot);

            // --- spawn marker -------------------------------------------------
            Transform spawn = systems.transform.Find(SpawnName);
            bool newSpawn = spawn == null;
            if (newSpawn)
            {
                var go = new GameObject(SpawnName);
                go.transform.SetParent(systems.transform, false);
                spawn = go.transform;
            }

            // The route is planned first and the markers are simply its two ends.
            // Deriving them separately is how the template ended up with every level
            // spawning on the same spot out on a border tile while the exit sat on a
            // small ring around the middle of the map.
            List<Vector3> route = LevelPopulator.PlanRoute(level, envScene);

            Vector3 spawnPoint = route[0] + Vector3.up * 1f;
            Vector3 goalPoint = route[route.Count - 1] + Vector3.up * 2f;

            spawn.position = spawnPoint;

            // Face the exit, so the player starts pointed down the route.
            Vector3 facing = goalPoint - spawnPoint;
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f) spawn.rotation = Quaternion.LookRotation(facing, Vector3.up);

            // --- level context ------------------------------------------------
            // Deliberately not using ??: UnityEngine.Object overloads ==, so a
            // missing component can be "fake null" that ?? does not catch.
            var context = systems.GetComponent<LevelContext>();
            if (context == null) context = systems.AddComponent<LevelContext>();

            var ctxSo = new SerializedObject(context);
            ctxSo.FindProperty("spawnPoint").objectReferenceValue = spawn;
            ctxSo.ApplyModifiedPropertiesWithoutUndo();

            // --- atmosphere ---------------------------------------------------
            var atmosphere = systems.GetComponent<LevelAtmosphere>();
            if (atmosphere == null) atmosphere = systems.AddComponent<LevelAtmosphere>();
            ApplyMood(atmosphere, envScene, mood);

            // --- goal ---------------------------------------------------------
            GameObject goal = FindOrCreateRoot(levelScene, GoalName);

            // Collider first: LevelGoal has [RequireComponent(typeof(Collider))],
            // and Unity cannot auto-add an abstract type to satisfy it.
            var box = goal.GetComponent<BoxCollider>();
            if (box == null) box = goal.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 4f, 4f);

            var goalComponent = goal.GetComponent<LevelGoal>();
            if (goalComponent == null) goalComponent = goal.AddComponent<LevelGoal>();

            // The scenes were authored with auto-advance on, which skips the results
            // screen entirely. Forced off here because the serialized value in the
            // twenty existing scenes will not change just because the default did.
            var goalSo = new SerializedObject(goalComponent);
            goalSo.FindProperty("loadNextAutomatically").boolValue = false;
            goalSo.ApplyModifiedPropertiesWithoutUndo();

            goal.transform.position = goalPoint;

            // --- HUD / kill volume / banner -----------------------------------
            string extras = AddLevelFurniture(levelScene, envScene, systems, spawn, goal.transform);

            // --- enemies, traps, pickups, secrets, boss ------------------------
            string content = LevelPopulator.Populate(level, levelScene, envScene,
                                                     spawn, goal.transform, route);

            float span = Vector3.Distance(spawnPoint, goalPoint);

            return $"mood={mood.Name,-8} span={span,4:F0}m {extras} {content}";
        }

        /// <summary>
        /// Everything a level needs to be playable regardless of its design: a HUD, a
        /// catch-all kill volume under the map, one mid-level checkpoint, and the
        /// intro/complete banner.
        /// </summary>
        private static string AddLevelFurniture(Scene levelScene, Scene envScene,
                                                GameObject systems, Transform spawn, Transform goal)
        {
            var added = new System.Collections.Generic.List<string>();

            // Banner lives on LevelSystems alongside LevelContext.
            if (systems.GetComponent<LevelBanner>() == null)
            {
                systems.AddComponent<LevelBanner>();
                added.Add("banner");
            }

            // The old per-level Canvas held a lone HealthText with no code behind it;
            // GameHUD replaces it. Removing it avoids two health readouts on screen.
            foreach (GameObject root in levelScene.GetRootGameObjects())
            {
                if (root.name == "Canvas" && root.GetComponentInChildren<HudController>(true) == null)
                {
                    Object.DestroyImmediate(root);
                    added.Add("old-canvas-removed");
                    break;
                }
            }

            if (InstantiateIfMissing(levelScene, HudPrefab, HudName, Vector3.zero, out _))
            {
                added.Add("hud");
            }

            // Sits well below the lowest ground so any fall is caught.
            Vector3 centre = GroundAt(MapCenter, envScene, 0f);
            if (InstantiateIfMissing(levelScene, KillVolumePrefab, KillVolumeName,
                                     new Vector3(MapCenter.x, centre.y - 30f, MapCenter.y), out _))
            {
                added.Add("killvolume");
            }

            // Checkpoints used to be a single one at the midpoint, created here.
            // LevelPopulator now spaces them along the path under LevelContent, so the
            // old root is removed to avoid a third checkpoint sitting between them.
            GameObject staleCheckpoint = levelScene.GetRootGameObjects()
                .FirstOrDefault(g => g.name == CheckpointName);

            if (staleCheckpoint != null)
            {
                Object.DestroyImmediate(staleCheckpoint);
                added.Add("old-checkpoint-removed");
            }

            return added.Count > 0 ? "+" + string.Join(",", added) : "furniture kept";
        }

        private static bool InstantiateIfMissing(Scene scene, string prefabPath, string objectName,
                                                 Vector3 position, out GameObject instance)
        {
            instance = scene.GetRootGameObjects().FirstOrDefault(g => g.name == objectName);
            if (instance != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LevelContentBuilder] Missing prefab: {prefabPath}");
                return false;
            }

            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = objectName;
            instance.transform.position = position;
            return true;
        }

        // ---------------------------------------------------------------------
        private static void ApplyMood(LevelAtmosphere atmosphere, Scene envScene, Mood mood)
        {
            // Read the biome's own lighting as the base, so each mood is a
            // modifier rather than a fixed look that ignores the biome.
            Color baseFog = RenderSettings.fogColor;
            float baseDensity = RenderSettings.fogDensity;
            FogMode baseMode = RenderSettings.fogMode;
            Color baseSky = RenderSettings.ambientSkyColor;
            Color baseEq = RenderSettings.ambientEquatorColor;
            Color baseGround = RenderSettings.ambientGroundColor;
            float baseAmbient = RenderSettings.ambientIntensity;

            Light sun = FindDirectional(envScene);
            Color baseSunColor = sun != null ? sun.color : Color.white;
            float baseSunIntensity = sun != null ? sun.intensity : 1.2f;
            Vector3 baseSunEuler = sun != null ? sun.transform.rotation.eulerAngles : new Vector3(45f, 40f, 0f);

            float azimuth = mood.SunAzimuth < 0f ? baseSunEuler.y : mood.SunAzimuth;

            var so = new SerializedObject(atmosphere);

            so.FindProperty("overrideFog").boolValue = true;
            so.FindProperty("fogColor").colorValue = Mul(baseFog, mood.FogTint);
            so.FindProperty("fogMode").enumValueIndex = (int)baseMode;
            so.FindProperty("fogDensity").floatValue = baseDensity * mood.FogMul;

            so.FindProperty("overrideAmbient").boolValue = true;
            so.FindProperty("ambientSky").colorValue = Mul(baseSky, mood.AmbientTint);
            so.FindProperty("ambientEquator").colorValue = Mul(baseEq, mood.AmbientTint);
            so.FindProperty("ambientGround").colorValue = Mul(baseGround, mood.AmbientTint);
            so.FindProperty("ambientIntensity").floatValue = baseAmbient * mood.AmbientMul;

            so.FindProperty("overrideSun").boolValue = true;
            so.FindProperty("sunColor").colorValue = Mul(baseSunColor, mood.SunTint);
            so.FindProperty("sunIntensity").floatValue = baseSunIntensity * mood.SunIntensityMul;
            so.FindProperty("sunRotation").vector3Value =
                new Vector3(baseSunEuler.x * mood.SunElevationMul, azimuth, 0f);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Mul(Color c, Color tint) =>
            new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);

        private static Light FindDirectional(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional) return light;
                }
            }
            return null;
        }

        /// <summary>World position on the terrain under the given XZ, plus an offset.
        /// Internal because LevelPopulator drops its content onto the same terrain.</summary>
        internal static Vector3 GroundAt(Vector2 xz, Scene envScene, float heightOffset)
        {
            float best = 0f;
            bool found = false;

            foreach (GameObject root in envScene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    Vector3 tp = terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;

                    if (xz.x < tp.x || xz.x > tp.x + size.x) continue;
                    if (xz.y < tp.z || xz.y > tp.z + size.z) continue;

                    float h = terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + tp.y;
                    if (!found || h > best) { best = h; found = true; }
                }
            }

            return new Vector3(xz.x, best + heightOffset, xz.y);
        }

        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
