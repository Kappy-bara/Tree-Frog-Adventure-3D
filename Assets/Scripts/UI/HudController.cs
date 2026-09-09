using TMPro;
using TreeFrogAdventure.Core;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.UI
{
    /// <summary>
    /// Drives the in-game HUD from GameSession.
    ///
    /// UICanvas.prefab has had LivesText / CoinsText / AmmoText since the project
    /// started with no code behind them and was instanced in no scene — dead FPS
    /// boilerplate. This is the working replacement.
    ///
    /// Lives, coins and flies come from events so they update the instant they change;
    /// health is polled because PlayerHealth has no change event and adding one would
    /// mean touching its damage path for no real benefit.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI livesText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI fliesText;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Formatting")]
        [SerializeField] private string livesFormat = "x{0}";
        [SerializeField] private string coinsFormat = "{0}";
        [SerializeField] private string fliesFormat = "{0}";
        [SerializeField] private string healthFormat = "{0}";

        private PlayerHealth _player;
        private int _lastHealth = -1;
        private float _playerSearchTimer;

        private void OnEnable()
        {
            GameSession.OnLivesChanged += SetLives;
            GameSession.OnCoinsChanged += SetCoins;
            GameSession.OnFliesChanged += SetFlies;

            // Push current values immediately — the events only fire on change, and
            // the HUD is usually created after the session already has state.
            SetLives(GameSession.Lives);
            SetCoins(GameSession.Coins);
            SetFlies(GameSession.Flies);
        }

        private void OnDisable()
        {
            GameSession.OnLivesChanged -= SetLives;
            GameSession.OnCoinsChanged -= SetCoins;
            GameSession.OnFliesChanged -= SetFlies;
        }

        private void Update()
        {
            if (healthText == null) return;

            if (_player == null)
            {
                _playerSearchTimer -= Time.unscaledDeltaTime;
                if (_playerSearchTimer > 0f) return;

                _playerSearchTimer = 0.5f;
                _player = FindAnyObjectByType<PlayerHealth>();
                if (_player == null) return;
            }

            if (_player.CurrentHealth != _lastHealth)
            {
                _lastHealth = _player.CurrentHealth;
                healthText.text = string.Format(healthFormat, _lastHealth);
            }
        }

        private void SetLives(int value)
        {
            if (livesText != null) livesText.text = string.Format(livesFormat, value);
        }

        private void SetCoins(int value)
        {
            if (coinsText != null) coinsText.text = string.Format(coinsFormat, value);
        }

        private void SetFlies(int value)
        {
            if (fliesText != null) fliesText.text = string.Format(fliesFormat, value);
        }
    }
}
