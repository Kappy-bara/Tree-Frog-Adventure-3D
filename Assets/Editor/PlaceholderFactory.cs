using System.IO;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Shared helpers for building placeholder prefabs out of primitives.
    ///
    /// The project has no rigged models beyond the frog and bunny and no .anim assets
    /// at all, so placeholders are assembled from cubes/capsules/spheres with distinct
    /// silhouettes and colours. Readability beats fidelity here: you need to tell a
    /// Rhino from a Pig at a glance while testing, which six recoloured bunnies would
    /// not give you.
    /// </summary>
    public static class PlaceholderFactory
    {
        public const string MaterialFolder = "Assets/Materials/Placeholders";

        /// <summary>Gets or creates a flat URP/Lit material of the given colour.</summary>
        public static Material GetMaterial(string name, Color color, float smoothness = 0.1f)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[PlaceholderFactory] URP/Lit shader not found.");
                return null;
            }

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Adds a primitive as a child. Its auto-generated collider is removed —
        /// visual parts must never collide, or the enemy snags on its own ears.
        /// </summary>
        public static GameObject AddPart(Transform parent, PrimitiveType type, string name,
                                         Vector3 localPosition, Vector3 localScale,
                                         Material material, Vector3? localEuler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (localEuler.HasValue) go.transform.localEulerAngles = localEuler.Value;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            return go;
        }

        public static GameObject NewRoot(string name, int layer)
        {
            var go = new GameObject(name);
            SetLayerRecursively(go, layer);
            return go;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>Saves to disk and destroys the scene instance.</summary>
        public static GameObject SavePrefab(GameObject instance, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
            Object.DestroyImmediate(instance);

            if (!success) Debug.LogError($"[PlaceholderFactory] Failed to save prefab: {path}");
            return asset;
        }

        /// <summary>Layer index by name, with a clear error instead of a silent 0.</summary>
        public static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                Debug.LogError($"[PlaceholderFactory] Layer '{name}' does not exist. " +
                               "Add it in ProjectSettings > Tags and Layers.");
                return 0;
            }
            return layer;
        }
    }
}
