using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI Debug Panel & Inspector Mode:
/// - Memungkinkan pemain mengklik NPC apa pun di layar (Tomb Warden, Skeleton Bomb, Prajurit / Soldier, dsb.)
///   untuk melihat status AI secara mendalam (Utility AI scores, FSM state, timers, target, HP).
/// - Menyediakan visualisasi 3D (lingkaran radius heal/deteksi/ledakan) dan indikator di atas kepala NPC.
/// - Menyediakan fitur testing (Damage, Heal, Kill, Slow-Mo / Fast-Forward).
/// - Tekan F1 atau klik tombol [AI Debug: ON/OFF] di layar untuk toggle.
/// </summary>
public class AIDebugPanel : MonoBehaviour
{
    public static AIDebugPanel Instance { get; private set; }

    [Header("Debug Settings")]
    [Tooltip("Jika aktif, mengklik NPC akan membuka panel inspeksi AI.")]
    [SerializeField] private bool debugModeActive = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool showWorldRings = true;

    // NPC yang sedang dipilih
    private GameObject selectedNPC;

    // Cache komponen pada target yang dipilih
    private TombWardenAI wardenAI;
    private SkeletonBombAI bombAI;
    private Soldier soldier;
    private EnemyHealth enemyHealth;
    private EnemyMovement enemyMovement;

    // Visual ring renderer
    private GameObject ringContainer;
    private LineRenderer primaryRing;
    private LineRenderer secondaryRing;
    private LineRenderer targetLine;

    // GUI Styles
    private GUIStyle panelBoxStyle;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle badgeStyle;
    private GUIStyle buttonStyle;
    private GUIStyle overheadStyle;
    private GUIStyle toggleBtnStyle;
    private Texture2D whiteTexture;

    private Rect panelRect;
    private Rect toggleBtnRect;

    public bool IsDebugMode => debugModeActive;
    public bool HasSelectedNPC => selectedNPC != null;
    public bool WasNpcClickedThisFrame { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindFirstObjectByType<AIDebugPanel>() == null)
        {
            GameObject go = new GameObject("[AIDebugManager]");
            go.AddComponent<AIDebugPanel>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        whiteTexture = Texture2D.whiteTexture;
        SetupVisualRings();
    }

    private void Start()
    {
        UpdatePanelDimensions();
    }

    private void UpdatePanelDimensions()
    {
        float pWidth = Mathf.Clamp(Screen.width * 0.38f, 520f, 650f);
        float pHeight = Mathf.Clamp(Screen.height * 0.72f, 540f, 680f);
        panelRect = new Rect(20f, Screen.height - pHeight - 20f, pWidth, pHeight);

        toggleBtnRect = new Rect(20f, 130f, 210f, 40f);
    }

    private void Update()
    {
        WasNpcClickedThisFrame = false;

        // Toggle Debug Mode dengan tombol hotkey
        if (Input.GetKeyDown(toggleKey))
        {
            debugModeActive = !debugModeActive;
            if (!debugModeActive)
            {
                Deselect();
            }
        }

        if (!debugModeActive)
        {
            HideVisualRings();
            return;
        }

        // Handle klik kiri untuk memilih NPC
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }

        // Handle deselect jika menekan Escape saat panel terbuka dan tidak pause
        if (Input.GetKeyDown(KeyCode.Escape) && selectedNPC != null)
        {
            Deselect();
        }

