using UnityEngine;

/// <summary>
/// Serangan jarak dekat sederhana untuk musuh. Musuh hanya menyerang Soldier yang sedang
/// menahannya: Soldier yang mengatur lawannya lewat SetOpponent / ClearOpponent.
/// Pasang di prefab musuh yang boleh melawan Soldier (misalnya Slime). Boleh juga di Warden.
/// </summary>
public class EnemyMelee : MonoBehaviour
{
    [Header("Serangan")]
    [SerializeField] private float damage = 5f;
    [Tooltip("Jumlah serangan per detik.")]
    [SerializeField] private float attackRate = 1f;

    [Header("Audio")]
    [Tooltip("Suara saat memukul Soldier. Boleh dikosongkan.")]
    [SerializeField] private AudioClip attackSound;
    [Range(0f, 1f)]
    [SerializeField] private float attackVolume = 0.5f;

    private Soldier opponent;
    private float attackTimer;

    public bool HasOpponent => opponent != null;
    public Soldier Opponent => opponent;
    public float Damage => damage;
    public float AttackRate => attackRate;

    public void SetOpponent(Soldier soldier)
    {
        if (opponent == soldier) return;

        opponent = soldier;
        attackTimer = 0f;
    }

    public void ClearOpponent(Soldier soldier)
    {
        // Hanya dilepas jika yang meminta adalah lawan saat ini
        if (opponent == soldier)
        {
            opponent = null;
        }
    }

    private void Update()
    {
        if (opponent == null) return;

        // Menghadap lawan
        Vector3 direction = opponent.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= 1f / Mathf.Max(0.01f, attackRate))
        {
            attackTimer = 0f;
            opponent.TakeDamage(damage);
            Sfx.Play(attackSound, attackVolume, 0.08f);
        }
    }
}