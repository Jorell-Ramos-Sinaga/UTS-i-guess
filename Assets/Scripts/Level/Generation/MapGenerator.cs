using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Generator peta prosedural berbasis BSP untuk tower defense gurun.
/// Hanya menghasilkan data grid (MapCell[,]) dan LaneData — TIDAK membuat objek di scene.
/// Objek scene dibuat oleh MapBuilder (M2).
///
/// Gunakan [ContextMenu] di Inspector:
///   - "Generate Preview" : generate + gambar Gizmos
///   - "Stress Test 50 Seeds" : uji 50 seed berturut-turut, log ke Console
/// </summary>
public class MapGenerator : MonoBehaviour
{
    // ── Parameter grid ──────────────────────────────────────────────
    [Header("Ukuran Peta")]
    [SerializeField] private int mapWidth  = 48;
    [SerializeField] private int mapHeight = 32;
    [Tooltip("Ukuran satu sel dalam unit Unity. Sesuaikan dengan ukuran prefab tile.")]
    [SerializeField] private float tileSize = 2f;

    // ── BSP ──────────────────────────────────────────────────────────
    [Header("BSP")]
    [SerializeField] private int minLeafSize = 10;
    [SerializeField] private int maxLeafSize = 16;
    [Tooltip("Jarak margin dari tepi leaf saat memilih jangkar.")]
    [SerializeField] private int anchorMargin = 2;

    // ── Jalur ────────────────────────────────────────────────────────
    [Header("Jalur")]
    [Tooltip("A = leaf terakhir DFS, B = terjauh Euclidean, C = terpanjang A* (paling mahal).")]
    [SerializeField] private BaseStrategy baseStrategy = BaseStrategy.B;
    [Tooltip("Bobot jumlah jalur: index 0 = 1 jalur, 1 = 2 jalur, 2 = 3 jalur.")]
    [SerializeField] private int[] laneCountWeights = { 85, 12, 3 };
    [Tooltip("0 = acak sesuai bobot. Isi 2 atau 3 untuk memaksa jumlah jalur (berguna untuk menguji).")]
    [SerializeField] private int   forceLaneCount = 0;
    [SerializeField] private float branchChance = 0.30f;
    [SerializeField] private int   maxBranchesPerLane = 1;
    [SerializeField] private int   minAnchorsPerLane  = 3;
    [SerializeField] private int   maxRoutesPerLane   = 4;

    // ── Validasi ─────────────────────────────────────────────────────
    [Header("Validasi")]
    [SerializeField] private int   minRouteLength = 45;
    [SerializeField] private int   minNodes       = 40;
    [Tooltip("Panjang minimal untuk jalur tambahan dan rute alternatif (cabang).")]
    [SerializeField] private int   minExtraRouteLength = 30;
    [Tooltip("Jarak minimal spawner jalur tambahan ke base, sebagai rasio diagonal peta.")]
    [SerializeField] private float minExtraSpawnDistanceRatio = 0.3f;

    // ── Node Barrack ──────────────────────────────────────────────────
    [Header("Node Barrack")]
    [SerializeField] private float barrackBaseChance  = 0.08f;
    [SerializeField] private float cornerMultiplier   = 3f;
    [SerializeField] private int   barrackMinSpacing  = 3;
    [SerializeField] private int   minBarrackNodes    = 2;
    [SerializeField] private int   maxBarrackNodes    = 8;

    // ── Seed & percobaan ──────────────────────────────────────────────
    [Header("Seed")]
    [SerializeField] private int  seed          = 12345;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int  maxAttempts   = 30;

    // ── API publik ────────────────────────────────────────────────────
    public bool          IsReady       { get; private set; }
    public int           Seed          { get; private set; }
    public float         TileSize      => tileSize;
    public MapCell[,]    Grid          { get; private set; }
    public Vector2Int    BaseCell      { get; private set; }
    public IReadOnlyList<LaneData> Lanes => lanes;

    // Properti untuk akses mode seed dari luar (dipakai MapBuilder saat R ditekan)
    public bool UseRandomSeed  => useRandomSeed;
    public int  InspectorSeed  => seed;

    public event Action OnMapGenerated;

    // ── State internal ────────────────────────────────────────────────
    private List<LaneData> lanes = new List<LaneData>();
    private List<BspLeaf>  bspLeaves = new List<BspLeaf>();
    private List<Vector2Int> anchors = new List<Vector2Int>();

    // ── Enum strategi base ────────────────────────────────────────────
    public enum BaseStrategy { A, B, C }

    // ── BSP leaf (data saja, tidak perlu MonoBehaviour) ──────────────
    private class BspLeaf
    {
        public int X, Y, W, H; // area leaf (x,y = pojok kiri-bawah dalam grid)
        public BspLeaf Left, Right;
        public bool IsLeaf => Left == null && Right == null;

        public BspLeaf(int x, int y, int w, int h)
        { X = x; Y = y; W = w; H = h; }
    }

    // ══════════════════════════════════════════════════════════════════
    // Awake: generate otomatis saat masuk Play
    // ══════════════════════════════════════════════════════════════════
    private void Awake()
    {
        int startSeed = useRandomSeed ? UnityEngine.Random.Range(0, int.MaxValue) : seed;
        Generate(startSeed);
    }

    // ══════════════════════════════════════════════════════════════════
    // Generate (sinkron) — API publik utama
    // ══════════════════════════════════════════════════════════════════
    public void Generate(int requestedSeed)
    {
        IsReady = false;
        lanes.Clear();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            int trySeed = requestedSeed + (attempt - 1);
            var rng = new System.Random(trySeed);

            bool ok = TryGenerate(rng, trySeed, attempt, out string log);

            if (ok)
            {
                Seed = trySeed;
                Debug.Log(log);
                IsReady = true;
                OnMapGenerated?.Invoke();
                return;
            }

            if (attempt < maxAttempts)
                Debug.LogWarning($"[MapGenerator] Seed {trySeed} gagal validasi (percobaan {attempt}). Coba berikutnya.");
        }

