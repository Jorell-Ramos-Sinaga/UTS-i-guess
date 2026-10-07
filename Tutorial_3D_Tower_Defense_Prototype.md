# MODUL PENGEMBANGAN PROTOTYPE
# 3D TOWER DEFENSE (UNITY 6 + C#)

## Implementasi Sistem Inti: Waypoint Navigation, Wave Spawner, Turret Targeting & Grid Placement

**Mata Kuliah:** Game Cerdas  
**Program Studi:** S1 Teknik Informatika  
**Tools:** Unity 6 (URP) + C#  
**Materi:** Prototype Game 3D Tower Defense  
**Prasyarat:** Dasar Transform, Prefab, Collider, Coroutine & Vector Math  

---

# 1. Tujuan Praktikum / Pengembangan

Pada panduan ini, Anda akan membangun sebuah **prototype awal 3D Tower Defense** yang ringkas, fungsional, dan mudah dikembangkan untuk kebutuhan tugas maupun proyek UTS Game Cerdas.

Siklus gameplay inti (*core gameplay loop*) yang akan dibuat:

```plaintext
Enemy Spawner
     │
     ▼
Enemy Berjalan Sepanjang Waypoint
     │
     ▼
Tower Mendeteksi Musuh Terdekat & Menembak
     │
     ├─────────────┬─────────────┐
     ▼             ▼             ▼
Musuh Mati    Tower Baru      Musuh Sampai
(+Gold)       Didirikan       ke Markas Base
               (Build Node)   (-Base Health)
```

Komponen fungsional yang akan dibangun:
1. **Lintasan & Pergerakan Musuh:** Musuh muncul dan bergerak mengikuti titik waypoint dari Spawn Point menuju Base.
2. **Sistem Spawner Bertahap (Wave System):** Memunculkan musuh secara berkala (*staggered*) dalam gelombang.
3. **Sistem Deteksi & Bidik Tower (Turret AI):** Menemukan target musuh terdekat dalam radius tembak (*range*), memutar kepala turret menghadap musuh, dan menembak secara berkala (*cooldown*).
4. **Sistem Proyektil (Bullet / Projectile):** Peluru meluncur mengejar musuh (*homing*), menghasilkan damage saat mengenai sasaran.
5. **Sistem Kesehatan & Ekonomi:** Musuh memiliki HP; saat mati memberikan Gold. Base memiliki HP; jika habis, memicu kondisi *Game Over*.
6. **Sistem Pembangunan Menara (Build Node):** Pemain dapat mengklik platform (*node*) di arena untuk mendirikan tower baru jika Gold mencukupi.

---

# 2. Capaian Pembelajaran

Setelah mempraktikkan modul ini, mahasiswa diharapkan mampu:
- Memahami konsep dasar logika **Tower Defense Architecture** pada Unity;
- Menerapkan **Waypoint Pathfinding** sederhana menggunakan `Transform` dan `Vector3.MoveTowards`;
- Menerapkan pembidikan target menggunakan **Radius Search / Distance Checking**;
- Mengontrol orientasi rotasi objek 3D menggunakan `Quaternion.LookRotation` dan `Quaternion.Slerp`;
- Mengatur interval serangan menggunakan konsep **Attack Cooldown** (`Time.time >= nextFireTime`);
- Menggunakan **Coroutine** untuk mengatur kemunculan musuh per wave secara asinkron;
- Menggunakan **Unity Gizmos** untuk visualisasi debugging radius jangkauan tower di Scene View;
- Mengimplementasikan pola arsitektur modular (*Separation of Concerns*) sehingga script mudah diperluas di masa depan (misal: menambahkan tipe musuh baru, efek perlambatan, atau machine learning).

---

# 3. Gambaran Sistem

### Arsitektur Alur Sistem (*Data & Logic Flow*)

```plaintext
                   ┌───────────────────────────────┐
                   │          GameManager          │
                   │  (Player Health, Gold, State) │
                   └───────────────┬───────────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         ▼                         ▼                         ▼
┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│   WaveSpawner    │      │    BuildNode     │      │     BaseGoal     │
│ Memunculkan Wave │      │ Membangun Tower  │      │ Base Defense HP  │
└────────┬─────────┘      └────────┬─────────┘      └────────▲─────────┘
         │                         │                         │
         ▼ Instansiasi             ▼ Instansiasi             │ Jika lolos
┌──────────────────┐      ┌──────────────────┐               │
│  EnemyMovement   │      │   Tower (Turret) │               │
│  & EnemyHealth   │◄─────┤ Radius Detection │───────────────┘
└────────┬─────────┘      │   & Projectile   │
         │                └──────────────────┘
         ▼ Mengikuti
┌──────────────────┐
│   WaypointPath   │
│ Titik 0 → 1 → 2  │
└──────────────────┘
```

