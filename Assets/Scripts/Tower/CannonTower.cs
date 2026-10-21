using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cannon: tower artileri dengan damage area (AoE).
/// Saat menembak, Cannon memilih satu musuh dalam range, MENGINGAT posisinya saat itu,
/// lalu menembakkan peluru ke posisi tersebut (bukan mengejar musuh). Musuh yang bergerak
/// cepat bisa lolos dari ledakan.
/// </summary>
public class CannonTower : Tower
{
    [Header("Cannon (AoE)")]
    [Tooltip("Radius ledakan di titik pendaratan peluru.")]
    [SerializeField] private float splashRadius = 2.5f;
    [Tooltip("Jika dicentang, Cannon memilih musuh di tengah kerumunan terpadat. Jika tidak, memilih musuh terdekat.")]
    [SerializeField] private bool targetDensestCluster = true;

    protected override void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        // Pilih target SAAT menembak, lalu ingat lokasinya
        Transform chosen = ChooseTarget();
        if (chosen == null) return;

        currentTarget = chosen; // agar turret menghadap target yang dipilih
        Vector3 rememberedPoint = chosen.position;

        GameObject shellObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        CannonShell shell = shellObj.GetComponent<CannonShell>();

        if (shell == null)
        {
            Debug.LogWarning($"{name}: Bullet Prefab Cannon harus memiliki script CannonShell.");
            Destroy(shellObj);
            return;
        }

        shell.Launch(rememberedPoint, damage, splashRadius);
    }

    private Transform ChooseTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        List<Transform> candidates = new List<Transform>();

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            if (Vector3.Distance(transform.position, enemy.transform.position) <= range)
            {
                candidates.Add(enemy.transform);
            }
        }

        if (candidates.Count == 0) return null;

        Transform best = null;
        int bestCount = -1;
        float bestDistance = Mathf.Infinity;

        foreach (Transform candidate in candidates)
        {
            // Jumlah musuh (termasuk dirinya) yang akan terkena ledakan jika menembak ke sini
            int count = targetDensestCluster ? CountEnemiesNear(candidate.position, enemies) : 0;
            float distance = Vector3.Distance(transform.position, candidate.position);

            // Skor lebih tinggi menang; kalau sama, pilih yang lebih dekat ke tower
            if (count > bestCount || (count == bestCount && distance < bestDistance))
            {
                best = candidate;
                bestCount = count;
                bestDistance = distance;
            }
        }

        return best;
    }

    private int CountEnemiesNear(Vector3 point, GameObject[] enemies)
    {
        int count = 0;
        point.y = 0f;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            Vector3 p = enemy.transform.position;
            p.y = 0f;

            if (Vector3.Distance(point, p) <= splashRadius)
            {
                count++;
            }
        }

        return count;
    }
}