using UnityEngine;

public static class PathPerception
{
    public static int CountTowersNearPath(WaypointPath path, float radius)
    {
        if (path == null || path.points == null || path.points.Length == 0)
        {
            return 0;
        }

        Tower[] towers = Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
        int count = 0;

        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] == null) continue;

            if (IsNearPath(towers[i].transform.position, path, radius))
            {
                count++;
            }
        }

        return count;
    }

    public static WaypointPath ChooseSaferPath(WaypointPath pathA, WaypointPath pathB, float radius)
    {
        if (pathA == null) return pathB;
        if (pathB == null) return pathA;

        int threatA = CountTowersNearPath(pathA, radius);
        int threatB = CountTowersNearPath(pathB, radius);

        if (threatA < threatB) return pathA;
        if (threatB < threatA) return pathB;

        return Random.value < 0.5f ? pathA : pathB;
    }

    /// <summary>
    /// Memilih rute dengan ancaman tower paling sedikit dari sekumpulan rute di dalam jalur yang sama (M3).
    /// </summary>
    public static WaypointPath ChooseSaferRoute(System.Collections.Generic.List<WaypointPath> routes, float radius)
    {
        if (routes == null || routes.Count == 0) return null;
        if (routes.Count == 1) return routes[0];

        WaypointPath bestRoute = routes[0];
        int lowestThreat = CountTowersNearPath(routes[0], radius);

        for (int i = 1; i < routes.Count; i++)
        {
            if (routes[i] == null) continue;
            int threat = CountTowersNearPath(routes[i], radius);
            if (threat < lowestThreat)
            {
                lowestThreat = threat;
                bestRoute = routes[i];
            }
        }

        return bestRoute;
    }

    public static bool IsNearPath(Vector3 worldPosition, WaypointPath path, float radius)
    {
        if (path == null) return false;
        return IsNearPath(worldPosition, path.points, radius);
    }

    public static bool IsNearPath(Vector3 worldPosition, Transform[] points, float radius)
    {
        if (points == null || points.Length == 0)
        {
            return false;
        }

        float radiusSqr = radius * radius;

        if (points.Length == 1)
        {
            Transform onlyPoint = points[0];
            return onlyPoint != null &&
                   (worldPosition - onlyPoint.position).sqrMagnitude <= radiusSqr;
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
            Transform from = points[i];
            Transform to = points[i + 1];
            if (from == null || to == null) continue;

            Vector3 closest = ClosestPointOnSegment(worldPosition, from.position, to.position);
            if ((worldPosition - closest).sqrMagnitude <= radiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
        {
            return a;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
        return a + ab * t;
    }
}