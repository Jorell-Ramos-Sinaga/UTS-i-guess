using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data satu jalur musuh: indeks jalur, sel spawn, dan daftar rute dari spawn ke base.
/// routes[0] = rute utama. Rute tambahan dari cabang.
/// </summary>
public class LaneData
{
    public int LaneIndex;
    public Vector2Int SpawnCell;
    // Tiap rute = daftar sel berurutan dari spawn ke base (inklusif)
    public List<List<Vector2Int>> Routes = new List<List<Vector2Int>>();
}
