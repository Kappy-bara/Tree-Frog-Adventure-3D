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
        [SerializeField] private TextMeshProUGUI crystalsText;

        [Tooltip("Shows active power-ups and their remaining seconds.")]
        [SerializeField] private TextMeshProUGUI buffsText;

        [Header("Formatting")]
        [SerializeField] private string livesFormat = "x{0}";
        [SerializeField] private string coinsFormat = "{0}";
        [SerializeField] private string fliesFormat = "{0}";
        [SerializeField] private string healthFormat = "{0}";
        [SerializeField] private string crystalsFormat = "{0}";

        private PlayerHealth _player;
        private PlayerBuffs _buffs;
        private int _lastHealth = -1;
        private float _playerSearchTimer;
        private readonly System.Text.StringBuilder _buffLine = new System.Text.StringBuilder();

        private void OnEnable()
        {
            GameSession.OnLivesChanged += SetLives;
            GameSession.OnCoinsChanged += SetCoins;
            GameSession.OnFliesChanged += SetFlies;
            GameSession.OnCrystalsChanged += SetCrystals;

            // Push current values immediately — the events only fire on change, and
            // the HUD is usually created after the session already has state.
            SetLives(GameSession.Lives);
            SetCoins(GameSession.Coins);
            SetFlies(GameSession.Flies);
            SetCrystals(GameSession.Crystals);
        }

        private void OnDisable()
        {
            GameSession.OnLivesChanged -= SetLives;
            GameSession.OnCoinsChanged -= SetCoins;
            GameSession.OnFliesChanged -= SetFlies;
            GameSession.OnCrystalsChanged -= SetCrystals;
        }

        private void Update()
        {
            if (_player == null)
            {
                _playerSearchTimer -= Time.unscaledDeltaTime;
                if (_playerSearchTimer > 0f) return;

                _playerSearchTimer = 0.5f;
                _player = FindAnyObjectByType<PlayerHealth>();
                if (_player == null) return;

                _buffs = _player.GetComponent<PlayerBuffs>();
            }

            if (healthText != null && _player.CurrentHealth != _lastHealth)
            {
                _lastHealth = _player.CurrentHealth;
                healthText.text = string.Format(healthFormat, _lastHealth);
            }

            UpdateBuffs();
        }

        /// <summary>
        /// Buffs are polled rather than event-driven because the countdown has to tick
        /// every frame anyway — an event would only tell us when one starts or ends.
        /// </summary>
        private void UpdateBuffs()
        {
            if (buffsText == null || _buffs == null) return;

            _buffLine.Clear();

            foreach (var buff in _buffs.ActiveBuffs())
            {
                if (_buffLine.Length > 0) _buffLine.Append("   ");
                _buffLine.Append(Label(buff.Key)).Append(' ').Append(Mathf.CeilToInt(buff.Value)).Append('s');
            }

            buffsText.text = _buffLine.ToString();
        }

        private static string Label(BuffKind kind) => kind switch
        {
            BuffKind.SpeedBoost => "SPEED",
            BuffKind.JumpBoost => "JUMP",
            BuffKind.Invincibility => "INVINCIBLE",
            _ => kind.ToString(),
        };

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

        private void SetCrystals(int value)
        {
            if (crystalsText != null) crystalsText.text = string.Format(crystalsFormat, value);
        }
    }
}
