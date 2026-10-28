using UnityEngine;

/// <summary>
/// Tower dasar (Archer): menembak peluru homing ke musuh terdekat.
/// CannonTower mewarisi script ini dan mengganti cara memilih target dan menembak.
/// </summary>
public class Tower : MonoBehaviour
{
    [Header("Placement Alignment")]
    [Tooltip("Jika dicentang, ketinggian Tower otomatis diselaraskan ke groundHeightY saat spawn.")]
    [SerializeField] protected bool alignGroundHeight = true;
    [Tooltip("Tinggi target permukaan tanah untuk Tower (default: 0.43).")]
    [SerializeField] protected float groundHeightY = 0.43f;

    [Header("Targeting & Attack")]
    [SerializeField] protected float range = 5f;
    [SerializeField] protected float fireRate = 1.5f; // Tembakan per detik
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected string enemyTag = "Enemy";

    [Header("Tower Parts & Prefabs")]
    [Tooltip("Bagian atas turret yang berputar horizontal")]
    [SerializeField] protected Transform partToRotate;
    [Tooltip("Titik keluarnya peluru")]
    [SerializeField] protected Transform firePoint;
    [SerializeField] protected GameObject bulletPrefab;

    [Header("Audio")]
    [Tooltip("Suara saat menembak. Boleh dikosongkan.")]
    [SerializeField] protected AudioClip shootSound;
    [Range(0f, 1f)]
    [SerializeField] protected float shootVolume = 0.6f;

    protected Transform currentTarget;
    private float fireCountdown = 0f;

    public float Range => range;
    public float FireRate => fireRate;
    public float Damage => damage;

    protected virtual void Start()
    {
        if (alignGroundHeight)
        {
            Vector3 pos = transform.position;
            pos.y = groundHeightY;
            transform.position = pos;
        }

        // Jalankan pencarian target secara berkala (2 kali per detik) untuk efisiensi performa
        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    protected virtual void UpdateTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy;
            }
        }

        // Kunci target jika berada di dalam jarak jangkauan
        if (nearestEnemy != null && shortestDistance <= range)
        {
            currentTarget = nearestEnemy.transform;
        }
        else
        {
            currentTarget = null;
        }
    }

    protected virtual void Update()
    {
        if (currentTarget == null) return;

        LockOnTarget();

        // Logika Cooldown Menembak
        if (fireCountdown <= 0f)
        {
            Shoot();
            fireCountdown = 1f / fireRate;
        }

        fireCountdown -= Time.deltaTime;
    }

    protected void LockOnTarget()
    {
        if (partToRotate == null || currentTarget == null) return;

        // Hitung arah pandang ke target
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0; // Kunci rotasi horizontal agar turret tidak miring aneh

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            // Rotasi halus menggunakan Slerp
            partToRotate.rotation = Quaternion.Slerp(partToRotate.rotation, lookRotation, Time.deltaTime * 25f);
        }
    }

    protected virtual void Shoot()
    {
        if (bulletPrefab == null || currentTarget == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 2.5f);
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, spawnRot);
        Sfx.Play(shootSound, shootVolume, 0.08f);
        Projectile projectile = bulletObj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.SetDamage(damage);
            projectile.Seek(currentTarget);
        }
    }

    // Menggambar radius tembak di Scene View
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}