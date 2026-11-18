# Instruksi Agent: Peta Prosedural (BSP) + Dekorasi PCG

Dokumen ini adalah satu-satunya sumber kebenaran desain untuk pekerjaan ini. Baca seluruhnya sebelum mulai.
Pekerjaan dibagi menjadi milestone M1–M5. **Kerjakan satu milestone per giliran, lalu BERHENTI** dan tulis
laporan "Serah-terima" (lihat bagian 8). Jangan lanjut ke milestone berikutnya sebelum user menyatakan selesai.

---

## 1. Aturan kerja (hemat kuota)

1. Baca hanya file yang relevan. Gunakan pencarian (grep) untuk menemukan kode. Jangan membaca seluruh project.
2. Edit hanya baris yang perlu diubah. Jangan menulis ulang file yang tidak diminta dan jangan refactor di luar lingkup.
3. **Jangan mengedit** `.unity`, `.prefab`, `.meta`, `.asset`, atau `ProjectSettings` (YAML rawan rusak dan boros token). Boleh membaca `ProjectSettings/ProjectVersion.txt` untuk mengetahui versi Unity.
4. Jangan mencoba menjalankan Unity atau build. User yang menjalankan Play dan menempelkan hasil Console atau screenshot.
5. Hal yang hanya bisa dilakukan di Unity Editor adalah **TUGAS USER**: membuat scene, membuat dan mengisi prefab, mengisi referensi di Inspector, material, tag/layer, Play mode. Tulis langkahnya di laporan, jangan dikerjakan sendiri.
6. Jangan membuat dokumentasi, README, atau ringkasan tambahan kecuali diminta.
7. Jika ada keputusan yang benar-benar menghalangi, ajukan **maksimal satu** pertanyaan singkat di laporan. Selain itu ambil nilai bawaan yang masuk akal dan catat sebagai asumsi.
8. Komentar kode dalam Bahasa Indonesia. Nama class, method, dan field dalam bahasa Inggris. Jawaban singkat.
9. Satu class publik per file, nama file sama dengan nama class (syarat Unity untuk MonoBehaviour).

---

## 2. Konteks project

- Game tower defense bertema gurun (Unity). Versi Unity dan render pipeline: baca dari project (`ProjectVersion.txt`); jika render pipeline tidak jelas, anggap Built-in dan jangan memakai shader khusus pipeline.
- Script ada di `Assets/Scripts/` dengan subfolder: `Core`, `Enemy`, `Enemy/AI`, `Tower`, `Level`, `UI`.
- Kode baru peta diletakkan di `Assets/Scripts/Level/Generation/`.
- Bahasa pemrograman C#. Topik tugas kuliah: Utility AI, FSM, dan PCG (BSP + spawner berbobot). Teknik PCG dari materi dipakai, tetapi **tidak harus identik** karena konteks game berbeda.

### Script yang sudah ada dan terkait (verifikasi dengan grep sebelum mengubah)

| Script | Hal penting |
|---|---|
| `Level/WaypointPath` | Komponen dengan `public Transform[] points` (urut spawn ke base). `Awake` mengisi `points` dari anak transform jika kosong. |
| `Level/WaveSpawner` | Punya `waypointPath` (Path 1) dan `alternatePath` (Path 2). Enum `PathSelectionType` {AutoPerception, Path1Only, Path2Only, RandomPath} per entri musuh. `spawnHeightY` default 0.55. Memanggil `EnemyMovement.SetupPath(points, spawnHeightY)`. Wave berisi `EnemySpawnEntry` (prefab, count, pathOption). Deteksi menang: semua wave selesai dan tidak ada `EnemyHealth`. |
| `Level/PathPerception` | static: `CountTowersNearPath`, `ChooseSaferPath(A, B, radius)`, `IsNearPath(pos, WaypointPath/Transform[], radius)`. |
| `Enemy/EnemyMovement` | Berjalan lewat `Transform[] waypoints`, menyediakan `Waypoints`, `RemainingDistance`, `SetBlocked(object, bool)`. Mengurangi HP base di akhir jalur. |
| `Enemy/AI/BomberAI` (class `SkeletonBombAI`) | Mendeteksi tower, membedakan on-path dan off-path memakai `onPathRadius` (default 1.0) dan `PathPerception.IsNearPath`. |
| `Tower/BuildNode` | Butuh Collider dan Renderer. Sudah memiliki `enum NodeType { Ground, Path }`, field privat serialized `nodeType`, dan `IsSelectedTowerAllowed()` (cocokkan dengan `TowerData.placeableOn` di `GameManager`). Tower dibangun di posisi node + `towerOffsetY` (0.43). |
| `Tower/BarrackTower` | Saat `Start` mencari titik `WaypointPath` terdekat (lewat `FindObjectsByType<WaypointPath>`) sebagai rally point Soldier. **Sehingga waypoint harus rapat (satu per tile) dan tidak boleh ada WaypointPath lama/statis tersisa di scene.** |
| `Core/GameManager` | `TowerData[] availableTowers` (towerName, prefab, cost, placeableOn), `SelectedTower`, gold, HP base, kondisi menang/kalah, `RestartLevel()` (memuat ulang scene aktif). |
| `UI/GameUI`, `UI/MainMenu` | HUD dan menu. Dipakai di M5 untuk tampilan seed. |