Hubungan antar entitas:
1. **WaypointPath** menyediakan daftar koordinat titik yang harus dilewati.
2. **Enemy** meminta koordinat target berikutnya dari WaypointPath sampai mencapai Base.
3. **Tower** mencari objek dengan tag `"Enemy"` yang berada dalam radius `range`.
4. Jika target ditemukan, **Tower** menginstansiasi **Projectile** yang diarahkan ke target.
5. Jika musuh terkena peluru dan HP habis, musuh memberikan hadiah Gold ke **GameManager**.
6. Jika musuh berhasil mencapai titik akhir, **BaseGoal** mengurangi HP markas pada **GameManager**.

---

# 4. Rekomendasi Implementasi Prototype

Untuk tahap prototype awal ini, kita menggunakan pendekatan:

```plaintext
Transform Waypoint Navigation
+
Physics/Distance-based Range Search
+
Direct Instantiation Projectile
+
Single Target Tracking (First / Closest Enemy)
+
Click-to-Build Node Platform
```

### Mengapa Menggunakan Waypoint Sederhana (bukan NavMesh dahulu)?
Untuk game Tower Defense prototype:
- Jalur musuh umumnya **pasti dan statis** (*fixed track*).
- Menggunakan array waypoint (`Transform[]`) jauh lebih mudah diatur, bebas lag, dan tidak membutuhkan proses *bake NavMesh* berulang kali jika lintasan diubah.
- Mengurangi kompleksitas awal sehingga mahasiswa dapat fokus pada logika penembakan, ekonomi, dan spawning.

*(Catatan: Modul lanjutan nanti dapat mengintegrasikan NavMesh jika rute musuh dibuat dinamis atau dapat diubah oleh labirin buatan pemain).*

---

# 5. Rekomendasi Struktur Folder Project

Pastikan struktur folder di dalam folder `Assets/` terorganisir dengan rapi:

```plaintext
Assets/
│
├── Materials/
│   ├── Mat_Ground.mat
│   ├── Mat_Path.mat
│   ├── Mat_Node.mat
│   ├── Mat_Enemy.mat
│   ├── Mat_Tower.mat
│   └── Mat_Bullet.mat
│
├── Prefabs/
│   ├── Enemy.prefab
│   ├── Tower.prefab
│   └── Bullet.prefab
│
├── Scenes/
│   └── TowerDefense_Prototype.unity
│
└── Scripts/
    ├── WaypointPath.cs
    ├── EnemyMovement.cs
    ├── EnemyHealth.cs
    ├── WaveSpawner.cs
    ├── Tower.cs
    ├── Projectile.cs
    ├── BuildNode.cs
    ├── BaseGoal.cs
    └── GameManager.cs
```

---

# 6. Desain Hierarchy Scene

Bentuk hierarki Scene yang direkomendasikan pada prototype:

```plaintext
TowerDefense_Prototype
│
├── Main Camera (Top-Down Isometric Angle)
├── Directional Light
│
├── Environment
│   ├── Ground (Plane)
│   └── PathBlocks (Kumpulan Cube penanda jalan)
│
├── Waypoints
│   ├── Waypoint_0 (Titik Spawn)
│   ├── Waypoint_1
│   ├── Waypoint_2
│   ├── Waypoint_3
│   └── Waypoint_Base (Titik Akhir Markas)
│
├── Spawner (Empty Object + WaveSpawner script)
│
├── BaseGoal (Cube / Empty Object di akhir jalan)
│
├── BuildNodes
│   ├── Node_01
│   ├── Node_02
│   ├── Node_03
│   └── Node_04
│
└── GameManager (Empty Object + GameManager script)
```

---

# 7. Pembuatan Script C# Lengkap

Buat script-script berikut di folder `Assets/Scripts/`:

---

### Script 1: `WaypointPath.cs`
Script ini bertugas menampung daftar titik jalur dan mempermudah visualisasi garis lintasan di editor Unity.

