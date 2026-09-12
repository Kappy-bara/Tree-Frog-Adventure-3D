using System.Text;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Fx;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Builds the four world guardians and the projectile two of them spit.
    ///
    /// Same contract as EnemyPrefabBuilder — layer Enemy, EnemyHealth, one AI script,
    /// DamageSource, a trigger collider, a Visual child — so a guardian is an ordinary
    /// enemy as far as stomping, thrown flies and hazards are concerned. What differs
    /// is scale, hit points and a BossBase-derived AI with phases.
    ///
    /// Guardians are ~2.5x the size of a regular enemy and carry EnemyFeedback, so a hit
    /// on something with 10 hit points still reads.
    ///
    /// Run: Tools > Tree Frog > Build Boss Prefabs
    /// Overwrites the generated prefabs; safe to re-run.
    /// </summary>
    public static class BossPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Bosses";
        private const string ProjectileFolder = "Assets/Prefabs/Combat";

        private const string BeePrefab = "Assets/Prefabs/Enemies/Enemy_Bee.prefab";
        private const string LavaPrefab = "Assets/Prefabs/Hazards/Hazard_LavaPool.prefab";

        // Default(0) + Player(10) + Barrier(15). Deliberately excludes Enemy, so a
        // guardian's own adds do not eat the shots aimed at the frog.
        private const int ProjectileHitMask = (1 << 0) | (1 << 10) | (1 << 15);

        [MenuItem("Tools/Tree Frog/Build Boss Prefabs")]
        public static void Build()
        {
            var report = new StringBuilder();
            report.AppendLine("[BossPrefabBuilder] Result:");

            int enemyLayer = PlaceholderFactory.Layer("Enemy");
            int projectileLayer = PlaceholderFactory.Layer("Projectile");

            // Built first: two guardians hold a reference to it.
            GameObject projectile = BuildProjectile(projectileLayer, report);

            report.AppendLine("  " + BuildForest(enemyLayer));
            report.AppendLine("  " + BuildCanopy(enemyLayer, projectile));
            report.AppendLine("  " + BuildSwamp(enemyLayer, projectile));
            report.AppendLine("  " + BuildVolcano(enemyLayer));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // =====================================================================

        private static GameObject BuildProjectile(int layer, StringBuilder report)
        {
            var root = PlaceholderFactory.NewRoot("Boss_Projectile", layer);
            Material mat = PlaceholderFactory.GetMaterial("PH_BossSpit", new Color(0.55f, 0.85f, 0.35f));

            PlaceholderFactory.AddPart(root.transform, PrimitiveType.Sphere, "Body",
                Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), mat);

            // No collider, like Fly_Projectile: BossProjectile sweeps a SphereCast so it
            // cannot tunnel, and sweeping also avoids needing a Rigidbody for trigger
            // callbacks against the player's CharacterController.
            var shot = root.AddComponent<BossProjectile>();

            var so = new SerializedObject(shot);
            so.FindProperty("hitLayers").intValue = ProjectileHitMask;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            GameObject asset = PlaceholderFactory.SavePrefab(root, $"{ProjectileFolder}/Boss_Projectile.prefab");

            report.AppendLine("  Boss_Projectile    arcing spit (SphereCast, no collider)");
            return asset;
        }

        // =====================================================================

        private static string BuildForest(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Boss_ForestGuardian", layer);

            Material hide = PlaceholderFactory.GetMaterial("PH_ForestGuardian", new Color(0.35f, 0.50f, 0.28f));
            Material bark = PlaceholderFactory.GetMaterial("PH_ForestGuardianBark", new Color(0.30f, 0.22f, 0.14f));
            Material inner = PlaceholderFactory.GetMaterial("PH_ForestGuardianInner", new Color(0.85f, 0.70f, 0.55f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Hop, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Capsule, "Body",
                new Vector3(0f, 0.95f, 0f), new Vector3(1.5f, 0.95f, 1.5f), hide);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarL",
                new Vector3(-0.40f, 1.35f, 0f), new Vector3(0.22f, 0.75f, 0.22f), inner);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "EarR",
                new Vector3(0.40f, 1.35f, 0f), new Vector3(0.22f, 0.75f, 0.22f), inner);

            // Bark plates on the back: the silhouette cue that this one does not die
            // to a single stomp like the bunnies do.
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "PlateA",
                new Vector3(0f, 0.35f, -0.35f), new Vector3(1.05f, 0.35f, 0.5f), bark);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "PlateB",
                new Vector3(0f, 0.05f, -0.10f), new Vector3(1.15f, 0.30f, 0.5f), bark);

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 2.1f, radius: 0.95f);
            AddTriggerCapsule(root, height: 2.1f, radius: 1f, centerY: 1.05f);
            AddHealth(root, maxHealth: 6);
            AddDamage(root, 1, knockback: 9f);
            root.AddComponent<EnemyFeedback>();

            var ai = root.AddComponent<ForestGuardianAI>();
            SetBossIdentity(ai, "Thornback", "Guardian of the Emerald Forest", phases: 3);

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Boss_ForestGuardian.prefab");

            return "Boss_ForestGuardian   ground,  6 HP, 3 phases, telegraphed charge + slam";
        }

        private static string BuildCanopy(int layer, GameObject projectile)
        {
            var root = PlaceholderFactory.NewRoot("Boss_CanopyGuardian", layer);

            Material feather = PlaceholderFactory.GetMaterial("PH_CanopyGuardian", new Color(0.35f, 0.45f, 0.70f));
            Material wing = PlaceholderFactory.GetMaterial("PH_CanopyGuardianWing", new Color(0.55f, 0.68f, 0.90f));
            Material beak = PlaceholderFactory.GetMaterial("PH_CanopyGuardianBeak", new Color(0.95f, 0.75f, 0.30f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Flap, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 0f, 0f), new Vector3(1.6f, 1.3f, 1.9f), feather);

            GameObject wingL = PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "WingL",
                new Vector3(-0.85f, 0.15f, 0f), new Vector3(1.5f, 0.08f, 0.75f), wing);
            GameObject wingR = PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "WingR",
                new Vector3(0.85f, 0.15f, 0f), new Vector3(1.5f, 0.08f, 0.75f), wing);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "Beak",
                new Vector3(0f, 0f, 0.62f), new Vector3(0.16f, 0.28f, 0.16f), beak,
                new Vector3(90f, 0f, 0f));

            BindVisual(ev, body.transform, wingL.transform, wingR.transform);

            // No CharacterController: a flyer wants neither gravity nor ground contact,
            // matching BatAI and BeeAI.
            AddTriggerSphere(root, radius: 1.1f, centerY: 0f);
            AddHealth(root, maxHealth: 6);
            AddDamage(root, 1);
            root.AddComponent<EnemyFeedback>();

            var ai = root.AddComponent<CanopyGuardianAI>();
            SetBossIdentity(ai, "Skyweaver", "Guardian of Canopy Heights", phases: 3);

            var so = new SerializedObject(ai);
            so.FindProperty("projectilePrefab").objectReferenceValue =
                projectile != null ? projectile.GetComponent<BossProjectile>() : null;
            so.FindProperty("projectileHitLayers").intValue = ProjectileHitMask;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Boss_CanopyGuardian.prefab");

            return "Boss_CanopyGuardian   flying, 6 HP, 3 phases, dive + spit (needs flies)";
        }

        private static string BuildSwamp(int layer, GameObject projectile)
        {
            var root = PlaceholderFactory.NewRoot("Boss_SwampGuardian", layer);

            Material skin = PlaceholderFactory.GetMaterial("PH_SwampGuardian", new Color(0.30f, 0.42f, 0.30f));
            Material sac = PlaceholderFactory.GetMaterial("PH_SwampGuardianSac", new Color(0.55f, 0.70f, 0.35f));
            Material eye = PlaceholderFactory.GetMaterial("PH_SwampGuardianEye", new Color(0.95f, 0.85f, 0.30f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Bob, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Sphere, "Body",
                new Vector3(0f, 1.05f, 0f), new Vector3(2.3f, 1.7f, 2.1f), skin);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "Throat",
                new Vector3(0f, -0.18f, 0.46f), new Vector3(0.7f, 0.55f, 0.6f), sac);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "EyeL",
                new Vector3(-0.24f, 0.42f, 0.34f), new Vector3(0.22f, 0.28f, 0.22f), eye);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "EyeR",
                new Vector3(0.24f, 0.42f, 0.34f), new Vector3(0.22f, 0.28f, 0.22f), eye);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "SacL",
                new Vector3(-0.42f, 0.15f, -0.30f), new Vector3(0.40f, 0.45f, 0.40f), sac);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Sphere, "SacR",
                new Vector3(0.42f, 0.15f, -0.30f), new Vector3(0.40f, 0.45f, 0.40f), sac);

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 2.2f, radius: 1.15f);
            AddTriggerCapsule(root, height: 2.2f, radius: 1.2f, centerY: 1.1f);
            AddHealth(root, maxHealth: 8);
            AddDamage(root, 1);
            root.AddComponent<EnemyFeedback>();

            var ai = root.AddComponent<SwampGuardianAI>();
            SetBossIdentity(ai, "Mirebloom", "Guardian of the Misty Swamp", phases: 3);

            var so = new SerializedObject(ai);
            so.FindProperty("projectilePrefab").objectReferenceValue =
                projectile != null ? projectile.GetComponent<BossProjectile>() : null;
            so.FindProperty("projectileHitLayers").intValue = ProjectileHitMask;
            so.FindProperty("addPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(BeePrefab);
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Boss_SwampGuardian.prefab");

            return "Boss_SwampGuardian    ground, 8 HP, 3 phases, volleys + summons bees";
        }

        private static string BuildVolcano(int layer)
        {
            var root = PlaceholderFactory.NewRoot("Boss_VolcanoGuardian", layer);

            Material rock = PlaceholderFactory.GetMaterial("PH_VolcanoGuardian", new Color(0.22f, 0.19f, 0.20f));
            Material ember = PlaceholderFactory.GetMaterial("PH_VolcanoGuardianEmber", new Color(1f, 0.35f, 0.10f), 0.4f);
            Material horn = PlaceholderFactory.GetMaterial("PH_VolcanoGuardianHorn", new Color(0.90f, 0.88f, 0.80f));

            Transform visual = MakeVisual(root, EnemyVisual.Style.Waddle, out EnemyVisual ev);

            GameObject body = PlaceholderFactory.AddPart(visual, PrimitiveType.Cube, "Body",
                new Vector3(0f, 1.15f, 0f), new Vector3(2.0f, 1.6f, 3.1f), rock);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "Head",
                new Vector3(0f, 0.08f, 0.66f), new Vector3(0.80f, 0.80f, 0.34f), rock);

            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Capsule, "Horn",
                new Vector3(0f, 0.52f, 0.72f), new Vector3(0.10f, 0.30f, 0.10f), horn,
                new Vector3(55f, 0f, 0f));

            // Glowing seams, so the charge telegraph reads even at a distance.
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "SeamL",
                new Vector3(-0.51f, 0.20f, 0f), new Vector3(0.04f, 0.35f, 0.85f), ember);
            PlaceholderFactory.AddPart(body.transform, PrimitiveType.Cube, "SeamR",
                new Vector3(0.51f, 0.20f, 0f), new Vector3(0.04f, 0.35f, 0.85f), ember);

            BindVisual(ev, body.transform);

            AddGroundMotor(root, height: 2.4f, radius: 1.05f);
            AddTriggerCapsule(root, height: 2.4f, radius: 1.1f, centerY: 1.2f);
            AddHealth(root, maxHealth: 10);
            AddDamage(root, 1, knockback: 12f);
            root.AddComponent<EnemyFeedback>();

            var ai = root.AddComponent<VolcanoGuardianAI>();
            SetBossIdentity(ai, "Emberhorn", "Guardian of the Volcanic Highlands", phases: 4);

            var so = new SerializedObject(ai);
            so.FindProperty("lavaPoolPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(LavaPrefab);
            so.ApplyModifiedPropertiesWithoutUndo();

            PlaceholderFactory.SetLayerRecursively(root, layer);
            PlaceholderFactory.SavePrefab(root, $"{Folder}/Boss_VolcanoGuardian.prefab");

            return "Boss_VolcanoGuardian  ground, 10 HP, 4 phases, charge leaves lava trail";
        }

        // =====================================================================
        // Shared wiring (mirrors EnemyPrefabBuilder)
        // =====================================================================

        private static void SetBossIdentity(BossBase boss, string name, string title, int phases)
        {
            var so = new SerializedObject(boss);
            so.FindProperty("bossName").stringValue = name;
            so.FindProperty("bossTitle").stringValue = title;
            so.FindProperty("phaseCount").intValue = phases;
            so.FindProperty("startInactive").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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
            cc.stepOffset = Mathf.Min(0.4f, height * 0.4f);
            cc.skinWidth = Mathf.Max(0.01f, radius * 0.08f);

            root.AddComponent<GroundEnemyMotor>();
        }

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

        /// <summary>
        /// Guardians are authored NOT stompable. BossBase opens the stomp window itself
        /// between phases, so leaving the prefab stompable would let a lucky jump skip
        /// the whole phase structure.
        /// </summary>
        private static void AddHealth(GameObject root, int maxHealth)
        {
            var health = root.AddComponent<EnemyHealth>();

            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").intValue = maxHealth;
            so.FindProperty("stompable").boolValue = false;

            // Long enough for the collapse animation and the victory banner to overlap.
            so.FindProperty("despawnDelay").floatValue = 1.2f;
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
