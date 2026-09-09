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
    /// Final cleanup pass:
    ///
    ///  1. Re-skins Env_EmeraldForest with the EF_* material set, so all four
    ///     biomes run on the same shader family. World 1 was the only one still
    ///     on the stock UNS materials - no wind, no translucency, no shore foam -
    ///     and the only thing still pulling in the 250 MB HDRI skybox.
    ///
    ///  2. Repairs its static flags. BiomeSceneOptimizer ran on that scene BEFORE
    ///     it had any foliage-shader materials, so it could not identify foliage
    ///     and marked all 26,828 renderers BatchingStatic. Static batching bakes
    ///     meshes into world space, which would break the foliage shader's
    ///     object-space wind mask and root-to-tip gradient. Foliage gets
    ///     BatchingStatic cleared here.
    ///
    ///  3. Drops the unused Level_1_Forest entry from Build Settings.
    ///
    ///  4. Deletes the three pre-split biome scenes, whose content now lives in
    ///     the Env_* scenes.
    ///
    /// Run: Tools > Tree Frog > Finalize Setup
    /// </summary>
    public static class FinalizeSetup
    {
        private const string EnvScene = "Assets/Scenes/Environments/Env_EmeraldForest.unity";
        private const string MaterialFolder = "Assets/Materials/Biomes/EmeraldForest";
        private const string FoliageShader = "TreeFrogAdventure/Biome/Foliage";
        private const string StaleBuildEntry = "Assets/Scenes/Level_1_Forest.unity";

        private static readonly Dictionary<string, string> MaterialMap = new Dictionary<string, string>
        {
            { "UNS_Spruce_Tree_Branch", "EF_TreeBranch" },
            { "UNS_Bush_Leaves",        "EF_BushLeaves" },
            { "UNS_Grass",              "EF_Grass" },
            { "UNS_Flower",             "EF_Flower" },
            { "UNS_Flower_Leaf",        "EF_FlowerLeaf" },
            { "UNS_Color_Palette",      "EF_Palette_Rock" },
            { "UNS_Water",              "EF_Water" },
            { "GrassGreen",             "EF_Grass" },
        };

        private static readonly string[] PreSplitScenes =
        {
            "Assets/Scenes/Level_6_CanopyHeights.unity",
            "Assets/Scenes/Level_11_MistySwamp.unity",
            "Assets/Scenes/Level_16_VolcanicHighlands.unity",
        };

        [MenuItem("Tools/Tree Frog/Finalize Setup")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[FinalizeSetup] Result:");

            report.AppendLine(ReskinEmeraldForest());
            report.AppendLine(RemoveStaleBuildEntry());
            report.AppendLine(DeletePreSplitScenes());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
        }

        private static string ReskinEmeraldForest()
        {
            if (!File.Exists(EnvScene))
            {
                return $"  SKIP: {EnvScene} not found";
            }

            var cache = new Dictionary<string, Material>();
            Scene scene = EditorSceneManager.OpenScene(EnvScene, OpenSceneMode.Single);

            int swapped = 0, terrains = 0, unbatched = 0, batched = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = renderer.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material src = mats[i];
                        if (src == null) continue;

                        string name = src.name.Replace(" (Instance)", "").Trim();
                        if (!MaterialMap.TryGetValue(name, out string replacementName)) continue;

                        Material replacement = Load(replacementName, cache);
                        if (replacement != null && mats[i] != replacement)
                        {
                            mats[i] = replacement;
                            changed = true;
                            swapped++;
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = mats;
                    }

                    // Re-derive batching eligibility now that the materials are
                    // known. Occlusion flags are left alone - they are safe for
                    // everything and were already applied correctly.
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);

                    if (UsesFoliageShader(renderer))
                    {
                        if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                        {
                            flags &= ~StaticEditorFlags.BatchingStatic;
                            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
                            unbatched++;
                        }
                    }
                    else if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    {
                        batched++;
                    }

                    EditorUtility.SetDirty(renderer);
                }

                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    Material terrainMat = Load("EF_Terrain", cache);
                    if (terrainMat != null)
                    {
                        terrain.materialTemplate = terrainMat;
                        terrain.drawInstanced = false;
                        EditorUtility.SetDirty(terrain);
                        terrains++;
                    }
                }
            }

            Material sky = Load("EF_Skybox", cache);
            string skyName = "<unchanged>";
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                skyName = sky.name;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, EnvScene);

            return $"  Env_EmeraldForest: {swapped} material slot(s) re-skinned, {terrains} terrain(s), " +
                   $"skybox '{skyName}', BatchingStatic cleared on {unbatched} foliage renderer(s) " +
                   $"({batched} non-foliage kept batched)";
        }

        private static bool UsesFoliageShader(Renderer renderer)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.shader != null && mat.shader.name == FoliageShader)
                {
                    return true;
                }
            }
            return false;
        }

        private static Material Load(string name, Dictionary<string, Material> cache)
        {
            if (cache.TryGetValue(name, out Material cached)) return cached;

            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogError($"[FinalizeSetup] Missing material: {path}");

            cache[name] = mat;
            return mat;
        }

        private static string RemoveStaleBuildEntry()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int before = scenes.Count;

            scenes.RemoveAll(s => s.path == StaleBuildEntry);

            if (scenes.Count == before)
            {
                return "  Build Settings: Level_1_Forest was not registered, nothing to remove";
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            return $"  Build Settings: removed Level_1_Forest ({before} -> {scenes.Count} entries)";
        }

        private static string DeletePreSplitScenes()
        {
            var deleted = new List<string>();

            foreach (string path in PreSplitScenes)
            {
                if (!File.Exists(path)) continue;

                long mb = new FileInfo(path).Length / 1024 / 1024;
                if (AssetDatabase.DeleteAsset(path))
                {
                    deleted.Add($"{Path.GetFileName(path)} ({mb} MB)");
                }
                else
                {
                    Debug.LogError($"[FinalizeSetup] Could not delete {path}");
                }
            }

            return deleted.Count == 0
                ? "  Pre-split scenes: none present"
                : $"  Deleted {deleted.Count} pre-split scene(s): {string.Join(", ", deleted)}";
        }
    }
}
