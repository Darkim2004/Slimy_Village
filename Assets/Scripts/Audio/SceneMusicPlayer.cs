using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource), typeof(MusicAudioSource))]
public sealed class SceneMusicPlayer : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Config ScriptableObject opzionale. Se nullo, prova auto-load da Resources.")]
    [SerializeField] private SceneMusicConfig musicConfig;

    [Tooltip("Path in Resources per auto-load config (senza estensione).")]
    [SerializeField] private string resourcesConfigPath = "Audio/SceneMusicConfig";

    [Header("Runtime")]
    [Min(0.05f)]
    [Tooltip("Ogni quanto sincronizzare il volume col valore salvato dal menu principale.")]
    [SerializeField] private float volumeRefreshInterval = 0.2f;

    private static SceneMusicPlayer instance;

    private AudioSource source;
    private MusicAudioSource musicSource;
    private AudioClip currentClip;
    private string activeSceneName;
    private bool loggedMissingConfig;
    private bool loggedMissingTrack;
    private float nextVolumeRefreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneMusicPlayer existing = FindFirstObjectByType<SceneMusicPlayer>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject go = new GameObject("SceneMusicPlayer");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioSource>();
        go.AddComponent<MusicAudioSource>();
        go.AddComponent<SceneMusicPlayer>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureComponents();
        ResolveConfig();
        ConfigureAudioSource();
        PlayForScene(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (instance == this)
            instance = null;
    }

    private void OnValidate()
    {
        if (volumeRefreshInterval < 0.05f)
            volumeRefreshInterval = 0.05f;

        EnsureComponents();
        ConfigureAudioSource();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextVolumeRefreshAt)
            return;

        if (musicSource != null)
            musicSource.ApplySavedVolume();

        RefreshActiveSceneTrack();
        nextVolumeRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, volumeRefreshInterval);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene);
    }

    private void PlayForScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        activeSceneName = scene.name;
        PlayForSceneName(activeSceneName);
    }

    private void RefreshActiveSceneTrack()
    {
        if (string.IsNullOrEmpty(activeSceneName))
            return;

        PlayForSceneName(activeSceneName);
    }

    private void PlayForSceneName(string sceneName)
    {
        EnsureComponents();
        ResolveConfig();
        ConfigureAudioSource();

        if (musicConfig == null)
        {
            StopMusic();
            LogMissingConfigOnce();
            return;
        }

        SceneMusicConfig.SceneTrack track = musicConfig.FindTrack(sceneName);
        if (track == null || !TryResolveTrack(track, out AudioClip clip, out float baseVolume))
        {
            if (musicConfig.stopWhenSceneHasNoTrack)
                StopMusic();

            LogMissingTrackOnce(sceneName);
            return;
        }

        loggedMissingTrack = false;

        if (musicSource != null)
            musicSource.SetBaseVolume(baseVolume);

        if (currentClip == clip && source != null && source.isPlaying)
            return;

        currentClip = clip;
        source.clip = currentClip;
        source.Play();
    }

    private bool TryResolveTrack(SceneMusicConfig.SceneTrack track, out AudioClip clip, out float baseVolume)
    {
        clip = null;
        baseVolume = 1f;

        if (track == null)
            return false;

        if (track.useDayNightCycle)
        {
            bool isNight = DayNightScript.NightFactor >= Mathf.Clamp01(track.nightSwitchThreshold);
            clip = isNight ? track.nightClip : track.dayClip;
            baseVolume = isNight ? track.nightBaseVolume : track.dayBaseVolume;

            if (clip != null)
                return true;
        }

        clip = track.clip;
        baseVolume = track.baseVolume;
        return clip != null;
    }

    private void StopMusic()
    {
        currentClip = null;
        activeSceneName = null;

        if (source != null)
            source.Stop();
    }

    private void ResolveConfig()
    {
        if (musicConfig != null)
            return;

        if (string.IsNullOrWhiteSpace(resourcesConfigPath))
            return;

        musicConfig = Resources.Load<SceneMusicConfig>(resourcesConfigPath);
    }

    private void EnsureComponents()
    {
        if (source == null)
            source = GetComponent<AudioSource>();

        if (musicSource == null)
            musicSource = GetComponent<MusicAudioSource>();
    }

    private void ConfigureAudioSource()
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = true;
    }

    private void LogMissingConfigOnce()
    {
#if UNITY_EDITOR
        if (loggedMissingConfig)
            return;

        Debug.LogWarning("[SceneMusicPlayer] Nessuna SceneMusicConfig trovata in Resources.", this);
        loggedMissingConfig = true;
#endif
    }

    private void LogMissingTrackOnce(string sceneName)
    {
#if UNITY_EDITOR
        if (musicConfig == null || !musicConfig.warnMissingTracks || loggedMissingTrack)
            return;

        Debug.LogWarning("[SceneMusicPlayer] Nessuna traccia music valida configurata per la scena '" + sceneName + "'.", this);
        loggedMissingTrack = true;
#endif
    }
}
