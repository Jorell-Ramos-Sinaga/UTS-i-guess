using UnityEngine;
using UnityEngine.SceneManagement;

public class GameUI : MonoBehaviour
{
    [Tooltip("Boleh dikosongkan, akan dicari otomatis.")]
    [SerializeField] private WaveSpawner waveSpawner;
    [SerializeField] private string mainMenuSceneName = "MainMenuScene"; // Nama Scene Main Menu

    private GUIStyle hudStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle sidebarStyle;
    private int baseFontSize;

    private bool isPaused = false;

    private void Start()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
        }
    }

    private void Update()
    {
        // Tekan ESC untuk Toggle Pause Menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    private void OnGUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        SetupStyles();
        DrawHud(gm);
        DrawSideBar(gm);

        // Layar Pause
        if (isPaused && !gm.GameEnded)
        {
            DrawPauseScreen();
        }

        // Layar Game Over / Victory
        if (gm.GameEnded)
        {
            DrawEndScreen(gm);
        }
    }

    private void SetupStyles()
    {
        baseFontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.03f));

        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.richText = true;
            hudStyle.alignment = TextAnchor.MiddleLeft;
            hudStyle.normal.textColor = Color.white;

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;

            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button);

            sidebarStyle = new GUIStyle(GUI.skin.label);
            sidebarStyle.alignment = TextAnchor.UpperCenter;
            sidebarStyle.fontStyle = FontStyle.Bold;
            sidebarStyle.normal.textColor = Color.yellow;
        }

        hudStyle.fontSize = baseFontSize;
        titleStyle.fontSize = baseFontSize * 3;
        subtitleStyle.fontSize = Mathf.RoundToInt(baseFontSize * 1.2f);
        buttonStyle.fontSize = Mathf.RoundToInt(baseFontSize * 1.1f);
        sidebarStyle.fontSize = Mathf.RoundToInt(baseFontSize * 1.2f);
    }

    private void DrawHud(GameManager gm)
    {
        int totalWaves = waveSpawner != null ? waveSpawner.TotalWaves : 0;
        int currentWave = waveSpawner != null ? waveSpawner.CurrentWave : 0;

        string waveText = currentWave == 0
            ? $"Wave: bersiap (0/{totalWaves})"
            : $"Wave: {currentWave}/{totalWaves}";

        string hpColor = gm.CurrentBaseHealth <= 3 ? "#FF5555" : "#FFFFFF";

        string[] lines =
        {
            $"<color=#FFD700>Gold: {gm.CurrentGold}</color>",
            $"<color={hpColor}>Base HP: {Mathf.Max(0, gm.CurrentBaseHealth)}/{gm.MaxBaseHealth}</color>",
            waveText
        };

        float lineHeight = baseFontSize * 1.6f;
        float padding = baseFontSize * 0.5f;
        float panelWidth = Screen.height * 0.42f;
        Rect panel = new Rect(10f, 10f, panelWidth, lines.Length * lineHeight + padding * 2f);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int i = 0; i < lines.Length; i++)
        {
            Rect line = new Rect(panel.x + padding, panel.y + padding + i * lineHeight, panelWidth - padding * 2f, lineHeight);
            GUI.Label(line, lines[i], hudStyle);
        }
    }

    private void DrawSideBar(GameManager gm)
    {
        TowerData[] towers = gm.AvailableTowers;
        if (towers == null || towers.Length == 0) return;

        float panelWidth = Screen.width * 0.2f;
        float panelHeight = Screen.height * 0.55f;
        float panelX = Screen.width - panelWidth - 10f;
        float panelY = 10f;

        Rect sidebarPanel = new Rect(panelX, panelY, panelWidth, panelHeight);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(sidebarPanel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect titleRect = new Rect(panelX, panelY + 10f, panelWidth, baseFontSize * 1.5f);
        GUI.Label(titleRect, "PILIH TOWER", sidebarStyle);

        float buttonY = panelY + baseFontSize * 2.5f;
        float buttonHeight = baseFontSize * 2.8f;
        float buttonWidth = panelWidth - 20f;

        for (int i = 0; i < towers.Length; i++)
        {
            TowerData t = towers[i];
            bool isSelected = gm.HasSelectedTower && gm.SelectedTower.prefab == t.prefab;
            string label = $"{t.towerName}\n({t.cost} Gold)";

            GUI.color = isSelected ? Color.green : Color.white;

            Rect btnRect = new Rect(panelX + 10f, buttonY + i * (buttonHeight + 10f), buttonWidth, buttonHeight);

            if (GUI.Button(btnRect, label, buttonStyle))
            {
                gm.SelectTower(i);
            }
        }

        GUI.color = Color.white;
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    private void DrawPauseScreen()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, baseFontSize * 3f), "GAME PAUSED", titleStyle);

        float buttonWidth = Screen.height * 0.45f;
        float buttonHeight = baseFontSize * 2.6f;
        float startY = Screen.height * 0.45f;

        // Tombol Resume
        if (GUI.Button(new Rect((Screen.width - buttonWidth) / 2f, startY, buttonWidth, buttonHeight), "Resume", buttonStyle))
        {
            TogglePause();
        }

        // Tombol Main Menu
        if (GUI.Button(new Rect((Screen.width - buttonWidth) / 2f, startY + buttonHeight + 15f, buttonWidth, buttonHeight), "Main Menu", buttonStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void DrawEndScreen(GameManager gm)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool victory = gm.IsVictory;

        titleStyle.normal.textColor = victory ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
        float titleHeight = baseFontSize * 4f;
        GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, titleHeight),
                  victory ? "VICTORY!" : "GAME OVER", titleStyle);

        string subtitle = victory
            ? "Semua wave berhasil dipertahankan!"
            : $"Base hancur di wave {(waveSpawner != null ? waveSpawner.CurrentWave : 0)}/{(waveSpawner != null ? waveSpawner.TotalWaves : 0)}";

        GUI.Label(new Rect(0f, Screen.height * 0.2f + titleHeight, Screen.width, baseFontSize * 2f), subtitle, subtitleStyle);

        float buttonWidth = Screen.height * 0.45f;
        float buttonHeight = baseFontSize * 2.6f;
        float startY = Screen.height * 0.55f;

        // Tombol Restart
        if (GUI.Button(new Rect((Screen.width - buttonWidth) / 2f, startY, buttonWidth, buttonHeight), "Restart", buttonStyle))
        {
            gm.RestartLevel();
        }

        // Tombol Main Menu pada Layar Game Over/Victory
        if (GUI.Button(new Rect((Screen.width - buttonWidth) / 2f, startY + buttonHeight + 15f, buttonWidth, buttonHeight), "Main Menu", buttonStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}