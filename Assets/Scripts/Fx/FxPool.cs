using System.Collections.Generic;
using UnityEngine;

namespace TreeFrogAdventure.Fx
{
    /// <summary>
    /// One-shot particle effects, pooled.
    ///
    /// Pooled rather than Instantiate/Destroy because the calls come in bursts — a
    /// stomped enemy, a row of coins, a boss changing phase — and per-effect allocation
    /// is exactly the pattern that produces GC spikes mid-fight.
    ///
    /// Every entry point is null-safe: with no catalog built, or a kind left unassigned,
    /// FxPool does nothing rather than throwing. Effects are decoration, and decoration
    /// must never be able to break a level.
    /// </summary>
    public static class FxPool
    {
        private class Live
        {
            public GameObject Instance;
            public ParticleSystem System;
            public FxKind Kind;
            public float ReleaseAt;
        }

        private const float FallbackLifetime = 2f;

        private static FxCatalog _catalog;
        private static bool _catalogLoaded;
        private static Transform _root;

        private static readonly Dictionary<FxKind, Stack<GameObject>> Idle = new();
        private static readonly List<Live> Active = new();

        /// <summary>
        /// Statics survive between play sessions when domain reload is disabled, and
        /// every pooled GameObject from the previous session is destroyed — so the pool
        /// would otherwise hand out references to dead objects.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            _catalog = null;
            _catalogLoaded = false;
            _root = null;
            Idle.Clear();
            Active.Clear();
        }

        // ---------------------------------------------------------------- api --

        public static void Play(FxKind kind, Vector3 position)
        {
            Play(kind, position, Quaternion.identity);
        }

        public static void Play(FxKind kind, Vector3 position, Quaternion rotation)
        {
            GameObject instance = Take(kind);
            if (instance == null) return;

            instance.transform.SetPositionAndRotation(position, rotation);
            Begin(kind, instance, null);
        }

        /// <summary>Tinted variant — coins spark gold, crystals spark green.</summary>
        public static void Play(FxKind kind, Vector3 position, Color tint)
        {
            GameObject instance = Take(kind);
            if (instance == null) return;

            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            Begin(kind, instance, tint);
        }

        // ------------------------------------------------------------ internals --

        private static void Begin(FxKind kind, GameObject instance, Color? tint)
        {
            instance.SetActive(true);

            var system = instance.GetComponentInChildren<ParticleSystem>(true);
            if (system != null)
            {
                if (tint.HasValue)
                {
                    ParticleSystem.MainModule main = system.main;
                    main.startColor = tint.Value;
                }

                system.Clear(true);
                system.Play(true);
            }

            Active.Add(new Live
            {
                Instance = instance,
                System = system,
                Kind = kind,
                ReleaseAt = Time.time + Lifetime(system),
            });
        }

        private static GameObject Take(FxKind kind)
        {
            EnsureCatalog();
            if (_catalog == null) return null;

            GameObject prefab = _catalog.GetPrefab(kind);
            if (prefab == null) return null;

            EnsureRoot();

            if (Idle.TryGetValue(kind, out Stack<GameObject> stack))
            {
                while (stack.Count > 0)
                {
                    GameObject pooled = stack.Pop();

                    // A pooled instance can still have been destroyed out from under
                    // the pool; skip it rather than handing back a dead reference.
                    if (pooled != null) return pooled;
                }
            }

            GameObject created = Object.Instantiate(prefab, _root);
            created.name = prefab.name;
            created.SetActive(false);
            return created;
        }

        private static void Release(Live live)
        {
            if (live.Instance == null) return;

            if (live.System != null)
            {
                live.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            live.Instance.SetActive(false);

            if (!Idle.TryGetValue(live.Kind, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                Idle[live.Kind] = stack;
            }
            stack.Push(live.Instance);
        }

        /// <summary>Called once per frame by <see cref="FxRunner"/>.</summary>
        internal static void Tick()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Live live = Active[i];

                if (live.Instance == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (Time.time < live.ReleaseAt) continue;

                Release(live);
                Active.RemoveAt(i);
            }
        }

        private static float Lifetime(ParticleSystem system)
        {
            if (system == null) return FallbackLifetime;

            ParticleSystem.MainModule main = system.main;
            return main.duration + main.startLifetime.constantMax;
        }

        private static void EnsureCatalog()
        {
            if (_catalogLoaded) return;
            _catalogLoaded = true;

            _catalog = Resources.Load<FxCatalog>(FxCatalog.ResourcePath);

            if (_catalog == null)
            {
                Debug.Log("[FxPool] No FxCatalog in Resources; effects are disabled. " +
                          "Run Tools > Tree Frog > Build FX Prefabs.");
            }
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;

            var go = new GameObject("~FxPool");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FxRunner>();
            _root = go.transform;

            if (_catalog == null) return;

            // Prewarm only after the root exists, so instances are parented into it
            // and survive the scene load rather than being collected with the level.
            foreach (FxCatalog.Entry entry in _catalog.Entries)
            {
                if (entry.prefab == null || entry.prewarm <= 0) continue;

                if (!Idle.TryGetValue(entry.kind, out Stack<GameObject> stack))
                {
                    stack = new Stack<GameObject>();
                    Idle[entry.kind] = stack;
                }

                for (int i = 0; i < entry.prewarm; i++)
                {
                    GameObject instance = Object.Instantiate(entry.prefab, _root);
                    instance.name = entry.prefab.name;
                    instance.SetActive(false);
                    stack.Push(instance);
                }
            }
        }
    }

    /// <summary>Drives <see cref="FxPool.Tick"/>. Created by the pool; never added by hand.</summary>
    [DisallowMultipleComponent]
    public class FxRunner : MonoBehaviour
    {
        private void Update() => FxPool.Tick();
    }
}
