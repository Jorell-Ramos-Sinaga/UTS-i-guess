using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility AI Tomb Warden.
/// Setiap "think tick", Warden menghitung skor (0-1) untuk tiap aksi lalu menjalankan yang tertinggi:
/// - Heal   : berhenti dan menyembuhkan musuh di sekitar (termasuk dirinya) bila ada yang terluka.
/// - Hold   : menunggu pasukan karena Warden terlalu jauh di depan (support harus di belakang).
/// - Advance: terus berjalan di path (aksi default).
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyHealth))]
public class TombWardenAI : MonoBehaviour
{
    private enum WardenAction { Advance, Heal, Hold }

    [Header("Heal")]
    [SerializeField] private float healRadius = 5f;
    [Tooltip("Total heal per pulse, dibagi di antara target yang memenuhi syarat.")]
    [SerializeField] private float healPerPulse = 12f;
    [SerializeField] private float healInterval = 1.0f;
    [Tooltip("Jika dicentang, Warden berhenti berjalan selama melakukan heal.")]
    [SerializeField] private bool stopWhileHealing = true;

    [Header("Efek Visual")]
    [Tooltip("VFX yang muncul di posisi Warden setiap pulse heal (efek area). Boleh dikosongkan.")]
    [SerializeField] private GameObject healAreaEffectPrefab;
    [SerializeField] private float healAreaEffectScale = 1f;
    [Tooltip("VFX yang muncul di setiap musuh yang benar-benar menerima heal. Boleh dikosongkan.")]
    [SerializeField] private GameObject healTargetEffectPrefab;
    [SerializeField] private float healTargetEffectScale = 1f;
    [Tooltip("Efek dihapus otomatis setelah waktu ini (detik).")]
    [SerializeField] private float healEffectLifetime = 2f;

    [Header("Group Awareness")]
    [Tooltip("Musuh dalam radius ini dianggap satu pasukan.")]
    [SerializeField] private float groupRadius = 10f;
    [Tooltip("Selisih jarak ke base yang dianggap 'sangat di depan' pasukan.")]
    [SerializeField] private float maxAheadDistance = 6f;

    [Header("Utility Weights")]
    [SerializeField] private float healWeight = 1.0f;
    [SerializeField] private float holdWeight = 0.8f;
    [SerializeField] private float advanceBase = 0.35f;
    [SerializeField] private float laggingBonus = 0.3f;
    [Tooltip("Jumlah musuh terluka yang dianggap 'banyak' (skor penuh).")]
    [SerializeField] private int injuredSaturation = 3;
    [Tooltip("Bonus untuk aksi yang sedang berjalan, mencegah Warden berganti aksi terlalu cepat.")]
    [SerializeField] private float currentActionBonus = 0.1f;

    [Header("Timing")]
    [SerializeField] private float thinkInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool logScores = true;

    private EnemyMovement movement;
    private EnemyHealth selfHealth;
    private EnemyHealth[] cachedEnemies;
    private WardenAction currentAction = WardenAction.Advance;
    private float thinkTimer;
    private float healTimer;

    // Skor terakhir (untuk Debug atau UI)
    private float lastHealScore, lastHoldScore, lastAdvanceScore;
    private int lastInjuredCount;
    private float lastWorstMissing;
    private int lastGroupCount;
    private float lastAhead;
    private float lastLagging;

    public string CurrentActionName => currentAction.ToString();
    public float LastHealScore => lastHealScore;
    public float LastHoldScore => lastHoldScore;
    public float LastAdvanceScore => lastAdvanceScore;
    public int LastInjuredCount => lastInjuredCount;
    public float LastWorstMissing => lastWorstMissing;
    public int LastGroupCount => lastGroupCount;
    public float LastAhead => lastAhead;
    public float LastLagging => lastLagging;
    public float HealRadius => healRadius;
    public float GroupRadius => groupRadius;
    public float HealInterval => healInterval;
    public float HealTimer => healTimer;
    public float ThinkInterval => thinkInterval;
    public float ThinkTimer => thinkTimer;

    private void Start()
    {
        movement = GetComponent<EnemyMovement>();
        selfHealth = GetComponent<EnemyHealth>();
        SetAction(WardenAction.Advance);
    }

    private void Update()
    {
        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = thinkInterval;
            Think();
        }

