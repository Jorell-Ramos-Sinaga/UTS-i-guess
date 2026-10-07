using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float reachDistanceThreshold = 0.2f;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    public void SetupPath(Transform[] pathPoints)
    {
        waypoints = pathPoints;
        currentWaypointIndex = 0;

        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            transform.position = waypoints[0].position;
        }
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (currentWaypointIndex >= waypoints.Length) return;

        Transform targetPoint = waypoints[currentWaypointIndex];
        if (targetPoint == null) return;

        // Hitung arah gerak
        Vector3 direction = (targetPoint.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // Rotasi musuh menghadap arah jalan
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // Cek apakah sudah dekat dengan target waypoint saat ini
        float distance = Vector3.Distance(transform.position, targetPoint.position);
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
