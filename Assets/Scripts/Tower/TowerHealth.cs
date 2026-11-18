using UnityEngine;

/// <summary>
/// HP sederhana untuk Tower. Pasang di prefab tower supaya bisa diserang monster.
/// </summary>
public class TowerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    private void Awake()
    {
        currentHealth = maxHealth;

        // Pastikan ada Collider agar bisa dideteksi oleh Physics.OverlapSphere (Bomber AI)
        if (GetComponentInChildren<Collider>() == null)
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 2f, 1f);
            col.center = new Vector3(0f, 1f, 0f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= amount;
        Debug.Log($"{name} terkena {amount} damage. Sisa HP: {Mathf.Max(currentHealth, 0f)}");

        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
        }
    }
}