```csharp
using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    [Header("Daftar Titik Waypoint")]
    [Tooltip("Urutkan titik dari spawn point sampai ke base")]
    public Transform[] points;

    private void Awake()
    {
        // Jika belum diisi manual di Inspector, ambil semua anak Transform secara otomatis
        if (points == null || points.Length == 0)
        {
            points = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                points[i] = transform.GetChild(i);
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Menggambar garis jalur di Scene View agar mudah dilihat
        if (transform.childCount < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < transform.childCount - 1; i++)
        {
            Transform p1 = transform.GetChild(i);
            Transform p2 = transform.GetChild(i + 1);
            if (p1 != null && p2 != null)
            {
                Gizmos.DrawLine(p1.position, p2.position);
                Gizmos.DrawWireSphere(p1.position, 0.3f);
            }
        }

        // Gambar titik terakhir
        Transform last = transform.GetChild(transform.childCount - 1);
        if (last != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(last.position, 0.4f);
        }
    }
}
```

---

### Script 2: `EnemyMovement.cs`
Script ini mengontrol musuh agar bergerak lurus menuju waypoint satu per satu hingga titik akhir.

```csharp
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private float reachDistanceThreshold = 0.2f;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    public void SetupPath(Transform[] pathPoints)
    {
        waypoints = pathPoints;
        currentWaypointIndex = 0;

        if (waypoints != null && waypoints.Length > 0)
        {
            transform.position = waypoints[0].position;
        }
    }

    private void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // Ambil titik target saat ini
        Transform targetPoint = waypoints[currentWaypointIndex];

        // Hitung arah gerak
        Vector3 direction = (targetPoint.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // Rotasi musuh menghadap arah jalan
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // Cek apakah sudah dekat dengan target waypoint saat ini
        float distance = Vector3.Distance(transform.position, targetPoint.position);
        if (distance <= reachDistanceThreshold)
        {
            currentWaypointIndex++;

            // Jika sudah mencapai titik terakhir (Base)
            if (currentWaypointIndex >= waypoints.Length)
            {
                ReachEndPath();
            }
        }
    }

    private void ReachEndPath()
    {
        // Mengurangi nyawa Base melalui GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DamageBase(1);
        }

        // Musuh hancur saat berhasil mencapai markas
        Destroy(gameObject);
    }
}
```

---

### Script 3: `EnemyHealth.cs`
Script ini mengelola nyawa musuh, merespons tembakan peluru, dan memberikan hadiah Gold saat mati.

```csharp
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private int goldReward = 15;

    private float currentHealth;
    private bool isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // Beri reward koin ke player
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(goldReward);
        }

        Destroy(gameObject);
    }
}
```

---

### Script 4: `WaveSpawner.cs`
Script untuk memunculkan kumpulan musuh bertahap dengan jeda waktu.

```csharp
using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private WaypointPath waypointPath;

    [Header("Wave Settings")]
    [SerializeField] private int totalWaves = 5;
    [SerializeField] private int enemiesPerWave = 5;
    [SerializeField] private float timeBetweenEnemies = 1.0f;
    [SerializeField] private float timeBetweenWaves = 5.0f;

    private int currentWaveIndex = 0;
    private bool isSpawning = false;

    private void Start()
    {
        StartCoroutine(SpawnAllWavesRoutine());
    }

    private IEnumerator SpawnAllWavesRoutine()
    {
        yield return new WaitForSeconds(2f); // Jeda persiapan sebelum gelombang 1

        while (currentWaveIndex < totalWaves)
        {
            currentWaveIndex++;
            Debug.Log($"--- Wave {currentWaveIndex} Dimulai ---");

            yield return StartCoroutine(SpawnWaveRoutine(enemiesPerWave + (currentWaveIndex * 2)));

            // Jeda antar wave
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        Debug.Log("Semua wave telah dimunculkan!");
    }

    private IEnumerator SpawnWaveRoutine(int enemyCount)
    {
        isSpawning = true;

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(timeBetweenEnemies);
        }

        isSpawning = false;
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || waypointPath == null || waypointPath.points.Length == 0) return;

        Transform spawnPoint = waypointPath.points[0];
        GameObject enemyObj = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        EnemyMovement movement = enemyObj.GetComponent<EnemyMovement>();
        if (movement != null)
        {
            movement.SetupPath(waypointPath.points);
        }
    }
}
```

---