Konvensi tinggi yang sudah ada: tower rata di Y 0.43, musuh di-spawn di Y 0.55.

---

## 3. Keputusan desain (FINAL, jangan diubah tanpa persetujuan user)

1. Peta berbasis **grid persegi panjang** `W x H`. Sumbu grid x ke dunia X, grid y ke dunia Z. Pusat sel = `origin + ((x + 0.5), 0, (y + 0.5)) * tileSize`, `origin` = posisi transform generator.
2. **BSP** memecah peta menjadi leaf. Setiap leaf hanya menyumbang satu **titik jangkar (anchor)**. **Tidak ada ruangan yang dipahat.**
3. **Jalur** = koridor selebar **1 sel**, dibuat dengan menghubungkan jangkar. Ini satu-satunya area yang dilewati musuh.
4. **Node** = sel kosong yang menempel pada sel jalur (8 arah, jadi sisi tikungan terisi), ketebalan **1 sel**. Node mendapat tipe `Ground` (umum) atau `Path` (tipe Barrack, jarang).
5. Sel kosong yang bukan jalur dan bukan node = area **dekorasi** (kaktus, batu, dst.).
6. Hanya **satu base**. Semua jalur berakhir di base yang sama.
7. **Satu spawner = satu jalur.** Jumlah jalur lebih dari satu diundi berbobot dan makin langka. Jalur-jalur terpisah total, hanya bertemu di sekitar base.
8. Setiap jalur boleh punya **cabang** (percabangan rute alternatif dalam jalur yang sama) dengan peluang tertentu. Dua rute dari cabang sama-sama berujung di base.
9. Pembagian musuh per wave (Opsi A): **total musuh per wave tetap, dibagi ke tiap jalur.** Ada pengali opsional untuk jalur tambahan.
10. Tidak ada tower yang berdiri di atas jalur. Semua node menempel di jalur, dan **semua tower dihitung on-path** untuk Bomber. Logika off-path Bomber **tidak dihapus**, hanya belum terpakai (mungkin dipakai lagi nanti).
11. Peta di-generate saat Play. Seed menentukan hasil secara deterministik. Restart setelah Game Over memakai peta yang sama. Mulai baru dari Main Menu memakai seed baru.

---

## 4. Spesifikasi generator (M1)

### 4.1 Data

```csharp
public enum MapCell { Empty, Path, Spawn, Base, Node, BarrackNode }
```

- Grid `MapCell[,]`. Hasil generate juga memuat daftar `LaneData` (indeks jalur, sel spawn, daftar rute) dan `BaseCell`.
- Satu **rute** = daftar sel berurutan dari sel spawn ke sel base. `routes[0]` pada tiap jalur adalah rute utama.
- Gunakan satu instance `System.Random(seed)` sendiri, **bukan** `UnityEngine.Random.InitState`, agar deterministik dan tidak bertabrakan dengan acak gameplay (mis. shuffle di `WaveSpawner`).

### 4.2 Langkah generate (urutan)

1. Isi grid dengan `Empty`. Sisakan margin tepi 1 sel.
2. **BSP**: akar = area dalam margin. Pecah rekursif (horizontal atau vertikal mengikuti rasio sisi, acak jika hampir persegi) sampai leaf <= `maxLeafSize`; split minimal `minLeafSize`. Simpan leaf dalam urutan DFS.
3. **Jangkar**: satu sel acak per leaf, di dalam leaf yang diperkecil `anchorMargin`.
4. **Spawner utama** = jangkar leaf pertama. **Base** dipilih dari jangkar lain memakai `BaseStrategy` (dari materi):
   - A: leaf terakhir (DFS).
   - B: jangkar dengan jarak Euclidean terjauh dari spawner.
   - C: jangkar dengan panjang langkah A* terpanjang dari spawner pada grid kosong.
