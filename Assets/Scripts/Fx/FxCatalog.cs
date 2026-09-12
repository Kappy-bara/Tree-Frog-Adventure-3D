using System;
using UnityEngine;

namespace TreeFrogAdventure.Fx
{
    /// <summary>
    /// Maps every <see cref="FxKind"/> to the prefab that plays it.
    ///
    /// Lives in Resources rather than being wired per scene: effects are fired from
    /// twenty gameplay scenes and from prefabs that exist outside any scene, so a
    /// serialized reference would have to be re-established twenty times and would
    /// still be missing wherever an enemy is spawned at runtime.
    ///
    /// Built by Tools > Tree Frog > Build FX Prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "FxCatalog", menuName = "Tree Frog/FX Catalog")]
    public class FxCatalog : ScriptableObject
    {
        public const string ResourcePath = "FxCatalog";

        [Serializable]
        public struct Entry
        {
            public FxKind kind;
            public GameObject prefab;

            [Tooltip("Instances kept warm. Effects that fire in bursts (enemy deaths, " +
                     "coin pickups) want more than one.")]
            public int prewarm;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        public GameObject GetPrefab(FxKind kind)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind) return entries[i].prefab;
            }
            return null;
        }

        public int GetPrewarm(FxKind kind)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind) return Mathf.Max(0, entries[i].prewarm);
            }
            return 0;
        }
    }
}
