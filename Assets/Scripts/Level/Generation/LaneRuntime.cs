using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data runtime untuk satu jalur musuh di scene:
/// menampung Transform spawn point dan daftar WaypointPath (rute) di jalur tersebut.
/// </summary>
public class LaneRuntime
{
    public int LaneIndex;
    public Transform SpawnPoint;
    public List<WaypointPath> Routes = new List<WaypointPath>();
}