5. **Jumlah jalur** diundi dari tabel berbobot `laneCountWeights` (default {1: 85, 2: 12, 3: 3}, maksimum 4 karena base hanya punya 4 sel tetangga).
6. **Jalur utama**: hubungkan jangkar dari leaf spawner ke leaf base mengikuti urutan DFS (boleh melewati sebagian jangkar), dengan koridor 1 sel. Minimal melewati `minAnchorsPerLane` jangkar agar tidak terlalu lurus. Pembuatan koridor memakai **A\*** dengan sel terlarang (sel jalur lain dan sel yang melanggar aturan jarak di 4.3). Boleh memakai koridor L ala materi sebagai upaya pertama dan A\* sebagai cadangan.
7. **Cabang**: dengan peluang `branchChance` per jalur (maksimum `maxBranchesPerLane`), pilih dua jangkar i < j (j >= i + 2) pada jalur itu dan tambah koridor alternatif di antaranya. Jika tidak muat, batalkan cabang itu (tidak apa-apa).
8. **Jalur tambahan** (spawner terpisah): pilih jangkar yang paling jauh dari base dan dari spawner lain (farthest-point), berada di leaf berbeda. Buat rute ke base dengan A\* yang menghindari jalur yang sudah ada. Jika gagal, **gugurkan jalur itu** (peta tetap valid dengan jumlah jalur lebih sedikit).
9. **Daftar rute**: untuk tiap jalur, telusuri graf koridor dari spawner ke base dan kumpulkan semua rute sederhana (batasi `maxRoutesPerLane`, default 4).
10. **Node**: setiap sel `Empty` dalam jarak Chebyshev 1 dari sel Path/Base menjadi `Node`, kecuali dalam radius `nodeExcludeAroundSpawn` (default 1) dari sel Spawn.
11. **Node Barrack**: untuk tiap node, peluang `barrackBaseChance` (default 0.08) dikali `cornerMultiplier` (default 3) jika node berada dalam jarak Chebyshev 1 dari **sel tikungan** atau **sel persimpangan**. Jadikan `BarrackNode`. Batasan: jarak minimum antar `BarrackNode` (`barrackMinSpacing`, default 3), maksimum `maxBarrackNodes` (default 8), dan minimum `minBarrackNodes` (default 2; jika kurang, promosikan node di dekat tikungan).
    - Sel tikungan: sel Path dengan tepat 2 tetangga Path (4 arah) yang tidak berseberangan.
    - Sel persimpangan: sel Path dengan >= 3 tetangga Path (4 arah).
12. **Validasi**, lalu ulangi dengan `seed + 1` jika gagal (maksimum `maxAttempts`, default 30). Catat jumlah percobaan.

### 4.3 Invariant dan validasi

- Setiap rute: terhubung dari spawner ke base (BFS), tanpa sel ganda, panjang >= `minRouteLength` (default 45 sel).
- **Aturan jarak jalur**: dua sel Path yang bukan tetangga berurutan pada rute yang sama harus berjarak Chebyshev >= 2 (ada minimal satu sel kosong di antaranya). Pengecualian: titik cabang atau persimpangan, dan sel dalam radius 2 dari Base.
- Jumlah node total >= `minNodes` (default 40). Jarak Euclidean spawner ke base >= 40% diagonal peta.
- Tidak ada exception pada 50 seed berturut-turut (lihat stress test di M1).

### 4.4 Parameter bawaan (semua serialized di Inspector)

| Parameter | Default |
|---|---|
| `mapWidth`, `mapHeight` | 48, 32 |
| `tileSize` | 2 (user menyesuaikan dengan ukuran prefab node) |
| `minLeafSize`, `maxLeafSize` | 10, 16 |
| `anchorMargin` | 2 |
| `baseStrategy` | B |
| `laneCountWeights` | 85, 12, 3 |
| `branchChance`, `maxBranchesPerLane` | 0.30, 1 |
| `minAnchorsPerLane` | 3 |
| `maxRoutesPerLane` | 4 |
| `minRouteLength`, `minNodes` | 45, 40 |
| `barrackBaseChance`, `cornerMultiplier` | 0.08, 3 |
| `barrackMinSpacing`, `minBarrackNodes`, `maxBarrackNodes` | 3, 2, 8 |
| `seed`, `useRandomSeed`, `maxAttempts` | 12345, true, 30 |

### 4.5 API publik (`MapGenerator`)