        Debug.LogError($"[MapGenerator] Gagal generate peta setelah {maxAttempts} percobaan mulai seed {requestedSeed}.");
    }

    // ══════════════════════════════════════════════════════════════════
    // Satu percobaan generate
    // ══════════════════════════════════════════════════════════════════
    private bool TryGenerate(System.Random rng, int trySeed, int attempt, out string log)
    {
        log = "";

        // 1. Inisialisasi grid
        var grid = new MapCell[mapWidth, mapHeight];
        // (default = Empty)

        // 2. BSP
        bspLeaves.Clear();
        anchors.Clear();
        var root = new BspLeaf(1, 1, mapWidth - 2, mapHeight - 2); // margin 1 sel
        SplitLeaf(root, rng);
        CollectLeavesDFS(root, bspLeaves);

        // 3. Jangkar
        foreach (var leaf in bspLeaves)
        {
            int ax = leaf.X + anchorMargin + rng.Next(Mathf.Max(1, leaf.W - 2 * anchorMargin));
            int ay = leaf.Y + anchorMargin + rng.Next(Mathf.Max(1, leaf.H - 2 * anchorMargin));
            ax = Mathf.Clamp(ax, leaf.X + anchorMargin, leaf.X + leaf.W - 1 - anchorMargin);
            ay = Mathf.Clamp(ay, leaf.Y + anchorMargin, leaf.Y + leaf.H - 1 - anchorMargin);
            anchors.Add(new Vector2Int(ax, ay));
        }

        if (anchors.Count < 2)
        { log = "Jangkar terlalu sedikit."; return false; }

        // 4. Spawner utama & base
        Vector2Int spawner0 = anchors[0];
        Vector2Int baseCell = ChooseBase(anchors, spawner0, grid, rng);

        // 5. Jumlah jalur
        int laneCount = SampleLaneCount(rng);

        // 6. Jalur utama (spawner0 → base)
        var newLanes = new List<LaneData>();
        var usedAnchors = new HashSet<Vector2Int> { spawner0, baseCell };
        var protectedSet = new HashSet<Vector2Int> { spawner0, baseCell };
        var allCorridorCells = new HashSet<Vector2Int>();

        // Kumpulkan jangkar antara untuk jalur utama
        var lane0Anchors = PickIntermediaryAnchors(anchors, spawner0, baseCell, usedAnchors, rng, minAnchorsPerLane);
        var corridor0 = BuildCorridor(grid, lane0Anchors, rng, baseCell, allCorridorCells);

        if (corridor0 == null) { log = "Gagal membangun koridor jalur utama."; return false; }

        MarkCorridor(grid, corridor0, spawner0, baseCell);
        foreach (var c in corridor0) allCorridorCells.Add(c);
        allCorridorCells.Add(spawner0);
        allCorridorCells.Add(baseCell);

        grid[spawner0.x, spawner0.y] = MapCell.Spawn;
        grid[baseCell.x,  baseCell.y]  = MapCell.Base;

        var lane0 = new LaneData { LaneIndex = 0, SpawnCell = spawner0 };
        newLanes.Add(lane0);

        // 7. Cabang jalur utama
        TryAddBranch(grid, rng, spawner0, baseCell);

        // 8. Jalur tambahan (spawner terpisah, tidak terhubung ke jalur lain kecuali di base)
        var spawnList = new List<Vector2Int> { spawner0 };
        for (int li = 1; li < laneCount; li++)
        {
            if (!TryBuildExtraLane(grid, rng, baseCell, spawnList, out Vector2Int extraSpawn))
            {
                Debug.LogWarning($"[MapGenerator] Jalur tambahan {li} digugurkan (tidak ada rute yang muat).");
                break;
            }

            spawnList.Add(extraSpawn);
            protectedSet.Add(extraSpawn);
            newLanes.Add(new LaneData { LaneIndex = li, SpawnCell = extraSpawn });

            TryAddBranch(grid, rng, extraSpawn, baseCell);
        }

        // Pangkas semua jalan buntu (dead-end pruning) sebelum rute dicari
        PruneDeadEnds(grid, protectedSet);

        // 9. Daftar rute per jalur
        foreach (var lane in newLanes)
        {
            var routes = GridAStar.FindAllSimpleRoutes(grid, lane.SpawnCell, baseCell, maxRoutesPerLane, mapWidth, mapHeight);
            if (routes.Count == 0) { log = $"Jalur {lane.LaneIndex}: tidak ada rute."; return false; }
            lane.Routes.AddRange(routes);
        }

        // 10. Node
        MarkNodes(grid, newLanes);

        // 11. Node Barrack
        AssignBarrackNodes(grid, rng);

        // 12. Validasi
        bool valid = Validate(grid, newLanes, baseCell, out string validMsg);
        if (!valid) { log = validMsg; return false; }

        // Simpan ke state objek
        Grid      = grid;
        BaseCell  = baseCell;
        lanes     = newLanes;

        // Log metrik
        log = BuildMetricLog(trySeed, attempt, newLanes, baseCell, grid);
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    // BSP split rekursif
    // ══════════════════════════════════════════════════════════════════
    private void SplitLeaf(BspLeaf leaf, System.Random rng)
    {
        if (leaf.W <= maxLeafSize && leaf.H <= maxLeafSize) return;
        if (leaf.W < 2 * minLeafSize && leaf.H < 2 * minLeafSize) return;

        bool splitH; // true = potong horizontal (pisahkan atas-bawah)
        float ratio = (float)leaf.W / leaf.H;
        if      (ratio > 1.25f) splitH = false; // lebar → potong vertikal
        else if (ratio < 0.80f) splitH = true;  // tinggi → potong horizontal
        else                    splitH = rng.Next(2) == 0;

        if (splitH)
        {
            if (leaf.H < 2 * minLeafSize) return;
            int split = minLeafSize + rng.Next(leaf.H - 2 * minLeafSize + 1);
            leaf.Left  = new BspLeaf(leaf.X, leaf.Y,         leaf.W, split);
            leaf.Right = new BspLeaf(leaf.X, leaf.Y + split, leaf.W, leaf.H - split);
        }
        else
        {
            if (leaf.W < 2 * minLeafSize) return;
            int split = minLeafSize + rng.Next(leaf.W - 2 * minLeafSize + 1);
            leaf.Left  = new BspLeaf(leaf.X,         leaf.Y, split,          leaf.H);
            leaf.Right = new BspLeaf(leaf.X + split,  leaf.Y, leaf.W - split, leaf.H);
        }

        SplitLeaf(leaf.Left,  rng);
        SplitLeaf(leaf.Right, rng);
    }

    private void CollectLeavesDFS(BspLeaf node, List<BspLeaf> result)
    {
        if (node == null) return;
        if (node.IsLeaf) { result.Add(node); return; }
        CollectLeavesDFS(node.Left,  result);
        CollectLeavesDFS(node.Right, result);
    }

    // ══════════════════════════════════════════════════════════════════
    // Strategi memilih base
    // ══════════════════════════════════════════════════════════════════
    private Vector2Int ChooseBase(List<Vector2Int> allAnchors, Vector2Int spawner,
                                   MapCell[,] grid, System.Random rng)
    {
        switch (baseStrategy)
        {
            case BaseStrategy.A:
                return allAnchors[allAnchors.Count - 1];

            case BaseStrategy.C:
            {
                // A* terpanjang pada grid kosong
                float best = -1f;
                Vector2Int chosen = allAnchors[allAnchors.Count - 1];
                for (int i = 1; i < allAnchors.Count; i++)
                {
                    var path = GridAStar.FindPath(grid, spawner, allAnchors[i], null, mapWidth, mapHeight);
                    if (path != null && path.Count > best)
                    { best = path.Count; chosen = allAnchors[i]; }
                }
                return chosen;
            }

            default: // B: Euclidean terjauh
            {
                float bestDist = -1f;
                Vector2Int chosen = allAnchors[allAnchors.Count - 1];
                for (int i = 1; i < allAnchors.Count; i++)
                {
                    float d = Vector2Int.Distance(spawner, allAnchors[i]);
                    if (d > bestDist) { bestDist = d; chosen = allAnchors[i]; }
                }
                return chosen;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Jumlah jalur dari bobot
    // ══════════════════════════════════════════════════════════════════
    private int SampleLaneCount(System.Random rng)
    {
        if (forceLaneCount > 0) return Mathf.Clamp(forceLaneCount, 1, 4);

        int total = 0;
        foreach (int w in laneCountWeights) total += w;
        int r = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < laneCountWeights.Length; i++)
        {
            acc += laneCountWeights[i];
            if (r < acc) return i + 1;
        }
        return 1;
    }

    // ══════════════════════════════════════════════════════════════════
    // Pilih jangkar perantara untuk satu jalur (antara spawn dan base)
    // ══════════════════════════════════════════════════════════════════
    private List<Vector2Int> PickIntermediaryAnchors(
        List<Vector2Int> allAnchors,
        Vector2Int spawn, Vector2Int baseC,
        HashSet<Vector2Int> usedAnchors,
        System.Random rng, int minCount)
    {
        // Ambil semua jangkar yang belum dipakai, urutkan kira-kira dari spawn ke base
        var candidates = new List<Vector2Int>();
        foreach (var a in allAnchors)
        {
            if (a == spawn || a == baseC || usedAnchors.Contains(a)) continue;
            candidates.Add(a);
        }

        // Urut berdasarkan jarak dari spawn (pendekatan greedy)
        candidates.Sort((a, b) =>
            Vector2Int.Distance(spawn, a).CompareTo(Vector2Int.Distance(spawn, b)));

        // Ambil sejumlah minCount atau sebanyak yang bisa (tanpa overflow)
        var chosen = new List<Vector2Int>();
        int take = Mathf.Min(minCount, candidates.Count);
        // Ambil merata dari rentang candidates agar tersebar
        if (take > 0 && candidates.Count > 0)
        {
            float step = (float)(candidates.Count - 1) / Mathf.Max(1, take - 1);
            for (int i = 0; i < take; i++)
            {
                int idx = Mathf.Min((int)(i * step), candidates.Count - 1);
                chosen.Add(candidates[idx]);
                usedAnchors.Add(candidates[idx]);
            }
        }

        // Jika jangkar leaf tidak cukup, buat titik belok (waypoint) di area kosong agar jalur cukup panjang dan berkelok
        if (chosen.Count < minCount)
        {
            int needed = minCount - chosen.Count;
            for (int k = 0; k < needed; k++)
            {
                Vector2Int bestPt = spawn;
                float bestScore = -1f;

                for (int t = 0; t < 20; t++)
                {
                    int rx = 3 + rng.Next(mapWidth - 6);
                    int ry = 3 + rng.Next(mapHeight - 6);
                    var candidate = new Vector2Int(rx, ry);

                    if (usedAnchors.Contains(candidate)) continue;
                    float dSpawn = Vector2Int.Distance(candidate, spawn);
                    float dBase  = Vector2Int.Distance(candidate, baseC);
                    if (dSpawn < 6 || dBase < 6) continue;

                    float pathDist = dSpawn + dBase;
                    if (pathDist > bestScore)
                    {
                        bestScore = pathDist;
                        bestPt = candidate;
                    }
                }

                if (bestScore > 0f)
                {
                    chosen.Add(bestPt);
                    usedAnchors.Add(bestPt);
                }
            }

            chosen.Sort((a, b) =>
                Vector2Int.Distance(spawn, a).CompareTo(Vector2Int.Distance(spawn, b)));
        }

        // Susun urutan: spawn → pilihan urut → base
        var result = new List<Vector2Int> { spawn };
        result.AddRange(chosen);
        result.Add(baseC);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════
    // Bangun koridor melewati daftar jangkar, kembalikan kumpulan sel jalur
    // ══════════════════════════════════════════════════════════════════
    private HashSet<Vector2Int> BuildCorridor(
        MapCell[,] grid,
        List<Vector2Int> anchorSequence,
        System.Random rng,
        Vector2Int baseC,
        HashSet<Vector2Int> existingCells)
    {
        var allCells = existingCells != null ? new HashSet<Vector2Int>(existingCells) : new HashSet<Vector2Int>();
        var newCellsInCorridor = new HashSet<Vector2Int>();

        for (int i = 0; i < anchorSequence.Count - 1; i++)
        {
            var from = anchorSequence[i];
            var to   = anchorSequence[i + 1];

            // Coba koridor L-shape dulu
            var lCorridor = BuildLCorridor(from, to, rng);
            bool lOk = IsSegmentValid(lCorridor, allCells, from, to, baseC);

            List<Vector2Int> segment;
            if (lOk)
            {
                segment = lCorridor;
            }
            else
            {
                // Fallback: A* yang aktif menghindari menempel pada allCells (Chebyshev >= 2)
                segment = GridAStar.FindCorridorSegment(from, to, allCells, baseC, mapWidth, mapHeight);
                if (segment == null) return null;
                if (to != baseC && segment.Contains(baseC)) return null; // jangan lewat base di tengah jalan
            }

            foreach (var c in segment)
            {
                allCells.Add(c);
                newCellsInCorridor.Add(c);
            }
        }
        return newCellsInCorridor;
    }

    // Koridor L-shape (horizontal dulu, lalu vertikal, atau sebaliknya)
    private List<Vector2Int> BuildLCorridor(Vector2Int from, Vector2Int to, System.Random rng)
    {
        var cells = new List<Vector2Int>();
        bool hFirst = rng.Next(2) == 0;
        int cx = from.x, cy = from.y;

        if (hFirst)
        {
            while (cx != to.x) { cells.Add(new Vector2Int(cx, cy)); cx += (to.x > cx) ? 1 : -1; }
            while (cy != to.y) { cells.Add(new Vector2Int(cx, cy)); cy += (to.y > cy) ? 1 : -1; }
        }
        else
        {
            while (cy != to.y) { cells.Add(new Vector2Int(cx, cy)); cy += (to.y > cy) ? 1 : -1; }
            while (cx != to.x) { cells.Add(new Vector2Int(cx, cy)); cx += (to.x > cx) ? 1 : -1; }
        }
        cells.Add(to);
        return cells;
    }

    // Cek apakah segmen koridor memenuhi aturan jarak (bebas self-intersect & bebas menempel pada koridor sebelumnya)
    private bool IsSegmentValid(List<Vector2Int> segment, HashSet<Vector2Int> existingCells, Vector2Int from, Vector2Int to, Vector2Int baseC)
    {
        var segSet = new HashSet<Vector2Int>();
        for (int i = 0; i < segment.Count; i++)
        {
            var p = segment[i];
            if (p.x <= 0 || p.x >= mapWidth - 1 || p.y <= 0 || p.y >= mapHeight - 1)
                return false;

            if (!segSet.Add(p)) return false; // Self-intersect dalam segmen
            if (p == baseC && p != to) return false; // jalur tidak boleh melewati base di tengah jalan

            // Cek jarak Chebyshev 1 di dalam segment itu sendiri (mencegah loop / clump)
            for (int j = 0; j < i - 1; j++)
            {
                if (Mathf.Max(Mathf.Abs(p.x - segment[j].x), Mathf.Abs(p.y - segment[j].y)) <= 1)
                    return false;
            }

            // Cek terhadap existingCells
            if (existingCells != null && existingCells.Count > 0)
            {
                if (existingCells.Contains(p) && p != from && p != to)
                    return false;

                if (p != from && p != to && Vector2Int.Distance(p, baseC) > 2.5f)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var adj = new Vector2Int(p.x + dx, p.y + dy);
                        if (existingCells.Contains(adj) && adj != from && adj != to)
                        {
                            return false; // Menempel pada koridor lain -> membentuk jalur tebal
                        }
                    }
                }
            }
        }
        return true;
    }

    private void MarkCorridor(MapCell[,] grid, HashSet<Vector2Int> cells, Vector2Int spawn, Vector2Int baseC)
    {
        foreach (var c in cells)
        {
            if (grid[c.x, c.y] == MapCell.Empty)
                grid[c.x, c.y] = MapCell.Path;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Cabang: koridor alternatif (loop) dalam satu jalur
    // ══════════════════════════════════════════════════════════════════
    private static readonly Vector2Int[] Dirs4 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    private void TryAddBranch(MapCell[,] grid, System.Random rng, Vector2Int spawnCell, Vector2Int baseC)
    {
        for (int b = 0; b < maxBranchesPerLane; b++)
        {
            if (rng.NextDouble() >= branchChance) continue;

            var route = TraceRoute(grid, spawnCell, baseC);
            if (route == null || route.Count < 24) return;

            for (int attempt = 0; attempt < 25; attempt++)
            {
                // Titik awal dan akhir cabang di jalur: jauh dari spawner dan base
                int i = 4 + rng.Next(route.Count - 4 - 12);
                int j = i + 12 + rng.Next(22);
                if (j > route.Count - 6) continue;

                var path = FindGapPath(grid, route[i], route[j], baseC, rng);
                if (path == null || path.Count < 8) continue;

                var inner = path.GetRange(1, path.Count - 2);

                // Cabang harus benar-benar menyimpang dari jalur utama (bukan sekadar menempel)
                if (MaxDeviation(inner, route) < 4) continue;

                MarkCells(grid, inner, MapCell.Path);
                if (Has2x2Near(grid, inner))
                {
                    MarkCells(grid, inner, MapCell.Empty);
                    continue;
                }
                break; // cabang berhasil dibuat
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Jalur tambahan: spawner baru + koridor sampai ke base
    // ══════════════════════════════════════════════════════════════════
    private bool TryBuildExtraLane(MapCell[,] grid, System.Random rng, Vector2Int baseC,
                                   List<Vector2Int> existingSpawns, out Vector2Int spawn)
    {
        spawn = new Vector2Int(-1, -1);
        float diagonal = Mathf.Sqrt(mapWidth * mapWidth + mapHeight * mapHeight);

        // Kandidat spawner: jangkar BSP + titik acak di dekat tepi peta
        var candidates = new List<Vector2Int>(anchors);
        const int edge = 2;
        for (int k = 0; k < 40; k++)
        {
            bool onHorizontalEdge = rng.Next(2) == 0;
            int x, y;
            if (onHorizontalEdge)
            {
                x = edge + rng.Next(mapWidth - 2 * edge);
                y = rng.Next(2) == 0 ? edge : mapHeight - 1 - edge;
            }
            else
            {
                x = rng.Next(2) == 0 ? edge : mapWidth - 1 - edge;
                y = edge + rng.Next(mapHeight - 2 * edge);
            }
            candidates.Add(new Vector2Int(x, y));
        }

        // Saring (harus kosong, punya ruang, cukup jauh dari base dan spawner lain) lalu beri skor
        var scored = new List<KeyValuePair<float, Vector2Int>>();
        foreach (var c in candidates)
        {
            if (c.x < 2 || c.y < 2 || c.x > mapWidth - 3 || c.y > mapHeight - 3) continue;
            if (grid[c.x, c.y] != MapCell.Empty) continue;
            if (HasPathLikeWithin(grid, c, 2)) continue;

            float dBase = Vector2Int.Distance(c, baseC);
            if (dBase < minExtraSpawnDistanceRatio * diagonal) continue;

            float dMin = float.MaxValue;
            foreach (var sp in existingSpawns) dMin = Mathf.Min(dMin, Vector2Int.Distance(c, sp));
            if (dMin < 0.25f * diagonal) continue;

            float score = dBase + dMin + (float)rng.NextDouble() * 10f; // sedikit acak agar bervariasi
            scored.Add(new KeyValuePair<float, Vector2Int>(score, c));
        }
        scored.Sort((p, q) => q.Key.CompareTo(p.Key));

        int tries = Mathf.Min(scored.Count, 12);
        for (int i = 0; i < tries; i++)
        {
            var cand = scored[i].Value;
            if (TryCarveLaneToBase(grid, rng, cand, baseC))
            {
                spawn = cand;
                return true;
            }
        }
        return false;
    }

    // Membuat koridor dari spawnCell ke base lewat salah satu sisi base yang masih kosong.
    // Jika panjangnya kurang, ditambah 1-2 titik singgah acak supaya jalur berkelok.
    private bool TryCarveLaneToBase(MapCell[,] grid, System.Random rng, Vector2Int spawnCell, Vector2Int baseC)
    {
        var entries = new List<Vector2Int>();
        foreach (var d in Dirs4)
        {
            var e = new Vector2Int(baseC.x + d.x, baseC.y + d.y);
            if (e.x < 1 || e.y < 1 || e.x > mapWidth - 2 || e.y > mapHeight - 2) continue;
            if (grid[e.x, e.y] != MapCell.Empty) continue;
            entries.Add(e);
        }
        Shuffle(entries, rng);

        foreach (var entry in entries)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int waypointCount = attempt < 4 ? 0 : (attempt < 12 ? 1 : 2);
                var carved = new List<Vector2Int>();

                bool ok = CarveVia(grid, rng, spawnCell, entry, baseC, waypointCount, carved);

                // +1 untuk sel base di ujung
                if (ok && carved.Count + 1 >= minExtraRouteLength)
                {
                    grid[spawnCell.x, spawnCell.y] = MapCell.Spawn;
                    return true;
                }

                // Gagal atau terlalu pendek: kembalikan grid seperti semula
                MarkCells(grid, carved, MapCell.Empty);
            }
        }
        return false;
    }

    private bool CarveVia(MapCell[,] grid, System.Random rng, Vector2Int start, Vector2Int entry,
                          Vector2Int baseC, int waypointCount, List<Vector2Int> carved)
    {
        var points = new List<Vector2Int> { start };

        for (int w = 0; w < waypointCount; w++)
        {
            bool found = false;
            for (int t = 0; t < 30 && !found; t++)
            {
                var c = new Vector2Int(3 + rng.Next(mapWidth - 6), 3 + rng.Next(mapHeight - 6));
                if (grid[c.x, c.y] != MapCell.Empty) continue;
                if (HasPathLikeWithin(grid, c, 2)) continue;
                if (Cheb(c, start) < 6 || Cheb(c, entry) < 6 || Cheb(c, baseC) < 6) continue;

                bool tooClose = false;
                for (int p = 1; p < points.Count; p++) if (Cheb(c, points[p]) < 6) { tooClose = true; break; }
                if (tooClose) continue;

                points.Add(c);
                found = true;
            }
            if (!found) break; // pakai waypoint yang berhasil didapat saja
        }
        points.Add(entry);

        // Tandai titik awal dulu supaya aturan jarak mengenalinya
        grid[start.x, start.y] = MapCell.Path;
        carved.Add(start);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var leg = FindGapPath(grid, points[i], points[i + 1], baseC, rng);
            if (leg == null) return false;

            var added = leg.GetRange(1, leg.Count - 1);
            MarkCells(grid, added, MapCell.Path);
            carved.AddRange(added);

            if (Has2x2Near(grid, added)) return false;
        }

        return !Has2x2Near(grid, carved);
    }

    // ══════════════════════════════════════════════════════════════════
    // A* yang menjaga jarak: sel baru tidak boleh menempel pada jalur yang sudah ada
    // (kecuali di sekitar titik awal dan titik tujuan sendiri)
    // ══════════════════════════════════════════════════════════════════
    private List<Vector2Int> FindGapPath(MapCell[,] grid, Vector2Int from, Vector2Int to,
                                         Vector2Int baseC, System.Random rng)
    {
        var gScore = new Dictionary<Vector2Int, float> { [from] = 0f };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        var open = new List<Vector2Int> { from };

        int guard = mapWidth * mapHeight * 8;
        while (open.Count > 0 && guard-- > 0)
        {
            // Ambil sel dengan f terkecil (peta kecil, daftar sederhana sudah cukup)
            int bestIdx = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                var o = open[i];
                float f = gScore[o] + Mathf.Abs(o.x - to.x) + Mathf.Abs(o.y - to.y);
                if (f < bestF) { bestF = f; bestIdx = i; }
            }

            var cur = open[bestIdx];
            open.RemoveAt(bestIdx);

            if (cur == to)
            {
                var path = new List<Vector2Int> { cur };
                while (cameFrom.ContainsKey(cur)) { cur = cameFrom[cur]; path.Add(cur); }
                path.Reverse();
                return path;
            }

            if (!closed.Add(cur)) continue;

            foreach (var d in Dirs4)
            {
                var n = new Vector2Int(cur.x + d.x, cur.y + d.y);
                if (closed.Contains(n)) continue;
                if (n != to && !IsCarvable(grid, n, from, to)) continue;

                // Biaya dasar 1 ditambah sedikit acak agar bentuk jalur bervariasi antar seed
                float ng = gScore[cur] + 1f + (float)rng.NextDouble() * 0.4f;
                if (!gScore.ContainsKey(n) || ng < gScore[n])
                {
                    gScore[n] = ng;
                    cameFrom[n] = cur;
                    if (!open.Contains(n)) open.Add(n);
                }
            }
        }
        return null;
    }

    // Sel boleh dipahat jika kosong, di dalam margin, dan tidak menempel (8 arah) pada sel jalur lain.
    // Pengecualian: sel jalur yang berada di sekitar titik awal atau titik tujuan.
    private bool IsCarvable(MapCell[,] grid, Vector2Int n, Vector2Int from, Vector2Int to)
    {
        if (n.x < 1 || n.y < 1 || n.x > mapWidth - 2 || n.y > mapHeight - 2) return false;
        if (grid[n.x, n.y] != MapCell.Empty) return false;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            int mx = n.x + dx, my = n.y + dy;
            if (mx < 0 || my < 0 || mx >= mapWidth || my >= mapHeight) continue;
            if (!IsPathLike(grid[mx, my])) continue;

            var m = new Vector2Int(mx, my);
            bool nearFrom = Cheb(n, from) <= 1 && Cheb(m, from) <= 1;
            bool nearTo   = Cheb(n, to)   <= 1 && Cheb(m, to)   <= 1;
            if (!nearFrom && !nearTo) return false;
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    // Helper kecil
    // ══════════════════════════════════════════════════════════════════
    private static int Cheb(Vector2Int a, Vector2Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private bool HasPathLikeWithin(MapCell[,] grid, Vector2Int c, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int x = c.x + dx, y = c.y + dy;
            if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) continue;
            if (IsPathLike(grid[x, y])) return true;
        }
        return false;
    }

    private void MarkCells(MapCell[,] grid, List<Vector2Int> cells, MapCell value)
    {
        foreach (var c in cells) grid[c.x, c.y] = value;
    }

    private bool Has2x2Near(MapCell[,] grid, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        for (int ox = -1; ox <= 0; ox++)
        for (int oy = -1; oy <= 0; oy++)
        {
            int x0 = c.x + ox, y0 = c.y + oy;
            if (x0 < 0 || y0 < 0 || x0 + 1 >= mapWidth || y0 + 1 >= mapHeight) continue;
            if (IsPathLike(grid[x0, y0]) && IsPathLike(grid[x0 + 1, y0]) &&
                IsPathLike(grid[x0, y0 + 1]) && IsPathLike(grid[x0 + 1, y0 + 1])) return true;
        }
        return false;
    }

    // Seberapa jauh (Chebyshev) sel-sel cabang menyimpang dari jalur utama
    private int MaxDeviation(List<Vector2Int> branchCells, List<Vector2Int> route)
    {
        int best = 0;
        foreach (var c in branchCells)
        {
            int min = int.MaxValue;
            foreach (var r in route) min = Mathf.Min(min, Cheb(c, r));
            best = Mathf.Max(best, min);
        }
        return best;
    }

    // Rute terpendek (BFS 4 arah) melalui sel jalur dari s ke g
    private List<Vector2Int> TraceRoute(MapCell[,] grid, Vector2Int s, Vector2Int g)
    {
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var seen = new HashSet<Vector2Int> { s };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(s);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == g)
            {
                var path = new List<Vector2Int> { cur };
                while (prev.ContainsKey(cur)) { cur = prev[cur]; path.Add(cur); }
                path.Reverse();
                return path;
            }

            foreach (var d in Dirs4)
            {
                var n = new Vector2Int(cur.x + d.x, cur.y + d.y);
                if (n.x < 0 || n.y < 0 || n.x >= mapWidth || n.y >= mapHeight) continue;
                if (!IsPathLike(grid[n.x, n.y]) || !seen.Add(n)) continue;
                prev[n] = cur;
                queue.Enqueue(n);
            }
        }
        return null;
    }

    private void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // Jumlah komponen sel jalur yang terhubung (sel base tidak dihitung sebagai penghubung)
    private int CountPathComponents(MapCell[,] grid, Vector2Int baseC)
    {
        var seen = new HashSet<Vector2Int>();
        int components = 0;

        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            var start = new Vector2Int(x, y);
            if (!IsPathLike(grid[x, y]) || start == baseC || seen.Contains(start)) continue;

            components++;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in Dirs4)
                {
                    var n = new Vector2Int(cur.x + d.x, cur.y + d.y);
                    if (n.x < 0 || n.y < 0 || n.x >= mapWidth || n.y >= mapHeight) continue;
                    if (!IsPathLike(grid[n.x, n.y]) || n == baseC || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
        }
        return components;
    }

    // ══════════════════════════════════════════════════════════════════
    // Pangkas semua jalan buntu (Dead-end Pruning)
    // ══════════════════════════════════════════════════════════════════
    private void PruneDeadEnds(MapCell[,] grid, HashSet<Vector2Int> protectedCells)
    {
        bool changed = true;
        int maxIter = mapWidth * mapHeight;
        while (changed && maxIter-- > 0)
        {
            changed = false;
            for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
            {
                if (grid[x, y] != MapCell.Path) continue;
                Vector2Int pos = new Vector2Int(x, y);
                if (protectedCells != null && protectedCells.Contains(pos)) continue;

                int neighbors = 0;
                if (x > 0           && IsPathLike(grid[x - 1, y])) neighbors++;
                if (x < mapWidth-1  && IsPathLike(grid[x + 1, y])) neighbors++;
                if (y > 0           && IsPathLike(grid[x, y - 1])) neighbors++;
                if (y < mapHeight-1 && IsPathLike(grid[x, y + 1])) neighbors++;

                // Jika hanya punya <= 1 tetangga jalur, berarti ini ujung buntu!
                if (neighbors <= 1)
                {
                    grid[x, y] = MapCell.Empty;
                    changed = true;
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Tandai Node dan BarrackNode
    // ══════════════════════════════════════════════════════════════════
    private void MarkNodes(MapCell[,] grid, List<LaneData> laneList)
    {
        // Kumpulkan posisi spawn untuk nodeExcludeAroundSpawn
        var spawnSet = new HashSet<Vector2Int>();
        foreach (var lane in laneList) spawnSet.Add(lane.SpawnCell);
        const int excludeRadius = 1;

        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            if (grid[x, y] != MapCell.Empty) continue;

            // Cek Chebyshev 1: ada Path/Base di sekitar?
            bool nearPath = false;
            for (int dx = -1; dx <= 1 && !nearPath; dx++)
            for (int dy = -1; dy <= 1 && !nearPath; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= mapWidth || ny >= mapHeight) continue;
                var t = grid[nx, ny];
                if (t == MapCell.Path || t == MapCell.Base) nearPath = true;
            }
            if (!nearPath) continue;

            // Cek jarak dari spawn
            bool tooCloseToSpawn = false;
            foreach (var sp in spawnSet)
            {
                if (Mathf.Max(Mathf.Abs(x - sp.x), Mathf.Abs(y - sp.y)) <= excludeRadius)
                { tooCloseToSpawn = true; break; }
            }
            if (tooCloseToSpawn) continue;

            grid[x, y] = MapCell.Node;
        }
    }

    private void AssignBarrackNodes(MapCell[,] grid, System.Random rng)
    {
        // Identifikasi sel tikungan dan persimpangan
        var cornerSet = new HashSet<Vector2Int>();
        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            if (grid[x, y] != MapCell.Path) continue;
            int pathNeighbors = CountPathNeighbors4(grid, x, y);
            if (pathNeighbors == 2 && IsTurning(grid, x, y)) cornerSet.Add(new Vector2Int(x, y));
            else if (pathNeighbors >= 3)                      cornerSet.Add(new Vector2Int(x, y));
        }

        var barrackList = new List<Vector2Int>();

        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            if (grid[x, y] != MapCell.Node) continue;
            if (barrackList.Count >= maxBarrackNodes) break;

            // Cek jarak minimum antar BarrackNode
            bool tooClose = false;
            foreach (var bn in barrackList)
            {
                if (Mathf.Max(Mathf.Abs(x - bn.x), Mathf.Abs(y - bn.y)) < barrackMinSpacing)
                { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Peluang
            bool nearCorner = false;
            foreach (var cs in cornerSet)
            {
                if (Mathf.Max(Mathf.Abs(x - cs.x), Mathf.Abs(y - cs.y)) <= 1)
                { nearCorner = true; break; }
            }
            float chance = barrackBaseChance * (nearCorner ? cornerMultiplier : 1f);
            if (rng.NextDouble() < chance)
            {
                grid[x, y] = MapCell.BarrackNode;
                barrackList.Add(new Vector2Int(x, y));
            }
        }

        // Jaminan minimum: promosikan node dekat tikungan jika kurang
        if (barrackList.Count < minBarrackNodes)
        {
            for (int x = 0; x < mapWidth && barrackList.Count < minBarrackNodes; x++)
            for (int y = 0; y < mapHeight && barrackList.Count < minBarrackNodes; y++)
            {
                if (grid[x, y] != MapCell.Node) continue;
                bool nearCorner = false;
                foreach (var cs in cornerSet)
                {
                    if (Mathf.Max(Mathf.Abs(x - cs.x), Mathf.Abs(y - cs.y)) <= 1)
                    { nearCorner = true; break; }
                }
                if (!nearCorner) continue;
                bool tooClose = false;
                foreach (var bn in barrackList)
                {
                    if (Mathf.Max(Mathf.Abs(x - bn.x), Mathf.Abs(y - bn.y)) < barrackMinSpacing)
                    { tooClose = true; break; }
                }
                if (tooClose) continue;
                grid[x, y] = MapCell.BarrackNode;
                barrackList.Add(new Vector2Int(x, y));
            }
        }
    }

    private int CountPathNeighbors4(MapCell[,] grid, int x, int y)
    {
        int count = 0;
        if (x > 0           && (grid[x-1, y] == MapCell.Path || grid[x-1, y] == MapCell.Base || grid[x-1, y] == MapCell.Spawn)) count++;
        if (x < mapWidth-1  && (grid[x+1, y] == MapCell.Path || grid[x+1, y] == MapCell.Base || grid[x+1, y] == MapCell.Spawn)) count++;
        if (y > 0           && (grid[x, y-1] == MapCell.Path || grid[x, y-1] == MapCell.Base || grid[x, y-1] == MapCell.Spawn)) count++;
        if (y < mapHeight-1 && (grid[x, y+1] == MapCell.Path || grid[x, y+1] == MapCell.Base || grid[x, y+1] == MapCell.Spawn)) count++;
        return count;
    }

    // Tikungan: 2 tetangga path, tapi TIDAK berseberangan (bukan garis lurus)
    private bool IsTurning(MapCell[,] grid, int x, int y)
    {
        bool L = x > 0           && IsPathLike(grid[x-1, y]);
        bool R = x < mapWidth-1  && IsPathLike(grid[x+1, y]);
        bool D = y > 0           && IsPathLike(grid[x, y-1]);
        bool U = y < mapHeight-1 && IsPathLike(grid[x, y+1]);
        // Lurus = kiri-kanan atau atas-bawah
        return !((L && R && !D && !U) || (!L && !R && D && U));
    }

    private bool IsPathLike(MapCell c)
        => c == MapCell.Path || c == MapCell.Base || c == MapCell.Spawn;

    // ══════════════════════════════════════════════════════════════════
    // Validasi
    // ══════════════════════════════════════════════════════════════════
    private bool Validate(MapCell[,] grid, List<LaneData> laneList, Vector2Int baseC, out string msg)
    {
        // Cek tidak boleh ada jalur tebal (2x2 sel bertumpuk)
        for (int x = 0; x < mapWidth - 1; x++)
        for (int y = 0; y < mapHeight - 1; y++)
        {
            if (IsPathLike(grid[x, y]) && IsPathLike(grid[x + 1, y]) &&
                IsPathLike(grid[x, y + 1]) && IsPathLike(grid[x + 1, y + 1]))
            {
                msg = $"Ditemukan jalur tebal (2x2 sel) di ({x}, {y}).";
                return false;
            }
        }

        // Rute panjang minimal:
        //  - jalur utama: rute terpanjangnya >= minRouteLength
        //  - semua rute (termasuk alternatif dan jalur tambahan): >= minExtraRouteLength
        foreach (var lane in laneList)
        {
            int longest = 0;
            foreach (var route in lane.Routes) longest = Mathf.Max(longest, route.Count);

            if (lane.LaneIndex == 0 && longest < minRouteLength)
            {
                msg = $"Jalur utama: rute terpanjang terlalu pendek ({longest} < {minRouteLength}).";
                return false;
            }
        }

        foreach (var lane in laneList)
        foreach (var route in lane.Routes)
        {
            if (route.Count < minExtraRouteLength)
            {
                msg = $"Jalur {lane.LaneIndex}: rute terlalu pendek ({route.Count} < {minExtraRouteLength}).";
                return false;
            }
            // BFS verifikasi konektivitas
            if (!IsConnectedBFS(grid, route[0], baseC))
            {
                msg = $"Jalur {lane.LaneIndex}: rute tidak terhubung ke base.";
                return false;
            }
        }

        // Minimal node
        int nodeCount = 0;
        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
            if (grid[x, y] == MapCell.Node || grid[x, y] == MapCell.BarrackNode) nodeCount++;

        if (nodeCount < minNodes)
        {
            msg = $"Node terlalu sedikit ({nodeCount} < {minNodes}).";
            return false;
        }

        // Jarak Euclidean spawner ke base >= 40% diagonal
        float diagonal = Mathf.Sqrt(mapWidth * mapWidth + mapHeight * mapHeight);
        foreach (var lane in laneList)
        {
            float d = Vector2Int.Distance(lane.SpawnCell, baseC);
            float ratio = lane.LaneIndex == 0 ? 0.4f : minExtraSpawnDistanceRatio;
            if (d < ratio * diagonal)
            {
                msg = $"Spawner {lane.LaneIndex} terlalu dekat base ({d:F1} < {ratio * 100f:F0}% diagonal {ratio * diagonal:F1}).";
                return false;
            }
        }

        // Tidak boleh ada sisa jalur yang terlepas atau jalur yang saling menempel
        int components = CountPathComponents(grid, baseC);
        if (components != laneList.Count)
        {
            msg = $"Komponen jalur ({components}) tidak sama dengan jumlah jalur ({laneList.Count}).";
            return false;
        }

        msg = "OK";
        return true;
    }

    private bool IsConnectedBFS(MapCell[,] grid, Vector2Int start, Vector2Int goal)
    {
        var visited = new HashSet<Vector2Int> { start };
        var queue   = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) return true;
            foreach (var nb in Neighbors4(cur))
            {
                if (visited.Contains(nb)) continue;
                visited.Add(nb);
                var t = grid[nb.x, nb.y];
                if (t == MapCell.Path || t == MapCell.Base || t == MapCell.Spawn)
                    queue.Enqueue(nb);
            }
        }
        return false;
    }

    private IEnumerable<Vector2Int> Neighbors4(Vector2Int p)
    {
        if (p.x > 0)            yield return new Vector2Int(p.x-1, p.y);
        if (p.x < mapWidth-1)   yield return new Vector2Int(p.x+1, p.y);
        if (p.y > 0)            yield return new Vector2Int(p.x, p.y-1);
        if (p.y < mapHeight-1)  yield return new Vector2Int(p.x, p.y+1);
    }

    // ══════════════════════════════════════════════════════════════════
    // Log metrik per generate
    // ══════════════════════════════════════════════════════════════════
    private string BuildMetricLog(int trySeed, int attempt, List<LaneData> laneList, Vector2Int baseC, MapCell[,] grid)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[MapGenerator] Seed={trySeed} Percobaan={attempt} Jalur={laneList.Count}");

        int pathCells = 0, nodeGround = 0, nodeBarrack = 0;
        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            switch (grid[x, y])
            {
                case MapCell.Path:        pathCells++; break;
                case MapCell.Node:        nodeGround++; break;
                case MapCell.BarrackNode: nodeBarrack++; break;
            }
        }
        sb.AppendLine($"  Sel jalur={pathCells}  Node Ground={nodeGround}  Node Barrack={nodeBarrack}");

        foreach (var lane in laneList)
        {
            sb.Append($"  Jalur {lane.LaneIndex} Spawn={lane.SpawnCell} Rute={lane.Routes.Count} [");
            foreach (var r in lane.Routes) sb.Append($"{r.Count} ");
            sb.AppendLine("]");
        }

        sb.AppendLine($"  HashGrid={ComputeGridHash(grid)}");
        return sb.ToString();
    }

    private int ComputeGridHash(MapCell[,] grid)
    {
        unchecked
        {
            int h = 17;
            for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
                h = h * 31 + (int)grid[x, y];
            return h;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // API: konversi koordinat grid → dunia Unity
    // ══════════════════════════════════════════════════════════════════
    public Vector3 CellToWorld(int x, int y)
        => transform.position + new Vector3((x + 0.5f) * tileSize, 0f, (y + 0.5f) * tileSize);

    // ══════════════════════════════════════════════════════════════════
    // ContextMenu: Generate Preview
    // ══════════════════════════════════════════════════════════════════
    [ContextMenu("Generate Preview")]
    private void GeneratePreview()
    {
        int s = useRandomSeed ? UnityEngine.Random.Range(0, int.MaxValue) : seed;
        Generate(s);
        if (IsReady)
            Debug.Log($"[MapGenerator] Preview selesai. Seed={Seed}. Lihat Scene View untuk Gizmos.");
    }

    // ══════════════════════════════════════════════════════════════════
    // ContextMenu: Stress Test 50 Seeds
    // ══════════════════════════════════════════════════════════════════
    [ContextMenu("Stress Test 50 Seeds")]
    private void StressTest()
    {
        int total    = 50;
        int valid    = 0;
        int totalAtt = 0;
        var laneDist = new Dictionary<int, int>();
        int withBranch = 0;
        int routeMin = int.MaxValue, routeMax = 0;
        long routeSum = 0; int routeCount = 0;
        var multiLaneSeeds = new List<string>();
        var branchSeeds    = new List<string>();

        for (int i = 0; i < total; i++)
        {
            int s = 10000 + i;
            var rng = new System.Random(s);
            bool ok = TryGenerate(rng, s, 1, out _);

            // Hitung percobaan aktual
            int att = 1;
            for (int a = 2; a <= maxAttempts && !ok; a++)
            {
                rng = new System.Random(s + a - 1);
                ok = TryGenerate(rng, s + a - 1, a, out _);
                att = a;
            }

            if (!ok) continue;

            valid++;
            totalAtt += att;

            int lc = lanes.Count;
            if (!laneDist.ContainsKey(lc)) laneDist[lc] = 0;
            laneDist[lc]++;

            if (lc >= 2) multiLaneSeeds.Add(s.ToString());

            bool hasBranch = false;
            foreach (var lane in lanes)
            {
                if (lane.Routes.Count > 1) hasBranch = true;
                foreach (var route in lane.Routes)
                {
                    if (route.Count < routeMin) routeMin = route.Count;
                    if (route.Count > routeMax) routeMax = route.Count;
                    routeSum += route.Count;
                    routeCount++;
                }
            }
            if (hasBranch) { withBranch++; branchSeeds.Add(s.ToString()); }
        }

        float avgRoute = routeCount > 0 ? (float)routeSum / routeCount : 0f;
        float avgAtt   = valid > 0 ? (float)totalAtt / valid : 0f;

        var sb = new StringBuilder();
        sb.AppendLine($"=== Stress Test {total} Seeds ===");
        sb.AppendLine($"Valid: {valid}/{total}  Rata-rata percobaan: {avgAtt:F2}");
        sb.Append("Distribusi jalur: ");
        foreach (var kv in laneDist) sb.Append($"{kv.Key} jalur={kv.Value}  ");
        sb.AppendLine();
        sb.AppendLine($"Punya cabang: {withBranch}");
        sb.AppendLine($"Panjang rute (min/avg/max): {routeMin} / {avgRoute:F1} / {routeMax}");
        sb.AppendLine($"Seed ≥2 jalur: [{string.Join(", ", multiLaneSeeds)}]");
        sb.AppendLine($"Seed punya cabang: [{string.Join(", ", branchSeeds)}]");
        Debug.Log(sb.ToString());
    }

    // ══════════════════════════════════════════════════════════════════
    // Gizmos (hanya di Editor, tidak membuat objek)
    // ══════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!IsReady || Grid == null) return;

        for (int x = 0; x < mapWidth; x++)
        for (int y = 0; y < mapHeight; y++)
        {
            Vector3 center = CellToWorld(x, y);
            float   size   = tileSize * 0.9f;

            switch (Grid[x, y])
            {
                case MapCell.Path:
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawCube(center, new Vector3(size, 0.05f, size));
                    break;
                case MapCell.Spawn:
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(center, new Vector3(size, 0.1f, size));
                    break;
                case MapCell.Base:
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(center, new Vector3(size, 0.1f, size));
                    break;
                case MapCell.Node:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(center, new Vector3(size * 0.6f, 0.05f, size * 0.6f));
                    break;
                case MapCell.BarrackNode:
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawCube(center, new Vector3(size * 0.6f, 0.05f, size * 0.6f));
                    break;
            }
        }

        // Garis abu-abu = batas leaf BSP
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        foreach (var leaf in bspLeaves)
        {
            Vector3 bl = transform.position + new Vector3(leaf.X        * tileSize, 0.15f, leaf.Y        * tileSize);
            Vector3 br = transform.position + new Vector3((leaf.X+leaf.W)* tileSize, 0.15f, leaf.Y        * tileSize);
            Vector3 tl = transform.position + new Vector3(leaf.X        * tileSize, 0.15f, (leaf.Y+leaf.H)* tileSize);
            Vector3 tr = transform.position + new Vector3((leaf.X+leaf.W)* tileSize, 0.15f, (leaf.Y+leaf.H)* tileSize);
            Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);
        }

        // Bola putih = jangkar BSP
        Gizmos.color = Color.white;
        foreach (var a in anchors)
            Gizmos.DrawSphere(CellToWorld(a.x, a.y) + Vector3.up * 0.3f, tileSize * 0.15f);
    }
#endif
}