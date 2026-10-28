using UnityEngine;

public class Soldier : MonoBehaviour
{
    [Header("Movement & Battle")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float blockRadius = 1.2f;

    [Header("Animation Names")]
    [SerializeField] private string idleAnim = "WK_heavy_infantry_05_combat_idle";
    [SerializeField] private string walkAnim = "WK_heavy_infantry_06_combat_walk";
    [SerializeField] private string attackAnim = "WK_heavy_infantry_08_attack_B";

    private BarrackTower homeBarrack;
    private Vector3 targetRallySpot;
    private float maxHealth;
    private float currentHealth;
    private float damage;
    private float attackRate;

    private Transform currentEnemyTarget;
    private EnemyMovement blockedEnemyMovement;
    private float attackTimer;

    private Animator animator;
    private string currentAnimState;

    // Properti agar HealthBar bisa membaca persentase HP Soldier
    public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public Transform CurrentEnemyTarget => currentEnemyTarget;
    public BarrackTower HomeBarrack => homeBarrack;
    public Vector3 TargetRallySpot => targetRallySpot;
    public string CurrentAnimState => currentAnimState;
    public float Damage => damage;
    public float AttackRate => attackRate;
    public float BlockRadius => blockRadius;
    public float MoveSpeed => moveSpeed;

    public void Setup(BarrackTower barrack, Vector3 rallySpot, float hp, float dmg, float rate)
    {
        homeBarrack = barrack;
        targetRallySpot = rallySpot;
        maxHealth = hp;
        currentHealth = hp;
        damage = dmg;
        attackRate = rate;
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        ChangeAnimation(idleAnim);
    }

    private void Update()
    {
        // Cek jika Barrack pembuatnya sudah hancur/hilang
        if (homeBarrack == null)
        {
            UnblockEnemy();
            Destroy(gameObject);
            return;
        }

        if (currentEnemyTarget == null)
        {
            MoveToRallySpot();
            FindEnemyToBlock();
        }
        else
        {
            FightTarget();
        }
    }

    private void MoveToRallySpot()
    {
        float dist = Vector3.Distance(transform.position, targetRallySpot);
        if (dist > 0.2f)
        {
            ChangeAnimation(walkAnim);

            Vector3 dir = (targetRallySpot - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;

            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }
        }
        else
        {
            ChangeAnimation(idleAnim);
        }
    }

    private void FindEnemyToBlock()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            EnemyHealth enemyHealth = enemyObj.GetComponent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;

            float distToSoldier = Vector3.Distance(transform.position, enemyObj.transform.position);

            if (distToSoldier <= blockRadius)
            {
                currentEnemyTarget = enemyObj.transform;
                blockedEnemyMovement = enemyObj.GetComponent<EnemyMovement>();

                if (blockedEnemyMovement != null)
                {
                    blockedEnemyMovement.enabled = false;
                }
                break;
            }
        }
    }

    private void FightTarget()
    {
        if (currentEnemyTarget == null)
        {
            UnblockEnemy();
            return;
        }

        Vector3 dirToEnemy = (currentEnemyTarget.position - transform.position);
        dirToEnemy.y = 0f;
        if (dirToEnemy.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToEnemy), 10f * Time.deltaTime);
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= 1f / attackRate)
        {
            attackTimer = 0f;
            ChangeAnimation(attackAnim);

            EnemyHealth enemyHealth = currentEnemyTarget.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
        else
        {
            if (currentAnimState == attackAnim && attackTimer >= 0.5f)
            {
                ChangeAnimation(idleAnim);
            }
        }

        EnemyHealth targetHealth = currentEnemyTarget.GetComponent<EnemyHealth>();
        if (targetHealth == null || targetHealth.IsDead)
        {
            UnblockEnemy();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void Kill()
    {
        TakeDamage(currentHealth > 0 ? currentHealth : 9999f);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            UnblockEnemy();
            Destroy(gameObject);
        }
    }

    private void UnblockEnemy()
    {
        if (blockedEnemyMovement != null)
        {
            blockedEnemyMovement.enabled = true;
            blockedEnemyMovement = null;
        }
        currentEnemyTarget = null;
        ChangeAnimation(idleAnim);
    }

    private void ChangeAnimation(string newAnimState)
    {
        if (animator == null || currentAnimState == newAnimState) return;

        currentAnimState = newAnimState;
        animator.CrossFade(newAnimState, 0.1f);
    }

    private void OnDestroy()
    {
        UnblockEnemy();
    }
}