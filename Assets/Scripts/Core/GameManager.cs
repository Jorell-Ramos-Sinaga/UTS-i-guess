using UnityEngine;
using UnityEngine.SceneManagement;
 
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
    public bool IsVictory { get; private set; }
 
    // True jika game sudah selesai, baik kalah maupun menang
    public bool GameEnded => IsGameOver || IsVictory;
 
    public int TowerCost => towerCost;
    public int MaxBaseHealth => baseHealth;
 
    private void Awake()
    {
        // Pastikan waktu berjalan normal (misalnya setelah restart dari layar Game Over)
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
 
        Debug.Log($"Game Dimulai! Gold: {CurrentGold} | Base HP: {CurrentBaseHealth}");
    }
 
    public bool CanAffordTower()
    {
        return !GameEnded && CurrentGold >= towerCost;
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
        if (GameEnded) return;
 
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
        Time.timeScale = 0f; // menjeda seluruh game
        Debug.LogWarning("GAME OVER! Markas Anda hancur!");
    }
 
    /// <summary>Dipanggil WaveSpawner saat semua wave selesai dan semua monster sudah dikalahkan.</summary>
    public void Victory()
    {
        if (GameEnded) return;
 
        IsVictory = true;
        Time.timeScale = 0f;
        Debug.Log("VICTORY! Semua wave berhasil dipertahankan!");
    }
 
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
 
    public GameObject GetTowerPrefab()
    {
        return defaultTowerPrefab;
    }
}