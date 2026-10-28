using UnityEngine;

/// <summary>
/// Pemutar SFX sederhana. Tidak perlu dipasang ke objek mana pun: cukup panggil
/// Sfx.Play(clip, volume, variasiPitch) dari script lain.
/// Suara diputar sebagai 2D (tidak mengecil karena jarak), cocok untuk kamera dari atas.
/// </summary>
public static class Sfx
{
    public static void Play(AudioClip clip, float volume = 1f, float pitchVariation = 0f)
    {
        if (clip == null) return;

        GameObject go = new GameObject("SFX_" + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();

        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f; // 2D
        // Variasi pitch kecil membuat suara yang sering diulang tidak terdengar monoton
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.Play();

        Object.Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
    }
}