```csharp
public bool IsReady { get; }
public int Seed { get; }                 // seed yang akhirnya valid
public float TileSize { get; }
public MapCell[,] Grid { get; }
public Vector2Int BaseCell { get; }
public IReadOnlyList<LaneData> Lanes { get; }
public event System.Action OnMapGenerated;
public Vector3 CellToWorld(int x, int y);
public void Generate(int seed);          // sinkron; memicu OnMapGenerated
```

---

## 5. Milestone

### M1: Generator BSP (hanya data, tanpa membuat objek game)

**Agent mengerjakan**
- `MapCell.cs`, `LaneData`, struktur BSP, dan `MapGenerator.cs` (MonoBehaviour) sesuai bagian 4. Pecah ke beberapa file kecil jika perlu (mis. `GridAStar.cs`).
- `[ContextMenu("Generate Preview")]`: menjalankan generate, hasil digambar lewat `OnDrawGizmos` (tidak membuat objek di scene). Warna: Path kuning, Spawn hijau, Base merah, Node cyan, BarrackNode magenta, garis leaf BSP abu-abu, jangkar bola putih.
- `[ContextMenu("Stress Test 50 Seeds")]`: menjalankan 50 seed dan mencatat di Console: jumlah valid, rata-rata percobaan, distribusi jumlah jalur, ada tidaknya cabang, panjang rute (min, rata-rata, maks), dan **daftar seed yang menghasilkan 2 atau lebih jalur** serta seed yang punya cabang (dipakai user untuk pengujian M3).
- Log metrik per generate (gaya materi): seed, percobaan, jumlah jalur dan rute, sel jalur, node (Ground/Barrack), panjang tiap rute, hash grid.

**Selesai jika**: seed yang sama dua kali menghasilkan hash grid yang sama; stress test 50 seed tanpa exception; semua invarian 4.3 terpenuhi.

**TUGAS USER (tulis di laporan)**: buat scene baru (salin dari scene statis), tambah objek kosong dengan `MapGenerator`, jalankan Generate Preview dan Stress Test, kirim screenshot Scene view dan teks metrik Console.

### M2: Pembangun peta (membuat objek di Play)

**Agent mengerjakan**
- `MapBuilder.cs`: mendengarkan `OnMapGenerated` lalu membangun di bawah satu objek `GeneratedMap` (hapus dan bangun ulang saat peta diganti; tidak boleh ada objek sisa).
  - Lantai dasar: **satu** objek (plane/quad) yang diskalakan seukuran peta, bukan satu objek per sel.
  - Tile jalur, node, node Barrack, penanda spawner, dan base dari prefab yang diisi user di Inspector. Jika prefab kosong, pakai primitif sementara (kubus) agar tetap jalan.
  - Node memakai prefab `BuildNode`; tipe diatur lewat method publik baru `BuildNode.Initialize(NodeType type)` (tambahkan di `BuildNode.cs`). `BarrackNode` bertipe `NodeType.Path`, diberi tint berbeda.
  - **WaypointPath per rute**: buat objek `WaypointPath` dengan **satu anak Transform per sel rute** (rapat, untuk rally point Barrack). Y waypoint = `waypointY` (default 0.55). Pastikan `points` terisi dengan benar (buat anak dulu sebelum menambah komponen, atau isi `points` eksplisit).
  - Expose `IReadOnlyList<LaneRuntime> Lanes` dengan `Transform spawnPoint` dan `List<WaypointPath> routes`.
- Jangan mengubah `WaveSpawner` di milestone ini.

**Selesai jika**: saat Play, peta muncul sesuai preview M1; klik node Ground membangun Archer/Cannon; node Barrack hanya menerima Barrack; Soldier berjalan ke jalur; tidak ada error Console; regenerasi tidak menyisakan objek.

**TUGAS USER**: siapkan prefab (node, tile jalur, base, penanda spawner, material pasir/jalur), isi referensi `MapBuilder`, hapus objek statis lama dari scene baru (node, WaypointPath, base, titik spawn lama), uji.

### M3: Integrasi WaveSpawner dan AI

**Agent mengerjakan**
- `WaveSpawner`: tunggu peta siap (`WaitUntil` map ready) sebelum `startDelay`. Ambil lane dari `MapBuilder`. Jika tidak ada `MapBuilder` di scene, pakai field lama (`waypointPath`, `alternatePath`) sebagai cadangan agar scene statis tetap jalan.
- **Pembagian Opsi A**: total musuh per wave tetap; bagi ke tiap jalur secara merata (round-robin atau proporsional). Field `extraLaneEnemyMultiplier` (default 1.0) mengalikan jumlah musuh untuk jalur tambahan. Catat jumlah per jalur di log tiap wave.
- Pemilihan rute di dalam satu jalur, mempertahankan enum `PathSelectionType` apa adanya (jangan ganti nama nilainya, data Inspector user bergantung padanya):
  - `Path1Only` = rute utama; `Path2Only` = rute alternatif pertama jika ada, jika tidak rute utama.
  - `RandomPath` = acak antar rute jalur itu.
  - `AutoPerception` = rute dengan tower terdekat paling sedikit.