        // Periksa apakah objek target yang dipilih sudah hancur
        if (selectedNPC != null)
        {
            UpdateVisualRings();
        }
        else
        {
            HideVisualRings();
        }
    }

    private void HandleMouseClick()
    {
        Vector2 mousePos = Input.mousePosition;
        // Konversi koordinat mouse untuk OnGUI (Y terbalik)
        Vector2 guiMousePos = new Vector2(mousePos.x, Screen.height - mousePos.y);

        // Abaikan klik jika kursor berada di atas panel Debug atau tombol toggle
        if (selectedNPC != null && panelRect.Contains(guiMousePos)) return;
        if (toggleBtnRect.Contains(guiMousePos)) return;

        // Abaikan juga jika di area sidebar kanan (Tower Shop)
        float sidebarWidth = Screen.width * 0.22f;
        if (guiMousePos.x >= Screen.width - sidebarWidth) return;

        // Tembakkan Raycast ke 3D World
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            // Cari NPC di collider atau parent-nya
            GameObject candidate = null;

            EnemyHealth eHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            Soldier sld = hit.collider.GetComponentInParent<Soldier>();
            TombWardenAI warden = hit.collider.GetComponentInParent<TombWardenAI>();
            SkeletonBombAI bomb = hit.collider.GetComponentInParent<SkeletonBombAI>();

            if (warden != null) candidate = warden.gameObject;
            else if (bomb != null) candidate = bomb.gameObject;
            else if (sld != null) candidate = sld.gameObject;
            else if (eHealth != null) candidate = eHealth.gameObject;

            if (candidate != null)
            {
                SelectNPC(candidate);
                WasNpcClickedThisFrame = true;
            }
        }
    }

    public void SelectNPC(GameObject npc)
    {
        selectedNPC = npc;

        // Ambil komponen-komponen yang relevan
        wardenAI = npc.GetComponent<TombWardenAI>();
        bombAI = npc.GetComponent<SkeletonBombAI>();
        soldier = npc.GetComponent<Soldier>();
        enemyHealth = npc.GetComponent<EnemyHealth>();
        enemyMovement = npc.GetComponent<EnemyMovement>();

        Debug.Log($"[AIDebug] Memilih NPC: {npc.name}");
    }

    public void Deselect()
    {
        selectedNPC = null;
        wardenAI = null;
        bombAI = null;
        soldier = null;
        enemyHealth = null;
        enemyMovement = null;
        HideVisualRings();
    }

    // =========================================================================
    // VISUAL 3D RINGS & LINES (Rendered directly in game)
    // =========================================================================

    private void SetupVisualRings()
    {
        ringContainer = new GameObject("[AIDebug_VisualRings]");
        ringContainer.transform.SetParent(transform);

        primaryRing = CreateRing("PrimaryRing", Color.green);
        secondaryRing = CreateRing("SecondaryRing", Color.cyan);
        targetLine = CreateLine("TargetLine", Color.red);

        HideVisualRings();
    }

    private LineRenderer CreateRing(string ringName, Color color)
    {
        GameObject go = new GameObject(ringName);
        go.transform.SetParent(ringContainer.transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 50;
        lr.startWidth = 0.12f;
        lr.endWidth = 0.12f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;

        return lr;
    }

    private LineRenderer CreateLine(string lineName, Color color)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(ringContainer.transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;

        return lr;
    }

    private void UpdateVisualRings()
    {
        if (!showWorldRings || selectedNPC == null)
        {
            HideVisualRings();
            return;
        }

        Vector3 pos = selectedNPC.transform.position;
        pos.y += 0.08f; // sedikit di atas permukaan tanah

        if (wardenAI != null)
        {
            // Lingkaran Hijau = Heal Radius
            DrawCirclePoints(primaryRing, pos, wardenAI.HealRadius, new Color(0.2f, 1f, 0.4f, 0.85f));
            // Lingkaran Cyan = Group Radius
            DrawCirclePoints(secondaryRing, pos, wardenAI.GroupRadius, new Color(0.2f, 0.8f, 1f, 0.5f));
            targetLine.enabled = false;
        }
        else if (bombAI != null)
        {
            // Lingkaran Kuning = Detection Radius
            DrawCirclePoints(primaryRing, pos, bombAI.DetectionRadius, new Color(1f, 0.9f, 0.2f, 0.8f));
            // Lingkaran Oranye/Merah = Explosion Radius
            DrawCirclePoints(secondaryRing, pos, bombAI.ExplosionRadius, new Color(1f, 0.35f, 0.1f, 0.9f));

            if (bombAI.LockedTarget != null)
            {
                targetLine.enabled = true;
                targetLine.SetPosition(0, pos + Vector3.up * 0.5f);
                targetLine.SetPosition(1, bombAI.LockedTarget.position + Vector3.up * 0.5f);
                targetLine.startColor = Color.red;
                targetLine.endColor = Color.yellow;
            }
            else
            {
                targetLine.enabled = false;
            }
        }
        else if (soldier != null)
        {
            // Lingkaran Biru = Block Radius
            DrawCirclePoints(primaryRing, pos, soldier.BlockRadius, new Color(0.3f, 0.6f, 1f, 0.85f));
            secondaryRing.enabled = false;

            if (soldier.CurrentEnemyTarget != null)
            {
                targetLine.enabled = true;
                targetLine.SetPosition(0, pos + Vector3.up * 0.5f);
                targetLine.SetPosition(1, soldier.CurrentEnemyTarget.position + Vector3.up * 0.5f);
                targetLine.startColor = Color.red;
                targetLine.endColor = Color.red;
            }
            else
            {
                // Garis ke Rally Point
                targetLine.enabled = true;
                targetLine.SetPosition(0, pos + Vector3.up * 0.3f);
                targetLine.SetPosition(1, soldier.TargetRallySpot + Vector3.up * 0.3f);
                targetLine.startColor = Color.cyan;
                targetLine.endColor = Color.cyan;
            }
        }
        else
        {
            // Musuh biasa: lingkaran putih kecil penanda seleksi
            DrawCirclePoints(primaryRing, pos, 1.2f, new Color(1f, 1f, 1f, 0.7f));
            secondaryRing.enabled = false;
            targetLine.enabled = false;
        }
    }

    private void DrawCirclePoints(LineRenderer lr, Vector3 center, float radius, Color color)
    {
        lr.enabled = true;
        lr.startColor = color;
        lr.endColor = color;
        int count = lr.positionCount;
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, center.y, z));
        }
    }

    private void HideVisualRings()
    {
        if (primaryRing != null) primaryRing.enabled = false;
        if (secondaryRing != null) secondaryRing.enabled = false;
        if (targetLine != null) targetLine.enabled = false;
    }

    // =========================================================================
    // ONGUI RENDERING (UI PANEL & OVERHEAD LABELS)
    // =========================================================================

    private void OnGUI()
    {
        UpdatePanelDimensions();
        InitStyles();

        // 1. Gambar Tombol Toggle AI Debug Mode di Layar
        DrawToggleButton();

        if (!debugModeActive) return;

        // 2. Gambar Overhead Label di atas NPC yang dipilih
        if (selectedNPC != null)
        {
            DrawOverheadBadge();
        }

        // 3. Gambar AI Debug Inspector Window
        if (selectedNPC != null)
        {
            DrawInspectorPanel();
        }
    }

    private void InitStyles()
    {
        if (panelBoxStyle != null) return;

        panelBoxStyle = new GUIStyle(GUI.skin.box);
        panelBoxStyle.normal.textColor = Color.white;

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 19;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = new Color(0.3f, 0.95f, 1f);

        subHeaderStyle = new GUIStyle(GUI.skin.label);
        subHeaderStyle.fontSize = 16;
        subHeaderStyle.fontStyle = FontStyle.Bold;
        subHeaderStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 15;
        labelStyle.richText = true;
        labelStyle.normal.textColor = Color.white;

        valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.fontSize = 15;
        valueStyle.fontStyle = FontStyle.Bold;
        valueStyle.richText = true;
        valueStyle.normal.textColor = Color.yellow;

        badgeStyle = new GUIStyle(GUI.skin.label);
        badgeStyle.fontSize = 15;
        badgeStyle.fontStyle = FontStyle.Bold;
        badgeStyle.alignment = TextAnchor.MiddleCenter;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.fontStyle = FontStyle.Bold;

        overheadStyle = new GUIStyle(GUI.skin.box);
        overheadStyle.fontSize = 14;
        overheadStyle.fontStyle = FontStyle.Bold;
        overheadStyle.alignment = TextAnchor.MiddleCenter;
        overheadStyle.richText = true;

        toggleBtnStyle = new GUIStyle(GUI.skin.button);
        toggleBtnStyle.fontSize = 14;
        toggleBtnStyle.fontStyle = FontStyle.Bold;
    }

    private void DrawToggleButton()
    {
        GUI.color = debugModeActive ? new Color(0.2f, 0.85f, 0.4f, 0.95f) : new Color(0.6f, 0.6f, 0.6f, 0.85f);
        string text = debugModeActive ? "🐛 AI Debug: ON (F1)" : "🐛 AI Debug: OFF (F1)";

        if (GUI.Button(toggleBtnRect, text, toggleBtnStyle))
        {
            debugModeActive = !debugModeActive;
            if (!debugModeActive) Deselect();
        }

        GUI.color = Color.white;
    }

    private void DrawOverheadBadge()
    {
        Camera cam = Camera.main;
        if (cam == null || selectedNPC == null) return;

        Vector3 headPos = selectedNPC.transform.position + Vector3.up * 2.2f;
        Vector3 screenPos = cam.WorldToScreenPoint(headPos);

        // Hanya tampilkan jika objek berada di depan kamera
        if (screenPos.z > 0f)
        {
            string info = selectedNPC.name;

            if (wardenAI != null)
                info = $"▼ <b>{selectedNPC.name}</b>\n<color=#55FF55>Action: {wardenAI.CurrentActionName}</color>";
            else if (bombAI != null)
                info = $"▼ <b>{selectedNPC.name}</b>\n<color=#FF9944>FSM: {bombAI.CurrentStateName}</color>";
            else if (soldier != null)
                info = $"▼ <b>Prajurit</b>\n<color=#44CCFF>{(soldier.CurrentEnemyTarget != null ? "Tarung" : "Patroli")}</color>";

            float badgeWidth = 180f;
            float badgeHeight = 42f;
            Rect badgeRect = new Rect(screenPos.x - badgeWidth / 2f, Screen.height - screenPos.y - badgeHeight, badgeWidth, badgeHeight);

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            GUI.DrawTexture(badgeRect, whiteTexture);
            GUI.color = Color.white;

            GUI.Label(badgeRect, info, overheadStyle);
        }
    }

    private void DrawInspectorPanel()
    {
        // Background panel semitransparan gelap
        GUI.color = new Color(0.08f, 0.09f, 0.14f, 0.94f);
        GUI.DrawTexture(panelRect, whiteTexture);
        GUI.color = Color.white;

        // Border panel
        GUI.Box(panelRect, GUIContent.none, panelBoxStyle);

        float pad = 16f;
        float curY = panelRect.y + pad;
        float width = panelRect.width - pad * 2f;

        // --- HEADER ---
        Rect headerRect = new Rect(panelRect.x + pad, curY, width - 45f, 28f);
        GUI.Label(headerRect, "🔍 AI DEBUG INSPECTOR", headerStyle);

        // Tombol Close [X]
        Rect closeRect = new Rect(panelRect.x + panelRect.width - pad - 36f, curY, 36f, 28f);
        if (GUI.Button(closeRect, "X", buttonStyle))
        {
            Deselect();
            return;
        }

        curY += 34f;

        // Subtitle Target
        string targetTitle = selectedNPC != null ? selectedNPC.name : "None";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 26f), $"Target: <b>{targetTitle}</b>", labelStyle);
        curY += 28f;

        // --- SECTION: HEALTH & POSITION ---
        DrawHealthBar(ref curY, width, pad);

        // --- SECTION: KONTEN SPESIFIK AI ---
        DrawDivider(ref curY, width, pad);

        if (wardenAI != null)
        {
            DrawWardenAIDebug(ref curY, width, pad);
        }
        else if (bombAI != null)
        {
            DrawBombAIDebug(ref curY, width, pad);
        }
        else if (soldier != null)
        {
            DrawSoldierDebug(ref curY, width, pad);
        }
        else if (enemyMovement != null)
        {
            DrawStandardEnemyDebug(ref curY, width, pad);
        }

        // --- SECTION: ACTION TOOLS / SANDBOX ---
        DrawDivider(ref curY, width, pad);
        DrawActionTools(ref curY, width, pad);
    }

    private void DrawHealthBar(ref float curY, float width, float pad)
    {
        float currentHP = 0f;
        float maxHP = 1f;

        if (enemyHealth != null)
        {
            currentHP = enemyHealth.CurrentHealth;
            maxHP = enemyHealth.MaxHealth;
        }
        else if (soldier != null)
        {
            currentHP = soldier.CurrentHealth;
            maxHP = soldier.MaxHealth;
        }

        float hpPct = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

        // Bar container
        Rect barBg = new Rect(panelRect.x + pad, curY, width, 24f);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(barBg, whiteTexture);

        // Bar Fill
        Color hpColor = hpPct > 0.5f ? Color.green : (hpPct > 0.25f ? Color.yellow : Color.red);
        GUI.color = hpColor;
        Rect barFill = new Rect(barBg.x, barBg.y, width * hpPct, 24f);
        GUI.DrawTexture(barFill, whiteTexture);
        GUI.color = Color.white;

        // Bar Text
        GUI.Label(barBg, $"  HP: {currentHP:F0} / {maxHP:F0} ({hpPct * 100f:F0}%)", labelStyle);
        curY += 30f;

        // Info posisi & gerak
        if (enemyMovement != null)
        {
            string moveInfo = $"Speed: {enemyMovement.Speed:F1} | Jarak ke Base: {enemyMovement.RemainingDistance:F1}m | WP: {enemyMovement.CurrentWaypointIndex}/{enemyMovement.TotalWaypoints}";
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), moveInfo, labelStyle);
            curY += 26f;
        }
    }

    private void DrawWardenAIDebug(ref float curY, float width, float pad)
    {
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), "⚡ UTILITY AI: TOMB WARDEN", subHeaderStyle);
        curY += 28f;

        // Aksi terpilih
        string actionName = wardenAI.CurrentActionName;
        Color actColor = actionName == "Heal" ? Color.green : (actionName == "Hold" ? Color.yellow : Color.cyan);
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), $"Aksi Saat Ini: <color=#{ColorUtility.ToHtmlStringRGB(actColor)}><b>[{actionName.ToUpper()}]</b></color>", labelStyle);
        curY += 26f;

        // Skor Utility
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), "<b>Utility Scores (Tertinggi Menang):</b>", labelStyle);
        curY += 24f;

        DrawScoreMeter(ref curY, width, pad, "Heal Action", wardenAI.LastHealScore, actionName == "Heal");
        DrawScoreMeter(ref curY, width, pad, "Hold Action", wardenAI.LastHoldScore, actionName == "Hold");
        DrawScoreMeter(ref curY, width, pad, "Advance Action", wardenAI.LastAdvanceScore, actionName == "Advance");

        curY += 6f;

        // Observasi / Persepsi
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), "<b>Persepsi AI:</b>", labelStyle);
        curY += 22f;

        string obs1 = $"• Kawan terluka di radius ({wardenAI.HealRadius}m): <b>{wardenAI.LastInjuredCount}</b>";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), obs1, labelStyle);
        curY += 22f;

        string obs2 = $"• HP hilang terparah: <b>{wardenAI.LastWorstMissing * 100f:F0}%</b>";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), obs2, labelStyle);
        curY += 22f;

        string obs3 = $"• Kawan di pasukan ({wardenAI.GroupRadius}m): <b>{wardenAI.LastGroupCount}</b> (Ahead: {wardenAI.LastAhead:F2}, Lag: {wardenAI.LastLagging:F2})";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), obs3, labelStyle);
        curY += 22f;

        // Timers
        string t1 = $"• Think Tick: <b>{wardenAI.ThinkTimer:F2}s</b> | Heal Pulse: <b>{wardenAI.HealTimer:F2}s / {wardenAI.HealInterval:F1}s</b>";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), t1, labelStyle);
        curY += 26f;
    }

    private void DrawBombAIDebug(ref float curY, float width, float pad)
    {
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), "💣 FSM: SKELETON BOMB (BOMBER)", subHeaderStyle);
        curY += 28f;

        string state = bombAI.CurrentStateName;
        Color stColor = state == "Run" ? Color.green : (state == "Lock" ? Color.yellow : (state == "Chase" ? new Color(1f, 0.5f, 0f) : Color.red));

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), $"State Saat Ini: <color=#{ColorUtility.ToHtmlStringRGB(stColor)}><b>[{state.ToUpper()}]</b></color>", labelStyle);
        curY += 26f;

        string targetName = bombAI.LockedTarget != null ? bombAI.LockedTarget.name : "None (Mencari...)";
        float dist = bombAI.LockedTarget != null ? Vector3.Distance(selectedNPC.transform.position, bombAI.LockedTarget.position) : 0f;

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Target Terkunci: <b>{targetName}</b>", labelStyle);
        curY += 22f;

        if (bombAI.LockedTarget != null)
        {
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Jarak ke Target: <b>{dist:F2}m</b> (Ledak saat <= {bombAI.ExplodeDistance:F1}m)", labelStyle);
            curY += 22f;
        }

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Radius Deteksi: <b>{bombAI.DetectionRadius:F1}m</b> | Radius AoE Ledakan: <b>{bombAI.ExplosionRadius:F1}m</b>", labelStyle);
        curY += 22f;

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Chase Speed: <b>{bombAI.ChaseSpeed:F1}</b> | Damage AoE: <b>{bombAI.PendingDamage:F0}</b>", labelStyle);
        curY += 22f;

        if (state == "Lock")
        {
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Lock Countdown: <color=yellow><b>{bombAI.LockTimer:F2}s</b></color>", labelStyle);
            curY += 22f;
        }
        else
        {
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Scan Timer: <b>{bombAI.ScanTimer:F2}s</b>", labelStyle);
            curY += 22f;
        }

        curY += 6f;
    }

    private void DrawSoldierDebug(ref float curY, float width, float pad)
    {
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), "⚔️ PRAJURIT (HEAVY INFANTRY ALLY)", subHeaderStyle);
        curY += 28f;

        string target = soldier.CurrentEnemyTarget != null ? soldier.CurrentEnemyTarget.name : "Tidak ada";
        string status = soldier.CurrentEnemyTarget != null ? "<color=#FF5555>BERTARUNG & MEMBLOKIR</color>" : "<color=#55FF55>PATROLI / DI RALLY POINT</color>";

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), $"Status: <b>{status}</b>", labelStyle);
        curY += 26f;

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Musuh Dihadapi: <b>{target}</b>", labelStyle);
        curY += 22f;

        float distRally = Vector3.Distance(selectedNPC.transform.position, soldier.TargetRallySpot);
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Jarak ke Rally Spot: <b>{distRally:F2}m</b>", labelStyle);
        curY += 22f;

        string barrackName = soldier.HomeBarrack != null ? soldier.HomeBarrack.name : "Hilang";
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Home Barrack: <b>{barrackName}</b>", labelStyle);
        curY += 22f;

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Damage: <b>{soldier.Damage}</b> | Attack Rate: <b>{soldier.AttackRate:F1}/s</b>", labelStyle);
        curY += 22f;

        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Animasi: <b>{soldier.CurrentAnimState}</b>", labelStyle);
        curY += 26f;
    }

    private void DrawStandardEnemyDebug(ref float curY, float width, float pad)
    {
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 24f), "👾 STANDARD ENEMY", subHeaderStyle);
        curY += 28f;

        if (enemyHealth != null)
        {
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Defense (DEF): <b>{enemyHealth.Defense:F1}</b>", labelStyle);
            curY += 22f;
        }

        if (enemyMovement != null)
        {
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Kecepatan: <b>{enemyMovement.Speed:F1}</b>", labelStyle);
            curY += 22f;
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Menuju Waypoint: <b>{enemyMovement.CurrentWaypointIndex + 1} / {enemyMovement.TotalWaypoints}</b>", labelStyle);
            curY += 22f;
            GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), $"• Sisa Jarak ke Base: <b>{enemyMovement.RemainingDistance:F1}m</b>", labelStyle);
            curY += 26f;
        }
    }

    private void DrawScoreMeter(ref float curY, float width, float pad, string label, float score, bool isWinner)
    {
        float labelW = 140f;
        float barW = width - labelW - 65f;

        Rect rLabel = new Rect(panelRect.x + pad, curY, labelW, 20f);
        GUI.Label(rLabel, isWinner ? $"<b><color=#55FF55>▶ {label}</color></b>" : $"  {label}", labelStyle);

        Rect rBarBg = new Rect(panelRect.x + pad + labelW, curY + 3f, barW, 16f);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(rBarBg, whiteTexture);

        float fillRatio = Mathf.Clamp01(score);
        Color barCol = isWinner ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(0.4f, 0.6f, 0.8f, 0.8f);
        GUI.color = barCol;
        Rect rBarFill = new Rect(rBarBg.x, rBarBg.y, barW * fillRatio, 16f);
        GUI.DrawTexture(rBarFill, whiteTexture);
        GUI.color = Color.white;

        Rect rVal = new Rect(panelRect.x + pad + labelW + barW + 10f, curY, 55f, 20f);
        GUI.Label(rVal, $"{score:F2}", isWinner ? valueStyle : labelStyle);

        curY += 24f;
    }

    private void DrawActionTools(ref float curY, float width, float pad)
    {
        GUI.Label(new Rect(panelRect.x + pad, curY, width, 22f), "<b>Sandbox / Testing Tools:</b>", labelStyle);
        curY += 26f;

        float btnW = (width - 15f) / 4f;
        float btnH = 30f;

        // 1. Damage -15 HP
        Rect btnDmg = new Rect(panelRect.x + pad, curY, btnW, btnH);
        if (GUI.Button(btnDmg, "-15 HP", buttonStyle))
        {
            if (enemyHealth != null) enemyHealth.TakeDamage(15f);
            else if (soldier != null) soldier.TakeDamage(15f);
        }

        // 2. Heal +15 HP
        Rect btnHeal = new Rect(panelRect.x + pad + btnW + 5f, curY, btnW, btnH);
        if (GUI.Button(btnHeal, "+15 HP", buttonStyle))
        {
            if (enemyHealth != null) enemyHealth.Heal(15f);
            else if (soldier != null) soldier.Heal(15f);
        }

        // 3. Kill NPC
        Rect btnKill = new Rect(panelRect.x + pad + (btnW + 5f) * 2f, curY, btnW, btnH);
        GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
        if (GUI.Button(btnKill, "Kill ☠", buttonStyle))
        {
            if (enemyHealth != null) enemyHealth.Kill();
            else if (soldier != null) soldier.Kill();
            Deselect();
            return;
        }
        GUI.color = Color.white;

        // 4. Toggle 3D Rings
        Rect btnRings = new Rect(panelRect.x + pad + (btnW + 5f) * 3f, curY, btnW, btnH);
        string ringText = showWorldRings ? "Rings: ON" : "Rings: OFF";
        if (GUI.Button(btnRings, ringText, buttonStyle))
        {
            showWorldRings = !showWorldRings;
        }

        curY += btnH + 10f;

        // Kontrol Game Speed (Slow-motion / Pause / Normal)
        GUI.Label(new Rect(panelRect.x + pad, curY, 80f, 28f), "Speed:", labelStyle);
        float spdW = (width - 80f - 15f) / 4f;

        float[] speeds = { 0f, 0.25f, 1f, 2f };
        string[] speedLabels = { "Pause", "0.25x", "1x", "2x" };

        for (int i = 0; i < speeds.Length; i++)
        {
            Rect spdRect = new Rect(panelRect.x + pad + 80f + i * (spdW + 5f), curY, spdW, 28f);
            bool isCurrent = Mathf.Approximately(Time.timeScale, speeds[i]);
            GUI.color = isCurrent ? Color.green : Color.white;

            if (GUI.Button(spdRect, speedLabels[i], buttonStyle))
            {
                Time.timeScale = speeds[i];
            }
        }
        GUI.color = Color.white;
    }

    private void DrawDivider(ref float curY, float width, float pad)
    {
        curY += 4f;
        Rect divRect = new Rect(panelRect.x + pad, curY, width, 1f);
        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.DrawTexture(divRect, whiteTexture);
        GUI.color = Color.white;
        curY += 8f;
    }
}

