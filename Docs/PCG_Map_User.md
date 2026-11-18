# Panduan Kamu: Peta Prosedural (BSP) Bergantian dengan Agent

Pembagian kerja: **agent menulis kode**, **kamu mengerjakan hal yang hanya bisa dilakukan di Unity** (scene, prefab,
mengisi Inspector, Play, dan melaporkan hasil). Dokumen desain lengkap untuk agent ada di `PCG_Map_Agent.md`.

Prinsipnya: agent mengerjakan satu milestone penuh, berhenti, lalu menulis "Serah-terima". Kamu mengerjakan tugasmu
satu blok, menguji, lalu melapor. Dengan begitu kamu hanya menyentuh Unity di titik serah-terima, bukan tiap file.

---

## Gambaran akhir

- Scene hampir **kosong** saat tidak Play. Isinya hanya kamera, lighting, GameManager, WaveSpawner, UI, BGM, dan satu objek
  `MapGenerator` (plus `MapBuilder`). Peta (lantai, jalur, node, base, spawner, dekorasi) **muncul saat Play** di bawah
  objek `GeneratedMap`.
- Seed yang sama selalu menghasilkan peta yang sama. Restart setelah Game Over memakai peta yang sama. Main Menu memberi peta baru.
- Jumlah jalur: paling sering 1, kadang 2, jarang 3. Jalur bisa bercabang.

---

## Persiapan awal (sekali saja)

- [ ] **Commit Git** sekarang (atau salin folder project) sebagai cadangan.
- [ ] **Duplikat scene yang ada** (klik scene di Project window, Ctrl+D) dan beri nama `Level_Desert_Static`. Jangan diubah lagi.
- [ ] Duplikat sekali lagi menjadi `Level_Desert_Generated`. Kerjakan semuanya di scene ini.
- [ ] Taruh `PCG_Map_Agent.md` di folder project (mis. `Docs/`, boleh di luar `Assets`) agar agent bisa membacanya.
- [ ] Siapkan info untuk agent: versi Unity dan render pipeline (Built-in atau URP).
- [ ] Opsional: ekstrak kedua zip materi (PCG Spawner dan BSP) ke folder `Reference/` di luar `Assets` supaya agent bisa merujuk.

### Prompt pembuka (tempel ke agent)

> Baca file `PCG_Map_Agent.md` sepenuhnya. Kerjakan HANYA Milestone M1, lalu berhenti dan tulis laporan Serah-terima sesuai format di bagian 8. Jangan mengedit file .unity, .prefab, atau .meta, dan jangan lanjut ke M2.
> Info project: Unity [versi], render pipeline [Built-in/URP].

---

## Milestone M1: Generator BSP

**Agent mengerjakan**: generator data (belum ada objek game). **Tunggu sampai agent menulis "Serah-terima M1".**

**Tugasmu**
- [ ] Pastikan tidak ada error kompilasi di Console. Jika ada, tempel ke agent.
- [ ] Di scene `Level_Desert_Generated`: **GameObject > Create Empty**, beri nama `MapGenerator`, lalu tambahkan komponen `MapGenerator`.
- [ ] Klik kanan judul komponen > **Generate Preview**. Lihat hasil di **Scene view** (pastikan tombol Gizmos menyala).
- [ ] Ubah `Seed` beberapa kali (matikan `Use Random Seed`) dan lihat bentuk petanya.
- [ ] Klik kanan > **Stress Test 50 Seeds**. Salin teks hasilnya dari Console.

**Cek**: jalur kuning berkelok dari Spawn (hijau) ke Base (merah), node cyan menempel di samping jalur, node magenta (Barrack) sedikit dan cenderung di tikungan, tidak ada dua jalur yang saling menempel.

**Tempel balik ke agent**: screenshot Scene view dan teks Stress Test. Catat **seed yang punya 2 jalur** dan **seed yang punya cabang** untuk dipakai di M3.

---

## Milestone M2: Pembangun peta

**Tunggu "Serah-terima M2".**

**Tugasmu**
- [ ] **Hapus dari scene baru** semua objek peta statis lama: node-node lama, objek `WaypointPath`, base lama, penanda spawn lama. (Penting: `BarrackTower` mencari semua `WaypointPath`, jadi yang lama bisa mengganggu.)
- [ ] Siapkan prefab (boleh sederhana dulu):
  - Prefab **node**: salin dari prefab node yang ada (harus punya Collider, Renderer, dan `BuildNode`).
  - Prefab **tile jalur**: kubus pipih dengan material tanah/jalur.
  - Prefab **base** dan **penanda spawner**: objek apa saja yang terlihat.
  - **Lantai pasir**: material pasir untuk plane dasar.
- [ ] Tambahkan komponen `MapBuilder` (di objek yang sama dengan `MapGenerator` atau terpisah, sesuai instruksi agent) lalu isi semua kolom prefab.
- [ ] Atur `Tile Size` agar sesuai ukuran prefab node (jika node berukuran 2 unit, isi 2).
- [ ] Tekan **Play**.

