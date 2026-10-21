using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifetime = 4f;

    [Header("Visual Orientation")]
    [Tooltip("Offset rotasi jika model panah menghadap samping/terbalik.\nContoh: jika moncong panah menghadap samping kanan, isi Y = -90 atau 90. Jika terbalik, isi Y = 180.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private Transform target;

    // Dipanggil oleh Tower agar damage diatur dari tower, bukan dari peluru
    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    public void Seek(Transform targetTransform)
    {
        target = targetTransform;

        // Segera orientasikan panah saat pertama kali ditembakkan
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(rotationOffset);
            }
        }

        Destroy(gameObject, lifetime); // Hapus otomatis jika tidak mengenai apa-apa
    }

    private void Update()
    {
        // Jika target musuh sudah hancur sebelum peluru sampai
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Arahkan peluru ke posisi target
        Vector3 direction = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        // Jika peluru sudah sangat dekat dengan target (mengenai musuh)
        if (direction.magnitude <= distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.Translate(direction.normalized * distanceThisFrame, Space.World);

        // Putar panah menghadap musuh dengan memperhitungkan offset model
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(rotationOffset);
        }
    }

    private void HitTarget()
    {
        if (target != null)
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Garis biru menunjukkan arah depan peluru (forward)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}