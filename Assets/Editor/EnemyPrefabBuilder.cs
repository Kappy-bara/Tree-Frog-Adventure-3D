using System.Text;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Builds the six placeholder enemy prefabs and the thrown-fly projectile.
    ///
    /// Every enemy follows the same contract, so swapping in real art later is a
    /// drag-and-drop with no code change:
    ///
    ///   Root  — layer Enemy, EnemyHealth, one AI script, DamageSource,
    ///           trigger collider (contact damage + stomp target),
    ///           CharacterController + GroundEnemyMotor for ground types
    ///   Visual — child holding EnemyVisual and the primitive meshes
    ///
    /// Run: Tools > Tree Frog > Build Enemy Prefabs
    /// Overwrites the generated prefabs; safe to re-run.
    /// </summary>
    public static class EnemyPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Enemies";
        private const string ProjectileFolder = "Assets/Prefabs/Combat";

        [MenuItem("Tools/Tree Frog/Build Enemy Prefabs")]
        public static void Build()
        {
            var report = new StringBuilder();
            report.AppendLine("[EnemyPrefabBuilder] Result:");

            int enemyLayer = PlaceholderFactory.Layer("Enemy");
            int projectileLayer = PlaceholderFactory.Layer("Projectile");

            report.AppendLine("  " + BuildBunny(enemyLayer));
            report.AppendLine("  " + BuildRhino(enemyLayer));
            report.AppendLine("  " + BuildChicken(enemyLayer));
            report.AppendLine("  " + BuildPig(enemyLayer));
            report.AppendLine("  " + BuildBat(enemyLayer));
            report.AppendLine("  " + BuildBee(enemyLayer));
            report.AppendLine("  " + BuildFly(projectileLayer));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // =====================================================================
        // Ground enemies
        // =====================================================================

        private static string BuildBunny(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Bunny", layer);
            Material fur = PlaceholderFactory.GetMaterial("PH_Bunny", new Color(0.90f, 0.88f, 0.84f));
            Material inner = PlaceholderFactory.GetMaterial("PH_BunnyInner", new Color(0.95f, 0.70f, 0.72f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Hop, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Capsule, "Body",
                new Vector3(0f, 0.30f, 0f), new Vector3(0.42f, 0.26f, 0.42f), fur);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarL",
                new Vector3(-0.45f, 1.5f, 0f), new Vector3(0.22f, 0.9f, 0.22f), inner);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarR",
                new Vector3(0.45f, 1.5f, 0f), new Vector3(0.22f, 0.9f, 0.22f), inner);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "Tail",
                new Vector3(0f, 0.1f, -1.0f), new Vector3(0.45f, 0.7f, 0.45f), fur);

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 0.62f, radius: 0.24f);
            AddTriggerCapsule(root, height: 0.62f, radius: 0.26f, centerY: 0.31f);
            AddHealth(root, maxHealth: 1, stompable: true);
            AddDamage(root, 1);
            root.AddComponent<BunnyAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Bunny.prefab");
            return "Enemy_Bunny        ground, 1 HP, hops";
        }

        private static string BuildRhino(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Rhino", layer);
            Material hide = PlaceholderFactory.GetMaterial("PH_Rhino", new Color(0.42f, 0.44f, 0.47f));
            Material horn = PlaceholderFactory.GetMaterial("PH_RhinoHorn", new Color(0.92f, 0.90f, 0.82f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Waddle, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "Body",
                new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.70f, 1.35f), hide);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "Head",
                new Vector3(0f, 0.1f, 0.72f), new Vector3(0.72f, 0.72f, 0.42f), hide);

            // Cones are not a Unity primitive; a squashed capsule reads as a horn.
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "Horn",
                new Vector3(0f, 0.55f, 0.95f), new Vector3(0.14f, 0.28f, 0.14f), horn,
                new Vector3(60f, 0f, 0f));

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 1.05f, radius: 0.45f);
            AddTriggerCapsule(root, height: 1.05f, radius: 0.48f, centerY: 0.53f);

            // 2 HP: one stomp staggers it, a second finishes. Stomping a charging
            // rhino should feel like a risk, not a free kill.
            AddHealth(root, maxHealth: 2, stompable: true);
            AddDamage(root, 1, knockback: 8f);
            root.AddComponent<RhinoAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Rhino.prefab");
            return "Enemy_Rhino        ground, 2 HP, telegraphed charge + knockback";
        }

        private static string BuildChicken(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Chicken", layer);
            Material feathers = PlaceholderFactory.GetMaterial("PH_Chicken", new Color(0.96f, 0.95f, 0.90f));
            Material comb = PlaceholderFactory.GetMaterial("PH_ChickenComb", new Color(0.85f, 0.18f, 0.16f));
            Material beak = PlaceholderFactory.GetMaterial("PH_ChickenBeak", new Color(0.95f, 0.72f, 0.20f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Waddle, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 0.34f, 0f), new Vector3(0.48f, 0.52f, 0.44f), feathers);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 0.62f, 0.30f), new Vector3(0.55f, 0.50f, 0.55f), feathers);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "Comb",
                new Vector3(0f, 0.95f, 0.28f), new Vector3(0.10f, 0.22f, 0.30f), comb);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "Beak",
                new Vector3(0f, 0.60f, 0.62f), new Vector3(0.16f, 0.14f, 0.26f), beak);

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 0.66f, radius: 0.24f);
            AddTriggerCapsule(root, height: 0.66f, radius: 0.26f, centerY: 0.33f);
            AddHealth(root, maxHealth: 1, stompable: true);
            AddDamage(root, 1);
            root.AddComponent<ChickenAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Chicken.prefab");
            return "Enemy_Chicken      ground, 1 HP, charges then flees";
        }

        private static string BuildPig(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Pig", layer);
            Material skin = PlaceholderFactory.GetMaterial("PH_Pig", new Color(0.93f, 0.62f, 0.66f));
            Material snout = PlaceholderFactory.GetMaterial("PH_PigSnout", new Color(0.82f, 0.45f, 0.50f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Hop, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Capsule, "Body",
                new Vector3(0f, 0.38f, 0f), new Vector3(0.55f, 0.34f, 0.55f), skin,
                new Vector3(90f, 0f, 0f));

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "Head",
                new Vector3(0f, -1.0f, 0.35f), new Vector3(0.85f, 0.85f, 1.3f), skin);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cylinder, "Snout",
                new Vector3(0f, -1.42f, 0.35f), new Vector3(0.35f, 0.12f, 0.35f), snout,
                new Vector3(90f, 0f, 0f));

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 0.72f, radius: 0.30f);
            AddTriggerCapsule(root, height: 0.72f, radius: 0.32f, centerY: 0.36f);
            AddHealth(root, maxHealth: 1, stompable: true);
            AddDamage(root, 1);
            root.AddComponent<PigAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Pig.prefab");
            return "Enemy_Pig          ground, 1 HP, runs while jumping";
        }

        // =====================================================================
        // Flying enemies — no CharacterController, no gravity
        // =====================================================================

        private static string BuildBat(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Bat", layer);
            Material hide = PlaceholderFactory.GetMaterial("PH_Bat", new Color(0.26f, 0.20f, 0.34f));
            Material wing = PlaceholderFactory.GetMaterial("PH_BatWing", new Color(0.38f, 0.28f, 0.46f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Flap, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.30f, 0.40f), hide);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarL",
                new Vector3(-0.35f, 0.75f, 0.1f), new Vector3(0.16f, 0.45f, 0.16f), hide);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarR",
                new Vector3(0.35f, 0.75f, 0.1f), new Vector3(0.16f, 0.45f, 0.16f), hide);

            GameObject wingL = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "WingL",
                new Vector3(-0.32f, 0.02f, 0f), new Vector3(0.55f, 0.04f, 0.30f), wing);
            GameObject wingR = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "WingR",
                new Vector3(0.32f, 0.02f, 0f), new Vector3(0.55f, 0.04f, 0.30f), wing);

            BindVisual(ev, body.transform, wingL.transform, wingR.transform);

            AddTriggerSphere(root, radius: 0.34f, centerY: 0f);
            AddHealth(root, maxHealth: 1, stompable: true);
            AddDamage(root, 1);
            root.AddComponent<BatAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Bat.prefab");
            return "Enemy_Bat          flying, 1 HP, circles then swoops";
        }

        private static string BuildBee(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Enemy_Bee", layer);
            Material yellow = PlaceholderFactory.GetMaterial("PH_Bee", new Color(0.96f, 0.80f, 0.15f));
            Material black = PlaceholderFactory.GetMaterial("PH_BeeStripe", new Color(0.12f, 0.11f, 0.10f));
            Material wing = PlaceholderFactory.GetMaterial("PH_BeeWing", new Color(0.85f, 0.92f, 1f), 0.6f);

            Transform visual = MakeVisual(root, EnemyVisual.Style.Hover, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.28f, 0.38f), yellow);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "Stripe",
                new Vector3(0f, 0f, -0.15f), new Vector3(1.05f, 1.05f, 0.22f), black);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "Sting",
                new Vector3(0f, 0f, -0.62f), new Vector3(0.12f, 0.20f, 0.12f), black,
                new Vector3(90f, 0f, 0f));

            GameObject wingL = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "WingL",
                new Vector3(-0.20f, 0.14f, 0f), new Vector3(0.34f, 0.02f, 0.18f), wing);
            GameObject wingR = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "WingR",
                new Vector3(0.20f, 0.14f, 0f), new Vector3(0.34f, 0.02f, 0.18f), wing);

            BindVisual(ev, body.transform, wingL.transform, wingR.transform);

            AddTriggerSphere(root, radius: 0.28f, centerY: 0f);
            AddHealth(root, maxHealth: 1, stompable: true);
            AddDamage(root, 1);
            root.AddComponent<BeeAI>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Enemy_Bee.prefab");
            return "Enemy_Bee          flying, 1 HP, hovers then dives straight down";
        }

        // =====================================================================
        // Projectile
        // =====================================================================

        private static string BuildFly(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Fly_Projectile", layer);
            Material bodyMat = PlaceholderFactory.GetMaterial("PH_Fly", new Color(0.18f, 0.20f, 0.16f));
            Material wingMat = PlaceholderFactory.GetMaterial("PH_FlyWing", new Color(0.85f, 0.92f, 1f), 0.6f);

            GameObject body = PlaceholderFactory.AddPart(root.transform, PrimitiveType.Sphere, "Body",
                Vector3.zero, new Vector3(0.16f, 0.14f, 0.20f), bodyMat);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "WingL",
                new Vector3(-0.6f, 0.4f, 0f), new Vector3(1.1f, 0.08f, 0.6f), wingMat);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "WingR",
                new Vector3(0.6f, 0.4f, 0f), new Vector3(1.1f, 0.08f, 0.6f), wingMat);

            // No collider: FlyProjectile sweeps a SphereCast instead, because a small
            // fast trigger tunnels straight through thin geometry between frames.
            root.AddComponent<FlyProjectile>();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{ProjectileFolder}/Fly_Projectile.prefab");
            return "Fly_Projectile     thrown ammo (SphereCast, no collider)";
        }

        // =====================================================================
        // Shared wiring
        // =====================================================================

        private static Transform MakeVisual(GameObject root, EnemyVisual.Style style, out EnemyVisual visual)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(root.transform, false);

            visual = go.AddComponent<EnemyVisual>();

            var so = new SerializedObject(visual);
            so.FindProperty("style").enumValueIndex = (int)style;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go.transform;
        }

        private static void BindVisual(EnemyVisual visual, Transform body,
                                       Transform leftWing = null, Transform rightWing = null)
        {
            var so = new SerializedObject(visual);
            so.FindProperty("body").objectReferenceValue = body;
            if (leftWing != null) so.FindProperty("leftWing").objectReferenceValue = leftWing;
            if (rightWing != null) so.FindProperty("rightWing").objectReferenceValue = rightWing;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddGroundMotor(GameObject root, float height, float radius)
        {
            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = Mathf.Min(0.3f, height * 0.4f);
            cc.skinWidth = Mathf.Max(0.01f, radius * 0.08f);

            root.AddComponent<GroundEnemyMotor>();
        }

        /// <summary>
        /// Separate trigger from the CharacterController: the CC handles movement, this
        /// is what PlayerHealth and PlayerStomp actually detect.
        /// </summary>
        private static void AddTriggerCapsule(GameObject root, float height, float radius, float centerY)
        {
            var col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.height = height;
            col.radius = radius;
            col.center = new Vector3(0f, centerY, 0f);
        }

        private static void AddTriggerSphere(GameObject root, float radius, float centerY)
        {
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            col.center = new Vector3(0f, centerY, 0f);
        }

        private static void AddHealth(GameObject root, int maxHealth, bool stompable)
        {
            var health = root.AddComponent<EnemyHealth>();

            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").intValue = maxHealth;
            so.FindProperty("stompable").boolValue = stompable;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddDamage(GameObject root, int amount, float knockback = 0f)
        {
            var damage = root.AddComponent<DamageSource>();
            damage.damageAmount = amount;
            damage.knockbackForce = knockback;
        }
    }
}