**Cek**
- [ ] Peta muncul sesuai preview. Klik node biasa untuk membangun Archer/Cannon.
- [ ] Barrack hanya bisa dibangun di node magenta, dan Soldier berjalan ke jalur.
- [ ] Tidak ada error di Console. Ganti seed dan Play lagi: tidak ada objek sisa.

**Tempel balik ke agent**: screenshot Game view dan pesan error (jika ada).

---

## Milestone M3: Wave dan AI

**Tunggu "Serah-terima M3".**

**Tugasmu**
- [ ] Di `WaveSpawner`, kolom referensi path lama boleh dikosongkan. Isi kolom referensi `MapBuilder` jika diminta agent.
- [ ] Atur wave seperti biasa. `Path Option` tiap musuh: Path1Only = rute utama, RandomPath = acak antar rute, AutoPerception = rute yang paling sedikit towernya.
- [ ] Uji di **seed yang punya 2 jalur** dan **seed yang punya cabang**.
- [ ] Uji Bomber: tower di samping jalur harus terdeteksi dan diledakkan.

**Cek**
- [ ] Musuh muncul di **setiap spawner**, dan jumlah total per wave sama seperti sebelumnya (dibagi ke jalur).
- [ ] Musuh tidak melewati dinding. Di jalur bercabang, musuh memilih salah satu rute lengkap sampai base.
- [ ] Kondisi menang tetap muncul setelah semua musuh habis.

**Tempel balik ke agent**: log pembagian musuh per jalur dan deskripsi masalah (seed dan wave) jika ada.

---

## Milestone M4: Dekorasi

**Tunggu "Serah-terima M4".**

**Tugasmu**
- [ ] Siapkan prefab dekorasi: kaktus, batu, tulang, dan sejenisnya (cek: tanpa Collider yang menghalangi klik node, atau pakai layer yang tidak ikut Physics raycast).
- [ ] Isi tabel `DecorSpawner`: `weight` besar untuk yang sering (mis. kaktus 5, batu 3, tulang 1), `yOffset` agar menempel tanah, dan `scaleRange`.
- [ ] Play dan amati.

**Cek**
- [ ] Dekorasi tidak menutupi jalur atau node dan tidak menumpuk berdekatan.
- [ ] Seed yang sama menghasilkan dekorasi yang sama.

---

## Milestone M5: Poles

**Tunggu "Serah-terima M5".**

**Tugasmu**
- [ ] Atur kamera jika perlu (agent menambahkan `MapCameraFit`).
- [ ] Cek: Main Menu > Play membuat peta baru; Game Over > Restart memakai **peta yang sama**; seed tampil kecil di layar.
- [ ] Cek tombol debug regenerate hanya ada di Editor.
- [ ] Lakukan balancing akhir wave dan jumlah musuh.

---

## Prompt untuk giliran berikutnya (tempel ke agent)

> Saya sudah menyelesaikan tugas M<n>. Hasil: <tempel teks Console / screenshot / deskripsi masalah>.
> Perbaiki jika ada masalah, lalu kerjakan HANYA M<n+1> sesuai `PCG_Map_Agent.md`, dan berhenti di Serah-terima.

Kalau ada bug di milestone sebelumnya, minta agent memperbaikinya dulu dalam giliran yang sama, jangan lanjut ke milestone berikutnya.

---

## Pemecahan masalah umum

| Gejala | Kemungkinan penyebab |
|---|---|
| Peta tidak muncul saat Play | `MapBuilder` belum terpasang atau prefab belum diisi; cek Console. |
| Node tidak bisa diklik | Prefab node tidak punya Collider, atau ada dekorasi/objek lain yang menutupi. |
| Barrack tidak mengirim Soldier ke jalur | Masih ada `WaypointPath` lama di scene, atau waypoint tidak rapat. |
| Musuh langsung hilang atau berjalan melintasi dinding | Waypoint kosong atau Y tidak sesuai; kirim log ke agent. |
| `Missing Script` pada objek | Nama file tidak sama dengan nama class, atau file dipindah lewat File Explorer. Ganti nama di Project window Unity. |
| Peta terlalu kecil atau besar | Ubah `Tile Size` dan `Map Width/Height` di `MapGenerator`. |
| Generate lambat atau sering gagal | Besarkan `maxAttempts` atau kecilkan `minRouteLength`; kirim hasil metrik ke agent. |

---

## Checklist besar

- [ ] Persiapan awal (cadangan, dua scene, prompt pembuka)
- [ ] M1 selesai dan teruji (screenshot dan Stress Test dikirim)
- [ ] M2 selesai (peta muncul, node dan Barrack berfungsi)
- [ ] M3 selesai (banyak spawner, pembagian musuh, Bomber)
- [ ] M4 selesai (dekorasi berbobot)
- [ ] M5 selesai (kamera, seed, restart)
- [ ] Balancing akhir dan demo
