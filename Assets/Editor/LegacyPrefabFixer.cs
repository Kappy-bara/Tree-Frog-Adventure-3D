using System.Text;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Fx;
using TreeFrogAdventure.Player;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Repairs the two prefabs that predate the current systems.
    ///
    /// 1. Bunny.prefab — the only enemy actually placed in any level scene, and the only
    ///    one in the game with real skeletal animation (an FBX walk clip driven by
    ///    char1.controller). It also had no EnemyHealth, which meant it could not be
    ///    stomped, shot or killed by anything, and it moved with DirectWaypointPatrol:
    ///    transform writes on a fixed Y with no gravity, so it floated over sloped
    ///    terrain and walked through geometry. The model is worth keeping and the
    ///    behaviour is not, so this produces Enemy_Bunny_Rigged with the real
    ///    components and the animation intact.
    ///
    /// The frog's Animator is deliberately LEFT ALONE, though its controller is empty
    /// and PureMathFrog drives every bone in code. Disabling it looks like free savings
    /// and is not: an enabled Animator restores the transforms it owns to their default
    /// pose at the start of each frame, and PureMathFrog's procedural walk was authored
    /// on top of that reset. Turning the Animator off visibly broke the walk cycle.
    ///
    /// Run: Tools > Tree Frog > Fix Legacy Prefabs
    /// </summary>
    public static class LegacyPrefabFixer
    {
        private const string LegacyBunny = "Assets/Prefabs/Enemies/Bunny.prefab";
        private const string RiggedBunny = "Assets/Prefabs/Enemies/Enemy_Bunny_Rigged.prefab";

        [MenuItem("Tools/Tree Frog/Fix Legacy Prefabs")]
        public static void Fix()
        {
            var report = new StringBuilder();
            report.AppendLine("[LegacyPrefabFixer] Result:");

            report.AppendLine("  " + BuildRiggedBunny());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------------

        private static string BuildRiggedBunny()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyBunny);
            if (source == null) return $"SKIP: {LegacyBunny} not found";

            // Work on an instance so the original prefab is left untouched — it is
            // still referenced by the existing level scenes until they are rebuilt.
            GameObject instance = Object.Instantiate(source);
            instance.name = "Enemy_Bunny_Rigged";

            var changes = new StringBuilder();

            // --- strip the old movement ---------------------------------------
            foreach (DirectWaypointPatrol patrol in instance.GetComponentsInChildren<DirectWaypointPatrol>(true))
            {
                Object.DestroyImmediate(patrol);
                changes.Append("-patrol ");
            }

            // --- the old colliders --------------------------------------------
            // Children too, not just the root: the stomp target has to be the single
            // trigger added below, and a leftover collider on a bone would give
            // GetComponentInParent<DamageSource> a second way to hit the player.
            foreach (Collider col in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            int enemyLayer = PlaceholderFactory.Layer("Enemy");
            PlaceholderFactory.SetLayerRecursively(instance, enemyLayer);

            // --- movement ------------------------------------------------------
            // THE MODEL ROOT IS SCALED 200x. Collider sizes are local, so authoring
            // them in metres produced a trigger 52 m across that damaged the player
            // from most of the map. Everything below is divided by the root scale so
            // the numbers below are metres in world space, which is what they mean.
            Vector3 scale = instance.transform.localScale;
            float rootScale = Mathf.Max(0.0001f,
                Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            float toLocal = 1f / rootScale;

            // Measured from the collider the legacy prefab shipped with, which works
            // out to 0.23 x 0.40 x 0.24 m in world space. The model is a small animal,
            // not a person-sized one.
            const float bodyHeight = 0.42f;
            const float bodyRadius = 0.12f;

            if (instance.GetComponent<CharacterController>() == null)
            {
                var cc = instance.AddComponent<CharacterController>();
                cc.height = bodyHeight * toLocal;
                cc.radius = bodyRadius * toLocal;
                cc.center = new Vector3(0f, bodyHeight * 0.5f * toLocal, 0f);
                cc.slopeLimit = 50f;
                cc.stepOffset = 0.25f * toLocal;
                cc.skinWidth = 0.02f * toLocal;
                changes.Append("+cc ");
            }

            if (instance.GetComponent<GroundEnemyMotor>() == null)
            {
                instance.AddComponent<GroundEnemyMotor>();
                changes.Append("+motor ");
            }

            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.height = bodyHeight * toLocal;
            capsule.radius = (bodyRadius + 0.02f) * toLocal;
            capsule.center = new Vector3(0f, bodyHeight * 0.5f * toLocal, 0f);
            changes.Append("+trigger ");

            // --- damage and death ----------------------------------------------
            // Deliberately not ?? — UnityEngine.Object overloads ==, so a missing
            // component is a "fake null" that the null-coalescing operator misses.
            var health = instance.GetComponent<EnemyHealth>();
            if (health == null)
            {
                health = instance.AddComponent<EnemyHealth>();

                var so = new SerializedObject(health);
                so.FindProperty("maxHealth").intValue = 1;
                so.FindProperty("stompable").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                changes.Append("+health ");
            }

            if (instance.GetComponent<EnemyFeedback>() == null)
            {
                instance.AddComponent<EnemyFeedback>();
                changes.Append("+feedback ");
            }

            var damage = instance.GetComponent<DamageSource>();
            if (damage == null) damage = instance.AddComponent<DamageSource>();
            damage.damageAmount = 1;

            if (instance.GetComponent<BunnyAI>() == null)
            {
                instance.AddComponent<BunnyAI>();
                changes.Append("+ai ");
            }

            PlaceholderFactory.SavePrefab(instance, RiggedBunny);

            return $"Enemy_Bunny_Rigged  built from Bunny.prefab ({changes.ToString().Trim()}), " +
                   $"animation kept, root scale {rootScale:F0}x compensated";
        }

    }
}
