using System;
using UnityEngine;

namespace TreeFrogAdventure.Menu
{
    /// <summary>
    /// Player-facing settings, backed by PlayerPrefs.
    ///
    /// This exists because settings were previously applied in OptionsPanel.OnEnable —
    /// meaning a saved volume was only restored once the player opened the options
    /// screen. Launch the game and drop straight into a level and your setting was
    /// silently ignored until you went looking for it. Applying from
    /// RuntimeInitializeOnLoadMethod fixes that for every entry point at once.
    ///
    /// Camera sensitivity lived as inspector-only fields on CameraController with no
    /// way for a player to change them and nothing that persisted them.
    ///
    /// The two original keys are kept verbatim so existing players keep their volume.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "Options_MasterVolume";
        private const string MutedKey = "Options_Muted";
        private const string MouseSensitivityKey = "Options_MouseSensitivity";
        private const string GamepadSensitivityKey = "Options_GamepadSensitivity";
        private const string InvertYKey = "Options_InvertY";

        public const float DefaultMouseSensitivity = 0.1f;
        public const float DefaultGamepadSensitivity = 220f;

        // Sliders run 0..1; these map that range onto usable sensitivities. A linear
        // 0..1 slider straight into the raw value would make the bottom half unusable.
        public const float MinMouseSensitivity = 0.02f;
        public const float MaxMouseSensitivity = 0.4f;
        public const float MinGamepadSensitivity = 60f;
        public const float MaxGamepadSensitivity = 500f;

        private static bool _loaded;

        private static float _masterVolume = 1f;
        private static bool _muted;
        private static float _mouseSensitivity = DefaultMouseSensitivity;
        private static float _gamepadSensitivity = DefaultGamepadSensitivity;
        private static bool _invertY;

        /// <summary>Raised whenever any setting changes, so live systems can re-read.</summary>
        public static event Action OnChanged;

        // ------------------------------------------------------------ lifecycle --

        /// <summary>
        /// Applied before the first scene loads, so settings are live no matter which
        /// scene the game starts in — including pressing Play directly on a level.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            Load();
            AudioListener.volume = _muted ? 0f : _masterVolume;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            _loaded = false;
            OnChanged = null;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            _mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity);
            _gamepadSensitivity = PlayerPrefs.GetFloat(GamepadSensitivityKey, DefaultGamepadSensitivity);
            _invertY = PlayerPrefs.GetInt(InvertYKey, 0) == 1;
        }

        // -------------------------------------------------------------- settings --

        public static float MasterVolume
        {
            get { Load(); return _masterVolume; }
            set
            {
                Load();
                _masterVolume = Mathf.Clamp01(value);

                PlayerPrefs.SetFloat(VolumeKey, _masterVolume);
                PlayerPrefs.Save();

                if (!_muted) AudioListener.volume = _masterVolume;
                OnChanged?.Invoke();
            }
        }

        public static bool Muted
        {
            get { Load(); return _muted; }
            set
            {
                Load();
                _muted = value;

                PlayerPrefs.SetInt(MutedKey, _muted ? 1 : 0);
                PlayerPrefs.Save();

                AudioListener.volume = _muted ? 0f : _masterVolume;
                OnChanged?.Invoke();
            }
        }

        public static float MouseSensitivity
        {
            get { Load(); return _mouseSensitivity; }
            set
            {
                Load();
                _mouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);

                PlayerPrefs.SetFloat(MouseSensitivityKey, _mouseSensitivity);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static float GamepadSensitivity
        {
            get { Load(); return _gamepadSensitivity; }
            set
            {
                Load();
                _gamepadSensitivity = Mathf.Clamp(value, MinGamepadSensitivity, MaxGamepadSensitivity);

                PlayerPrefs.SetFloat(GamepadSensitivityKey, _gamepadSensitivity);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        public static bool InvertY
        {
            get { Load(); return _invertY; }
            set
            {
                Load();
                _invertY = value;

                PlayerPrefs.SetInt(InvertYKey, _invertY ? 1 : 0);
                PlayerPrefs.Save();
                OnChanged?.Invoke();
            }
        }

        // ------------------------------------------------------ slider mapping --

        /// <summary>Slider 0..1 for the current mouse sensitivity.</summary>
        public static float MouseSensitivityNormalised
        {
            get => Mathf.InverseLerp(MinMouseSensitivity, MaxMouseSensitivity, MouseSensitivity);
            set => MouseSensitivity = Mathf.Lerp(MinMouseSensitivity, MaxMouseSensitivity, Mathf.Clamp01(value));
        }

        /// <summary>Slider 0..1 for the current gamepad sensitivity.</summary>
        public static float GamepadSensitivityNormalised
        {
            get => Mathf.InverseLerp(MinGamepadSensitivity, MaxGamepadSensitivity, GamepadSensitivity);
            set => GamepadSensitivity = Mathf.Lerp(MinGamepadSensitivity, MaxGamepadSensitivity, Mathf.Clamp01(value));
        }
    }
}
