using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    [Header("Daftar Titik Waypoint")]
    [Tooltip("Urutkan titik dari spawn point sampai ke base")]
    public Transform[] points;

    private void Awake()
    {
        // Jika belum diisi manual di Inspector, ambil semua anak Transform secara otomatis
        if (points == null || points.Length == 0)
        {
            points = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                points[i] = transform.GetChild(i);
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Menggambar garis jalur di Scene View agar mudah dilihat
        if (transform.childCount < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < transform.childCount - 1; i++)
        {
            Transform p1 = transform.GetChild(i);
            Transform p2 = transform.GetChild(i + 1);
            if (p1 != null && p2 != null)
            {
                Gizmos.DrawLine(p1.position, p2.position);
                Gizmos.DrawWireSphere(p1.position, 0.3f);
            }
        }

        // Gambar titik terakhir
        Transform last = transform.GetChild(transform.childCount - 1);
        if (last != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(last.position, 0.4f);
        }
    }
}
