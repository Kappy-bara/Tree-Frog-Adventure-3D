using TreeFrogAdventure.Core;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    public enum CollectibleKind
    {
        Coin,
        Fly,
        ExtraLife,
        Health,

        // Timed power-ups. These are handed to PlayerBuffs on the player rather than
        // run here: Apply() is followed immediately by Destroy(gameObject), so a
        // coroutine started on the pickup would die with it.
        SpeedBoost,
        JumpBoost,
        Invincibility,

        /// <summary>The story collectible — one hidden per level.</summary>
        NatureCrystal,
    }

    /// <summary>
    /// A pickup. Coin_1.prefab has had a "Collectible" tag and a trigger collider since
    /// the project started but never had a script, so nothing could ever be collected —
    /// this is that missing half.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private CollectibleKind kind = CollectibleKind.Coin;
        [SerializeField] private int amount = 1;

        [Header("Idle motion")]
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bobAmount = 0.18f;
        [SerializeField] private float bobSpeed = 2f;

        [Tooltip("Optional burst spawned where it was collected.")]
        [SerializeField] private GameObject collectEffectPrefab;

        private Vector3 _basePosition;
        private bool _collected;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            _basePosition = transform.position;

            // Randomised so a row of coins does not pulse in lockstep.
            _basePosition.y += Random.Range(0f, 0.01f);
        }

        private void Update()
        {
            if (spinSpeed != 0f)
            {
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            }

            if (bobAmount != 0f)
            {
                float offset = Mathf.Sin(Time.time * bobSpeed + _basePosition.x) * bobAmount;
                transform.position = _basePosition + Vector3.up * offset;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;

            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null && !other.CompareTag("Player")) return;

            _collected = true;
            Apply(player);

            if (collectEffectPrefab != null)
            {
                Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        private void Apply(PlayerHealth player)
        {
            PlayerController controller = player != null ? player.GetComponent<PlayerController>() : null;
            PlayerData data = controller != null ? controller.Data : null;
            PlayerBuffs buffs = player != null ? player.GetComponent<PlayerBuffs>() : null;

            switch (kind)
            {
                case CollectibleKind.Coin:
                    GameSession.AddCoins(amount);
                    break;

                case CollectibleKind.Fly:
                {
                    // Cap comes from PlayerData so ammo capacity lives in one place.
                    int max = data != null ? data.maxFlies : 10;
                    GameSession.AddFlies(amount, max);
                    break;
                }

                case CollectibleKind.ExtraLife:
                    GameSession.AddLife(amount);
                    break;

                case CollectibleKind.Health:
                    if (player != null) player.Heal(amount);
                    break;

                case CollectibleKind.SpeedBoost:
                    if (buffs != null && data != null)
                        buffs.Apply(BuffKind.SpeedBoost, data.speedBoostDuration);
                    break;

                case CollectibleKind.JumpBoost:
                    if (buffs != null && data != null)
                        buffs.Apply(BuffKind.JumpBoost, data.jumpBoostDuration);
                    break;

                case CollectibleKind.Invincibility:
                    if (buffs != null && data != null)
                        buffs.Apply(BuffKind.Invincibility, data.invincibilityPowerUpDuration);
                    break;

                case CollectibleKind.NatureCrystal:
                    GameSession.AddCrystal(amount);
                    // Crystals are the story item, so record them immediately rather
                    // than waiting for the level to be completed — finding one and then
                    // dying should not lose it.
                    SaveSystem.RecordCrystal(LevelContext.Current != null
                        ? LevelContext.Current.LevelNumber
                        : 0);
                    break;
            }
        }
    }
}
