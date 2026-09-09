using System.Text;
using TreeFrogAdventure.Hazards;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Converts the Ultimate Nature water prefabs into swimmable trigger volumes.
    ///
    /// Before this, UNS_Water_Flat carried a solid non-trigger MeshCollider, so the
    /// player walked on top of every pond in the game. UNS_Water_Detailed was already a
    /// convex trigger — the two prefabs disagreed.
    ///
    /// Both are converted to a BoxCollider trigger rather than a convex MeshCollider,
    /// for two reasons:
    ///   1. A flat water plane's mesh has almost no thickness, so a mesh trigger only
    ///      fires in a paper-thin shell. Swimming needs a volume with depth.
    ///   2. Convex MeshColliders are capped at 255 hull polygons; the box sidesteps
    ///      the cook entirely and is far cheaper for 192 instances per scene.
    ///
    /// Editing the shared prefab fixes all instances in every environment scene at once.
    /// Note this modifies a vendor asset — a pack update would overwrite it.
    ///
    /// Run: Tools > Tree Frog > Setup Water
    /// </summary>
    public static class WaterSetupBuilder
    {
        private const string WaterRoot = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Water/Prefabs";

        private static readonly string[] WaterPrefabs =
        {
            WaterRoot + "/UNS_Water_Flat.prefab",
            WaterRoot + "/UNS_Water_Detailed.prefab",
        };

        [Tooltip("How far below the surface the trigger extends.")]
        private const float VolumeDepth = 12f;

        [MenuItem("Tools/Tree Frog/Setup Water")]
        public static void Setup()
        {
            int waterLayer = PlaceholderFactory.Layer("Water");

            var report = new StringBuilder();
            report.AppendLine("[WaterSetupBuilder] Result:");

            foreach (string path in WaterPrefabs)
            {
                report.AppendLine("  " + Convert(path, waterLayer));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        private static string Convert(string path, int waterLayer)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return $"FAILED to load {path}";

            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            try
            {
                // Measure the visible water from its meshes, before touching colliders.
                Bounds bounds = MeasureLocalBounds(root);

                int removed = 0;
                foreach (var mesh in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    Object.DestroyImmediate(mesh, true);
                    removed++;
                }

                // Box spans the surface in XZ and hangs downward, so entering from above
                // registers immediately and submersion depth is measurable.
                var box = root.GetComponent<BoxCollider>();
                if (box == null) box = root.AddComponent<BoxCollider>();

                box.isTrigger = true;
                box.size = new Vector3(
                    Mathf.Max(0.1f, bounds.size.x),
                    VolumeDepth,
                    Mathf.Max(0.1f, bounds.size.z));
                box.center = new Vector3(
                    bounds.center.x,
                    bounds.max.y - VolumeDepth * 0.5f,
                    bounds.center.z);

                var volume = root.GetComponent<WaterVolume>();
                if (volume == null) root.AddComponent<WaterVolume>();

                PlaceholderFactory.SetLayerRecursively(root, waterLayer);

                PrefabUtility.SaveAsPrefabAsset(root, path);

                return $"{name,-22} removed {removed} MeshCollider(s), " +
                       $"box {box.size.x:F1} x {VolumeDepth} x {box.size.z:F1}, layer Water";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Combined local-space bounds of every mesh under the prefab.</summary>
        private static Bounds MeasureLocalBounds(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool started = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null) continue;

                Bounds meshBounds = filter.sharedMesh.bounds;

                // Into the prefab root's space, so nested LOD children are handled.
                Vector3 centre = root.transform.InverseTransformPoint(
                    filter.transform.TransformPoint(meshBounds.center));
                Vector3 size = Vector3.Scale(meshBounds.size, filter.transform.lossyScale);

                var local = new Bounds(centre, size);

                if (!started) { bounds = local; started = true; }
                else bounds.Encapsulate(local);
            }

            if (!started)
            {
                Debug.LogWarning($"[WaterSetupBuilder] No meshes under {root.name}; using a 10x10 default.");
                bounds = new Bounds(Vector3.zero, new Vector3(10f, 0.1f, 10f));
            }

            return bounds;
        }
    }
}
