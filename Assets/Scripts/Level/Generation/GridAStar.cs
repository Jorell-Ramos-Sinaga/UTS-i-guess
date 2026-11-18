using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pada grid 2D. Dipakai MapGenerator untuk membangun koridor jalur musuh
/// dan mencari rute dari spawner ke base.
/// Hanya bergerak 4 arah (N/S/E/W).
/// </summary>
public static class GridAStar
{
    // Cari jalur dari start ke goal. forbiddenTypes = set tipe sel yang tidak boleh dilalui
    // (kecuali goal itu sendiri). Kembalikan null jika tidak ditemukan.
    public static List<Vector2Int> FindPath(
        MapCell[,] grid,
        Vector2Int start,
        Vector2Int goal,
        HashSet<MapCell> forbiddenTypes,
        int width, int height)
    {
        var open   = new SortedList<float, Queue<Vector2Int>>();
        var gScore = new Dictionary<Vector2Int, float>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();

        gScore[start] = 0f;
        Enqueue(open, Heuristic(start, goal), start);

        while (open.Count > 0)
        {
            Vector2Int cur = Dequeue(open);
            if (cur == goal)
                return Reconstruct(parent, start, goal);

            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            foreach (var nb in Neighbors(cur, width, height))
            {
                if (closed.Contains(nb)) continue;
                // Boleh melewati sel yang tipenya bukan terlarang, atau sel goal
                MapCell cellType = grid[nb.x, nb.y];
                if (forbiddenTypes != null && forbiddenTypes.Contains(cellType) && nb != goal)
                    continue;

                float newG = gScore[cur] + 1f;
                if (!gScore.TryGetValue(nb, out float existing) || newG < existing)
                {
                    gScore[nb] = newG;
                    parent[nb] = cur;
                    float f = newG + Heuristic(nb, goal);
                    Enqueue(open, f, nb);
                }
            }
        }
        return null; // tidak ditemukan
    }

