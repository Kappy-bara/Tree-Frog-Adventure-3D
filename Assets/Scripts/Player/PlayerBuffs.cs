using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeFrogAdventure.Player
{
    public enum BuffKind
    {
        SpeedBoost,
        JumpBoost,
        Invincibility,
    }

    /// <summary>
    /// Timed power-up effects. The project had no buff system at all — the only timed
    /// player-state change was PlayerHealth's post-hit invincibility.
    ///
    /// Two things this deliberately gets right:
    ///
    /// 1. Multipliers are pushed onto PlayerController, never into PlayerData.
    ///    PlayerData is a ScriptableObject, so a runtime write survives play mode and
    ///    the boost would still be applied the next time you pressed Play.
    ///
    /// 2. Re-applying a buff EXTENDS it rather than restarting a competing timer, so
    ///    picking up two Speed Boosts is additive instead of one silently ending the
    ///    other — the exact failure the old invincibility bool had.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerBuffs : MonoBehaviour
    {
        private readonly Dictionary<BuffKind, float> _expiry = new Dictionary<BuffKind, float>();

        private PlayerController _controller;
        private PlayerHealth _health;

        /// <summary>Raised when a buff starts, is extended, or ends (remaining = 0).</summary>
        public event Action<BuffKind, float> OnBuffChanged;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (_expiry.Count > 0) ExpireFinished();

            ApplyMultipliers();
        }

        // ------------------------------------------------------------------ api --

        /// <summary>Applies a buff for at least <paramref name="duration"/> seconds.</summary>
        public void Apply(BuffKind kind, float duration)
        {
            if (duration <= 0f) return;

            // Stack by adding remaining time, so a second pickup is never a downgrade.
            float remaining = Remaining(kind);
            float total = remaining + duration;
            _expiry[kind] = Time.time + total;

            if (kind == BuffKind.Invincibility && _health != null)
            {
                // Blink off: 10 seconds of flicker is unpleasant, and the post-hit
                // window is what the blink is meant to communicate.
                _health.SetInvincible(total, blink: false);
            }

            ApplyMultipliers();
            OnBuffChanged?.Invoke(kind, total);
        }

        public bool IsActive(BuffKind kind) =>
            _expiry.TryGetValue(kind, out float until) && Time.time < until;

        public float Remaining(BuffKind kind) =>
            _expiry.TryGetValue(kind, out float until) ? Mathf.Max(0f, until - Time.time) : 0f;

        /// <summary>Drops every buff. Called on respawn so death is a clean slate.</summary>
        public void ClearAll()
        {
            if (_expiry.Count > 0)
            {
                var kinds = new List<BuffKind>(_expiry.Keys);
                _expiry.Clear();

                foreach (BuffKind kind in kinds) OnBuffChanged?.Invoke(kind, 0f);
            }

            if (_health != null) _health.ClearInvincibility();

            ApplyMultipliers();
        }

        /// <summary>The buffs currently running, for the HUD.</summary>
        public IEnumerable<KeyValuePair<BuffKind, float>> ActiveBuffs()
        {
            foreach (var pair in _expiry)
            {
                float remaining = pair.Value - Time.time;
                if (remaining > 0f) yield return new KeyValuePair<BuffKind, float>(pair.Key, remaining);
            }
        }

        // -------------------------------------------------------------- internal --

        private void ExpireFinished()
        {
            List<BuffKind> finished = null;

            foreach (var pair in _expiry)
            {
                if (Time.time >= pair.Value)
                {
                    finished ??= new List<BuffKind>();
                    finished.Add(pair.Key);
                }
            }

            if (finished == null) return;

            foreach (BuffKind kind in finished)
            {
                _expiry.Remove(kind);
                OnBuffChanged?.Invoke(kind, 0f);
            }
        }

        private void ApplyMultipliers()
        {
            if (_controller == null) return;

            PlayerData data = _controller.Data;
            if (data == null) return;

            _controller.SpeedMultiplier = IsActive(BuffKind.SpeedBoost) ? data.speedBoostMultiplier : 1f;
            _controller.JumpMultiplier = IsActive(BuffKind.JumpBoost) ? data.jumpBoostMultiplier : 1f;
        }
    }
}
