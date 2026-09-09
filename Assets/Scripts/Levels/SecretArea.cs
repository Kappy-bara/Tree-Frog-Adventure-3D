using TreeFrogAdventure.Core;
using TreeFrogAdventure.Player;
using UnityEngine;

namespace TreeFrogAdventure.Levels
{
    /// <summary>
    /// A hidden area that reveals itself when the player finds it.
    ///
    /// The reveal is deliberately two-sided: something concealing is hidden (a fake
    /// wall, a waterfall) and something hidden is shown (the reward alcove). Either
    /// side is optional, so this works both for "break through" and "walk behind"
    /// secrets.
    ///
    /// Discovery is one-way and counted in GameSession, then written to the save when
    /// the level is completed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SecretArea : MonoBehaviour
    {
        [Tooltip("Hidden on discovery — the fake wall or vines covering the entrance.")]
        [SerializeField] private GameObject concealer;

        [Tooltip("Shown on discovery — the reward alcove and its contents.")]
        [SerializeField] private GameObject reveal;

        [Tooltip("Show a banner when found. Off for secrets you would rather keep quiet.")]
        [SerializeField] private bool announce = true;

        [Tooltip("Seconds the concealer takes to fade out. 0 disables it instantly.")]
        [SerializeField] private float fadeDuration = 0.4f;

        private bool _found;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            if (reveal != null) reveal.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_found) return;
            if (other.GetComponentInParent<PlayerHealth>() == null && !other.CompareTag("Player")) return;

            Discover();
        }

        public void Discover()
        {
            if (_found) return;
            _found = true;

            if (reveal != null) reveal.SetActive(true);

            if (concealer != null)
            {
                if (fadeDuration > 0f) StartCoroutine(FadeOutConcealer());
                else concealer.SetActive(false);
            }

            GameSession.AddSecret();

            if (announce)
            {
                var banner = FindAnyObjectByType<LevelBanner>();
                if (banner != null) banner.ShowMessage("Secret Found!", string.Empty, 1.4f);
            }
        }

        /// <summary>
        /// Fades the concealer's materials out before disabling it. Instance materials
        /// are used, so this never writes into the shared biome material asset.
        /// </summary>
        private System.Collections.IEnumerator FadeOutConcealer()
        {
            var renderers = concealer.GetComponentsInChildren<Renderer>(true);
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material mat in renderer.materials)
                    {
                        if (!mat.HasProperty("_BaseColor")) continue;

                        Color c = mat.GetColor("_BaseColor");
                        c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }

                yield return null;
            }

            concealer.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }
}
