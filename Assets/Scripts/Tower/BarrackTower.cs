using System.Collections.Generic;
using UnityEngine;

public class BarrackTower : MonoBehaviour
{
    [Header("Barrack Settings")]
    [SerializeField] private GameObject soldierPrefab;
    [SerializeField] private int maxSoldiers = 3;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private float rallyRadius = 2.5f;

    [Header("Prajurit Stats")]
    [SerializeField] private float soldierMaxHealth = 60f;
    [SerializeField] private float soldierDamage = 8f;
    [SerializeField] private float soldierAttackRate = 1f;

    private List<GameObject> activeSoldiers = new List<GameObject>();
    private Vector3 rallyPoint;
    private float spawnTimer;

    public Vector3 RallyPoint => rallyPoint;
    public float RallyRadius => rallyRadius;

    private void Start()
    {
        SetRallyPointToNearestPath();
        FaceTowardsPath(); // Otomatis rotasi bangunan Barrack ke arah jalan
    }

    private void Update()
    {
        activeSoldiers.RemoveAll(soldier => soldier == null);

        if (activeSoldiers.Count < maxSoldiers)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                SpawnSoldier();
            }
        }
    }

    private void SetRallyPointToNearestPath()
    {
        WaypointPath[] paths = FindObjectsByType<WaypointPath>(FindObjectsSortMode.None);
        float shortestDistance = Mathf.Infinity;
        Vector3 closestPoint = transform.position;

        foreach (WaypointPath path in paths)
        {
            if (path == null || path.points == null) continue;

            foreach (Transform p in path.points)
            {
                if (p == null) continue;
                float dist = Vector3.Distance(transform.position, p.position);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    closestPoint = p.position;
                }
            }
        }

        rallyPoint = closestPoint;
    }

    private void FaceTowardsPath()
    {
        Vector3 direction = (rallyPoint - transform.position);
        direction.y = 0f; // Kunci sumbu Y agar bangunan tidak miring ke atas/bawah

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void SpawnSoldier()
    {
        if (soldierPrefab == null) return;

        // Hitung arah menuju jalan
        Vector3 directionToPath = (rallyPoint - transform.position);
        directionToPath.y = 0f;
        Quaternion spawnRotation = directionToPath.sqrMagnitude > 0.001f 
            ? Quaternion.LookRotation(directionToPath) 
            : transform.rotation;

        // Spawn prajurit di depan Barrack dengan rotasi mengarah ke jalan
        Vector3 spawnPos = transform.position + transform.forward * 0.8f;
        GameObject soldierObj = Instantiate(soldierPrefab, spawnPos, spawnRotation);
        activeSoldiers.Add(soldierObj);

        // Cari titik spot di sekitar Rally Point
        Vector2 offset = Random.insideUnitCircle * (rallyRadius * 0.4f);
        Vector3 targetSpot = rallyPoint + new Vector3(offset.x, 0f, offset.y);

        Soldier soldier = soldierObj.GetComponent<Soldier>();
        if (soldier != null)
        {
            soldier.Setup(this, targetSpot, soldierMaxHealth, soldierDamage, soldierAttackRate);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rallyPoint, rallyRadius);
        Gizmos.DrawLine(transform.position, rallyPoint);
    }
}