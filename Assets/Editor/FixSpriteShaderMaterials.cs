using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Moves materials off "Universal Render Pipeline/2D/Mesh2D-Lit-Default" onto
    /// the standard URP Lit shader.
    ///
    /// That 2D shader is meant for sprites lit by 2D lights. Nine materials in this
    /// project sit on it - including the player frog, the bunny enemy and the coins -
    /// which is why those objects do not respond correctly to the scene's
    /// directional light or cast shadows in the 3D forest.
    ///
    /// Run: Tools > Tree Frog > Fix 2D Shader Materials
    /// The operation is undoable and safe to run more than once.
    /// </summary>
    public static class FixSpriteShaderMaterials
    {
        private const string BrokenShaderName = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";
        private const string TargetShaderName = "Universal Render Pipeline/Lit";

        // Properties that only exist on the 2D shader. The shader swap orphans
        // them; clearing the texture slots stops them holding asset references.
        private static readonly string[] TwoDOnlyTextureProperties = { "_MaskTex", "_NormalMap" };

        [MenuItem("Tools/Tree Frog/Fix 2D Shader Materials")]
        public static void Fix()
        {
            Shader target = Shader.Find(TargetShaderName);
            if (target == null)
            {
                Debug.LogError($"[FixSpriteShaderMaterials] Could not find shader '{TargetShaderName}'. Is URP installed?");
                return;
            }

            List<Material> broken = FindBrokenMaterials();
            if (broken.Count == 0)
            {
                Debug.Log("[FixSpriteShaderMaterials] Nothing to do - no materials are using the 2D shader.");
                return;
            }

            Undo.RecordObjects(broken.ToArray(), "Fix 2D Shader Materials");

            var report = new StringBuilder();
            report.AppendLine($"[FixSpriteShaderMaterials] Converted {broken.Count} material(s) to {TargetShaderName}:");

            foreach (Material mat in broken)
            {
                // Carry the albedo across before the swap - property IDs are
                // resolved against the current shader.
                Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                if (baseMap == null && mat.HasProperty("_MainTex"))
                {
                    baseMap = mat.GetTexture("_MainTex");
                }

                Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

                foreach (string prop in TwoDOnlyTextureProperties)
                {
                    if (mat.HasProperty(prop))
                    {
                        mat.SetTexture(prop, null);
                    }
                }

                mat.shader = target;

                if (baseMap != null)
                {
                    mat.SetTexture("_BaseMap", baseMap);
                    mat.SetTexture("_MainTex", baseMap);
                }
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_Color", baseColor);

                // Opaque, lit, non-metallic - correct for these stylised
                // character and pickup meshes.
                mat.SetFloat("_WorkflowMode", 1f);   // metallic
                mat.SetFloat("_Surface", 0f);        // opaque
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_SrcBlend", 1f);
                mat.SetFloat("_DstBlend", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetFloat("_Cull", 2f);           // back-face culling
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_ReceiveShadows", 1f);

                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = -1;

                mat.enableInstancing = true;

                EditorUtility.SetDirty(mat);
                report.AppendLine("  " + AssetDatabase.GetAssetPath(mat));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/Tree Frog/Fix 2D Shader Materials", validate = true)]
        private static bool FixValidate()
        {
            return !Application.isPlaying;
        }

        private static List<Material> FindBrokenMaterials()
        {
            var result = new List<Material>();

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only touch materials that live in this project, not ones
                // vendored inside package folders.
                if (!path.StartsWith("Assets/"))
                {
                    continue;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader != null && mat.shader.name == BrokenShaderName)
                {
                    result.Add(mat);
                }
            }

            return result;
        }
    }
}
