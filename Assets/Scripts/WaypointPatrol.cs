using System.Collections;
using UnityEngine;

public class DirectWaypointPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    [Tooltip("Territory markers — the bunny wanders randomly within the area they define, rather than looping between them directly.")]
    public Transform[] waypoints;

    [Tooltip("Wander/return movement speed in units per second.")]
    public float moveSpeed = 3.0f;

    [Tooltip("Rotation speed towards the current target.")]
    public float rotationSpeed = 5.0f;

    [Header("Wander")]
    public float minWanderIdle = 0.5f;
    public float maxWanderIdle = 2.5f;
    [Tooltip("How close counts as \"arrived\" at a wander point or back home.")]
    public float arrivalThreshold = 0.3f;

    [Header("Chase")]
    public float chaseSpeed = 4.5f;
    [Tooltip("Distance at which the bunny notices the player and starts chasing.")]
    public float detectionRadius = 6f;
    [Tooltip("Give up the chase if the player gets this far away.")]
    public float loseInterestDistance = 10f;

    private enum State { Wander, Chase, Return }
    private State _state = State.Wander;

    private Vector3 _homeCenter;
    private float _territoryRadius;
    private Vector3 _wanderTarget;
    private bool _isWaiting;
    private Transform _player;

    private const float FallbackTerritoryRadius = 5f;

    void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("No waypoints assigned to " + gameObject.name);
            enabled = false;
            return;
        }

        ComputeHomeAndRadius(out _homeCenter, out _territoryRadius);

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        _wanderTarget = PickNewWanderPoint();
        _state = State.Wander;
    }

    private void ComputeHomeAndRadius(out Vector3 home, out float radius)
    {
        home = Vector3.zero;
        int count = 0;
        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            home += wp.position;
            count++;
        }

        if (count == 0)
        {
            home = transform.position;
            radius = FallbackTerritoryRadius;
            return;
        }

        home /= count;

        radius = 0f;
        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            radius = Mathf.Max(radius, Vector3.Distance(home, wp.position));
        }

        if (radius <= 0.01f) radius = FallbackTerritoryRadius;
    }

    private Vector3 PickNewWanderPoint()
    {
        Vector2 offset = Random.insideUnitCircle * _territoryRadius;
        return new Vector3(_homeCenter.x + offset.x, _homeCenter.y, _homeCenter.z + offset.y);
    }

    void Update()
    {
        switch (_state)
        {
            case State.Wander:
                UpdateWander();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Return:
                UpdateReturn();
                break;
        }
    }

    private void UpdateWander()
    {
        // Checked before the idle-wait guard so an approaching player isn't ignored
        // just because the bunny happens to be pausing at a wander point.
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= detectionRadius)
        {
            _state = State.Chase;
            return;
        }

        if (_isWaiting) return;

        MoveAndRotateTowards(_wanderTarget, moveSpeed);

        if (Vector3.Distance(transform.position, _wanderTarget) < arrivalThreshold)
        {
            StartCoroutine(WanderIdle());
        }
    }

    private IEnumerator WanderIdle()
    {
        _isWaiting = true;
        yield return new WaitForSeconds(Random.Range(minWanderIdle, maxWanderIdle));
        _wanderTarget = PickNewWanderPoint();
        _isWaiting = false;
    }

    private void UpdateChase()
    {
        if (_player == null)
        {
            _state = State.Return;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        float distFromHome = Vector3.Distance(transform.position, _homeCenter);

        if (distToPlayer > loseInterestDistance || distFromHome > _territoryRadius)
        {
            _state = State.Return;
            return;
        }

        MoveAndRotateTowards(_player.position, chaseSpeed);
    }

    private void UpdateReturn()
    {
        // Deliberately ignores the player entirely to avoid flapping right at the
        // territory edge — only re-engages once fully home and back to Wander.
        MoveAndRotateTowards(_homeCenter, moveSpeed);

        if (Vector3.Distance(transform.position, _homeCenter) < arrivalThreshold)
        {
            _wanderTarget = PickNewWanderPoint();
            _state = State.Wander;
        }
    }

    private void MoveAndRotateTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    // Visualize territory, detection and lose-interest ranges in the Scene window
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                Gizmos.DrawWireSphere(waypoints[i].position, 0.4f);

                int nextIndex = (i + 1) % waypoints.Length;
                if (waypoints[nextIndex] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                }
            }
        }

        ComputeHomeAndRadius(out Vector3 home, out float radius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(home, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseInterestDistance);
    }
}
