using UnityEngine;

/// <summary>Tipe node pembangunan. Ground = di samping jalur, Path = tepat di atas jalur.</summary>
public enum NodeType
{
    Ground,
    Path
}

[RequireComponent(typeof(Collider))]
public class BuildNode : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color notEnoughGoldColor = Color.red;
    [Tooltip("Warna saat tower yang dipilih tidak boleh dibangun di tipe node ini.")]
    [SerializeField] private Color wrongTypeColor = new Color(1f, 0.6f, 0f);

    [Header("Node Type")]
    [Tooltip("Ground = untuk Archer/Cannon. Path = untuk Barrack di atas jalur. Harus cocok dengan 'Placeable On' tower di GameManager.")]
    [SerializeField] private NodeType nodeType = NodeType.Ground;

    [Header("Placement Offset")]
    [Tooltip("Ketinggian posisi spawn tower relatif terhadap posisi node (default: 0.43).")]
    [SerializeField] private float towerOffsetY = 0.43f;

    private GameObject currentTower;
    private Renderer nodeRenderer;
    private Color startColor;

    public NodeType Type => nodeType;

    /// <summary>
    /// Diinisialisasi secara prosedural oleh MapBuilder saat spawn.
    /// Mengatur tipe node dan memberi visual tint pembeda untuk Barrack node (Path).
    /// </summary>
    public void Initialize(NodeType type)
    {
        nodeType = type;
        if (nodeRenderer == null)
        {
            nodeRenderer = GetComponent<Renderer>();
        }

        if (nodeRenderer != null)
        {
            if (nodeType == NodeType.Path)
            {
                // Tint pembeda (ungu/magenta) untuk node Barrack di atas/samping jalan
                nodeRenderer.material.color = new Color(0.85f, 0.45f, 0.95f);
            }
            startColor = nodeRenderer.material.color;
        }
    }

    // Apakah tower yang sedang dipilih boleh dibangun di tipe node ini?
    private bool IsSelectedTowerAllowed()
    {
        return GameManager.Instance != null
            && GameManager.Instance.HasSelectedTower
            && GameManager.Instance.SelectedTower.placeableOn == nodeType;
    }

    private void Start()
    {
        if (nodeRenderer == null)
        {
            nodeRenderer = GetComponent<Renderer>();
        }

        if (nodeRenderer != null && startColor == default)
        {
            startColor = nodeRenderer.material.color;
        }
    }

    private void OnMouseEnter()
    {
        if (currentTower != null || GameManager.Instance == null) return;

        if (nodeRenderer != null)
        {
            bool canAfford = GameManager.Instance.CanAffordSelectedTower();
            Color feedback = canAfford ? hoverColor : notEnoughGoldColor;

            // Tipe node tidak cocok dengan tower yang dipilih
            if (GameManager.Instance.HasSelectedTower && !IsSelectedTowerAllowed())
            {
                feedback = wrongTypeColor;
            }

            nodeRenderer.material.color = feedback;
        }
    }

    private void OnMouseExit()
    {
        if (nodeRenderer != null)
        {
            nodeRenderer.material.color = startColor;
        }
    }

    private void OnMouseDown()
    {
        // Cegah membangun tower jika sedang mengklik NPC di mode AI Debug
        if (AIDebugPanel.Instance != null && AIDebugPanel.Instance.WasNpcClickedThisFrame)
        {
            return;
        }

        if (currentTower != null)
        {
            Debug.Log("Node ini sudah memiliki Tower!");
            return;
        }

        if (GameManager.Instance == null || !GameManager.Instance.HasSelectedTower) return;

        if (!IsSelectedTowerAllowed())
        {
            Debug.Log($"{GameManager.Instance.SelectedTower.towerName} tidak bisa dibangun di node bertipe {nodeType}.");
            return;
        }

        TowerData towerToBuild = GameManager.Instance.SelectedTower;

        if (GameManager.Instance.BuySelectedTower())
        {
            Vector3 buildPosition = transform.position + Vector3.up * towerOffsetY;
            currentTower = Instantiate(towerToBuild.prefab, buildPosition, towerToBuild.prefab.transform.rotation);

            if (nodeRenderer != null)
            {
                nodeRenderer.material.color = startColor;
            }
        }
        else
        {
            Debug.Log("Gold tidak mencukupi atau belum memilih jenis Tower!");
        }
    }

    // Kotak di Scene view: hijau = Ground, kuning = Path
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = nodeType == NodeType.Path ? Color.yellow : Color.green;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}