using UnityEngine;
using UnityEngine.SceneManagement;

public static class GlobalAudioVolume
{
    public const string PrefSfxVolume = "MainMenu.SfxVolume";
    public const string PrefMusicVolume = "MainMenu.MusicVolume";

    public static void GetSavedVolumes(out float sfxVolume, out float musicVolume)
    {
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, 1f));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusicVolume, 1f));
    }

    public static float GetSavedSfxVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, 1f));
    }

    public static void ApplyToSceneAudioSources()
    {
        GetSavedVolumes(out float sfxVolume, out float musicVolume);
        ApplyToSceneAudioSources(sfxVolume, musicVolume);
    }

    public static void ApplyToSceneAudioSources(float sfxVolume, float musicVolume)
    {
        float clampedSfx = Mathf.Clamp01(sfxVolume);
        float clampedMusic = Mathf.Clamp01(musicVolume);

        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.volume = IsMusicSource(source) ? clampedMusic : clampedSfx;
        }
    }

    public static void PlaySfx2D(AudioClip clip, Vector3 position, float baseVolume, float pitch = 1f)
    {
        if (clip == null)
            return;

        float finalVolume = Mathf.Clamp01(baseVolume) * GetSavedSfxVolume();
        if (finalVolume <= 0.0001f)
            return;

        float safePitch = Mathf.Clamp(pitch, 0.1f, 3f);

        GameObject go = new GameObject("OneShotSfx2D");
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = finalVolume;
        source.pitch = safePitch;
        source.clip = clip;
        source.Play();

        float duration = clip.length / Mathf.Abs(safePitch);
        Object.Destroy(go, Mathf.Max(0.05f, duration + 0.05f));
    }

    private static bool IsMusicSource(AudioSource source)
    {
        if (source == null)
            return false;

        if (source.loop)
            return true;

        string sourceName = source.gameObject.name.ToLowerInvariant();
        return sourceName.Contains("music")
            || sourceName.Contains("bgm")
            || sourceName.Contains("theme");
    }
}

public static class RuntimeAudioVolumeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplyCurrentSceneVolumes();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid())
            return;

        ApplyCurrentSceneVolumes();
    }

    private static void ApplyCurrentSceneVolumes()
    {
        GlobalAudioVolume.ApplyToSceneAudioSources();
    }
}
