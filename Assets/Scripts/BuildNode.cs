using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BuildNode : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color notEnoughGoldColor = Color.red;

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
            bool canAfford = GameManager.Instance.CanAffordTower();
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
        // Cek apakah node sudah ada towernya
        if (currentTower != null)
        {
            Debug.Log("Node ini sudah memiliki Tower!");
            return;
        }

        if (GameManager.Instance == null) return;

        // Cek apakah koin mencukupi
        if (GameManager.Instance.BuyTower())
        {
            Vector3 buildPosition = transform.position + Vector3.up * 0.5f;
            currentTower = Instantiate(GameManager.Instance.GetTowerPrefab(), buildPosition, Quaternion.identity);

            // Reset warna node kembali normal
            if (nodeRenderer != null)
            {
                nodeRenderer.material.color = startColor;
            }
        }
        else
        {
            Debug.Log("Gold tidak mencukupi untuk membangun Tower!");
        }
    }
}
