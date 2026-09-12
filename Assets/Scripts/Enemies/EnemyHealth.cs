using System;
using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>How an enemy was hurt. Some enemies resist some kinds.</summary>
    public enum DamageKind
    {
        Generic,
        Stomp,
        Projectile,
        Environment,
    }

    /// <summary>
    /// Hit points for an enemy, and the only way anything in the game can kill one.
    ///
    /// Before this existed, DamageSource was one-directional — enemies could hurt the
    /// player and nothing could hurt them back.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 1;

        [Header("Stomping")]
        [Tooltip("Can the player kill this by landing on it? Turn off for spiky or " +
                 "armoured enemies — the player takes contact damage instead.")]
        [SerializeField] private bool stompable = true;

        [Tooltip("Damage a single stomp deals.")]
        [SerializeField] private int stompDamage = 1;

        [Header("Death")]
        [Tooltip("Optional pickup spawned where the enemy died (coin, fly, extra life).")]
        [SerializeField] private GameObject dropPrefab;

        [Tooltip("Seconds the corpse lingers before despawning, for a death animation.")]
        [SerializeField] private float despawnDelay = 0.35f;

        public int CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Starting hit points. Needed to draw a health bar as a fraction.</summary>
        public int MaxHealth => Mathf.Max(1, maxHealth);

        /// <summary>How long the corpse lingers, so death effects can match it.</summary>
        public float DespawnDelay => Mathf.Max(0f, despawnDelay);

        /// <summary>
        /// Settable so a boss can open and close a stomp window between phases. The
        /// serialized field remains the authored default.
        /// </summary>
        public bool Stompable
        {
            get => stompable;
            set => stompable = value;
        }

        /// <summary>Fired once, when health reaches zero.</summary>
        public event Action<EnemyHealth, DamageKind> OnDeath;

        /// <summary>Fired on any damage that actually landed, including the fatal one.</summary>
        public event Action<EnemyHealth, int, DamageKind> OnDamaged;

        private void Awake()
        {
            CurrentHealth = Mathf.Max(1, maxHealth);
        }

        /// <summary>Returns true if the damage landed (false if already dead).</summary>
        public bool TakeDamage(int amount, DamageKind kind = DamageKind.Generic)
        {
            if (IsDead || amount <= 0) return false;

            CurrentHealth -= amount;
            OnDamaged?.Invoke(this, amount, kind);

            if (CurrentHealth <= 0)
            {
                Die(kind);
            }

            return true;
        }

        /// <summary>
        /// Called by PlayerStomp when the player lands on top.
        ///
        /// Returns whether the stomp CONNECTED, not whether it killed — the player
        /// bounces off a surviving enemy too. False means this enemy cannot be
        /// stomped, and the caller should let normal contact damage happen instead.
        /// </summary>
        public bool TryStomp()
        {
            if (IsDead || !stompable) return false;

            TakeDamage(stompDamage, DamageKind.Stomp);
            return true;
        }

        /// <summary>Bypasses hit points entirely — pits, lava, crushers.</summary>
        public void Kill(DamageKind kind = DamageKind.Environment)
        {
            if (IsDead) return;

            CurrentHealth = 0;
            Die(kind);
        }

        private void Die(DamageKind kind)
        {
            if (IsDead) return;
            IsDead = true;

            OnDeath?.Invoke(this, kind);

            if (dropPrefab != null)
            {
                Instantiate(dropPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            // Stop hurting the player the instant it dies, even while the corpse is
            // still visible during the despawn delay.
            foreach (var damage in GetComponentsInChildren<TreeFrogAdventure.Player.DamageSource>(true))
            {
                damage.enabled = false;
            }
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            Destroy(gameObject, Mathf.Max(0f, despawnDelay));
        }
    }
}
