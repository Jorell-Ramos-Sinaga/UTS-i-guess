using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Satu jenis monster dalam sebuah wave (prefab + jumlahnya).</summary>
[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject enemyPrefab;
    [Min(1)] public int count = 1;
}

/// <summary>Satu wave: daftar monster yang akan muncul.</summary>
[System.Serializable]
public class WaveData
{
    public string waveName = "Wave";
    public EnemySpawnEntry[] enemies;
    [Min(0.05f)] public float timeBetweenEnemies = 1.0f;
    [Tooltip("Centang untuk mengacak urutan muncul. Kalau tidak, monster muncul berurutan sesuai daftar.")]
    public bool shuffleOrder = false;
}

public class WaveSpawner : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private WaypointPath waypointPath;
    [Tooltip("Jalur kedua menuju base. Kosongkan jika map hanya punya 1 path.")]
    [SerializeField] private WaypointPath alternatePath;

    [Header("Path Perception")]
    [SerializeField] private bool useTowerPerception = true;
    [Tooltip("Tower dalam jarak ini dari garis path dihitung sebagai ancaman di jalur tersebut.")]
    [SerializeField] private float perceptionRadius = 7f;

    [Header("Wave Settings")]
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private float timeBetweenWaves = 5.0f;
    [SerializeField] private WaveData[] waves;

    private int currentWaveIndex = 0;
    private bool isSpawning = false;

    // Dibaca oleh GameUI. CurrentWave = jumlah wave yang sudah dimulai (0 sebelum wave pertama).
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

    private IEnumerator SpawnAllWavesRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (currentWaveIndex < waves.Length)
        {
            if (IsGameEnded()) yield break;

            WaveData wave = waves[currentWaveIndex];
            currentWaveIndex++;
            Debug.Log($"--- Wave {currentWaveIndex}/{waves.Length} ({wave.waveName}) Dimulai ---");

            yield return StartCoroutine(SpawnWaveRoutine(wave));

            // Jeda antar wave (tidak perlu setelah wave terakhir)
            if (currentWaveIndex < waves.Length)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        Debug.Log("Semua wave telah dimunculkan! Menunggu semua monster dikalahkan...");

        // Menang jika semua monster sudah habis dan base masih hidup
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

    private IEnumerator SpawnWaveRoutine(WaveData wave)
    {
        isSpawning = true;

        List<GameObject> spawnQueue = BuildSpawnQueue(wave);

        for (int i = 0; i < spawnQueue.Count; i++)
        {
            if (IsGameEnded()) break;

            SpawnEnemy(spawnQueue[i]);
            yield return new WaitForSeconds(wave.timeBetweenEnemies);
        }

        isSpawning = false;
    }

    // Ubah daftar (prefab + jumlah) menjadi satu antrean spawn
    private List<GameObject> BuildSpawnQueue(WaveData wave)
    {
        List<GameObject> queue = new List<GameObject>();
        if (wave.enemies == null) return queue;

        foreach (EnemySpawnEntry entry in wave.enemies)
        {
            if (entry == null || entry.enemyPrefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                queue.Add(entry.enemyPrefab);
            }
        }

        if (wave.shuffleOrder)
        {
            // Fisher-Yates shuffle
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
        }

        return queue;
    }

    private void SpawnEnemy(GameObject enemyPrefab)
    {
        WaypointPath chosenPath = ChoosePath();
        if (enemyPrefab == null || chosenPath == null || chosenPath.points == null || chosenPath.points.Length == 0) return;

        Transform spawnPoint = chosenPath.points[0];
        if (spawnPoint == null) return;

        GameObject enemyObj = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        EnemyMovement movement = enemyObj.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.SetupPath(chosenPath.points);
        }
    }

    private WaypointPath ChoosePath()
    {
        if (!useTowerPerception || alternatePath == null)
        {
            return waypointPath;
        }

        WaypointPath saferPath = PathPerception.ChooseSaferPath(waypointPath, alternatePath, perceptionRadius);

        int threatA = PathPerception.CountTowersNearPath(waypointPath, perceptionRadius);
        int threatB = PathPerception.CountTowersNearPath(alternatePath, perceptionRadius);
        Debug.Log($"Enemy perception: Path1 towers={threatA}, Path2 towers={threatB}, pilih {(saferPath == waypointPath ? "Path 1" : "Path 2")}");

        return saferPath;
    }
}