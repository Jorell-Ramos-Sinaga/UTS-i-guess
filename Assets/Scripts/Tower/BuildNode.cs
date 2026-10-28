using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BuildNode : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color notEnoughGoldColor = Color.red;

    [Header("Placement Offset")]
    [Tooltip("Ketinggian posisi spawn tower relatif terhadap posisi node (default: 0.43).")]
    [SerializeField] private float towerOffsetY = 0.43f;

    private GameObject currentTower;
    private Renderer nodeRenderer;
    private Color startColor;

    private void Start()
    {
        nodeRenderer = GetComponent<Renderer>();
        if (nodeRenderer != null)
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
            nodeRenderer.material.color = canAfford ? hoverColor : notEnoughGoldColor;
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
}