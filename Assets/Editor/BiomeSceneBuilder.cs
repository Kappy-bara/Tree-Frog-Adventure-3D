using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Clones Level_1_Forest into one scene per remaining biome and re-skins each
    /// clone with the TreeFrogAdventure/Biome/* shaders.
    ///
    /// A biome is roughly half surface shading and half atmosphere, so this does
    /// both: material swaps AND fog, ambient gradient, sun colour/angle, skybox
    /// and a per-biome post-processing profile.
    ///
    /// Run: Tools > Tree Frog > Build Biome Scenes
    ///
    /// Safe to re-run - it overwrites the generated scenes and leaves
    /// Level_1_Forest, every UNS source material and every prefab untouched.
    /// (Material changes land as scene-local prefab overrides.)
    /// </summary>
    public static class BiomeSceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/Level_1_Forest.unity";
        private const string SceneFolder = "Assets/Scenes";
        private const string MaterialRoot = "Assets/Materials/Biomes";
        private const string ProfileFolder = "Assets/Settings/Biomes";

        // ------------------------------------------------------------------
        private class Biome
        {
            public string SceneName;
            public string Folder;   // under Assets/Materials/Biomes
            public string Prefix;   // CH_ / MS_ / VH_

            // Water is replaced by lava in the volcanic biome.
            public string WaterMaterial = "Water";

            public Color FogColor;
            public FogMode FogMode;
            public float FogDensity;

            public Color AmbientSky, AmbientEquator, AmbientGround;
            public float AmbientIntensity;

            public Color SunColor;
            public float SunIntensity;
            public float SunElevation;      // X rotation, degrees
            public float SunAzimuth;        // Y rotation, degrees
            public float SunShadowStrength;

            public float BloomThreshold, BloomIntensity;
            public float Contrast, Saturation;
            public float Temperature, Tint;
            public float VignetteIntensity;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static readonly Biome[] Biomes =
        {
            new Biome
            {
                SceneName = "Level_6_CanopyHeights", Folder = "CanopyHeights", Prefix = "CH_",
                FogColor = Hex("#C7E0F5"), FogMode = FogMode.Exponential, FogDensity = 0.0022f,
                AmbientSky = Hex("#9ED1FF"), AmbientEquator = Hex("#8CB8DB"), AmbientGround = Hex("#6B7A8C"),
                AmbientIntensity = 1.0f,
                SunColor = Hex("#FFF7E6"), SunIntensity = 1.6f,
                SunElevation = 55f, SunAzimuth = 40f, SunShadowStrength = 0.85f,
                BloomThreshold = 1.1f, BloomIntensity = 0.4f,
                Contrast = 10f, Saturation = 5f, Temperature = 5f, Tint = 0f,
                VignetteIntensity = 0.15f,
            },
            new Biome
            {
                SceneName = "Level_11_MistySwamp", Folder = "MistySwamp", Prefix = "MS_",
                FogColor = Hex("#6B7A66"), FogMode = FogMode.ExponentialSquared, FogDensity = 0.012f,
                AmbientSky = Hex("#616F61"), AmbientEquator = Hex("#525C4D"), AmbientGround = Hex("#2A2E24"),
                AmbientIntensity = 0.65f,
                SunColor = Hex("#B8C7AD"), SunIntensity = 0.6f,
                SunElevation = 35f, SunAzimuth = 160f, SunShadowStrength = 0.4f,
                BloomThreshold = 0.9f, BloomIntensity = 0.3f,
                Contrast = -5f, Saturation = -25f, Temperature = -10f, Tint = 15f,
                VignetteIntensity = 0.35f,
            },
            new Biome
            {
                SceneName = "Level_16_VolcanicHighlands", Folder = "VolcanicHighlands", Prefix = "VH_",
                WaterMaterial = "Lava",
                FogColor = Hex("#4D2320"), FogMode = FogMode.Exponential, FogDensity = 0.005f,
                AmbientSky = Hex("#4D2921"), AmbientEquator = Hex("#47241A"), AmbientGround = Hex("#1F120F"),
                AmbientIntensity = 0.7f,
                SunColor = Hex("#FF7338"), SunIntensity = 0.9f,
                SunElevation = 12f, SunAzimuth = 210f, SunShadowStrength = 0.7f,
                BloomThreshold = 0.8f, BloomIntensity = 1.0f,
                Contrast = 15f, Saturation = -10f, Temperature = 25f, Tint = 5f,
                VignetteIntensity = 0.4f,
            },
        };

        /// <summary>Source material name -> biome material suffix.</summary>
        private static readonly Dictionary<string, string> MaterialMap = new Dictionary<string, string>
        {
            { "UNS_Spruce_Tree_Branch", "TreeBranch" },
            { "UNS_Bush_Leaves",        "BushLeaves" },
            { "UNS_Grass",              "Grass" },
            { "UNS_Flower",             "Flower" },
            { "UNS_Flower_Leaf",        "FlowerLeaf" },
            { "UNS_Color_Palette",      "Palette_Rock" },
            { "GrassGreen",             "Grass" },
        };

        // ==================================================================
        [MenuItem("Tools/Tree Frog/Build Biome Scenes")]
        public static void BuildAll()
        {
            if (!File.Exists(SourceScene))
            {
                Debug.LogError($"[BiomeSceneBuilder] Source scene not found: {SourceScene}");
                return;
            }

            // The prompt would block a batchmode/CI run, and there is nothing
            // unsaved to lose there.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[BiomeSceneBuilder] Result:");

            Directory.CreateDirectory(ProfileFolder);
            AssetDatabase.Refresh();

            foreach (Biome biome in Biomes)
            {
                Build(biome, report);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
        }

        private static void Build(Biome biome, StringBuilder report)
        {
            string targetPath = $"{SceneFolder}/{biome.SceneName}.unity";

            if (File.Exists(targetPath))
            {
                AssetDatabase.DeleteAsset(targetPath);
            }

            if (!AssetDatabase.CopyAsset(SourceScene, targetPath))
            {
                report.AppendLine($"  FAILED to copy scene -> {targetPath}");
                return;
            }
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);

            int swapped = ApplyMaterials(biome, scene, report);
            int terrains = ApplyTerrains(biome, scene);
            ApplyLighting(biome);
            int lights = ApplySun(biome, scene);
            bool volume = ApplyVolume(biome, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, targetPath);

            AddToBuildSettings(targetPath);

            report.AppendLine($"  {biome.SceneName}: {swapped} renderer material slot(s), " +
                              $"{terrains} terrain(s), {lights} directional light(s), " +
                              $"volume profile {(volume ? "assigned" : "NOT FOUND")}");
        }

        // ------------------------------------------------------- materials --
        private static int ApplyMaterials(Biome biome, Scene scene, StringBuilder report)
        {
            var cache = new Dictionary<string, Material>();
            var unmapped = new HashSet<string>();
            int swapped = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = renderer.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material src = mats[i];
                        if (src == null)
                        {
                            continue;
                        }

                        string suffix = ResolveSuffix(biome, src.name);
                        if (suffix == null)
                        {
                            // Already a biome material? Then this is a re-run.
                            if (!src.name.StartsWith(biome.Prefix))
                            {
                                unmapped.Add(src.name);
                            }
                            continue;
                        }

                        Material replacement = Load(biome, suffix, cache);
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
                        EditorUtility.SetDirty(renderer);
                    }
                }
            }

            if (unmapped.Count > 0)
            {
                report.AppendLine($"    (left alone: {string.Join(", ", unmapped.OrderBy(n => n))})");
            }

            return swapped;
        }

        private static string ResolveSuffix(Biome biome, string materialName)
        {
            // Instanced materials come through as "Foo (Instance)".
            string name = materialName.Replace(" (Instance)", "").Trim();

            if (name == "UNS_Water")
            {
                return biome.WaterMaterial;
            }

            return MaterialMap.TryGetValue(name, out string suffix) ? suffix : null;
        }

        private static Material Load(Biome biome, string suffix, Dictionary<string, Material> cache)
        {
            if (cache.TryGetValue(suffix, out Material cached))
            {
                return cached;
            }

            string path = $"{MaterialRoot}/{biome.Folder}/{biome.Prefix}{suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"[BiomeSceneBuilder] Missing material: {path}");
            }

            cache[suffix] = mat;
            return mat;
        }

        // --------------------------------------------------------- terrain --
        private static int ApplyTerrains(Biome biome, Scene scene)
        {
            string path = $"{MaterialRoot}/{biome.Folder}/{biome.Prefix}Terrain.mat";
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (terrainMat == null)
            {
                Debug.LogError($"[BiomeSceneBuilder] Missing terrain material: {path}");
                return 0;
            }

            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    terrain.materialTemplate = terrainMat;
                    // The custom terrain shader uses the non-instanced path.
                    terrain.drawInstanced = false;
                    EditorUtility.SetDirty(terrain);
                    count++;
                }
            }

            return count;
        }

        // -------------------------------------------------------- lighting --
        private static void ApplyLighting(Biome biome)
        {
            string skyPath = $"{MaterialRoot}/{biome.Folder}/{biome.Prefix}Skybox.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
            }
            else
            {
                Debug.LogError($"[BiomeSceneBuilder] Missing skybox material: {skyPath}");
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = biome.FogMode;
            RenderSettings.fogColor = biome.FogColor;
            RenderSettings.fogDensity = biome.FogDensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = biome.AmbientSky;
            RenderSettings.ambientEquatorColor = biome.AmbientEquator;
            RenderSettings.ambientGroundColor = biome.AmbientGround;
            RenderSettings.ambientIntensity = biome.AmbientIntensity;
        }

        private static int ApplySun(Biome biome, Scene scene)
        {
            int count = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional)
                    {
                        continue;
                    }

                    light.color = biome.SunColor;
                    light.intensity = biome.SunIntensity;
                    light.shadowStrength = biome.SunShadowStrength;
                    light.transform.rotation = Quaternion.Euler(biome.SunElevation, biome.SunAzimuth, 0f);

                    EditorUtility.SetDirty(light);
                    count++;
                }
            }

            if (count > 0)
            {
                // Keeps the skybox sun disc and ambient in sync with the light.
                RenderSettings.sun = FindFirstDirectional(scene);
            }

            return count;
        }

        private static Light FindFirstDirectional(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional)
                    {
                        return light;
                    }
                }
            }
            return null;
        }

        // ---------------------------------------------------------- volume --
        private static bool ApplyVolume(Biome biome, Scene scene)
        {
            string profilePath = $"{ProfileFolder}/{biome.Prefix}VolumeProfile.asset";

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            if (File.Exists(profilePath))
            {
                AssetDatabase.DeleteAsset(profilePath);
            }
            AssetDatabase.CreateAsset(profile, profilePath);

            // Volcanic lava emission is HDR; without bloom it reads as flat orange.
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = biome.BloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = biome.BloomIntensity;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            var colorAdjust = profile.Add<ColorAdjustments>(true);
            colorAdjust.contrast.overrideState = true;
            colorAdjust.contrast.value = biome.Contrast;
            colorAdjust.saturation.overrideState = true;
            colorAdjust.saturation.value = biome.Saturation;

            var whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.overrideState = true;
            whiteBalance.temperature.value = biome.Temperature;
            whiteBalance.tint.overrideState = true;
            whiteBalance.tint.value = biome.Tint;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = biome.VignetteIntensity;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            bool assigned = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
                {
                    if (!volume.isGlobal)
                    {
                        continue;
                    }

                    volume.sharedProfile = profile;
                    EditorUtility.SetDirty(volume);
                    assigned = true;
                }
            }

            return assigned;
        }

        // --------------------------------------------------- build settings --
        private static void AddToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(s => s.path == scenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
