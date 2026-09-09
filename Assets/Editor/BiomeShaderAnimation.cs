using UnityEditor;
using UnityEngine;

namespace TreeFrogAdventure.EditorTools
{
    /// <summary>
    /// Freezes the biome shaders' animation clock while you are editing, and
    /// releases it in play mode.
    ///
    /// Unity's _Time keeps advancing in the editor, so wind, waves, lava and
    /// clouds animate in the Scene view every time it repaints - which it does on
    /// any interaction, not just when "Always Refresh" is on. Turning that toggle
    /// off does not stop it. This does.
    ///
    /// Drives the global _TFA_FreezeAnim that TFA_AnimTime() reads. The polarity
    /// is fail-safe: unset reads as 0 = "animate normally", so builds are never
    /// affected by this script even though it is editor-only.
    ///
    /// Toggle: Tools > Tree Frog > Freeze Shader Animation In Edit Mode
    /// </summary>
    [InitializeOnLoad]
    public static class BiomeShaderAnimation
    {
        private const string MenuPath = "Tools/Tree Frog/Freeze Shader Animation In Edit Mode";
        private const string PrefKey = "TreeFrogAdventure.FreezeShaderAnimation";

        private static readonly int FreezeId = Shader.PropertyToID("_TFA_FreezeAnim");

        // -1 = nothing pushed yet, so the first Apply always writes.
        private static float _lastPushed = -1f;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static BiomeShaderAnimation()
        {
            EditorApplication.update += Apply;
            EditorApplication.playModeStateChanged += _ => Apply();
            Apply();
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();

            // Repaint so the change is visible immediately rather than on the
            // next time something else happens to invalidate the view.
            foreach (SceneView view in SceneView.sceneViews)
            {
                view.Repaint();
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            // Fully qualified: this project has its own TreeFrogAdventure.Menu
            // namespace, which otherwise shadows UnityEditor.Menu.
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            // Play mode always animates, regardless of the toggle - otherwise you
            // could not see the wind while testing.
            bool freeze = Enabled && !EditorApplication.isPlayingOrWillChangePlaymode;
            float value = freeze ? 1f : 0f;

            // Runs on every editor tick, so only touch the global when it changes.
            if (Mathf.Approximately(value, _lastPushed))
            {
                return;
            }

            _lastPushed = value;
            Shader.SetGlobalFloat(FreezeId, value);
        }
    }
}