### Script 5: `Tower.cs`
Script inti AI Tower yang mendeteksi musuh terdekat dalam jangkauan, memutar kepala turret, dan meluncurkan proyektil.

```csharp
using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Targeting & Attack")]
    [SerializeField] private float range = 5f;
    [SerializeField] private float fireRate = 1.5f; // Tembakan per detik
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Tower Parts & Prefabs")]
    [Tooltip("Bagian atas turret yang berputar horizontal")]
    [SerializeField] private Transform partToRotate;
    [Tooltip("Titik keluarnya peluru")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;

    private Transform currentTarget;
    private float fireCountdown = 0f;

    private void Start()
    {
        // Jalankan pencarian target secara berkala (2 kali per detik) untuk efisiensi performa
        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    private void UpdateTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy;
            }
        }

        // Kunci target jika berada di dalam jarak jangkauan
        if (nearestEnemy != null && shortestDistance <= range)
        {
            currentTarget = nearestEnemy.transform;
        }
        else
        {
            currentTarget = null;
        }
    }

    private void Update()
    {
        if (currentTarget == null) return;

        LockOnTarget();

        // Logika Cooldown Menembak
        if (fireCountdown <= 0f)
        {
            Shoot();
            fireCountdown = 1f / fireRate;
        }

        fireCountdown -= Time.deltaTime;
    }

    private void LockOnTarget()
    {
        if (partToRotate == null) return;

        // Hitung arah pandang ke target
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0; // Kunci rotasi horizontal agar turret tidak miring aneh

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            // Rotasi halus menggunakan Slerp
            partToRotate.rotation = Quaternion.Slerp(partToRotate.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Projectile projectile = bulletObj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Seek(currentTarget);
        }
    }

    // Menggambar radius tembak di Scene View
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
```

---

### Script 6: `Projectile.cs`
Script peluru yang meluncur menuju sasaran dan memberikan damage.

```csharp
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float lifetime = 4f;

    private Transform target;

    public void Seek(Transform targetTransform)
    {
        target = targetTransform;
        Destroy(gameObject, lifetime); // Hapus otomatis jika tidak mengenai apa-apa
    }

    private void Update()
    {
        // Jika target musuh sudah hancur sebelum peluru sampai
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Arahkan peluru ke posisi target
        Vector3 direction = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        // Jika peluru sudah sangat dekat dengan target (mengenai musuh)
        if (direction.magnitude <= distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.Translate(direction.normalized * distanceThisFrame, Space.World);
        transform.LookAt(target);
    }

    private void HitTarget()
    {
        if (target != null)
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}
```

---

### Script 7: `BuildNode.cs`
Script yang diletakkan pada platform petak tanah untuk mendeteksi klik pemain dan mendirikan Tower.

```csharp
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
```

---

### Script 8: `GameManager.cs`
Pusat kontrol status game: Gold, Base Health, Tower Cost, dan penanganan Game Over.

```csharp
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
```

---

# 8. Langkah Demi Langkah Setup Scene di Editor Unity

Ikuti panduan berikut dengan teliti untuk merakit prototype di Unity Editor:

### Langkah 1 — Menyiapkan Material Warna
Di folder `Assets/Materials/`, buat Material sederhana:
- `Mat_Ground`: Abu-abu tua / abu-abu gelap
- `Mat_Path`: Cokelat terang atau pasir (penanda jalur)
- `Mat_Node`: Putih / abu-abu terang (titik bangun tower)
- `Mat_Enemy`: Merah (musuh)
- `Mat_Tower`: Biru / Cyan (menara pertahanan)
- `Mat_Bullet`: Kuning cerah (peluru)

---

### Langkah 2 — Membuat Tag "Enemy"
1. Klik sembarang objek atau pilih dropdown **Tag** di bagian atas Inspector.
2. Klik **Add Tag...**
3. Klik tombol **+**, ketikkan `Enemy` (perhatikan huruf besar/kecil), lalu klik **Save**.

---

### Langkah 3 — Membuat Arena & Lintasan
1. **Ground:**
   - `GameObject -> 3D Object -> Plane`
   - Beri nama `Ground`, atur Scale `(3, 1, 3)`, posisikan di `(0, 0, 0)`.
   - Pasangkan material `Mat_Ground`.
2. **Jalur (Visual):**
   - Buat Cube tipis (Scale: `(2, 0.1, 10)` atau sesuai jalur berkelok).
   - Pasangkan material `Mat_Path`.

