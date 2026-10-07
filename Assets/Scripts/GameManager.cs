using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Status Pemain")]
    [SerializeField] private int startingGold = 100;
    [SerializeField] private int towerCost = 50;
    [SerializeField] private int baseHealth = 10;

    [Header("Tower Prefab")]
    [SerializeField] private GameObject defaultTowerPrefab;

    public int CurrentGold { get; private set; }
    public int CurrentBaseHealth { get; private set; }
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CurrentGold = startingGold;
        CurrentBaseHealth = baseHealth;
        IsGameOver = false;

        Debug.Log($"Game Dimulai! Gold: {CurrentGold} | Base HP: {CurrentBaseHealth}");
    }

    public bool CanAffordTower()
    {
        return CurrentGold >= towerCost;
    }

    public bool BuyTower()
    {
        if (CanAffordTower())
        {
            CurrentGold -= towerCost;
            Debug.Log($"Tower Dibeli! Sisa Gold: {CurrentGold}");
            return true;
        }
        return false;
    }

    public void AddGold(int amount)
    {
        CurrentGold += amount;
        Debug.Log($"Dapat Gold: +{amount} | Total Gold: {CurrentGold}");
    }

    public void DamageBase(int amount)
    {
        if (IsGameOver) return;

        CurrentBaseHealth -= amount;
        Debug.Log($"Base Terserang! Sisa Base HP: {CurrentBaseHealth}");

        if (CurrentBaseHealth <= 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        IsGameOver = true;
        Debug.LogWarning("GAME OVER! Markas Anda hancur!");
    }

    public GameObject GetTowerPrefab()
    {
        return defaultTowerPrefab;
    }
}
