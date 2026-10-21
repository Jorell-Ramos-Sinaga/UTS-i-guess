using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifetime = 4f;

    private Transform target;

    public void Seek(Transform targetTransform)
    {
        target = targetTransform;
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
        transform.LookAt(target);
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
}