        if (currentAction == WardenAction.Heal)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= healInterval)
            {
                healTimer = 0f;
                DoHealPulse();
            }
        }
    }

    // ---------- Utility AI: nilai semua aksi, pilih yang tertinggi ----------

    private void Think()
    {
        cachedEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

        // --- Pengamatan (inputs) ---
        int injuredCount = 0;
        float worstMissing = 0f;      // 0-1, persentase HP hilang terbesar di radius heal

        int groupCount = 0;
        float groupRemainingSum = 0f; // jarak ke base, rata-rata pasukan
        float myRemaining = movement.RemainingDistance;

        for (int i = 0; i < cachedEnemies.Length; i++)
        {
            EnemyHealth other = cachedEnemies[i];
            if (other == null || other.IsDead) continue;

            float distance = FlatDistance(transform.position, other.transform.position);

            if (distance <= healRadius && other.NeedsHeal)
            {
                injuredCount++;
                worstMissing = Mathf.Max(worstMissing, 1f - other.HealthPercent);
            }

            if (other != selfHealth && distance <= groupRadius)
            {
                EnemyMovement otherMovement = other.GetComponent<EnemyMovement>();
                if (otherMovement != null)
                {
                    groupCount++;
                    groupRemainingSum += otherMovement.RemainingDistance;
                }
            }
        }

        // --- Skor tiap aksi ---
        float healScore = 0f;
        if (injuredCount > 0)
        {
            float countFactor = Mathf.Clamp01((float)injuredCount / injuredSaturation);
            healScore = healWeight * (0.6f * worstMissing + 0.4f * countFactor);
        }

        float holdScore = 0f;
        float lagging = 0f;
        float ahead = 0f;
        if (groupCount > 0)
        {
            float groupAvgRemaining = groupRemainingSum / groupCount;
            // Remaining kecil = sudah lebih dekat ke base = lebih di depan
            ahead = Mathf.Clamp01((groupAvgRemaining - myRemaining) / maxAheadDistance);
            lagging = Mathf.Clamp01((myRemaining - groupAvgRemaining) / maxAheadDistance);
            holdScore = holdWeight * ahead;
        }

        float advanceScore = advanceBase + laggingBonus * lagging;

        // Hysteresis: aksi yang sedang berjalan mendapat sedikit bonus
        switch (currentAction)
        {
            case WardenAction.Heal: healScore += currentActionBonus; break;
            case WardenAction.Hold: holdScore += currentActionBonus; break;
            case WardenAction.Advance: advanceScore += currentActionBonus; break;
        }

        lastHealScore = healScore;
        lastHoldScore = holdScore;
        lastAdvanceScore = advanceScore;
        lastInjuredCount = injuredCount;
        lastWorstMissing = worstMissing;
        lastGroupCount = groupCount;
        lastAhead = ahead;
        lastLagging = lagging;

        // --- Pilih aksi dengan skor tertinggi ---
        WardenAction best = WardenAction.Advance;
        float bestScore = advanceScore;

        if (healScore > bestScore) { best = WardenAction.Heal; bestScore = healScore; }
        if (holdScore > bestScore) { best = WardenAction.Hold; bestScore = holdScore; }

        if (best != currentAction)
        {
            SetAction(best);
        }
    }

    private void SetAction(WardenAction newAction)
    {
        currentAction = newAction;

        if (logScores)
        {
            Debug.Log($"{name}: {newAction} (heal={lastHealScore:F2}, hold={lastHoldScore:F2}, advance={lastAdvanceScore:F2})");
        }

        switch (newAction)
        {
            case WardenAction.Advance:
                movement.enabled = true;
                break;

            case WardenAction.Heal:
                movement.enabled = !stopWhileHealing;
                healTimer = healInterval; // pulse pertama langsung berjalan
                break;

            case WardenAction.Hold:
                movement.enabled = false;
                break;
        }
    }

    // ---------- Heal: dibagi rata, sisa dialihkan, jika semua penuh sisa dibuang ----------

    private void DoHealPulse()
    {
        if (cachedEnemies == null) return;

        List<EnemyHealth> eligible = new List<EnemyHealth>();
        for (int i = 0; i < cachedEnemies.Length; i++)
        {
            EnemyHealth other = cachedEnemies[i];
            if (other == null || !other.NeedsHeal) continue;

            if (FlatDistance(transform.position, other.transform.position) <= healRadius)
            {
                eligible.Add(other);
            }
        }

        // Efek area hanya muncul jika memang ada yang bisa disembuhkan
        if (eligible.Count > 0 && healAreaEffectPrefab != null)
        {
            SpawnEffect(healAreaEffectPrefab, transform.position, healAreaEffectScale, null);
        }

        HashSet<EnemyHealth> healedTargets = new HashSet<EnemyHealth>();
        float remaining = healPerPulse;

        while (remaining > 0.001f && eligible.Count > 0)
        {
            float share = remaining / eligible.Count;
            remaining = 0f;

            for (int i = eligible.Count - 1; i >= 0; i--)
            {
                float used = eligible[i].Heal(share);
                if (used > 0f) healedTargets.Add(eligible[i]);
                remaining += share - used; // sisa dari target yang hampir penuh

                if (!eligible[i].NeedsHeal)
                {
                    eligible.RemoveAt(i);  // sudah penuh, tidak ikut putaran berikutnya
                }
            }
        }
        // Jika masih ada sisa tapi eligible kosong, sisa heal dibuang.

        // Efek pada tiap musuh yang benar-benar menerima heal (menempel dan ikut bergerak)
        if (healTargetEffectPrefab != null)
        {
            foreach (EnemyHealth target in healedTargets)
            {
                if (target != null)
                {
                    SpawnEffect(healTargetEffectPrefab, target.transform.position, healTargetEffectScale, target.transform);
                }
            }
        }
    }

    private void SpawnEffect(GameObject prefab, Vector3 position, float scale, Transform parent)
    {
        GameObject fx = Instantiate(prefab, position, Quaternion.identity, parent);
        fx.transform.localScale *= scale;
        Destroy(fx, healEffectLifetime);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, groupRadius);
    }
}