using UnityEngine;

/// <summary>
/// FSM Skeleton Bomb dengan Serangan AoE:
/// - Memindai Tower, Barrack, dan Soldier di sekitarnya.
/// - Saat meledak, memberikan damage AoE ke SEMUA target di radius ledakan.
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class SkeletonBombAI : MonoBehaviour
{
    private enum State { Run, Lock, Chase, Explode }

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 6f;
    [SerializeField] private float scanInterval = 0.2f;
    [SerializeField] private float onPathRadius = 1.0f;

    [Header("Chase & Explode")]
    [SerializeField] private float lockDuration = 0.25f;
    [SerializeField] private float chaseSpeed = 9f;
    [SerializeField] private float explodeDistance = 0.8f;
    [Tooltip("Radius area ledakan AoE saat bom meledak.")]
    [SerializeField] private float explosionRadius = 2.5f;

    [Header("Damage ke Target")]
    [SerializeField] private float offPathDamage = 25f;
    [SerializeField] private float onPathDamage = 50f;

    [Header("Efek Visual")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectScale = 1f;
    [SerializeField] private float explosionEffectLifetime = 3f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    private EnemyMovement movement;
    private State currentState;
    private Transform lockedTarget;
    private float scanTimer;
    private float lockTimer;
    private float pendingDamage;

    public string CurrentStateName => currentState.ToString();
    public Transform LockedTarget => lockedTarget;
    public float DetectionRadius => detectionRadius;
    public float ExplosionRadius => explosionRadius;
    public float ExplodeDistance => explodeDistance;
    public float ScanTimer => scanTimer;
    public float ScanInterval => scanInterval;
    public float LockTimer => lockTimer;
    public float LockDuration => lockDuration;
    public float PendingDamage => pendingDamage;
    public float ChaseSpeed => chaseSpeed;

    private void Start()
    {
        movement = GetComponent<EnemyMovement>();
        ChangeState(State.Run);
    }

    private void Update()
    {
        switch (currentState)
        {
            case State.Run:
                UpdateRun();
                break;
            case State.Lock:
                UpdateLock();
                break;
            case State.Chase:
                UpdateChase();
                break;
        }
    }

    private void ChangeState(State newState)
    {
        currentState = newState;

        if (logStateChanges)
        {
            Debug.Log($"{name}: state -> {newState}");
        }

        switch (newState)
        {
            case State.Run:
                lockedTarget = null;
                movement.enabled = true;
                break;

            case State.Lock:
                movement.enabled = false;
                lockTimer = lockDuration;
                break;

            case State.Chase:
                movement.enabled = false;
                break;

            case State.Explode:
                movement.enabled = false;
                Explode();
                break;
        }
    }

    private void UpdateRun()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;

        Transform nearestOffPath = null;
        Transform collidedOnPath = null;
        float bestDistance = detectionRadius;

        // Memindai Objek Menggunakan OverlapSphere (Mencakup Tower, Barrack, & Soldier)
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius);

        foreach (Collider col in hitColliders)
        {
            if (col == null || col.gameObject == gameObject) continue;

            // Cek apakah objek punya komponen HP (TowerHealth / Barrack / Soldier)
            TowerHealth tHealth = col.GetComponentInParent<TowerHealth>();
            Soldier soldier = col.GetComponentInParent<Soldier>();

            Transform targetTransform = null;
            if (tHealth != null) targetTransform = tHealth.transform;
            else if (soldier != null) targetTransform = soldier.transform;

            if (targetTransform == null) continue;

            float distance = FlatDistance(transform.position, targetTransform.position);

            if (!IsOnPath(targetTransform.position))
            {
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    nearestOffPath = targetTransform;
                }
            }
            else if (distance <= explodeDistance)
            {
                collidedOnPath = targetTransform;
            }
        }

        if (nearestOffPath != null)
        {
            lockedTarget = nearestOffPath;
            ChangeState(State.Lock);
        }
        else if (collidedOnPath != null)
        {
            lockedTarget = collidedOnPath;
            pendingDamage = onPathDamage;
            ChangeState(State.Explode);
        }
    }

    private void UpdateLock()
    {
        if (lockedTarget == null)
        {
            ChangeState(State.Run);
            return;
        }

        FaceTowards(lockedTarget.position);

        lockTimer -= Time.deltaTime;
        if (lockTimer <= 0f)
        {
            ChangeState(State.Chase);
        }
    }

    private void UpdateChase()
    {
        if (lockedTarget == null)
        {
            ChangeState(State.Run);
            return;
        }

        Vector3 toTarget = lockedTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= explodeDistance)
        {
            pendingDamage = offPathDamage;
            ChangeState(State.Explode);
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * chaseSpeed * Time.deltaTime;
        FaceTowards(lockedTarget.position);
    }

    /// <summary>
    /// Ledakan Area (AoE) yang merusak semua Tower, Barrack, dan Soldier di sekitarnya.
    /// </summary>
    private void Explode()
    {
        // Deteksi Semua Objek di Dalam Radius Ledakan AoE
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider col in hitColliders)
        {
            if (col == null) continue;

            // Berikan Damage ke Tower & Barrack
            TowerHealth tHealth = col.GetComponentInParent<TowerHealth>();
            if (tHealth != null)
            {
                tHealth.TakeDamage(pendingDamage);
            }

            // Berikan Damage ke Prajurit (Soldier)
            Soldier soldier = col.GetComponentInParent<Soldier>();
            if (soldier != null)
            {
                soldier.TakeDamage(pendingDamage);
            }
        }

        // Efek Ledakan Visual
        if (explosionEffectPrefab != null)
        {
            GameObject fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale *= explosionEffectScale;
            Destroy(fx, explosionEffectLifetime);
        }

        Destroy(gameObject);
    }

    private bool IsOnPath(Vector3 position)
    {
        return PathPerception.IsNearPath(position, movement.Waypoints, onPathRadius);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void FaceTowards(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explodeDistance);

        // Lingkaran Oranye menunjukkan area ledakan AoE
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}