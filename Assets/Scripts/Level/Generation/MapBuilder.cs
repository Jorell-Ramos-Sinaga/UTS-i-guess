using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Membangun objek-objek game di scene berdasarkan data grid dari MapGenerator (M2).
/// Mendengarkan event OnMapGenerated, lalu merekonstruksi seluruh peta
/// di bawah satu GameObject induk 'GeneratedMap'.
/// </summary>
public class MapBuilder : MonoBehaviour
{
    [Header("Referensi Generator")]
    [SerializeField] private MapGenerator mapGenerator;
    [Tooltip("Komponen DecorSpawner untuk dekorasi prosedural (M4). Jika kosong, dicari otomatis.")]
    [SerializeField] private DecorSpawner decorSpawner;

    [Header("Prefab Lingkungan")]
    [Tooltip("Prefab lantai keseluruhan (misal Plane atau Ground tile). Jika kosong, otomatis membuat 1 Unity Plane primitif.")]
    [SerializeField] private GameObject floorPrefab;
    [Tooltip("Material lantai jika memakai Plane primitif.")]
    [SerializeField] private Material floorMaterial;
    [Tooltip("Tambahan margin ukuran lantai di luar batas grid (dalam unit dunia).")]
    [SerializeField] private float floorPadding = 2f;
    [Tooltip("Prefab untuk sel jalur (Path). Jika kosong, memakai kubus tipis.")]
    [SerializeField] private GameObject pathTilePrefab;

    [Header("Prefab Node Pembangunan")]
    [Tooltip("Prefab node pembangunan Ground (harus memiliki komponen BuildNode).")]
    [SerializeField] private GameObject groundNodePrefab;
    [Tooltip("Prefab node pembangunan Barrack (Path). Jika kosong, memakai groundNodePrefab dengan tint.")]
    [SerializeField] private GameObject barrackNodePrefab;

    [Header("Prefab Bangunan Kunci")]
    [Tooltip("Prefab penanda portal / titik spawn musuh.")]
    [SerializeField] private GameObject spawnerPrefab;
    [Tooltip("Prefab markas / kristal utama.")]
    [SerializeField] private GameObject basePrefab;

    [Header("Pengaturan Ketinggian")]
    [Tooltip("Ketinggian Y untuk Waypoint (default: 0.55).")]
    [SerializeField] private float waypointY = 0.55f;

    // ── Properti Publik ──────────────────────────────────────────────
    public bool IsBuilt { get; private set; }
    public Transform GeneratedRoot => generatedRoot;
    public IReadOnlyList<LaneRuntime> Lanes => runtimeLanes;

    public event Action OnMapBuilt;

    // ── State Internal ───────────────────────────────────────────────
    private Transform generatedRoot;
    private readonly List<LaneRuntime> runtimeLanes = new List<LaneRuntime>();

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

        if (decorSpawner == null)
        {
            decorSpawner = GetComponent<DecorSpawner>();
            if (decorSpawner == null)
            {
                decorSpawner = FindAnyObjectByType<DecorSpawner>();
            }
        }
    }

    private void OnEnable()
    {
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerated += HandleMapGenerated;
        }
    }

    private void OnDisable()
    {
        if (mapGenerator != null)
        {
            mapGenerator.OnMapGenerated -= HandleMapGenerated;
        }
    }

    private void Start()
    {
        // Jika generator sudah siap sebelum MapBuilder sempat mendengarkan event
        if (mapGenerator != null && mapGenerator.IsReady && !IsBuilt)
        {
            BuildMap();
        }
    }

    private void Update()
    {
        // Tekan R (hanya saat Play di Editor atau development build) untuk regenerate peta
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateMap();
        }
