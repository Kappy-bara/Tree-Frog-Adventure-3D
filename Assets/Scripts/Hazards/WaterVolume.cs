using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// Marks a trigger volume as swimmable water.
    ///
    /// Follows the VineClimbable pattern: a marker component that forces its collider to
    /// be a trigger, so a designer cannot accidentally ship a water body the player
    /// walks on. That is exactly what shipped before this — UNS_Water_Flat had a solid
    /// MeshCollider, so every pond in the game was a floor.
    ///
    /// The surface height matters more than the collider's top face: the trigger box is
    /// deliberately extended downward so submersion can be measured, which means its
    /// bounds.max.y is NOT where the water visually sits.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaterVolume : MonoBehaviour
    {
        [Tooltip("World Y of the visible surface. Auto-filled from the renderer bounds " +
                 "on Awake when left at the default.")]
        [SerializeField] private float surfaceHeight = float.NaN;

        [Tooltip("Treat as lava/acid: touching it kills instead of letting the player swim.")]
        [SerializeField] private bool isDeadly;

        [Tooltip("How buoyant this body is. 1 = normal water; lower for thin/marshy water " +
                 "so swamp levels can feel heavier than alpine pools.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float buoyancyScale = 1f;

        public bool IsDeadly => isDeadly;
        public float BuoyancyScale => buoyancyScale;

        /// <summary>World Y of the water surface.</summary>
        public float SurfaceHeight { get; private set; }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }

            if (float.IsNaN(surfaceHeight))
            {
                // The trigger extends below the surface, so its own bounds are wrong.
                // Prefer the renderer, which is the visible water plane.
                var renderer = GetComponentInChildren<Renderer>();
                SurfaceHeight = renderer != null
                    ? renderer.bounds.max.y
                    : (col != null ? col.bounds.max.y : transform.position.y);
            }
            else
            {
                SurfaceHeight = surfaceHeight;
            }
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
