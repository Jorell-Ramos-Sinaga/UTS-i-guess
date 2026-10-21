using UnityEngine;

/// <summary>
/// FSM Skeleton Bomb: Run -> Lock -> Chase -> Explode.
/// - Run    : berlari di path (dijalankan EnemyMovement), sambil memindai tower.
/// - Lock   : tower off-path terdeteksi, berhenti sebentar dan mengunci target.
/// - Chase  : mengejar target terkunci; target lain tidak mengalihkan aggro.
/// - Explode: meledak dan memberi damage ke tower, lalu hancur.
/// Jika tidak ada target off-path dan menabrak tower on-path, langsung meledak (damage lebih besar).
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class SkeletonBombAI : MonoBehaviour
{
    private enum State { Run, Lock, Chase, Explode }

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 6f;
    [Tooltip("Seberapa sering daftar tower diperbarui (detik).")]
    [SerializeField] private float scanInterval = 0.2f;
    [Tooltip("Tower dalam jarak ini dari garis path dianggap on-path.")]
    [SerializeField] private float onPathRadius = 1.0f;

    [Header("Chase")]
    [SerializeField] private float lockDuration = 0.25f;
    [SerializeField] private float chaseSpeed = 9f;
    [Tooltip("Jarak ke tower saat bom meledak / dianggap menabrak.")]
    [SerializeField] private float explodeDistance = 0.8f;

    [Header("Damage ke Tower")]
    [SerializeField] private float offPathDamage = 15f;
    [SerializeField] private float onPathDamage = 40f;

    [Header("Efek Visual")]
    [Tooltip("Prefab VFX ledakan dari Project window (bukan objek di scene). Boleh dikosongkan.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectScale = 1f;
    [Tooltip("Efek dihapus otomatis setelah waktu ini (detik).")]
    [SerializeField] private float explosionEffectLifetime = 3f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    private EnemyMovement movement;
    private State currentState;
    private Tower lockedTarget;
    private Tower[] cachedTowers;
    private float scanTimer;
    private float lockTimer;
    private float pendingDamage;

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

    // ---------- State transitions ----------

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
                movement.enabled = true;   // lanjut berjalan di path
                break;

            case State.Lock:
                movement.enabled = false;  // berhenti mengikuti path
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

    // ---------- State updates ----------

    private void UpdateRun()
    {
        RefreshTowerCache();

        Tower nearestOffPath = null;
        Tower collidedOnPath = null;
        float bestDistance = detectionRadius;

        for (int i = 0; i < cachedTowers.Length; i++)
        {
            Tower tower = cachedTowers[i];
            if (tower == null) continue;

            float distance = FlatDistance(transform.position, tower.transform.position);

            if (!IsOnPath(tower.transform.position))
            {
                // Tower off-path dalam radius deteksi: kandidat target
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    nearestOffPath = tower;
                }
            }
            else if (distance <= explodeDistance)
            {
                // Menabrak tower yang berada di path
                collidedOnPath = tower;
            }
        }

        // Prioritas: target off-path dulu. Hanya jika tidak ada, tabrakan on-path dihitung.
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
        // Target hilang (misalnya sudah hancur): kembali berlari di path
        if (lockedTarget == null)
        {
            ChangeState(State.Run);
            return;
        }

        FaceTowards(lockedTarget.transform.position);

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

        Vector3 toTarget = lockedTarget.transform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= explodeDistance)
        {
            pendingDamage = offPathDamage;
            ChangeState(State.Explode);
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * chaseSpeed * Time.deltaTime;
        FaceTowards(lockedTarget.transform.position);
    }

    private void Explode()
    {
        if (lockedTarget != null)
        {
            TowerHealth towerHealth = lockedTarget.GetComponent<TowerHealth>();
            if (towerHealth != null)
            {
                towerHealth.TakeDamage(pendingDamage);
            }
            else
            {
                Debug.LogWarning($"{lockedTarget.name} belum punya TowerHealth, damage tidak diberikan.");
            }
        }

        // Efek ledakan muncul di posisi skeleton, lalu hilang sendiri
        if (explosionEffectPrefab != null)
        {
            GameObject fx = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale *= explosionEffectScale;
            Destroy(fx, explosionEffectLifetime);
        }

        Destroy(gameObject);
    }

    // ---------- Helpers ----------

    private void RefreshTowerCache()
    {
        scanTimer -= Time.deltaTime;

        if (cachedTowers == null || scanTimer <= 0f)
        {
            cachedTowers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            scanTimer = scanInterval;
        }
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
    }
}