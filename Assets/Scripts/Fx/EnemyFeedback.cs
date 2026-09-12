using TreeFrogAdventure.Enemies;
using UnityEngine;

namespace TreeFrogAdventure.Fx
{
    /// <summary>
    /// Makes hitting an enemy read as a hit.
    ///
    /// EnemyHealth has raised OnDamaged and OnDeath since it was written and nothing
    /// has ever subscribed to them, so damage was silent and death was a bare
    /// Destroy() — the corpse simply stopped existing mid-frame. This is the listener
    /// those events were built for.
    ///
    /// The flash goes through a MaterialPropertyBlock, never renderer.material.
    /// The placeholder enemies share their materials across every instance of a type,
    /// so touching .material would both leak a material clone per enemy and, worse,
    /// leave the shared asset edited in the project when it happened in the editor.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyFeedback : MonoBehaviour
    {
        [Header("Hit")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.12f;

        [Header("Death")]
        [Tooltip("Collapse the body over the despawn delay. Off leaves it rigid until " +
                 "EnemyHealth destroys it.")]
        [SerializeField] private bool collapseOnDeath = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private EnemyHealth _health;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;

        private float _flashUntil;
        private bool _flashing;

        private bool _dying;
        private Vector3 _deathStartScale;
        private float _deathElapsed;
        private float _deathDuration = 0.35f;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _block = new MaterialPropertyBlock();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Material shared = _renderers[i].sharedMaterial;

                _baseColors[i] = shared != null && shared.HasProperty(BaseColorId)
                    ? shared.GetColor(BaseColorId)
                    : Color.white;
            }
        }

        private void OnEnable()
        {
            _health.OnDamaged += HandleDamaged;
            _health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _health.OnDamaged -= HandleDamaged;
            _health.OnDeath -= HandleDeath;
        }

        // ---------------------------------------------------------------- hit --

        private void HandleDamaged(EnemyHealth health, int amount, DamageKind kind)
        {
            // A fatal hit goes straight to the death effect; flashing white for a
            // twelfth of a second and then vanishing reads as a glitch.
            if (health.CurrentHealth <= 0) return;

            FxPool.Play(FxKind.EnemyHit, Centre());

            _flashUntil = Time.time + flashDuration;
            _flashing = true;
            ApplyTint(flashColor);
        }

        private void HandleDeath(EnemyHealth health, DamageKind kind)
        {
            FxPool.Play(FxKind.EnemyDeath, Centre());

            if (_flashing) ClearTint();

            if (!collapseOnDeath) return;

            _dying = true;
            _deathStartScale = transform.localScale;
            _deathElapsed = 0f;

            // Match EnemyHealth's own despawn delay so the collapse finishes exactly
            // as the object is destroyed rather than being cut off part way.
            _deathDuration = Mathf.Max(0.05f, health.DespawnDelay);
        }

        private void Update()
        {
            if (_flashing && Time.time >= _flashUntil)
            {
                _flashing = false;
                ClearTint();
            }

            if (!_dying) return;

            _deathElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_deathElapsed / _deathDuration);

            // Squash flat and spin: the silhouette collapsing downward is what sells a
            // stomp, and the spin keeps a symmetric placeholder from reading as static.
            transform.localScale = new Vector3(
                _deathStartScale.x * Mathf.Lerp(1f, 1.35f, t),
                _deathStartScale.y * Mathf.Lerp(1f, 0.05f, t),
                _deathStartScale.z * Mathf.Lerp(1f, 1.35f, t));

            transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
        }

        // -------------------------------------------------------------- tint --

        private void ApplyTint(Color color)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void ClearTint()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, _baseColors[i]);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        // ------------------------------------------------------------ helpers --

        /// <summary>Roughly the middle of the body, so bursts do not spawn at the feet.</summary>
        private Vector3 Centre()
        {
            var controller = GetComponent<CharacterController>();
            if (controller != null) return transform.TransformPoint(controller.center);

            var col = GetComponent<Collider>();
            if (col != null) return col.bounds.center;

            return transform.position + Vector3.up * 0.5f;
        }
    }
}