    // Cari jalur dengan biaya tambahan pada sel Path (mendorong menghindari jalur lain)
    public static List<Vector2Int> FindPathAvoidPath(
        MapCell[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int width, int height,
        float pathCostPenalty = 1000f)
    {
        var open   = new SortedList<float, Queue<Vector2Int>>();
        var gScore = new Dictionary<Vector2Int, float>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();

        gScore[start] = 0f;
        Enqueue(open, Heuristic(start, goal), start);

        while (open.Count > 0)
        {
            Vector2Int cur = Dequeue(open);
            if (cur == goal)
                return Reconstruct(parent, start, goal);

            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            foreach (var nb in Neighbors(cur, width, height))
            {
                if (closed.Contains(nb)) continue;
                MapCell cellType = grid[nb.x, nb.y];

                float stepCost = 1f;
                if ((cellType == MapCell.Path || cellType == MapCell.Spawn || cellType == MapCell.Base)
                    && nb != goal)
                    stepCost += pathCostPenalty;

                float newG = gScore[cur] + stepCost;
                if (!gScore.TryGetValue(nb, out float existing) || newG < existing)
                {
                    gScore[nb] = newG;
                    parent[nb] = cur;
                    float f = newG + Heuristic(nb, goal);
                    Enqueue(open, f, nb);
                }
            }
        }
        return null;
    }

    // ── Cari segmen koridor antar-jangkar yang secara aktif menghindari menempel pada existingCells (Chebyshev >= 2) ──
    public static List<Vector2Int> FindCorridorSegment(
        Vector2Int start,
        Vector2Int goal,
        HashSet<Vector2Int> existingCells,
        Vector2Int baseCell,
        int width, int height)
    {
        var open   = new SortedList<float, Queue<Vector2Int>>();
        var gScore = new Dictionary<Vector2Int, float>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();

        gScore[start] = 0f;
        Enqueue(open, Heuristic(start, goal), start);

        while (open.Count > 0)
        {
            Vector2Int cur = Dequeue(open);
            if (cur == goal)
                return Reconstruct(parent, start, goal);

            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            foreach (var nb in Neighbors(cur, width, height))
            {
                if (closed.Contains(nb)) continue;

                // Tidak boleh melintasi existingCells kecuali di titik goal
                if (existingCells != null && existingCells.Contains(nb) && nb != goal)
                    continue;

                // Hitung penalti jika sel bersebelahan (Chebyshev 1) dengan existingCells
                float stepCost = 1f;
                bool nearExisting = false;
                if (existingCells != null && nb != goal && Vector2Int.Distance(nb, baseCell) > 2.5f)
                {
                    for (int dx = -1; dx <= 1 && !nearExisting; dx++)
                    for (int dy = -1; dy <= 1 && !nearExisting; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var adj = new Vector2Int(nb.x + dx, nb.y + dy);
                        if (existingCells.Contains(adj) && adj != cur && adj != start && adj != goal)
                        {
                            nearExisting = true;
                        }
                    }
                }

                if (nearExisting)
                    stepCost += 500f; // Jauhi jalur lama agar tidak menempel / membentuk jalur tebal

                float newG = gScore[cur] + stepCost;
                if (!gScore.TryGetValue(nb, out float existing) || newG < existing)
                {
                    gScore[nb] = newG;
                    parent[nb] = cur;
                    float f = newG + Heuristic(nb, goal);
                    Enqueue(open, f, nb);
                }
            }
        }
        return null;
    }

    // ── Temukan hingga maxRoutes rute berbeda dari start ke goal HANYA lewat sel jalur ──
    public static List<List<Vector2Int>> FindAllSimpleRoutes(
        MapCell[,] grid,
        Vector2Int start,
        Vector2Int goal,
        int maxRoutes,
        int width, int height)
    {
        var results = new List<List<Vector2Int>>();
        var pathOnlyForbidden = new HashSet<MapCell> { MapCell.Empty, MapCell.Node, MapCell.BarrackNode };

        // Rute pertama: A* hanya melalui sel jalur (Path, Spawn, Base)
        var first = FindPath(grid, start, goal, pathOnlyForbidden, width, height);
        if (first == null) return results;
        results.Add(first);
        if (maxRoutes <= 1) return results;

        // Rute tambahan (variasi cabang): blokir sel-sel tengah untuk menemukan jalur alternatif
        var blocked = new HashSet<Vector2Int>();
        int maxIter = 100;
        int iter = 0;

        for (int ri = 0; ri < results.Count && results.Count < maxRoutes && iter < maxIter; ri++)
        {
            var baseRoute = results[ri];
            for (int pi = 1; pi < baseRoute.Count - 1 && results.Count < maxRoutes; pi++, iter++)
            {
                var blockCell = baseRoute[pi];
                if (blocked.Contains(blockCell)) continue;
                blocked.Add(blockCell);

                var tempGrid = (MapCell[,])grid.Clone();
                tempGrid[blockCell.x, blockCell.y] = MapCell.Empty; // Menjadi Empty yang forbidden

                var alt = FindPath(tempGrid, start, goal, pathOnlyForbidden, width, height);
                if (alt == null || alt.Count == 0) continue;

                // Pastikan rute tidak duplikat
                bool duplicate = false;
                foreach (var existing in results)
                {
                    if (existing.Count == alt.Count)
                    {
                        bool same = true;
                        for (int k = 0; k < existing.Count; k++)
                        {
                            if (existing[k] != alt[k]) { same = false; break; }
                        }
                        if (same) { duplicate = true; break; }
                    }
                }
                if (!duplicate) results.Add(alt);
            }
        }

        return results;
    }

    // ── Pembantu ──
    private static float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static IEnumerable<Vector2Int> Neighbors(Vector2Int p, int w, int h)
    {
        if (p.x > 0)     yield return new Vector2Int(p.x - 1, p.y);
        if (p.x < w - 1) yield return new Vector2Int(p.x + 1, p.y);
        if (p.y > 0)     yield return new Vector2Int(p.x, p.y - 1);
        if (p.y < h - 1) yield return new Vector2Int(p.x, p.y + 1);
    }

    private static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> parent, Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        Vector2Int cur = goal;
        while (cur != start)
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }

    private static void Enqueue(SortedList<float, Queue<Vector2Int>> sl, float f, Vector2Int v)
    {
        // SortedList tidak terima key duplikat; atasi dengan queue per f
        if (!sl.TryGetValue(f, out var q))
        {
            q = new Queue<Vector2Int>();
            sl[f] = q;
        }
        q.Enqueue(v);
    }

    private static Vector2Int Dequeue(SortedList<float, Queue<Vector2Int>> sl)
    {
        float key = sl.Keys[0];
        var q = sl.Values[0];
        Vector2Int v = q.Dequeue();
        if (q.Count == 0) sl.RemoveAt(0);
        return v;
    }
}
