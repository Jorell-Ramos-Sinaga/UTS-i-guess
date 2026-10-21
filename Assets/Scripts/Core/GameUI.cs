using UnityEngine;
 
/// <summary>
/// HUD sederhana (gold, HP base, wave) dan layar Game Over / Victory dengan tombol Restart.
/// Memakai OnGUI sehingga tidak perlu Canvas atau TextMeshPro. Pasang di objek apa pun di scene
/// (misalnya objek GameManager).
/// </summary>
public class GameUI : MonoBehaviour
{
    [Tooltip("Boleh dikosongkan, akan dicari otomatis.")]
    [SerializeField] private WaveSpawner waveSpawner;
 
    private GUIStyle hudStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private int baseFontSize;
 
    private void Start()
    {
        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
        }
    }
 
    private void OnGUI()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
 
        SetupStyles();
        DrawHud(gm);
 
        if (gm.GameEnded)
        {
            DrawEndScreen(gm);
        }
    }
 
    private void SetupStyles()
    {
        // Ukuran huruf mengikuti tinggi layar supaya tetap terbaca di resolusi berbeda
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
        }
 
        hudStyle.fontSize = baseFontSize;
        titleStyle.fontSize = baseFontSize * 3;
        subtitleStyle.fontSize = Mathf.RoundToInt(baseFontSize * 1.2f);
        buttonStyle.fontSize = Mathf.RoundToInt(baseFontSize * 1.2f);
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
            waveText,
            $"Tower: {gm.TowerCost} gold"
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
 
    private void DrawEndScreen(GameManager gm)
    {
        // Layar digelapkan
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
 
        bool victory = gm.IsVictory;
 
        titleStyle.normal.textColor = victory ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
        float titleHeight = baseFontSize * 4f;
        GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, titleHeight),
                  victory ? "VICTORY!" : "GAME OVER", titleStyle);
 
        string subtitle;
        if (victory)
        {
            subtitle = "Semua wave berhasil dipertahankan!";
        }
        else
        {
            int totalWaves = waveSpawner != null ? waveSpawner.TotalWaves : 0;
            int currentWave = waveSpawner != null ? waveSpawner.CurrentWave : 0;
            subtitle = $"Base hancur di wave {currentWave}/{totalWaves}";
        }
 
        GUI.Label(new Rect(0f, Screen.height * 0.22f + titleHeight, Screen.width, baseFontSize * 2f), subtitle, subtitleStyle);
 
        float buttonWidth = Screen.height * 0.45f;
        float buttonHeight = baseFontSize * 2.6f;
        Rect buttonRect = new Rect((Screen.width - buttonWidth) / 2f, Screen.height * 0.6f, buttonWidth, buttonHeight);
 
        if (GUI.Button(buttonRect, "Restart", buttonStyle))
        {
            gm.RestartLevel();
        }
    }
}