---

### Langkah 4 — Menyiapkan Waypoints
1. Buat Empty GameObject, beri nama `Waypoints`.
2. Pasangkan script `WaypointPath.cs` pada objek `Waypoints`.
3. Di dalam objek `Waypoints`, buat beberapa anak objek (Empty GameObject):
   - `WP_0` (Posisi: `-10, 0.5, 0`) -> *Titik Muncul Musuh*
   - `WP_1` (Posisi: `-2, 0.5, 0`)
   - `WP_2` (Posisi: `-2, 0.5, 6`)
   - `WP_3` (Posisi: `6, 0.5, 6`)
   - `WP_4` (Posisi: `6, 0.5, -4`)
   - `WP_Base` (Posisi: `12, 0.5, -4`) -> *Titik Markas Base*
4. Di Scene View, Anda akan melihat garis kuning Gizmos yang menghubungkan titik-titik tersebut secara otomatis.

---

### Langkah 5 — Membuat Prefab Enemy
1. Buat `GameObject -> 3D Object -> Capsule`.
2. Beri nama `Enemy`.
3. Pasangkan Tag **`Enemy`** pada objek ini di Inspector.
4. Pasangkan material `Mat_Enemy`.
5. Pasang komponen script:
   - `EnemyMovement.cs`
   - `EnemyHealth.cs`
6. Tarik objek `Enemy` dari Hierarchy ke folder `Assets/Prefabs/` agar menjadi Prefab.
7. Hapus objek `Enemy` yang ada di Hierarchy.

---

### Langkah 6 — Membuat Prefab Projectile (Peluru)
1. Buat `GameObject -> 3D Object -> Sphere`.
2. Beri nama `Bullet`.
3. Atur Scale menjadi kecil: `(0.3, 0.3, 0.3)`.
4. Pasang material `Mat_Bullet`.
5. Hapus `SphereCollider` (atau beri tanda centang `Is Trigger`), karena pendeteksian hit sudah dilakukan secara matematis jarak oleh script.
6. Pasangkan script `Projectile.cs`.
7. Tarik objek `Bullet` ke folder `Assets/Prefabs/` sebagai Prefab, lalu hapus dari Hierarchy.

---

### Langkah 7 — Membuat Prefab Tower
1. Buat Empty GameObject di Hierarchy, beri nama `Tower`.
2. Buat anak objek:
   - `Base_Model` (`Cube`, Scale: `(1, 0.5, 1)`, Position: `(0, 0.25, 0)`).
   - `TurretHead` (`Cube`, Scale: `(0.8, 0.8, 1.2)`, Position: `(0, 0.8, 0)`).
   - `FirePoint` (Empty GameObject, posisikan di ujung depan moncong `TurretHead`, misal: `(0, 0.8, 0.8)`).
3. Pasang material `Mat_Tower` pada kedua bagian kubus.
4. Pada objek induk `Tower`, pasangkan script `Tower.cs`.
5. Hubungkan referensi di Inspector `Tower.cs`:
   - **Part To Rotate:** Tarik objek `TurretHead`
   - **Fire Point:** Tarik objek `FirePoint`
   - **Bullet Prefab:** Tarik prefab `Bullet` dari folder `Assets/Prefabs/`
6. Tarik objek `Tower` ke folder `Assets/Prefabs/` sebagai Prefab, lalu hapus dari Hierarchy.

---

### Langkah 8 — Membuat Build Nodes (Platform Bangun Tower)
1. Buat `GameObject -> 3D Object -> Cube`.
2. Beri nama `Node_01`.
3. Atur Scale: `(1.5, 0.2, 1.5)`. Posisikan di dekat tikungan lintasan jalan.
4. Pasang material `Mat_Node`.
5. Pastikan memiliki komponen `BoxCollider`.
6. Pasang script `BuildNode.cs`.
7. Duplikasi objek ini menjadi beberapa titik (`Node_02`, `Node_03`, dst.) di sepanjang rute.

---

### Langkah 9 — Menyiapkan Spawner & GameManager
1. **Spawner:**
   - Buat Empty GameObject, beri nama `Spawner`.
   - Pasang script `WaveSpawner.cs`.
   - Tarik prefab `Enemy` ke slot **Enemy Prefab**.
   - Tarik objek `Waypoints` ke slot **Waypoint Path**.
