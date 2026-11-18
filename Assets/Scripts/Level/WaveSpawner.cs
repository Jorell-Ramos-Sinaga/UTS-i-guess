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

    [Header("Integrasi Peta Prosedural")]
    [Tooltip("Referensi ke MapBuilder. Jika kosong, otomatis dicari di scene.")]
    [SerializeField] private MapBuilder mapBuilder;
    [Tooltip("Pengali jumlah musuh untuk jalur tambahan (default: 1.0).")]
    [SerializeField] private float extraLaneEnemyMultiplier = 1.0f;

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

    private void Awake()
    {
        if (mapBuilder == null)
        {
            mapBuilder = FindAnyObjectByType<MapBuilder>();
        }
    }

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
        // Tunggu peta siap dibangun jika ada MapBuilder di scene
        if (mapBuilder != null)
        {
            yield return new WaitUntil(() => mapBuilder.IsBuilt);
        }

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

            SpawnEnemy(spawnQueue[i].prefab, spawnQueue[i].pathOption, spawnQueue[i].targetLane);
            yield return new WaitForSeconds(wave.timeBetweenEnemies);
        }

        isSpawning = false;
    }

    private struct EnemySpawnEntryData
    {
        public GameObject prefab;
        public PathSelectionType pathOption;
        public LaneRuntime targetLane;
    }

    private List<EnemySpawnEntryData> BuildSpawnQueue(WaveData wave)
    {
        List<EnemySpawnEntryData> queue = new List<EnemySpawnEntryData>();
        if (wave.enemies == null) return queue;

        bool hasLanes = mapBuilder != null && mapBuilder.IsBuilt
            && mapBuilder.Lanes != null && mapBuilder.Lanes.Count > 0;
        var lanes = hasLanes ? mapBuilder.Lanes : null;
        int laneCount = hasLanes ? lanes.Count : 1;

        int[] perLaneCount = new int[laneCount];

        foreach (EnemySpawnEntry entry in wave.enemies)
        {
            if (entry == null || entry.enemyPrefab == null) continue;

            if (!hasLanes || laneCount == 1)
            {
                // Single lane atau scene statis lama
                LaneRuntime singleLane = hasLanes ? lanes[0] : null;
                for (int i = 0; i < entry.count; i++)
                {
                    queue.Add(new EnemySpawnEntryData
                    {
                        prefab = entry.enemyPrefab,
                        pathOption = entry.pathOption,
                        targetLane = singleLane
                    });
                    perLaneCount[0]++;
                }
            }
            else
            {
                // Pembagian Opsi A: total musuh dasar dibagi rata secara round-robin antar jalur
                int totalToSpawn = entry.count;
                for (int i = 0; i < totalToSpawn; i++)
                {
                    int laneIdx = i % laneCount;
                    queue.Add(new EnemySpawnEntryData
                    {
                        prefab = entry.enemyPrefab,
                        pathOption = entry.pathOption,
                        targetLane = lanes[laneIdx]
                    });
                    perLaneCount[laneIdx]++;
                }

                // Jika ada pengali ekstra musuh untuk jalur tambahan (jalur index > 0)
                if (!Mathf.Approximately(extraLaneEnemyMultiplier, 1.0f))
                {
                    for (int li = 1; li < laneCount; li++)
                    {
                        int extraAmount = Mathf.RoundToInt(perLaneCount[li] * (extraLaneEnemyMultiplier - 1f));
                        for (int k = 0; k < extraAmount; k++)
                        {
                            queue.Add(new EnemySpawnEntryData
                            {
                                prefab = entry.enemyPrefab,
                                pathOption = entry.pathOption,
                                targetLane = lanes[li]
                            });
                            perLaneCount[li]++;
                        }
                    }
                }
            }
        }

        // Catat jumlah musuh per jalur di log tiap wave
        if (hasLanes && laneCount > 1)
        {
            var laneStats = new List<string>();
            for (int li = 0; li < laneCount; li++)
            {
                laneStats.Add($"Jalur {li}: {perLaneCount[li]}");
            }
            Debug.Log($"[WaveSpawner] Wave {currentWaveIndex} ({wave.waveName}): Total {queue.Count} musuh [{string.Join(", ", laneStats)}]");
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

    private void SpawnEnemy(GameObject enemyPrefab, PathSelectionType pathOption, LaneRuntime targetLane)
    {
        WaypointPath chosenPath = ChoosePath(pathOption, targetLane);
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

    private WaypointPath ChoosePath(PathSelectionType pathOption, LaneRuntime targetLane)
    {
        // Jalur prosedural dengan daftar rute
        if (targetLane != null && targetLane.Routes != null && targetLane.Routes.Count > 0)
        {
            var routes = targetLane.Routes;
            switch (pathOption)
            {
                case PathSelectionType.Path1Only:
                    return routes[0]; // Rute utama

                case PathSelectionType.Path2Only:
                    return routes.Count > 1 ? routes[1] : routes[0]; // Rute alternatif pertama

                case PathSelectionType.RandomPath:
                    return routes[Random.Range(0, routes.Count)]; // Acak antar rute jalur ini

                case PathSelectionType.AutoPerception:
                default:
                    if (!useTowerPerception || routes.Count <= 1)
                    {
                        return routes[0];
                    }
                    return PathPerception.ChooseSaferRoute(routes, perceptionRadius);
            }
        }

        // Fallback untuk scene statis lama tanpa MapBuilder
        return ChoosePathByOption(pathOption);
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