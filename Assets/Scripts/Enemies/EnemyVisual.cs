using UnityEngine;

namespace TreeFrogAdventure.Enemies
{
    /// <summary>
    /// Procedural animation for the placeholder enemies.
    ///
    /// The project has no .anim assets at all and animates the player procedurally
    /// (PureMathFrog), so placeholders follow the same idiom rather than dragging in
    /// an Animator with a single baked take. When real rigged models arrive, drop them
    /// in as the visual child and delete this component — nothing else references it.
    /// </summary>
    public class EnemyVisual : MonoBehaviour
    {
        public enum Style
        {
            Bob,     // gentle idle float
            Hop,     // squash on land, stretch in air
            Waddle,  // side-to-side roll while moving
            Flap,    // wings beat, body banks
            Hover,   // fast wing beat, tight vertical bob
        }

        [SerializeField] private Style style = Style.Bob;

        [Tooltip("The part that squashes / bobs. Defaults to this transform.")]
        [SerializeField] private Transform body;

        [Tooltip("Optional wings for Flap / Hover styles.")]
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;

        [Header("Tuning")]
        [SerializeField] private float idleSpeed = 2f;
        [SerializeField] private float moveSpeed = 9f;
        [SerializeField] private float amount = 0.09f;
        [SerializeField] private float wingBeatSpeed = 22f;
        [SerializeField] private float wingBeatAngle = 42f;

        private Vector3 _bodyBasePos;
        private Vector3 _bodyBaseScale;
        private Quaternion _bodyBaseRot;
        private Quaternion _leftWingBase;
        private Quaternion _rightWingBase;

        private float _intensity;
        private bool _grounded = true;
        private float _phase;

        private void Awake()
        {
            if (body == null) body = transform;

            _bodyBasePos = body.localPosition;
            _bodyBaseScale = body.localScale;
            _bodyBaseRot = body.localRotation;

            if (leftWing != null) _leftWingBase = leftWing.localRotation;
            if (rightWing != null) _rightWingBase = rightWing.localRotation;
        }

        /// <summary>Called by the AI each frame. intensity 0 = idle, 1 = full speed.</summary>
        public void SetMotion(float intensity, bool grounded)
        {
            _intensity = Mathf.Clamp01(intensity);
            _grounded = grounded;
        }

        private void LateUpdate()
        {
            // Phase advances faster the faster the enemy is moving, so the animation
            // reads as driven by locomotion rather than running on a fixed clock.
            _phase += Time.deltaTime * Mathf.Lerp(idleSpeed, moveSpeed, _intensity);
            float s = Mathf.Sin(_phase);

            switch (style)
            {
                case Style.Bob:
                    body.localPosition = _bodyBasePos + Vector3.up * (s * amount);
                    break;

                case Style.Hop:
                {
                    // Squashed while planted, stretched while airborne.
                    float squash = _grounded ? Mathf.Abs(s) * amount : -amount * 1.4f;
                    body.localScale = new Vector3(
                        _bodyBaseScale.x * (1f + squash),
                        _bodyBaseScale.y * (1f - squash),
                        _bodyBaseScale.z * (1f + squash));
                    body.localPosition = _bodyBasePos + Vector3.up * (_grounded ? 0f : amount);
                    break;
                }

                case Style.Waddle:
                    body.localRotation = _bodyBaseRot *
                        Quaternion.AngleAxis(s * 14f * (0.3f + _intensity), Vector3.forward);
                    body.localPosition = _bodyBasePos + Vector3.up * (Mathf.Abs(s) * amount * 0.6f);
                    break;

                case Style.Flap:
                case Style.Hover:
                {
                    float beat = Mathf.Sin(Time.time * wingBeatSpeed);
                    float angle = beat * wingBeatAngle;

                    if (leftWing != null)
                        leftWing.localRotation = _leftWingBase * Quaternion.AngleAxis(angle, Vector3.forward);
                    if (rightWing != null)
                        rightWing.localRotation = _rightWingBase * Quaternion.AngleAxis(-angle, Vector3.forward);

                    float bobAmount = style == Style.Hover ? amount * 0.5f : amount;
                    body.localPosition = _bodyBasePos + Vector3.up * (s * bobAmount);
                    break;
                }
            }
        }
    }
}
