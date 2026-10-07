using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private int goldReward = 15;

    private float currentHealth;
    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // Beri reward koin ke player
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(goldReward);
        }

        Destroy(gameObject);
    }
}
