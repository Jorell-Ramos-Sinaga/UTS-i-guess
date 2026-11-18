using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float reachDistanceThreshold = 0.2f;
    [Tooltip("Ketinggian Y musuh saat berjalan (default: 0.55).")]
    [SerializeField] private float groundHeightY = 0.55f;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    // Sumber-sumber yang sedang menahan gerakan (Soldier, Warden, Bomber, dll.).
    // Musuh hanya bergerak jika tidak ada satu pun penahan, sehingga tiap script
    // tidak saling menimpa status gerak yang lain.
    private readonly HashSet<object> movementBlockers = new HashSet<object>();

    public bool IsBlocked
    {
        get
        {
            // Bersihkan penahan yang objeknya sudah dihancurkan
            movementBlockers.RemoveWhere(source => source is UnityEngine.Object unityObject && unityObject == null);
            return movementBlockers.Count > 0;
        }
    }

    /// <summary>Menahan atau melepas gerakan musuh atas nama 'source' (biasanya 'this').</summary>
    public void SetBlocked(object source, bool blocked)
    {
        if (source == null) return;

        if (blocked) movementBlockers.Add(source);
        else movementBlockers.Remove(source);
    }

    // Dibaca oleh AI (misalnya Skeleton Bomb) untuk mengetahui jalur yang sedang dipakai
    public Transform[] Waypoints => waypoints;
    public float Speed => speed;
    public int CurrentWaypointIndex => currentWaypointIndex;
    public int TotalWaypoints => waypoints != null ? waypoints.Length : 0;

    // Sisa jarak menuju base sepanjang path. Makin kecil = makin di depan.
    public float RemainingDistance
    {
        get
        {
            if (waypoints == null || currentWaypointIndex >= waypoints.Length) return 0f;
            if (waypoints[currentWaypointIndex] == null) return 0f;

            float total = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);

            for (int i = currentWaypointIndex; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null) continue;
                total += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
            }

            return total;
        }
    }

    public void SetupPath(Transform[] pathPoints, float customHeightY = -999f)
    {
        if (customHeightY > -900f) groundHeightY = customHeightY;

        waypoints = pathPoints;
        currentWaypointIndex = 0;

        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            Vector3 startPos = waypoints[0].position;
            startPos.y = groundHeightY;
            transform.position = startPos;
        }
    }

    private void Update()
    {
        if (IsBlocked) return;
        if (waypoints == null || waypoints.Length == 0) return;
        if (currentWaypointIndex >= waypoints.Length) return;

        Transform targetPoint = waypoints[currentWaypointIndex];
        if (targetPoint == null) return;

        Vector3 targetPos = targetPoint.position;
        targetPos.y = groundHeightY;

        // Hitung arah gerak
        Vector3 direction = (targetPos - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // Pastikan posisi Y tetap di groundHeightY
        Vector3 currentPos = transform.position;
        currentPos.y = groundHeightY;
        transform.position = currentPos;

        // Rotasi musuh menghadap arah jalan
        if (direction != Vector3.zero)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }

        // Cek apakah sudah dekat dengan target waypoint saat ini
        float distance = Vector3.Distance(transform.position, targetPos);
        if (distance <= reachDistanceThreshold)
        {
            currentWaypointIndex++;

            // Jika sudah mencapai titik terakhir (Base)
            if (currentWaypointIndex >= waypoints.Length)
            {
                ReachEndPath();
            }
        }
    }

    private void ReachEndPath()
    {
        // Mengurangi nyawa Base melalui GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DamageBase(1);
        }

        // Musuh hancur saat berhasil mencapai markas
        Destroy(gameObject);
    }
}