using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Performance pass over the generated biome scenes.
    ///
    /// Three fixes, all measured against the scene contents (6,808 prefab
    /// instances, 4,664 of them grass):
    ///
    ///  1. Static flags. Nothing in the scene was marked static, which meant the
    ///     baked occlusion culling data was inert - the camera was rendering
    ///     geometry hidden behind cliffs. Marking environment props
    ///     Occluder/Occludee Static makes that bake actually cull.
    ///
    ///  2. Grass shadow casting off. 4,664 grass instances were rendered into
    ///     every shadow cascade for shadows that are invisible at that density.
    ///
    ///  3. Shadow cascades 4 -> 2 on the URP asset.
    ///
    /// IMPORTANT - why foliage does not get BatchingStatic:
    /// Static batching bakes meshes into world space and leaves
    /// unity_ObjectToWorld as identity, so object-space positions stop being
    /// object-space. The foliage shader reads positionOS.y for both the wind
    /// mask and the root-to-tip colour gradient, so batched grass would sway as
    /// a rigid slab and lose its gradient. Rocks and props are world-space only,
    /// so they batch safely.
    ///
    /// Run: Tools > Tree Frog > Optimize Biome Scenes
    /// Then re-bake occlusion culling once: Window > Rendering > Occlusion Culling > Bake.
    /// </summary>
    public static class BiomeSceneOptimizer
    {
        private const string FoliageShader = "TreeFrogAdventure/Biome/Foliage";
        private const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        private const int TargetCascades = 2;

        // Both the pre-split biome scenes and the post-split environment scenes.
        // Missing entries are skipped, so this works whichever order you run
        // this and Build Level Structure in.
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Environments/Env_EmeraldForest.unity",
            "Assets/Scenes/Environments/Env_CanopyHeights.unity",
            "Assets/Scenes/Environments/Env_MistySwamp.unity",
            "Assets/Scenes/Environments/Env_VolcanicHighlands.unity",
            "Assets/Scenes/Level_6_CanopyHeights.unity",
            "Assets/Scenes/Level_11_MistySwamp.unity",
            "Assets/Scenes/Level_16_VolcanicHighlands.unity",
        };

        [MenuItem("Tools/Tree Frog/Optimize Biome Scenes")]
        public static void Optimize()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = new StringBuilder();
            report.AppendLine("[BiomeSceneOptimizer] Result:");

            foreach (string path in Scenes)
            {
                if (!File.Exists(path))
                {
                    report.AppendLine($"  SKIP (missing): {path}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                int occluder = 0, batching = 0, shadowsOff = 0, skipped = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        GameObject go = renderer.gameObject;

                        if (IsDynamic(go))
                        {
                            skipped++;
                            continue;
                        }

                        bool isFoliage = UsesFoliageShader(renderer);

                        // Occlusion flags are safe for everything - they only
                        // control visibility culling, never mesh merging.
                        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                        flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                        occluder++;

                        // Mesh merging only where the shader has no object-space
                        // dependency. See the class comment.
                        if (!isFoliage)
                        {
                            flags |= StaticEditorFlags.BatchingStatic;
                            batching++;
                        }

                        GameObjectUtility.SetStaticEditorFlags(go, flags);

                        if (IsGrass(renderer) && renderer.shadowCastingMode != ShadowCastingMode.Off)
                        {
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            shadowsOff++;
                        }

                        EditorUtility.SetDirty(renderer);
                    }

                    // Terrain is world-space in this shader and never moves.
                    foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                    {
                        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(terrain.gameObject);
                        flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                        GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, flags);
                        EditorUtility.SetDirty(terrain);
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);

                report.AppendLine($"  {Path.GetFileNameWithoutExtension(path)}: " +
                                  $"{occluder} occluder/occludee, {batching} batching, " +
                                  $"{shadowsOff} grass shadow casters disabled, {skipped} left dynamic");
            }

            report.AppendLine(SetShadowCascades());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("  NEXT: re-bake occlusion culling once " +
                              "(Window > Rendering > Occlusion Culling > Bake).");

            Debug.Log(report.ToString());
        }

        /// <summary>Anything that can move must not be marked static.</summary>
        private static bool IsDynamic(GameObject go)
        {
            if (go.GetComponentInParent<Rigidbody>() != null) return true;
            if (go.GetComponentInParent<CharacterController>() != null) return true;
            if (go.GetComponentInParent<Animator>() != null) return true;
            if (go.GetComponentInParent<Animation>() != null) return true;
            if (go.GetComponent<ParticleSystemRenderer>() != null) return true;
            if (go.GetComponent<SkinnedMeshRenderer>() != null) return true;
            return false;
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

        private static bool IsGrass(Renderer renderer)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.name.EndsWith("_Grass"))
                {
                    return true;
                }
            }
            return false;
        }

        private static string SetShadowCascades()
        {
            var rp = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RpAssetPath);
            if (rp == null)
            {
                return $"  RP asset not found: {RpAssetPath}";
            }

            var so = new SerializedObject(rp);
            SerializedProperty prop = so.FindProperty("m_ShadowCascadeCount");
            if (prop == null)
            {
                return "  m_ShadowCascadeCount not found on the RP asset - left unchanged.";
            }

            int before = prop.intValue;
            prop.intValue = TargetCascades;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);

            return $"  PC_RPAsset shadow cascades: {before} -> {TargetCascades}";
        }
    }
}
