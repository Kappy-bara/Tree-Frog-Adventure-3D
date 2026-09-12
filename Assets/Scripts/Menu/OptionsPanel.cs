using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Wires the options controls to <see cref="GameSettings"/>.
    ///
    /// This class used to own the settings themselves — reading PlayerPrefs and writing
    /// AudioListener.volume in OnEnable, which meant a saved volume was not applied
    /// until the player opened this screen. It is now purely the view: GameSettings
    /// applies everything at launch, and this reflects and edits it.
    ///
    /// Every control is optional, so the same component drives both the main-menu panel
    /// and the pause panel even if one of them is built with fewer rows.
    /// </summary>
    public class OptionsPanel : MonoBehaviour
    {
        public Slider volumeSlider;
        public Toggle muteToggle;

        [Header("Camera")]
        public Slider mouseSensitivitySlider;
        public Slider gamepadSensitivitySlider;
        public Toggle invertYToggle;

        private bool _wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (muteToggle != null) muteToggle.onValueChanged.AddListener(OnMuteChanged);

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);

            if (gamepadSensitivitySlider != null)
                gamepadSensitivitySlider.onValueChanged.AddListener(OnGamepadSensitivityChanged);

            if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        }

        private void OnEnable()
        {
            // Awake does not run on a panel that starts disabled and is only ever
            // enabled by the menu, so wiring is idempotent and repeated here.
            Wire();
            Refresh();
        }

        /// <summary>Pushes current settings into the controls without firing callbacks.</summary>
        private void Refresh()
        {
            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(GameSettings.Muted);

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivityNormalised);

            if (gamepadSensitivitySlider != null)
                gamepadSensitivitySlider.SetValueWithoutNotify(GameSettings.GamepadSensitivityNormalised);

            if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(GameSettings.InvertY);
        }

        private static void OnVolumeChanged(float value) => GameSettings.MasterVolume = value;
        private static void OnMuteChanged(bool value) => GameSettings.Muted = value;
        private static void OnMouseSensitivityChanged(float v) => GameSettings.MouseSensitivityNormalised = v;
        private static void OnGamepadSensitivityChanged(float v) => GameSettings.GamepadSensitivityNormalised = v;
        private static void OnInvertYChanged(bool value) => GameSettings.InvertY = value;
    }
}