2. **GameManager:**
   - Buat Empty GameObject, beri nama `GameManager`.
   - Pasang script `GameManager.cs`.
   - Tarik prefab `Tower` ke slot **Default Tower Prefab**.

---

### Langkah 10 — Mengatur Kamera
1. Pilih `Main Camera`.
2. Posisikan agar menghadap seluruh arena dari sudut atas isometrik:
   - Position: `(0, 15, -12)`
   - Rotation: `(50, 0, 0)`

---

# 9. Tabel Parameter Rekomendasi di Inspector

| Komponen | Parameter | Nilai Rekomendasi | Fungsi |
| :--- | :--- | :--- | :--- |
| **EnemyMovement** | `Speed` | `4` | Kecepatan lari musuh |
| | `Reach Distance Threshold` | `0.2` | Jarak toleransi musuh dianggap sampai di titik waypoint |
| **EnemyHealth** | `Max Health` | `30` | Total darah musuh |
| | `Gold Reward` | `15` | Koin yang didapat saat musuh terbunuh |
| **WaveSpawner** | `Total Waves` | `5` | Jumlah ronde gelombang musuh |
| | `Enemies Per Wave` | `5` | Jumlah musuh dasar di gelombang pertama |
| | `Time Between Enemies` | `1.0` | Jeda antar musuh keluar dalam satu gelombang |
| | `Time Between Waves` | `5.0` | Jeda istirahat sebelum gelombang berikutnya |
| **Tower** | `Range` | `6` | Radius tembak melingkar |
| | `Fire Rate` | `1.5` | Kecepatan menembak (1.5 peluru per detik) |
| | `Enemy Tag` | `"Enemy"` | Tag objek yang dicari |
| **Projectile** | `Speed` | `18` | Kecepatan meluncur peluru ke target |
| | `Damage` | `10` | Kerusakan yang dikurangi dari HP musuh (3 tembakan untuk kill) |
| **GameManager** | `Starting Gold` | `100` | Cukup untuk mendirikan 2 tower di awal |
| | `Tower Cost` | `50` | Biaya mendirikan 1 unit tower |
| | `Base Health` | `10` | Markas kalah jika 10 musuh lolos |

---

# 10. Prosedur Pengujian Bertahap (*Testing Phase*)

Lakukan pengujian secara bertahap saat menekan tombol **Play** di Unity:

### Test 1 — Pergerakan Musuh & Waypoint
- **Aksi:** Tekan Play dan amati musuh yang muncul di titik awal (`WP_0`).
- **Hasil yang Diharapkan:** Musuh bergerak menyusuri titik `WP_0 -> WP_1 -> ... -> WP_Base` dengan mulus tanpa terhenti di tengah jalan.

### Test 2 — Interaksi Build Node (Membangun Tower)
- **Aksi:** Arahkan kursor mouse ke salah satu platform Node. Klik kiri pada node tersebut.
- **Hasil yang Diharapkan:**
  - Warna node berubah menjadi hijau saat mouse melayang di atasnya (*hover*).
  - Saat diklik, sebuah Tower muncul di atas node.
  - Console Unity menampilkan pesan: `Tower Dibeli! Sisa Gold: 50`.

### Test 3 — Pembidikan Target & Rotasi Turret
- **Aksi:** Amati kepala turret saat musuh melintas di dekatnya.
- **Hasil yang Diharapkan:** Kepala turret (`TurretHead`) berputar menghadap musuh terdekat dan terus mengunci arah ke musuh selama masih dalam radius *range*.

### Test 4 — Tembakan Proyektil & Pengurangan HP
- **Aksi:** Biarkan tower menembak musuh.
- **Hasil yang Diharapkan:**
  - Peluru keluar dari moncong `FirePoint` dan terbang mengejar musuh.
  - Setelah 3 kali terkena tembakan (3 x 10 damage = 30), musuh hancur/hilang dari arena.
  - Console menampilkan: `Dapat Gold: +15 | Total Gold: 65`.

### Test 5 — Pertahanan Markas & Game Over
- **Aksi:** Jangan bangun tower dan biarkan musuh berjalan sampai ke titik akhir (`WP_Base`).
- **Hasil yang Diharapkan:**
  - Setiap musuh yang mencapai garis akhir akan menghilang.
  - Nilai Base HP berkurang. Jika mencapai 0, muncul pesan `GAME OVER!`.

---

