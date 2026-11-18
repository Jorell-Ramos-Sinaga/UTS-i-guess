using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Entry data dekorasi dengan sistem probabilitas berbobot (Weighted Spawner) untuk M4.
/// </summary>
[Serializable]
public class DecorEntry
{
    [Tooltip("Nama label dekorasi (opsional, untuk log statistik).")]
    public string name;

    [Tooltip("Prefab dekorasi (mis. Kaktus, Batu, Tulang).")]
    public GameObject prefab;

    [Tooltip("Bobot kemungkinan terpilih. Semakin besar bobot, semakin sering muncul.")]
    [Min(0)] public int weight = 1;

    [Tooltip("Offset ketinggian Y dari permukaan tanah.")]
    public float yOffset = 0f;

    [Tooltip("Rentang skala acak (Min, Max).")]
    public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

    [Tooltip("Jika true, rotasi sumbu Y (yaw) akan diacak 0-360 derajat.")]
    public bool randomYaw = true;
}

/// <summary>
/// M4: Dekorasi PCG berbasis grid dan probabilitas berbobot.
/// Menempatkan objek lingkungan pada sel MapCell.Empty secara deterministik berdasarkan seed.
/// </summary>
public class DecorSpawner : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private MapGenerator mapGenerator;

    [Header("Tabel Bobot Dekorasi")]
    [Tooltip("Daftar dekorasi dan bobot kemunculannya.")]
    [SerializeField] private DecorEntry[] decorEntries;

    [Header("Parameter Penempatan")]
    [Tooltip("Radius aman (dalam sel grid) dari Spawn dan Base agar dekorasi tidak menutupi area kunci.")]
    [SerializeField] private int decorClearRadius = 2;

    [Tooltip("Persentase sel Empty layak yang akan diisi dekorasi (default: 0.18 = 18%).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float decorDensity = 0.18f;

    [Tooltip("Jarak minimal (dalam sel grid) antar objek dekorasi agar tidak menumpuk rapat.")]
    [SerializeField] private int decorMinSpacing = 2;

    [Tooltip("Jika true, menonaktifkan Collider pada dekorasi agar tidak menghalangi raycast klik mouse ke BuildNode.")]
    [SerializeField] private bool disableColliders = true;

    [Header("Runtime Info / Debug")]
    [SerializeField] private int lastDecorSeed;
    [SerializeField] private int lastSpawnedCount;
    [SerializeField] private int lastSkippedCount;

    public IReadOnlyList<DecorEntry> Entries => decorEntries;

    private void Awake()
    {
        if (mapGenerator == null)
        {
            mapGenerator = GetComponent<MapGenerator>();
            if (mapGenerator == null)
            {
                mapGenerator = FindAnyObjectByType<MapGenerator>();
            }
        }
    }

    /// <summary>
    /// Men-spawn dekorasi ke dalam container decorParent berdasarkan data grid MapGenerator.
    /// </summary>
    public void SpawnDecorations(Transform decorParent, MapGenerator generator)
    {
        if (generator == null || !generator.IsReady)
        {
            Debug.LogWarning("[DecorSpawner] MapGenerator belum siap atau kosong. Batal spawn dekorasi.");
            return;
        }

        if (decorEntries == null || decorEntries.Length == 0)
        {
            Debug.LogWarning("[DecorSpawner] Tabel dekorasi kosong (Decor Entries = 0). Tidak ada dekorasi yang di-spawn.");
            return;
        }

        int totalWeight = 0;
        foreach (var entry in decorEntries)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0)
        {
            Debug.LogWarning("[DecorSpawner] Total bobot dekorasi valid <= 0. Periksa prefab dan weight pada Inspector.");
            return;
        }

        // 1. Inisialisasi RNG mandiri dengan seed deterministik turunan
        int mapSeed = generator.Seed;
        lastDecorSeed = mapSeed * 7919 + 13;
        System.Random rng = new System.Random(lastDecorSeed);

        // 2. Kumpulkan sel-sel kunci yang harus dihindari (Spawn dan Base)
        List<Vector2Int> keyCells = new List<Vector2Int>();
        if (generator.Lanes != null)
        {
            for (int l = 0; l < generator.Lanes.Count; l++)
            {
                keyCells.Add(generator.Lanes[l].SpawnCell);
            }
        }
        keyCells.Add(generator.BaseCell);

        // 3. Cari semua sel MapCell.Empty yang memenuhi syarat (di luar decorClearRadius)
        int width = generator.Grid.GetLength(0);
        int height = generator.Grid.GetLength(1);
        List<Vector2Int> eligibleCells = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (generator.Grid[x, y] != MapCell.Empty) continue;

                // Cek jarak ke seluruh titik spawn dan base
                bool tooCloseToKey = false;
                for (int k = 0; k < keyCells.Count; k++)
                {
                    int dx = Mathf.Abs(x - keyCells[k].x);
                    int dy = Mathf.Abs(y - keyCells[k].y);
                    if (Mathf.Max(dx, dy) <= decorClearRadius)
                    {
                        tooCloseToKey = true;
                        break;
                    }
                }

                if (!tooCloseToKey)
                {
                    eligibleCells.Add(new Vector2Int(x, y));
                }
            }
        }

        if (eligibleCells.Count == 0)
        {
            Debug.LogWarning("[DecorSpawner] Tidak ada sel Empty yang layak untuk dekorasi.");
            return;
        }

        // 4. Fisher-Yates Shuffle pada sel layak menggunakan RNG mandiri
        for (int i = eligibleCells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int temp = eligibleCells[i];
            eligibleCells[i] = eligibleCells[j];
            eligibleCells[j] = temp;
        }

        // 5. Target jumlah dekorasi berdasarkan decorDensity
        int targetCount = Mathf.Clamp(Mathf.RoundToInt(eligibleCells.Count * decorDensity), 1, eligibleCells.Count);

        List<Vector2Int> placedCells = new List<Vector2Int>();
        Dictionary<string, int> stats = new Dictionary<string, int>();
        int skippedCount = 0;

        for (int i = 0; i < eligibleCells.Count && placedCells.Count < targetCount; i++)
        {
            Vector2Int candidate = eligibleCells[i];

            // Cek jarak minimal terhadap dekorasi yang sudah ditempatkan
            bool spacingConflict = false;
            for (int p = 0; p < placedCells.Count; p++)
            {
                int dx = Mathf.Abs(candidate.x - placedCells[p].x);
                int dy = Mathf.Abs(candidate.y - placedCells[p].y);
                if (Mathf.Max(dx, dy) < decorMinSpacing)
                {
                    spacingConflict = true;
                    break;
                }
            }

            if (spacingConflict)
            {
                skippedCount++;
                continue;
            }

            // Pilih dekorasi berdasarkan bobot
            DecorEntry selectedEntry = SelectWeightedEntry(decorEntries, totalWeight, rng);
            if (selectedEntry == null || selectedEntry.prefab == null) continue;

            // Hitung posisi dunia
            Vector3 worldPos = generator.CellToWorld(candidate.x, candidate.y);
            worldPos.y += selectedEntry.yOffset;

            // Hitung rotasi acak
            float yaw = selectedEntry.randomYaw ? (float)(rng.NextDouble() * 360.0) : 0f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            // Spawn objek dekorasi
            GameObject decorObj = Instantiate(selectedEntry.prefab, worldPos, rotation, decorParent);
            string entryName = string.IsNullOrEmpty(selectedEntry.name) ? selectedEntry.prefab.name : selectedEntry.name;
            decorObj.name = $"{entryName}_{candidate.x}_{candidate.y}";

            // Skala acak
            float t = (float)rng.NextDouble();
            float scale = Mathf.Lerp(selectedEntry.scaleRange.x, selectedEntry.scaleRange.y, t);
            decorObj.transform.localScale = selectedEntry.prefab.transform.localScale * scale;

            // Nonaktifkan collider jika diminta agar tidak menghalangi klik raycast BuildNode
            if (disableColliders)
            {
                Collider[] colliders = decorObj.GetComponentsInChildren<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    colliders[c].enabled = false;
                }
            }

            placedCells.Add(candidate);

            if (!stats.ContainsKey(entryName)) stats[entryName] = 0;
            stats[entryName]++;
        }

        lastSpawnedCount = placedCells.Count;
        lastSkippedCount = skippedCount;

        // 6. Log statistik dekorasi
        List<string> statParts = new List<string>();
        foreach (var pair in stats)
        {
            statParts.Add($"{pair.Key}: {pair.Value}");
        }
        string statSummary = statParts.Count > 0 ? string.Join(", ", statParts) : "None";

        Debug.Log($"[DecorSpawner] Selesai spawn dekorasi. Seed: {lastDecorSeed} (Map: {mapSeed}), Total: {lastSpawnedCount}/{targetCount}, Dilewati Spacing: {lastSkippedCount}. Detail: [{statSummary}]");
    }

    private DecorEntry SelectWeightedEntry(DecorEntry[] entries, int totalWeight, System.Random rng)
    {
        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < entries.Length; i++)
        {
            DecorEntry entry = entries[i];
            if (entry == null || entry.prefab == null || entry.weight <= 0) continue;

            cumulative += entry.weight;
            if (roll < cumulative)
            {
                return entry;
            }
        }

        return null;
    }
}
