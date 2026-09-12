using System.Collections.Generic;
using System.IO;
using System.Text;
using TreeFrogAdventure.Fx;
using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Builds the one-shot particle effects and the catalog that maps them to
    /// <see cref="FxKind"/>.
    ///
    /// The project had no ParticleSystem anywhere — enemies were destroyed mid-frame
    /// with no feedback at all — so these are built rather than imported, in the same
    /// spirit as the primitive enemy placeholders: readable shapes and distinct colours
    /// that a real VFX pass can replace prefab-for-prefab without touching code.
    ///
    /// Every system is authored with playOnAwake off and looping off, because FxPool
    /// activates a pooled instance and calls Play itself.
    ///
    /// Run: Tools > Tree Frog > Build FX Prefabs
    /// </summary>
    public static class FxPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/Fx";
        private const string MaterialFolder = "Assets/Materials/Fx";
        private const string CatalogPath = "Assets/Resources/FxCatalog.asset";

        [MenuItem("Tools/Tree Frog/Build FX Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory("Assets/Resources");

            var report = new StringBuilder();
            report.AppendLine("[FxPrefabBuilder] Result:");

            Material spark = ParticleMaterial("FX_Spark", new Color(1f, 0.95f, 0.75f));
            Material soft = ParticleMaterial("FX_Soft", new Color(1f, 1f, 1f));

            var entries = new List<FxCatalog.Entry>
            {
                Make(FxKind.EnemyHit, "Fx_EnemyHit", spark, prewarm: 4, report,
                     count: 10, lifetime: 0.28f, speed: 5f, size: 0.16f, gravity: 0.2f,
                     color: new Color(1f, 0.95f, 0.6f), radius: 0.12f, cone: false),

                Make(FxKind.EnemyDeath, "Fx_EnemyDeath", spark, prewarm: 4, report,
                     count: 24, lifetime: 0.65f, speed: 6.5f, size: 0.22f, gravity: 1.1f,
                     color: new Color(1f, 0.75f, 0.35f), radius: 0.25f, cone: false),

                Make(FxKind.Collect, "Fx_Collect", spark, prewarm: 6, report,
                     count: 14, lifetime: 0.5f, speed: 3.2f, size: 0.14f, gravity: -0.35f,
                     color: new Color(1f, 0.9f, 0.4f), radius: 0.2f, cone: true),

                Make(FxKind.Land, "Fx_Land", soft, prewarm: 3, report,
                     count: 12, lifetime: 0.45f, speed: 2.6f, size: 0.35f, gravity: 0.1f,
                     color: new Color(0.85f, 0.82f, 0.72f, 0.8f), radius: 0.45f, cone: false,
                     flat: true),

                Make(FxKind.Splash, "Fx_Splash", soft, prewarm: 3, report,
                     count: 20, lifetime: 0.6f, speed: 4.5f, size: 0.2f, gravity: 1.6f,
                     color: new Color(0.6f, 0.85f, 1f, 0.9f), radius: 0.35f, cone: true),

                Make(FxKind.BossPhase, "Fx_BossPhase", spark, prewarm: 2, report,
                     count: 44, lifetime: 1f, speed: 9f, size: 0.3f, gravity: 0.6f,
                     color: new Color(1f, 0.55f, 0.25f), radius: 0.5f, cone: false),

                Make(FxKind.Checkpoint, "Fx_Checkpoint", spark, prewarm: 2, report,
                     count: 22, lifetime: 0.8f, speed: 4f, size: 0.18f, gravity: -0.5f,
                     color: new Color(0.4f, 0.9f, 1f), radius: 0.3f, cone: true),
            };

            WriteCatalog(entries, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------------

        private static FxCatalog.Entry Make(FxKind kind, string name, Material material, int prewarm,
                                            StringBuilder report,
                                            int count, float lifetime, float speed, float size,
                                            float gravity, Color color, float radius,
                                            bool cone, bool flat = false)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = lifetime;
            main.loop = false;

            // FxPool activates a pooled instance and calls Play; playing on awake would
            // fire the effect once at prewarm time, in whatever place the pool root is.
            main.playOnAwake = false;

            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = gravity;

            // World space, so a burst does not follow the enemy that spawned it — or,
            // once pooled, get dragged around by the pool root.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(count * 2, 32);
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;

            if (cone)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 22f;
                shape.radius = radius;
                shape.rotation = new Vector3(-90f, 0f, 0f);   // point the cone upward
            }
            else if (flat)
            {
                // A ground puff spreads outward, not upward.
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                shape.radiusThickness = 0f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
            }

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            colorOverLife.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            string path = $"{Folder}/{name}.prefab";
            GameObject asset = PlaceholderFactory.SavePrefab(go, path);

            report.AppendLine($"  {name,-18} {count,2} particles, {lifetime:0.00}s");

            return new FxCatalog.Entry { kind = kind, prefab = asset, prewarm = prewarm };
        }

        /// <summary>Alpha 1 to 0, colour untouched so the runtime tint still shows.</summary>
        private static Gradient FadeOutGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Material ParticleMaterial(string name, Color tint)
        {
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP will not render a particle system with a Lit material correctly;
            // the unlit particle shader is the one that respects vertex colour.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                Debug.LogWarning("[FxPrefabBuilder] URP particle shader not found; " +
                                 "falling back to Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) { name = name };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);

            // Additive-ish: bursts should brighten what is behind them rather than
            // punching an opaque hole in the scene.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);   // transparent
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);       // additive
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

            material.renderQueue = 3000;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void WriteCatalog(List<FxCatalog.Entry> entries, StringBuilder report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FxCatalog>(CatalogPath);
            bool created = catalog == null;

            if (created)
            {
                catalog = ScriptableObject.CreateInstance<FxCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            SerializedProperty array = so.FindProperty("entries");
            array.arraySize = entries.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("kind").enumValueIndex = (int)entries[i].kind;
                element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("prewarm").intValue = entries[i].prewarm;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            report.AppendLine($"  FxCatalog          {(created ? "created" : "updated")} " +
                              $"at {CatalogPath} ({entries.Count} kinds)");
        }
    }
}