#endif
    }

    /// <summary>
    /// Regenerate peta: panggil ulang MapGenerator.Generate lalu rebuild scene.
    /// - Jika useRandomSeed aktif: seed baru diacak.
    /// - Jika tidak: pakai seed yang sama di Inspector.
    /// </summary>
    [ContextMenu("Regenerate Map (R)")]
    public void RegenerateMap()
    {
        if (mapGenerator == null)
        {
            Debug.LogWarning("[MapBuilder] Tidak ada MapGenerator. Tekan R tidak berefek.");
            return;
        }

        int nextSeed = mapGenerator.UseRandomSeed
            ? UnityEngine.Random.Range(0, int.MaxValue)
            : mapGenerator.InspectorSeed;

        Debug.Log($"[MapBuilder] Regenerate peta — seed: {nextSeed} (randomSeed={mapGenerator.UseRandomSeed})");
        mapGenerator.Generate(nextSeed);
        // HandleMapGenerated dipanggil melalui event OnMapGenerated → BuildMap()
    }

    private void HandleMapGenerated()
    {
        BuildMap();
    }

    /// <summary>
    /// Membangun seluruh objek peta secara prosedural.
    /// Menghapus objek sebelumnya agar tidak ada objek sisa.
    /// </summary>
    [ContextMenu("Build Map")]
    public void BuildMap()
    {
        if (mapGenerator == null || !mapGenerator.IsReady)
        {
            Debug.LogWarning("[MapBuilder] MapGenerator belum siap atau kosong.");
            return;
        }

        ClearMap();

        // 1. Buat kontainer induk tunggal
        GameObject rootGo = new GameObject("GeneratedMap");
        rootGo.transform.SetParent(transform, false);
        generatedRoot = rootGo.transform;

        int width = mapGenerator.Grid.GetLength(0);
        int height = mapGenerator.Grid.GetLength(1);
        float tileSize = mapGenerator.TileSize;

        // 2. Bangun SATU objek lantai dasar
        BuildFloor(generatedRoot, width, height, tileSize);

        // Sub-kontainer untuk kerapihan hierarki scene
        Transform pathTilesParent = new GameObject("PathTiles").transform;
        pathTilesParent.SetParent(generatedRoot, false);

        Transform nodesParent = new GameObject("Nodes").transform;
        nodesParent.SetParent(generatedRoot, false);

        Transform lanesParent = new GameObject("Lanes").transform;
        lanesParent.SetParent(generatedRoot, false);

        Transform baseParent = new GameObject("Base").transform;
        baseParent.SetParent(generatedRoot, false);

        Transform decorParent = new GameObject("Decor").transform;
        decorParent.SetParent(generatedRoot, false);

        // 3. Bangun Tile, Node, dan Base dari grid
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            MapCell cell = mapGenerator.Grid[x, y];
            Vector3 worldPos = mapGenerator.CellToWorld(x, y);

            switch (cell)
            {
                case MapCell.Path:
                    CreatePathTile(worldPos, tileSize, pathTilesParent);
                    break;

                case MapCell.Spawn:
                    CreatePathTile(worldPos, tileSize, pathTilesParent);
                    break;

                case MapCell.Node:
                    CreateNode(worldPos, tileSize, NodeType.Ground, nodesParent);
                    break;

                case MapCell.BarrackNode:
                    CreateNode(worldPos, tileSize, NodeType.Path, nodesParent);
                    break;

                case MapCell.Base:
                    CreatePathTile(worldPos, tileSize, pathTilesParent);
                    CreateBase(worldPos, tileSize, baseParent);
                    break;
            }
        }

        // 4. Bangun WaypointPath per rute dan runtime lanes
        BuildLanesAndWaypoints(lanesParent);

        // 5. Spawn dekorasi PCG berbobot (M4)
        if (decorSpawner == null)
        {
            decorSpawner = GetComponent<DecorSpawner>();
            if (decorSpawner == null)
            {
                decorSpawner = FindAnyObjectByType<DecorSpawner>();
            }
        }

        if (decorSpawner != null)
        {
            decorSpawner.SpawnDecorations(decorParent, mapGenerator);
        }

        IsBuilt = true;
        Debug.Log($"[MapBuilder] Berhasil membangun peta: {width}x{height}, {runtimeLanes.Count} jalur runtime.");
        OnMapBuilt?.Invoke();
    }

    /// <summary>
    /// Menghapus seluruh objek peta yang sudah dibangun.
    /// </summary>
    [ContextMenu("Clear Map")]
    public void ClearMap()
    {
        runtimeLanes.Clear();
        IsBuilt = false;

        Transform existing = transform.Find("GeneratedMap");
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }
    }

    // ── Konstruksi Lantai Dasar ──────────────────────────────────────
    private void BuildFloor(Transform parent, int width, int height, float tileSize)
    {
        Vector3 center = mapGenerator.CellToWorld(0, 0)
            - new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f)
            + new Vector3(width * tileSize * 0.5f, 0f, height * tileSize * 0.5f);

        float targetWidth = width * tileSize + floorPadding * 2f;
        float targetHeight = height * tileSize + floorPadding * 2f;

        if (floorPrefab != null)
        {
            GameObject floor = Instantiate(floorPrefab, center, Quaternion.identity, parent);
            floor.name = "Floor";

            // Deteksi ukuran mesh dasar prefab agar skala menyesuaikan ukuran penuh peta
            MeshFilter mf = floor.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 meshSize = mf.sharedMesh.bounds.size;
                float baseW = Mathf.Max(0.001f, meshSize.x);
                float baseH = Mathf.Max(0.001f, meshSize.z > 0.001f ? meshSize.z : meshSize.y);
                floor.transform.localScale = new Vector3(targetWidth / baseW, floorPrefab.transform.localScale.y, targetHeight / baseH);
            }
            else
            {
                floor.transform.localScale = new Vector3(targetWidth, 1f, targetHeight);
            }
        }
        else
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.position = center;
            // Default plane bawaan Unity berukuran dasar 10x10
            floor.transform.localScale = new Vector3(targetWidth / 10f, 1f, targetHeight / 10f);

            Renderer rend = floor.GetComponent<Renderer>();
            if (floorMaterial != null)
            {
                rend.material = floorMaterial;
            }
            else
            {
                rend.material.color = new Color(0.85f, 0.75f, 0.55f); // Warna pasir
            }
        }
    }

    // ── Konstruksi Tile Jalur ────────────────────────────────────────
    private void CreatePathTile(Vector3 pos, float tileSize, Transform parent)
    {
        if (pathTilePrefab != null)
        {
            Instantiate(pathTilePrefab, pos, Quaternion.identity, parent);
        }
        else
        {
            // Primitif fallback: kubus tipis warna jalur tanah
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "PathTile";
            tile.transform.SetParent(parent, false);
            tile.transform.position = pos + Vector3.up * 0.02f;
            tile.transform.localScale = new Vector3(tileSize * 0.98f, 0.04f, tileSize * 0.98f);

            Renderer rend = tile.GetComponent<Renderer>();
            rend.material.color = new Color(0.72f, 0.60f, 0.40f);
        }
    }

    // ── Konstruksi Node Pembangunan ──────────────────────────────────
    private void CreateNode(Vector3 pos, float tileSize, NodeType type, Transform parent)
    {
        GameObject prefabToUse = type == NodeType.Path
            ? (barrackNodePrefab != null ? barrackNodePrefab : groundNodePrefab)
            : groundNodePrefab;

        GameObject nodeObj;
        if (prefabToUse != null)
        {
            nodeObj = Instantiate(prefabToUse, pos, Quaternion.identity, parent);
        }
        else
        {
            // Primitif fallback jika user belum mengisi prefab di Inspector
            nodeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nodeObj.transform.SetParent(parent, false);
            nodeObj.transform.position = pos + Vector3.up * 0.1f;
            nodeObj.transform.localScale = new Vector3(tileSize * 0.8f, 0.2f, tileSize * 0.8f);

            if (nodeObj.GetComponent<BuildNode>() == null)
            {
                nodeObj.AddComponent<BuildNode>();
            }
        }

        BuildNode bn = nodeObj.GetComponent<BuildNode>();
        if (bn != null)
        {
            bn.Initialize(type);
        }
        else
        {
            Debug.LogWarning($"[MapBuilder] Prefab node pada posisi {pos} tidak memiliki komponen BuildNode!");
        }
    }

    // ── Konstruksi Base ──────────────────────────────────────────────
    private void CreateBase(Vector3 pos, float tileSize, Transform parent)
    {
        if (basePrefab != null)
        {
            Instantiate(basePrefab, pos, Quaternion.identity, parent);
        }
        else
        {
            // Primitif fallback warna merah
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "BaseCrystal";
            baseObj.transform.SetParent(parent, false);
            baseObj.transform.position = pos + Vector3.up * 0.5f;
            baseObj.transform.localScale = new Vector3(tileSize * 0.6f, 0.5f, tileSize * 0.6f);

            Renderer rend = baseObj.GetComponent<Renderer>();
            rend.material.color = new Color(0.9f, 0.2f, 0.2f);
        }
    }

    // ── Konstruksi Waypoint & Jalur Runtime ───────────────────────────
    private void BuildLanesAndWaypoints(Transform parent)
    {
        runtimeLanes.Clear();

        foreach (var laneData in mapGenerator.Lanes)
        {
            GameObject laneGo = new GameObject($"Lane_{laneData.LaneIndex}");
            laneGo.transform.SetParent(parent, false);

            // Titik spawn musuh
            Vector3 spawnWorldPos = mapGenerator.CellToWorld(laneData.SpawnCell.x, laneData.SpawnCell.y);
            spawnWorldPos.y = waypointY;

            GameObject spawnPointGo = new GameObject("SpawnPoint");
            spawnPointGo.transform.SetParent(laneGo.transform, false);
            spawnPointGo.transform.position = spawnWorldPos;

            if (spawnerPrefab != null)
            {
                Instantiate(spawnerPrefab, spawnWorldPos, Quaternion.identity, spawnPointGo.transform);
            }
            else
            {
                // Penanda spawner primitif (portal hijau)
                GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                portal.name = "PortalMarker";
                portal.transform.SetParent(spawnPointGo.transform, false);
                portal.transform.position = spawnWorldPos;
                portal.transform.localScale = Vector3.one * (mapGenerator.TileSize * 0.5f);
                portal.GetComponent<Renderer>().material.color = Color.green;
            }

            var laneRuntime = new LaneRuntime
            {
                LaneIndex = laneData.LaneIndex,
                SpawnPoint = spawnPointGo.transform
            };

            // Bangun WaypointPath untuk tiap rute di dalam jalur ini
            for (int r = 0; r < laneData.Routes.Count; r++)
            {
                var route = laneData.Routes[r];
                GameObject routeGo = new GameObject($"Route_{laneData.LaneIndex}_{r}");
                routeGo.transform.SetParent(laneGo.transform, false);

                // Buat anak Transform untuk tiap sel rute (rapat, 1 per sel untuk rally point Barrack)
                Transform[] routePoints = new Transform[route.Count];
                for (int i = 0; i < route.Count; i++)
                {
                    GameObject wpGo = new GameObject($"Wp_{i:D3}");
                    wpGo.transform.SetParent(routeGo.transform, false);

                    Vector3 wpPos = mapGenerator.CellToWorld(route[i].x, route[i].y);
                    wpPos.y = waypointY;
                    wpGo.transform.position = wpPos;

                    routePoints[i] = wpGo.transform;
                }

                // Pasang komponen WaypointPath dan isi poin eksplisit
                WaypointPath wpPath = routeGo.AddComponent<WaypointPath>();
                wpPath.points = routePoints;

                laneRuntime.Routes.Add(wpPath);
            }

            runtimeLanes.Add(laneRuntime);
        }
    }
}
