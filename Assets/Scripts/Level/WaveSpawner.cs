using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PathSelectionType
{
    AutoPerception, // Otomatis memilih jalur paling aman lewat PathPerception
    Path1Only,      // Selalu lewat Path 1
    Path2Only,      // Selalu lewat Path 2
    RandomPath      // Acak 50:50 antara Path 1 dan Path 2
}

/// <summary>Satu jenis monster dalam sebuah wave (prefab + jumlahnya + jalur yang dipilih).</summary>
[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject enemyPrefab;
    [Min(1)] public int count = 1;
    [Tooltip("Pilih jalur mana yang akan dilewati jenis musuh ini.")]
    public PathSelectionType pathOption = PathSelectionType.AutoPerception;
}

/// <summary>Satu wave: daftar monster yang akan muncul.</summary>
[System.Serializable]
public class WaveData
{
    public string waveName = "Wave";
    public EnemySpawnEntry[] enemies;
    [Min(0.05f)] public float timeBetweenEnemies = 1.0f;
    public bool shuffleOrder = false;
}

public class WaveSpawner : MonoBehaviour
{
    [Header("Referensi Path")]
    [SerializeField] private WaypointPath waypointPath;   // Path 1
    [SerializeField] private WaypointPath alternatePath;  // Path 2

    [Header("Path Perception")]
    [SerializeField] private bool useTowerPerception = true;
    [SerializeField] private float perceptionRadius = 7f;

    [Header("Wave Settings")]
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private float timeBetweenWaves = 5.0f;
    [Tooltip("Tinggi posisi Y saat musuh di-spawn (default: 0.55 agar menempel di tanah).")]
    [SerializeField] private float spawnHeightY = 0.55f;
    [SerializeField] private WaveData[] waves;

    private int currentWaveIndex = 0;
    private bool isSpawning = false;

    public int CurrentWave => currentWaveIndex;
    public int TotalWaves => waves != null ? waves.Length : 0;

    private void Start()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning("WaveSpawner: daftar Waves masih kosong. Isi di Inspector.");
            return;
        }

        StartCoroutine(SpawnAllWavesRoutine());
    }

    private System.Collections.IEnumerator SpawnAllWavesRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (currentWaveIndex < waves.Length)
        {
            if (IsGameEnded()) yield break;

            WaveData wave = waves[currentWaveIndex];
            currentWaveIndex++;
            Debug.Log($"--- Wave {currentWaveIndex}/{waves.Length} ({wave.waveName}) Dimulai ---");

            yield return StartCoroutine(SpawnWaveRoutine(wave));

            if (currentWaveIndex < waves.Length)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        while (FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length > 0)
        {
            if (IsGameEnded()) yield break;
            yield return new WaitForSeconds(0.5f);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Victory();
        }
    }

    private bool IsGameEnded()
    {
        return GameManager.Instance != null && GameManager.Instance.GameEnded;
    }

    private System.Collections.IEnumerator SpawnWaveRoutine(WaveData wave)
    {
        isSpawning = true;

        List<EnemySpawnEntryData> spawnQueue = BuildSpawnQueue(wave);

        for (int i = 0; i < spawnQueue.Count; i++)
        {
            if (IsGameEnded()) break;

            SpawnEnemy(spawnQueue[i].prefab, spawnQueue[i].pathOption);
            yield return new WaitForSeconds(wave.timeBetweenEnemies);
        }

        isSpawning = false;
    }

    private struct EnemySpawnEntryData
    {
        public GameObject prefab;
        public PathSelectionType pathOption;
    }

    private List<EnemySpawnEntryData> BuildSpawnQueue(WaveData wave)
    {
        List<EnemySpawnEntryData> queue = new List<EnemySpawnEntryData>();
        if (wave.enemies == null) return queue;

        foreach (EnemySpawnEntry entry in wave.enemies)
        {
            if (entry == null || entry.enemyPrefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                queue.Add(new EnemySpawnEntryData
                {
                    prefab = entry.enemyPrefab,
                    pathOption = entry.pathOption
                });
            }
        }

        if (wave.shuffleOrder)
        {
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
        }

        return queue;
    }

    private void SpawnEnemy(GameObject enemyPrefab, PathSelectionType pathOption)
    {
        WaypointPath chosenPath = ChoosePathByOption(pathOption);
        if (enemyPrefab == null || chosenPath == null || chosenPath.points == null || chosenPath.points.Length == 0) return;

        Transform spawnPoint = chosenPath.points[0];
        if (spawnPoint == null) return;

        Vector3 spawnPos = spawnPoint.position;
        spawnPos.y = spawnHeightY;

        GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, spawnPoint.rotation);

        EnemyMovement movement = enemyObj.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.SetupPath(chosenPath.points, spawnHeightY);
        }
    }

    private WaypointPath ChoosePathByOption(PathSelectionType pathOption)
    {
        switch (pathOption)
        {
            case PathSelectionType.Path1Only:
                return waypointPath;

            case PathSelectionType.Path2Only:
                return alternatePath != null ? alternatePath : waypointPath;

            case PathSelectionType.RandomPath:
                if (alternatePath == null) return waypointPath;
                return Random.value < 0.5f ? waypointPath : alternatePath;

            case PathSelectionType.AutoPerception:
            default:
                if (!useTowerPerception || alternatePath == null)
                {
                    return waypointPath;
                }
                return PathPerception.ChooseSaferPath(waypointPath, alternatePath, perceptionRadius);
        }
    }
}