using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Targeting & Attack")]
    [SerializeField] private float range = 5f;
    [SerializeField] private float fireRate = 1.5f; // Tembakan per detik
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Tower Parts & Prefabs")]
    [Tooltip("Bagian atas turret yang berputar horizontal")]
    [SerializeField] private Transform partToRotate;
    [Tooltip("Titik keluarnya peluru")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    private Transform currentTarget;
    private float fireCountdown = 0f;

    private void Start()
    {
        // Jalankan pencarian target secara berkala (2 kali per detik) untuk efisiensi performa
        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    private void UpdateTarget()
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

    private void Update()
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

    private void LockOnTarget()
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

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null || currentTarget == null) return;

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Projectile projectile = bulletObj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Seek(currentTarget);
        }
    }

    // Menggambar radius tembak di Scene View
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