# 11. Visual Debugging dengan Gizmos

Untuk memastikan jarak jangkauan tower akurat:
1. Klik salah satu objek **Tower** di Hierarchy saat permainan berjalan.
2. Lingkaran garis biru (*Cyan Wire Sphere*) akan langsung terlihat di Scene View sesuai nilai parameter `Range`.
3. Garis lintasan rute musuh berwarna kuning juga akan terlihat di Scene View melalui `WaypointPath`.

---

# 12. Masalah Umum & Troubleshooting

| Gejala Masalah | Penyebab Utama | Solusi |
| :--- | :--- | :--- |
| **Tower tidak mau menembak musuh** | Tag `Enemy` belum dipasang pada prefab Enemy. | Buka prefab Enemy, pastikan dropdown Tag diisi `"Enemy"`. |
| **Turret berputar terbalik atau miring ke tanah** | Sumbu forward pivot objek belum sejajar. | Pastikan `dir.y = 0` aktif pada `Tower.LockOnTarget()` dan pivot `TurretHead` menghadap sumbu Z (biru). |
| **Peluru tidak muncul saat menembak** | Slot `FirePoint` atau `BulletPrefab` masih kosong (*None*) di Inspector. | Drag GameObject anak `FirePoint` dan prefab `Bullet` ke slot yang bersangkutan di Inspector. |
| **Klik Node tidak merespon / tidak muncul Tower** | Node tidak memiliki `Collider` atau kamera tidak ada Physics Raycaster. | Pastikan objek Node memiliki komponen `BoxCollider`. |
| **Musuh berhenti di waypoint pertama** | Jarak toleransi `reachDistanceThreshold` terlalu kecil atau koordinat waypoint melayang di udara. | Pastikan tinggi Y seluruh waypoint sejajar dengan tanah dan set threshold ke `0.2f`. |

---

# 13. Rencana Pengembangan Lanjutan (Tugas Mandiri)

Prototype ini telah dirancang secara modular agar siap dikembangkan untuk tugas lanjutan atau penilaian UTS:

### Level A — Fitur Esensial (Pondasi Tambahan)
- [ ] Buat Canvas UI sederhana untuk menampilkan teks `Gold`, `Base HP`, dan `Wave Number`.
- [ ] Tambahkan efek partikel sederhana saat peluru mengenai musuh (`Instantiate Hit Effect`).

### Level B — Variasi Gameplay
- [ ] Buat 2 jenis tower: **Rapid Tower** (kecepatan tembak tinggi, damage kecil) dan **Sniper Tower** (jarak jauh, damage besar, tembakan lambat).
- [ ] Buat jenis musuh khusus: **Tank Enemy** (darah tebal, gerak lambat) dan **Fast Enemy** (darah tipis, gerak sangat cepat).

### Level C — Fitur Cerdas (AI / Algoritma Game Cerdas)
- [ ] **Targeting Priority AI:** Tambahkan opsi pemilihan target pada Tower:
  - *Closest to Tower* (terdekat dari tower).
  - *First in Path* (musuh yang jalurnya paling mendekati Base).
  - *Lowest HP* (musuh dengan sisa darah paling sedikit).
- [ ] **Dynamic Difficulty Adjustment (DDA):** Spawner menyesuaikan jumlah musuh secara adaptif jika pemain terlalu cepat membersihkan gelombang musuh.

---

# 14. Checklist Akhir Sebelum Menjalankan Game

- [ ] Folder terstruktur rapi (`Materials`, `Prefabs`, `Scripts`, `Scenes`).
- [ ] Tag `Enemy` sudah terdaftar di Project Settings dan dipasangkan ke prefab Enemy.
- [ ] Waypoints memiliki minimal 4 anak objek dan garis kuning terlihat di Scene View.
- [ ] Prefab `Bullet` sudah diberi script `Projectile.cs`.
- [ ] Prefab `Tower` memiliki referensi `TurretHead`, `FirePoint`, dan `BulletPrefab`.
- [ ] Prefab `Enemy` memiliki komponen `EnemyMovement` dan `EnemyHealth`.
- [ ] Build Nodes memiliki `BoxCollider` dan script `BuildNode.cs`.
- [ ] Spawner terpasang di Scene dan memiliki referensi `EnemyPrefab` serta `WaypointPath`.
- [ ] GameManager terpasang di Scene dan memiliki referensi `TowerPrefab`.
