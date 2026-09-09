using UnityEngine;

namespace TreeFrogAdventure.Hazards
{
    /// <summary>
    /// A block that slams down and grinds back up on a loop.
    ///
    /// Damage is left to a DamageSource on the same object (normally with
    /// instantKill on) rather than being handled here — that keeps one contact-damage
    /// system across enemies and traps.
    /// </summary>
    public class CrusherTrap : MonoBehaviour
    {
        [Tooltip("How far it travels down from its rest position.")]
        [SerializeField] private float travelDistance = 4f;

        [SerializeField] private float slamSpeed = 22f;
        [SerializeField] private float retractSpeed = 2.5f;

        [Tooltip("Seconds held at the top before slamming.")]
        [SerializeField] private float waitTop = 1.4f;

        [Tooltip("Seconds held at the bottom before retracting.")]
        [SerializeField] private float waitBottom = 0.5f;

        [Tooltip("Offsets the loop so a row of crushers is not perfectly in sync.")]
        [SerializeField] private float startDelay;

        private enum Phase { WaitTop, Slam, WaitBottom, Retract }

        private Vector3 _topPosition;
        private Vector3 _bottomPosition;
        private Phase _phase = Phase.WaitTop;
        private float _timer;

        private void Awake()
        {
            _topPosition = transform.position;
            _bottomPosition = _topPosition + Vector3.down * travelDistance;
            _timer = waitTop + startDelay;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (_phase)
            {
                case Phase.WaitTop:
                    _timer -= dt;
                    if (_timer <= 0f) _phase = Phase.Slam;
                    break;

                case Phase.Slam:
                    transform.position = Vector3.MoveTowards(transform.position, _bottomPosition, slamSpeed * dt);
                    if (Vector3.Distance(transform.position, _bottomPosition) < 0.01f)
                    {
                        _phase = Phase.WaitBottom;
                        _timer = waitBottom;
                    }
                    break;

                case Phase.WaitBottom:
                    _timer -= dt;
                    if (_timer <= 0f) _phase = Phase.Retract;
                    break;

                case Phase.Retract:
                    transform.position = Vector3.MoveTowards(transform.position, _topPosition, retractSpeed * dt);
                    if (Vector3.Distance(transform.position, _topPosition) < 0.01f)
                    {
                        _phase = Phase.WaitTop;
                        _timer = waitTop;
                    }
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 top = Application.isPlaying ? _topPosition : transform.position;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawLine(top, top + Vector3.down * travelDistance);
            Gizmos.DrawWireCube(top + Vector3.down * travelDistance, Vector3.one * 0.4f);
        }
    }
}
