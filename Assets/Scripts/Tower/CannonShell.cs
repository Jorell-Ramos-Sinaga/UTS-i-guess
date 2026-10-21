using UnityEngine;

/// <summary>
/// Peluru Cannon. Terbang melengkung ke SATU titik tetap (tidak mengejar musuh),
/// lalu meledak dan memberi damage ke semua musuh dalam radius ledakan.
/// </summary>
public class CannonShell : MonoBehaviour
{
    [Header("Gerakan")]
    [Tooltip("Kecepatan horizontal peluru. Makin lambat, makin mudah dihindari musuh cepat.")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float arcHeight = 3f;

    [SerializeField] private string enemyTag = "Enemy";

    [Header("Efek Visual")]
    [Tooltip("Prefab VFX ledakan dari Project window. Boleh dikosongkan.")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectScale = 1f;
    [SerializeField] private float explosionEffectLifetime = 3f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float damage;
    private float splashRadius;
    private float travelTime;
    private float elapsed;
    private bool launched;

    public void Launch(Vector3 target, float damageAmount, float radius)
    {
        startPosition = transform.position;
        targetPosition = target;
        damage = damageAmount;
        splashRadius = radius;

        Vector3 flatStart = new Vector3(startPosition.x, 0f, startPosition.z);
        Vector3 flatTarget = new Vector3(target.x, 0f, target.z);
        travelTime = Mathf.Max(0.2f, Vector3.Distance(flatStart, flatTarget) / speed);

        elapsed = 0f;
        launched = true;
    }

    private void Update()
    {
        if (!launched) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / travelTime);

        // Gerak lurus dari awal ke tujuan, ditambah lengkungan parabola di sumbu Y
        Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);
        position.y += arcHeight * 4f * t * (1f - t);

        Vector3 direction = position - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        transform.position = position;

        if (t >= 1f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        launched = false;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Vector3 center = targetPosition;
        center.y = 0f;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            Vector3 p = enemy.transform.position;
            p.y = 0f;

            if (Vector3.Distance(center, p) <= splashRadius)
            {
                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
            }
        }

        if (explosionEffectPrefab != null)
        {
            GameObject fx = Instantiate(explosionEffectPrefab, targetPosition, Quaternion.identity);
            fx.transform.localScale *= explosionEffectScale;
            Destroy(fx, explosionEffectLifetime);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (!launched) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPosition, splashRadius);
    }
}