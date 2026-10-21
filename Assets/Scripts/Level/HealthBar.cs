using UnityEngine;

/// <summary>
/// HP bar yang melayang di atas objek. Cukup pasang di prefab yang punya
/// EnemyHealth atau TowerHealth. Tidak perlu Canvas, prefab, atau material tambahan:
/// bar dibuat lewat kode dari dua SpriteRenderer, lalu selalu menghadap kamera.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("Ukuran (world units)")]
    [SerializeField] private float width = 1.2f;
    [SerializeField] private float height = 0.15f;
    [SerializeField] private float border = 0.03f;

    [Header("Posisi")]
    [Tooltip("Jarak tambahan di atas bagian tertinggi model.")]
    [SerializeField] private float heightPadding = 0.4f;
    [Tooltip("Geser bar ke arah atas layar. Penting untuk kamera dari atas (top-down), supaya bar tidak menumpuk di tengah model. Isi kira-kira setengah ukuran model + sedikit jarak.")]
    [SerializeField] private float screenUpOffset = 0.8f;

    [Header("Tampilan")]
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private Color fullColor = Color.green;
    [SerializeField] private Color lowColor = Color.red;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

    private EnemyHealth enemyHealth;
    private TowerHealth towerHealth;

    private Transform barRoot;
    private Transform fillTransform;
    private SpriteRenderer fillRenderer;
    private Camera cam;
    private float yOffset = 2f;

    private static Sprite whiteSprite;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        towerHealth = GetComponent<TowerHealth>();

        if (enemyHealth == null && towerHealth == null)
        {
            Debug.LogWarning($"{name}: HealthBar butuh EnemyHealth atau TowerHealth di objek yang sama.");
            enabled = false;
            return;
        }

        yOffset = CalculateHeightOffset();
        BuildBar();
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // Ikuti posisi objek tanpa mewarisi scale/rotasi, dan selalu menghadap kamera
        Vector3 p = transform.position;
        Vector3 position = new Vector3(p.x, p.y + yOffset, p.z);

        // Makin curam kamera melihat ke bawah, makin besar geseran ke atas layar
        float lookDown = Mathf.Clamp01(-cam.transform.forward.y);
        position += cam.transform.up * (screenUpOffset * lookDown);

        barRoot.position = position;
        barRoot.rotation = cam.transform.rotation;

        float percent = Mathf.Clamp01(GetHealthPercent());

        fillTransform.localScale = new Vector3(width * percent, height, 1f);
        fillRenderer.color = Color.Lerp(lowColor, fullColor, percent);

        if (hideWhenFull)
        {
            barRoot.gameObject.SetActive(percent < 0.999f);
        }
    }

    private void OnDestroy()
    {
        if (barRoot != null)
        {
            Destroy(barRoot.gameObject);
        }
    }

    private float GetHealthPercent()
    {
        if (enemyHealth != null) return enemyHealth.HealthPercent;
        if (towerHealth != null) return towerHealth.HealthPercent;
        return 1f;
    }

    // Hitung tinggi bagian paling atas model, supaya bar tidak menembus model
    private float CalculateHeightOffset()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds) return 2f;

        return (bounds.max.y - transform.position.y) + heightPadding;
    }

    private void BuildBar()
    {
        barRoot = new GameObject(name + "_HealthBar").transform;

        // Background (sedikit lebih besar dari fill, sebagai bingkai)
        SpriteRenderer bg = CreateSpriteObject("Background", backgroundColor, 100);
        bg.transform.localScale = new Vector3(width + border * 2f, height + border * 2f, 1f);
        bg.transform.localPosition = new Vector3(-width / 2f - border, 0f, 0f);

        // Fill (pivot di kiri, jadi mengecil dari kanan ke kiri)
        fillRenderer = CreateSpriteObject("Fill", fullColor, 101);
        fillTransform = fillRenderer.transform;
        fillTransform.localScale = new Vector3(width, height, 1f);
        fillTransform.localPosition = new Vector3(-width / 2f, 0f, 0f);
    }

    private SpriteRenderer CreateSpriteObject(string objName, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(barRoot, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    // Sprite putih 1x1 unit dengan pivot di kiri-tengah, dipakai bersama oleh semua bar
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        }
        return whiteSprite;
    }
}