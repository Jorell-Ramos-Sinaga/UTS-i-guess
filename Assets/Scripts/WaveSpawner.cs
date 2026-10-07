using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private WaypointPath waypointPath;

    [Header("Wave Settings")]
    [SerializeField] private int totalWaves = 5;
    [SerializeField] private int enemiesPerWave = 5;
    [SerializeField] private float timeBetweenEnemies = 1.0f;
    [SerializeField] private float timeBetweenWaves = 5.0f;

    private int currentWaveIndex = 0;
    private bool isSpawning = false;

    private void Start()
    {
        StartCoroutine(SpawnAllWavesRoutine());
    }

    private IEnumerator SpawnAllWavesRoutine()
    {
        yield return new WaitForSeconds(2f); // Jeda persiapan sebelum gelombang 1

        while (currentWaveIndex < totalWaves)
        {
            currentWaveIndex++;
            Debug.Log($"--- Wave {currentWaveIndex} Dimulai ---");

            yield return StartCoroutine(SpawnWaveRoutine(enemiesPerWave + (currentWaveIndex * 2)));

            // Jeda antar wave
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        Debug.Log("Semua wave telah dimunculkan!");
    }

    private IEnumerator SpawnWaveRoutine(int enemyCount)
    {
        isSpawning = true;

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenEnemies);
        }

        isSpawning = false;
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || waypointPath == null || waypointPath.points == null || waypointPath.points.Length == 0) return;

        Transform spawnPoint = waypointPath.points[0];
        if (spawnPoint == null) return;

        GameObject enemyObj = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        EnemyMovement movement = enemyObj.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.SetupPath(waypointPath.points);
        }
    }
}