- `PathPerception`: tambah overload untuk daftar rute (`ChooseSaferRoute(List<WaypointPath>, radius)`), jangan hapus overload lama.
- `BomberAI`: jadikan `onPathRadius` dinyatakan dalam satuan **tile** (default 1.5 tile) dikali `MapGenerator.TileSize`, dengan cadangan ke nilai lama jika tidak ada peta. Logika off-path **tetap ada**.

**Selesai jika**: musuh muncul di semua spawner, jumlah total per wave sesuai, musuh mengikuti rute yang dipilih, dan perilaku Bomber tidak berubah di peta statis lama.

**TUGAS USER**: atur wave, uji di seed yang punya 2 jalur dan cabang (dari daftar M1).

### M4: Dekorasi PCG

**Agent mengerjakan**: `DecorSpawner.cs`, adaptasi `PCGSpawner` materi untuk grid.
- Sel layak = `Empty` yang tidak dalam radius `decorClearRadius` (default 2) dari Spawn dan Base.
- Tabel berbobot: `name`, `prefab`, `weight`, `yOffset`, `scaleRange`, `randomYaw`.
- `decorDensity` (default 0.18 dari sel layak) dan `decorMinSpacing` (default 2 sel).
- Seed turunan deterministik dari seed peta (mis. `seed * 7919 + 13`), RNG sendiri.
- Hasil dipasang di bawah `GeneratedMap/Decor`. Log statistik jumlah per jenis, jumlah dilewati, dan seed.

**Selesai jika**: dekorasi tidak pernah menutupi jalur atau node, hasil identik untuk seed yang sama, dan tabel kosong tidak menyebabkan error (hanya peringatan).

**TUGAS USER**: siapkan prefab kaktus, batu, tulang, dll. dan isi tabel serta bobotnya.

### M5: Poles

**Agent mengerjakan**
- `MapCameraFit`: menyesuaikan posisi kamera utama agar seluruh peta terlihat (kompatibel dengan perspektif dan ortografik).
- Seed ditampilkan kecil di `GameUI`.
- Seed disimpan di static class (mis. `MapSeedSession`) sehingga **`RestartLevel` memakai peta yang sama** dan tombol Play dari Main Menu mengosongkannya (seed baru).
- Tombol debug regenerate (seed + 1) hanya di Editor atau development build, jangan bentrok dengan tombol yang sudah dipakai (cek `Input.GetKeyDown` yang ada, mis. F1 dipakai `AIDebugPanel`).
- Pembersihan: pastikan tidak ada `Debug.Log` berlebihan per frame.

**Selesai jika**: peta selalu terlihat penuh, restart mempertahankan peta, Main Menu memberi peta baru.

---

## 6. Hal yang harus dijaga

- Kompatibilitas: jangan mengganti nama field serialized atau enum yang sudah dipakai di Inspector user tanpa alasan kuat. Jika harus, catat di laporan.
- `EnemyMovement.SetupPath(Transform[], float)` tetap dipakai apa adanya.
- Tidak ada WaypointPath lama di scene, karena `BarrackTower` mencari seluruh WaypointPath.
- Performa: hindari membuat objek per sel lantai; gunakan satu objek lantai.

---

## 7. Batas lingkup

Di luar lingkup: upgrade tower, tower baru, perubahan AI selain `onPathRadius`, UI baru selain tampilan seed, dan perubahan balancing wave.

---

## 8. Format laporan Serah-terima (wajib di akhir setiap milestone)

```
## Serah-terima M<n>
Diubah/Dibuat: <daftar file, satu baris per file>
Asumsi: <jika ada>
TUGAS USER: <langkah bernomor di Unity Editor>
Cek saat Play: <apa yang harus terlihat>
Tempel balik ke agent: <teks Console / screenshot yang dibutuhkan>
Pertanyaan: <maksimal 1, atau "tidak ada">
```

---

## 9. Log status (agent memperbarui tiap serah-terima)

| Milestone | Status | Catatan |
|---|---|---|
| M1 Generator BSP | belum | |
| M2 Pembangun peta | belum | |
| M3 Integrasi WaveSpawner dan AI | belum | |
| M4 Dekorasi PCG | belum | |
| M5 Poles | belum | |
