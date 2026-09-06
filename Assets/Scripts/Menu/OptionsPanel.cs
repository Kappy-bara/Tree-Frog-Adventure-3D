using UnityEngine;
using UnityEngine.UI;

namespace TreeFrogAdventure.Menu
{
    public class OptionsPanel : MonoBehaviour
    {
        public Slider volumeSlider;
        public Toggle muteToggle;

        private const string VolumeKey = "Options_MasterVolume";
        private const string MutedKey = "Options_Muted";

        private float _volumeBeforeMute = 1f;

        private void Awake()
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            muteToggle.onValueChanged.AddListener(OnMuteChanged);
        }

        private void OnEnable()
        {
            float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            bool muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;

            _volumeBeforeMute = savedVolume;
            AudioListener.volume = muted ? 0f : savedVolume;

            volumeSlider.SetValueWithoutNotify(savedVolume);
            muteToggle.SetIsOnWithoutNotify(muted);
        }

        private void OnVolumeChanged(float value)
        {
            _volumeBeforeMute = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            PlayerPrefs.Save();

            if (!muteToggle.isOn)
            {
                AudioListener.volume = value;
            }
        }

        private void OnMuteChanged(bool isMuted)
        {
            AudioListener.volume = isMuted ? 0f : _volumeBeforeMute;
            PlayerPrefs.SetInt(MutedKey, isMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
