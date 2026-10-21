using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct TowerData
{
    public string towerName;
    public GameObject prefab;
    public int cost;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Status Pemain")]
    [SerializeField] private int startingGold = 100;
    [SerializeField] private int baseHealth = 10;

    [Header("Daftar Tower")]
    [SerializeField] private TowerData[] availableTowers;

    public int CurrentGold { get; private set; }
    public int CurrentBaseHealth { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }

    public bool GameEnded => IsGameOver || IsVictory;
    public int MaxBaseHealth => baseHealth;
    public TowerData[] AvailableTowers => availableTowers;

    // Menyimpan data tower yang sedang dipilih dari UI
    public TowerData SelectedTower { get; private set; }
    public bool HasSelectedTower => SelectedTower.prefab != null;

    private void Awake()
    {
        Time.timeScale = 1f;

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
        IsVictory = false;

        // Pilih tower pertama secara default jika ada
        if (availableTowers != null && availableTowers.Length > 0)
        {
            SelectTower(0);
        }
    }

    public void SelectTower(int index)
    {
        if (index >= 0 && index < availableTowers.Length)
        {
            SelectedTower = availableTowers[index];
            Debug.Log($"Tower dipilih: {SelectedTower.towerName} (Harga: {SelectedTower.cost})");
        }
    }

    public bool CanAffordSelectedTower()
    {
        return !GameEnded && HasSelectedTower && CurrentGold >= SelectedTower.cost;
    }

    public bool BuySelectedTower()
    {
        if (CanAffordSelectedTower())
        {
            CurrentGold -= SelectedTower.cost;
            Debug.Log($"Berhasil membangun {SelectedTower.towerName}! Sisa Gold: {CurrentGold}");
            return true;
        }
        return false;
    }

    public void AddGold(int amount)
    {
        CurrentGold += amount;
    }

    public void DamageBase(int amount)
    {
        if (GameEnded) return;

        CurrentBaseHealth -= amount;

        if (CurrentBaseHealth <= 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        IsGameOver = true;
        Time.timeScale = 0f;
    }

    public void Victory()
    {
        if (GameEnded) return;

        IsVictory = true;
        Time.timeScale = 0f;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}