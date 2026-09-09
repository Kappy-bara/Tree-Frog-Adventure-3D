using System.Text;
using TreeFrogAdventure.Hazards;
using TreeFrogAdventure.Levels;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Builds the trap, pickup and checkpoint prefabs.
    ///
    /// Traps carry a DamageSource rather than doing damage themselves, so enemies and
    /// hazards share one contact-damage path through PlayerHealth.
    ///
    /// Run: Tools > Tree Frog > Build Hazard Prefabs
    /// </summary>
    public static class HazardPrefabBuilder
    {
        private const string HazardFolder = "Assets/Prefabs/Hazards";
        private const string PickupFolder = "Assets/Prefabs/Pickups";
        private const string LevelFolder = "Assets/Prefabs/Level";

        private const string LavaMaterial = "Assets/Materials/Biomes/VolcanicHighlands/VH_Lava.mat";

        [MenuItem("Tools/Tree Frog/Build Hazard Prefabs")]
        public static void Build()
        {
            var report = new StringBuilder();
            report.AppendLine("[HazardPrefabBuilder] Result:");

            int hazardLayer = PlaceholderFactory.Layer("Hazard");

            report.AppendLine("  " + BuildMovingPlatform());
            report.AppendLine("  " + BuildFallingPlatform());
            report.AppendLine("  " + BuildCrusher(hazardLayer));
            report.AppendLine("  " + BuildSpikes(hazardLayer));
            report.AppendLine("  " + BuildLavaPool(hazardLayer));
            report.AppendLine("  " + BuildKillVolume(hazardLayer));
            report.AppendLine("  " + BuildCheckpoint());
            report.AppendLine("  " + BuildPickup("Pickup_Coin", CollectibleKind.Coin, 1,
                                                 new Color(1f, 0.83f, 0.20f), PrimitiveType.Cylinder,
                                                 new Vector3(0.4f, 0.06f, 0.4f), new Vector3(90f, 0f, 0f)));
            report.AppendLine("  " + BuildPickup("Pickup_Fly", CollectibleKind.Fly, 3,
                                                 new Color(0.30f, 0.85f, 0.55f), PrimitiveType.Sphere,
                                                 new Vector3(0.3f, 0.26f, 0.34f), null));
            report.AppendLine("  " + BuildPickup("Pickup_ExtraLife", CollectibleKind.ExtraLife, 1,
                                                 new Color(0.95f, 0.30f, 0.45f), PrimitiveType.Sphere,
                                                 new Vector3(0.42f, 0.42f, 0.42f), null));
            report.AppendLine("  " + BuildPickup("Pickup_Health", CollectibleKind.Health, 1,
                                                 new Color(0.35f, 0.85f, 0.95f), PrimitiveType.Cube,
                                                 new Vector3(0.32f, 0.32f, 0.32f), new Vector3(45f, 45f, 0f)));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------ platforms --

        private static string BuildMovingPlatform()
        {
            var root = PlaceholderFactory.NewRoot("Hazard_MovingPlatform", 0);
            Material mat = PlaceholderFactory.GetMaterial("PH_Platform", new Color(0.55f, 0.58f, 0.64f));

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            surface.transform.SetParent(root.transform, false);
            surface.transform.localScale = new Vector3(3f, 0.35f, 3f);
            surface.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Waypoints are children so the prefab ships with a working path, but they
            // are meant to be dragged out and placed in the scene.
            var a = new GameObject("Waypoint_A");
            a.transform.SetParent(root.transform, false);
            a.transform.localPosition = Vector3.zero;

            var b = new GameObject("Waypoint_B");
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0f, 0f, 6f);

            var platform = root.AddComponent<MovingPlatform>();
            var so = new SerializedObject(platform);
            SerializedProperty points = so.FindProperty("waypoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).objectReferenceValue = a.transform;
            points.GetArrayElementAtIndex(1).objectReferenceValue = b.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_MovingPlatform.prefab");
            return "Hazard_MovingPlatform   carries the player (CharacterController needs this)";
        }

        private static string BuildFallingPlatform()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Hazard_FallingPlatform";
            root.transform.localScale = new Vector3(2.5f, 0.3f, 2.5f);
            root.GetComponent<MeshRenderer>().sharedMaterial =
                PlaceholderFactory.GetMaterial("PH_FallingPlatform", new Color(0.70f, 0.55f, 0.38f));

            root.AddComponent<FallingPlatform>();

            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_FallingPlatform.prefab");
            return "Hazard_FallingPlatform  shakes, drops, respawns";
        }

        // --------------------------------------------------------------- traps --

        private static string BuildCrusher(int layer)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Hazard_Crusher";
            root.layer = layer;
            root.transform.localScale = new Vector3(2.4f, 1.6f, 2.4f);
            root.GetComponent<MeshRenderer>().sharedMaterial =
                PlaceholderFactory.GetMaterial("PH_Crusher", new Color(0.32f, 0.30f, 0.34f));

            root.AddComponent<CrusherTrap>();

            var damage = root.AddComponent<DamageSource>();
            damage.instantKill = true;

            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_Crusher.prefab");
            return "Hazard_Crusher          slams down, instant kill";
        }

        private static string BuildSpikes(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Hazard_Spikes", layer);
            Material baseMat = PlaceholderFactory.GetMaterial("PH_SpikeBase", new Color(0.30f, 0.28f, 0.30f));
            Material spikeMat = PlaceholderFactory.GetMaterial("PH_Spikes", new Color(0.78f, 0.80f, 0.85f), 0.6f);

            PlaceholderFactory.AddPart(root.transform, PrimitiveType.Cube, "Base",
                new Vector3(0f, 0.05f, 0f), new Vector3(2f, 0.1f, 2f), baseMat);

            var spikes = new GameObject("Spikes");
            spikes.transform.SetParent(root.transform, false);
            spikes.transform.localPosition = new Vector3(0f, -0.4f, 0f);

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    PlaceholderFactory.AddPart(spikes.transform, PrimitiveType.Capsule, $"Spike_{x}_{z}",
                        new Vector3(x * 0.6f, 0.45f, z * 0.6f),
                        new Vector3(0.16f, 0.45f, 0.16f), spikeMat);
                }
            }

            var hazardCollider = spikes.AddComponent<BoxCollider>();
            hazardCollider.isTrigger = true;
            hazardCollider.size = new Vector3(1.9f, 0.9f, 1.9f);
            hazardCollider.center = new Vector3(0f, 0.45f, 0f);

            var damage = spikes.AddComponent<DamageSource>();
            damage.damageAmount = 1;
            damage.knockbackForce = 5f;

            var trap = root.AddComponent<SpikeTrap>();
            var so = new SerializedObject(trap);
            so.FindProperty("spikes").objectReferenceValue = spikes.transform;
            so.FindProperty("hazardCollider").objectReferenceValue = hazardCollider;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_Spikes.prefab");
            return "Hazard_Spikes           extend/retract, collider off while down";
        }

        private static string BuildLavaPool(int layer)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Hazard_LavaPool";
            root.layer = layer;
            root.transform.localScale = new Vector3(8f, 0.4f, 8f);

            var lava = AssetDatabase.LoadAssetAtPath<Material>(LavaMaterial);
            root.GetComponent<MeshRenderer>().sharedMaterial = lava != null
                ? lava
                : PlaceholderFactory.GetMaterial("PH_Lava", new Color(1f, 0.35f, 0.05f));

            var col = root.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var damage = root.AddComponent<DamageSource>();
            damage.instantKill = true;

            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_LavaPool.prefab");
            return lava != null
                ? "Hazard_LavaPool         uses VH_Lava shader, instant kill"
                : "Hazard_LavaPool         VH_Lava.mat NOT FOUND, used flat orange";
        }

        private static string BuildKillVolume(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Hazard_KillVolume", layer);

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(400f, 10f, 400f);

            root.AddComponent<KillVolume>();

            PlaceholderFactory.SavePrefab(root, $"{HazardFolder}/Hazard_KillVolume.prefab");
            return "Hazard_KillVolume       400x400 catch-all for falls (no renderer)";
        }

        // ---------------------------------------------------- level furniture --

        private static string BuildCheckpoint()
        {
            var root = PlaceholderFactory.NewRoot("Checkpoint", 0);
            Material post = PlaceholderFactory.GetMaterial("PH_CheckpointPost", new Color(0.45f, 0.40f, 0.35f));
            Material off = PlaceholderFactory.GetMaterial("PH_CheckpointOff", new Color(0.55f, 0.58f, 0.60f));
            Material on = PlaceholderFactory.GetMaterial("PH_CheckpointOn", new Color(0.25f, 0.95f, 0.55f));

            PlaceholderFactory.AddPart(root.transform, PrimitiveType.Cylinder, "Post",
                new Vector3(0f, 0.9f, 0f), new Vector3(0.12f, 0.9f, 0.12f), post);

            var inactive = new GameObject("Inactive");
            inactive.transform.SetParent(root.transform, false);
            PlaceholderFactory.AddPart(inactive.transform, PrimitiveType.Cube, "FlagOff",
                new Vector3(0.35f, 1.55f, 0f), new Vector3(0.7f, 0.45f, 0.05f), off);

            var active = new GameObject("Active");
            active.transform.SetParent(root.transform, false);
            PlaceholderFactory.AddPart(active.transform, PrimitiveType.Cube, "FlagOn",
                new Vector3(0.35f, 1.55f, 0f), new Vector3(0.7f, 0.45f, 0.05f), on);

            var respawn = new GameObject("RespawnPoint");
            respawn.transform.SetParent(root.transform, false);
            respawn.transform.localPosition = new Vector3(0f, 0.4f, 0.8f);

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2.4f, 2f);
            col.center = new Vector3(0f, 1.2f, 0f);

            var checkpoint = root.AddComponent<Checkpoint>();
            var so = new SerializedObject(checkpoint);
            so.FindProperty("respawnPoint").objectReferenceValue = respawn.transform;
            so.FindProperty("activeVisual").objectReferenceValue = active;
            so.FindProperty("inactiveVisual").objectReferenceValue = inactive;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SavePrefab(root, $"{LevelFolder}/Checkpoint.prefab");
            return "Checkpoint              flag turns green, sets respawn";
        }

        private static string BuildPickup(string name, CollectibleKind kind, int amount, Color color,
                                          PrimitiveType shape, Vector3 scale, Vector3? euler)
        {
            var root = PlaceholderFactory.NewRoot(name, 0);
            Material mat = PlaceholderFactory.GetMaterial("PH_" + name, color, 0.4f);

            PlaceholderFactory.AddPart(root.transform, shape, "Visual",
                Vector3.zero, scale, mat, euler);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.55f;

            var collectible = root.AddComponent<Collectible>();
            var so = new SerializedObject(collectible);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("amount").intValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SavePrefab(root, $"{PickupFolder}/{name}.prefab");
            return $"{name,-23} {kind} x{amount}";
        }
    }
}
