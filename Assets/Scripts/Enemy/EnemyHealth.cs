using System;
using UnityEngine;
 
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private int goldReward = 15;
 
    [Header("Defense Settings")]
    [Tooltip("Pengurang damage (flat). 0 = tanpa DEF.")]
    [SerializeField] private float defense = 0f;
    [Tooltip("Damage minimum sebagai rasio dari damage asli, supaya DEF tinggi tidak membuat damage jadi 0.")]
    [Range(0f, 1f)]
    [SerializeField] private float minDamageRatio = 0.1f;
 
    private float currentHealth;
    private bool isDead = false;
 
    // Properti baca-saja, dipakai AI (misalnya Tomb Warden) untuk menilai siapa yang butuh heal
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float Defense => defense;
    public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float MissingHealth => maxHealth - currentHealth;
    public bool IsDead => isDead;
    public bool NeedsHeal => !isDead && currentHealth < maxHealth;
 
    // Dipanggil saat monster mati (berguna nanti untuk split Sandstone Guardian)
    public event Action<EnemyHealth> OnDied;
 
    private void Awake()
    {
        currentHealth = maxHealth;
    }
 
    public void TakeDamage(float amount)
    {
        if (isDead) return;
 
        // DEF mengurangi damage, tapi tidak boleh di bawah damage minimum
        float finalDamage = Mathf.Max(amount - defense, amount * minDamageRatio);
        currentHealth -= finalDamage;
 
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
 
    /// <summary>
    /// Memulihkan HP. Mengembalikan jumlah heal yang benar-benar terpakai,
    /// sehingga sisanya bisa dialihkan ke target lain.
    /// </summary>
    public float Heal(float amount)
    {
        if (isDead || amount <= 0f) return 0f;
 
        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        return currentHealth - before;
    }
 
    public void Kill()
    {
        if (isDead) return;
        currentHealth = 0f;
        Die();
    }

    private void Die()
    {
        isDead = true;
 
        // Beri reward koin ke player
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(goldReward);
        }
 
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}