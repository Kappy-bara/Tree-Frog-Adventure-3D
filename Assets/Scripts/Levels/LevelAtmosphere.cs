using UnityEngine;
using UnityEngine.Rendering;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// Per-level lighting override, applied on top of the shared biome
    /// environment scene.
    ///
    /// All five levels of a world share one map, so changing the light is the
    /// cheapest way to stop them reading as the same place five times: level 1 at
    /// dawn, level 3 under storm haze, level 5 at night. That shifts the mood far
    /// more than moving enemies around does.
    ///
    /// Lives in the additively-loaded gameplay scene. It works because fog,
    /// ambient and the sun are global render state - the environment scene sets
    /// them when it loads as the primary scene, and this overwrites them
    /// afterwards in Start().
    /// </summary>
    public class LevelAtmosphere : MonoBehaviour
    {
        [Header("Fog")]
        [SerializeField] private bool overrideFog = true;
        [SerializeField] private Color fogColor = new Color(0.6f, 0.7f, 0.75f);
        [SerializeField] private FogMode fogMode = FogMode.Exponential;
        [SerializeField] private float fogDensity = 0.002f;

        [Header("Ambient")]
        [SerializeField] private bool overrideAmbient = true;
        [SerializeField] private Color ambientSky = new Color(0.55f, 0.7f, 0.9f);
        [SerializeField] private Color ambientEquator = new Color(0.45f, 0.55f, 0.65f);
        [SerializeField] private Color ambientGround = new Color(0.2f, 0.22f, 0.25f);
        [SerializeField] private float ambientIntensity = 1f;

        [Header("Sun")]
        [SerializeField] private bool overrideSun = true;
        [SerializeField] private Color sunColor = Color.white;
        [SerializeField] private float sunIntensity = 1.2f;
        [SerializeField] private Vector3 sunRotation = new Vector3(45f, 40f, 0f);

        [Header("Post-processing (optional)")]
        [Tooltip("Assigned to the environment's global Volume. Leave empty to keep the biome's own profile.")]
        [SerializeField] private VolumeProfile volumeProfile;

        private void Start()
        {
            Apply();
        }

        /// <summary>Public so an editor tool or a cutscene can re-apply it.</summary>
        public void Apply()
        {
            if (overrideFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }

            if (overrideAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.ambientIntensity = ambientIntensity;
            }

            if (overrideSun)
            {
                Light sun = FindMainDirectionalLight();
                if (sun != null)
                {
                    sun.color = sunColor;
                    sun.intensity = sunIntensity;
                    sun.transform.rotation = Quaternion.Euler(sunRotation);
                    RenderSettings.sun = sun;
                }
            }

            if (volumeProfile != null)
            {
                foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                {
                    if (!volume.isGlobal) continue;
                    // sharedProfile swaps the reference; .profile would clone the
                    // asset every load and leak instances.
                    volume.sharedProfile = volumeProfile;
                }
            }
        }

        /// <summary>
        /// The sun lives in the environment scene, not this one, so search across
        /// all loaded scenes rather than this GameObject's hierarchy.
        /// </summary>
        private static Light FindMainDirectionalLight()
        {
            Light best = null;

            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }

            return best;
        }
    }
}
