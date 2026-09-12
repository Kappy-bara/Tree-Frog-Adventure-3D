using System.Collections;
using TreeFrogAdventure.Enemies;
using TreeFrogAdventure.Player;
using TreeFrogAdventure.UI;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// Seals the arena, wakes the guardian, and opens the way out once it is beaten.
    ///
    /// The guardian starts inactive and the LevelGoal starts disabled, so walking into
    /// a boss level does not let you stroll past the fight to the exit. Both are
    /// switched on by this trigger and by the guardian's defeat respectively.
    ///
    /// The barrier is a plain GameObject the level author fills with colliders — the
    /// level scenes already use invisible boxes on the Barrier layer for their outer
    /// walls, so this is the same idiom rather than a new one.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossArena : MonoBehaviour
    {
        [Header("Fight")]
        [SerializeField] private BossBase boss;

        [Tooltip("Enabled while the fight is running, disabled once it is won.")]
        [SerializeField] private GameObject barriers;

        [Header("Reward")]
        [Tooltip("Spawned where the guardian fell. The level's Nature Crystal.")]
        [SerializeField] private GameObject crystalPrefab;

        [SerializeField] private float crystalHeight = 1.2f;

        [Tooltip("Enabled once the guardian is defeated. Disabled at load so the exit " +
                 "cannot be reached without fighting.")]
        [SerializeField] private GameObject levelGoal;

        [Header("Presentation")]
        [SerializeField] private float introHold = 2f;
        [SerializeField] private float victoryHold = 2.5f;

        private BossHealthBar _bar;
        private bool _started;
        private bool _finished;

        /// <summary>The guardian, while there is still a fight to point the player at.</summary>
        public Transform BossTarget => !_finished && boss != null ? boss.transform : null;

        /// <summary>True before the fight has been entered, so the compass can lead
        /// the player to the arena rather than to the guardian standing inside it.</summary>
        public bool AwaitingPlayer => !_started;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            if (barriers != null) barriers.SetActive(false);
            if (levelGoal != null) levelGoal.SetActive(false);

            if (boss != null) boss.OnDefeated += HandleDefeated;
        }

        private void OnDestroy()
        {
            if (boss != null) boss.OnDefeated -= HandleDefeated;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_started) return;
            if (other.GetComponentInParent<PlayerHealth>() == null && !other.CompareTag("Player")) return;

            BeginFight();
        }

        public void BeginFight()
        {
            if (_started) return;
            _started = true;

            if (barriers != null) barriers.SetActive(true);

            if (boss == null)
            {
                // A boss level with no guardian wired would otherwise seal the player
                // into an arena with no way to open it again.
                Debug.LogWarning("[BossArena] No boss assigned; opening the exit immediately.", this);
                OpenExit();
                return;
            }

            boss.Activate();
            _bar = BossHealthBar.Show(boss);

            var banner = FindAnyObjectByType<LevelBanner>();
            if (banner != null) banner.ShowMessage(boss.BossName, boss.BossTitle, introHold);
        }

        private void HandleDefeated(BossBase defeated)
        {
            if (_finished) return;
            _finished = true;

            if (_bar != null) _bar.Dismiss();

            // Position is captured now: EnemyHealth destroys the guardian after its
            // despawn delay, so reading it later would be reading a dead reference.
            Vector3 where = defeated.transform.position;

            StartCoroutine(Victory(where));
        }

        private IEnumerator Victory(Vector3 where)
        {
            var banner = FindAnyObjectByType<LevelBanner>();
            if (banner != null) banner.ShowMessage("Guardian Defeated", "The crystal is yours", victoryHold);

            yield return new WaitForSeconds(0.6f);

            if (crystalPrefab != null)
            {
                Instantiate(crystalPrefab, where + Vector3.up * crystalHeight, Quaternion.identity);
            }

            OpenExit();
        }

        private void OpenExit()
        {
            if (barriers != null) barriers.SetActive(false);
            if (levelGoal != null) levelGoal.SetActive(true);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.2f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

            if (boss != null)
            {
                Gizmos.DrawLine(col.bounds.center, boss.transform.position);
            }
        }
    }
}
