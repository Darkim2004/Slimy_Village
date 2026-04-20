using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonClickSfxService : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Config ScriptableObject opzionale. Se nullo, prova auto-load da Resources.")]
    [SerializeField] private UIButtonClickSfxConfig clickConfig;

    [Tooltip("Path in Resources per auto-load config (senza estensione).")]
    [SerializeField] private string resourcesConfigPath = "Audio/UIButtonClickSfxConfig";

    [Header("Fallback (used when config is missing)")]
    [Header("Button Click Audio")]
    [Tooltip("Clip usate per il click dei button UI. Se vuoto, usa un click procedurale di fallback.")]
    [SerializeField] private AudioClip[] buttonClickClips;

    [Range(0f, 1f)]
    [Tooltip("Volume base locale del click UI (prima del volume globale SFX).")]
    [SerializeField] private float buttonClickVolume = 0.5f;

    [Tooltip("Range di pitch random per variare il click UI.")]
    [SerializeField] private Vector2 buttonClickPitchRange = new Vector2(0.98f, 1.02f);

    [Min(0f)]
    [Tooltip("Intervallo minimo globale tra due click audio UI.")]
    [SerializeField] private float clickMinInterval = 0.02f;

    [Header("Binding")]
    [Min(0.05f)]
    [Tooltip("Ogni quanto rivalutare nuovi button in scena.")]
    [SerializeField] private float rebindInterval = 0.5f;

    [Tooltip("Se true, crea automaticamente un click breve procedurale quando mancano clip assegnate.")]
    [SerializeField] private bool useProceduralFallback = true;

    [Header("Debug")]
    [Tooltip("Logga warning una sola volta se non ci sono clip click assegnate.")]
    [SerializeField] private bool warnMissingClickClips = true;

    private readonly Dictionary<Button, UnityAction> _boundButtons = new Dictionary<Button, UnityAction>();

    private AudioClip _proceduralFallbackClip;
    private bool _loggedMissingClickClips;
    private float _lastClickAt = float.NegativeInfinity;
    private float _nextRebindAt;

    private static UIButtonClickSfxService _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        UIButtonClickSfxService existing = FindFirstObjectByType<UIButtonClickSfxService>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject go = new GameObject("UIButtonClickSfxService");
        DontDestroyOnLoad(go);
        go.AddComponent<UIButtonClickSfxService>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveConfig();

        if (GetUseProceduralFallback())
            _proceduralFallbackClip = CreateFallbackClip();

        BindAllButtons();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindAllButtons();

        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRebindAt)
            return;

        BindAllButtons();
        _nextRebindAt = Time.unscaledTime + Mathf.Max(0.05f, GetRebindInterval());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindAllButtons();
    }

    private void BindAllButtons()
    {
        CleanupDeadBindings();

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
            BindButton(buttons[i]);
    }

    private void BindButton(Button button)
    {
        if (button == null)
            return;

        if (_boundButtons.ContainsKey(button))
            return;

        UnityAction action = PlayButtonClick;
        button.onClick.AddListener(action);
        _boundButtons.Add(button, action);
    }

    private void CleanupDeadBindings()
    {
        if (_boundButtons.Count == 0)
            return;

        List<Button> toRemove = null;
        foreach (KeyValuePair<Button, UnityAction> kvp in _boundButtons)
        {
            if (kvp.Key != null)
                continue;

            if (toRemove == null)
                toRemove = new List<Button>();

            toRemove.Add(kvp.Key);
        }

        if (toRemove == null)
            return;

        for (int i = 0; i < toRemove.Count; i++)
            _boundButtons.Remove(toRemove[i]);
    }

    private void UnbindAllButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> kvp in _boundButtons)
        {
            Button button = kvp.Key;
            if (button == null)
                continue;

            button.onClick.RemoveListener(kvp.Value);
        }

        _boundButtons.Clear();
    }

    private void PlayButtonClick()
    {
        if (Time.unscaledTime < _lastClickAt + Mathf.Max(0f, GetClickMinInterval()))
            return;

        AudioClip clip = PickClickClip();
        if (clip == null)
            return;

        Vector2 pitchRange = GetButtonClickPitchRange();
        float minPitch = Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        float pitch = Random.Range(minPitch, maxPitch);

        GlobalAudioVolume.PlaySfx2D(clip, Vector3.zero, Mathf.Clamp01(GetButtonClickVolume()), pitch);
        _lastClickAt = Time.unscaledTime;
    }

    private AudioClip PickClickClip()
    {
        AudioClip clip = PickRandomValidClip(GetButtonClickClips());
        if (clip != null)
        {
            _loggedMissingClickClips = false;
            return clip;
        }

#if UNITY_EDITOR
        if (GetWarnMissingClickClips() && !_loggedMissingClickClips)
        {
            Debug.LogWarning("[UIButtonClickSfxService] Nessuna clip click UI assegnata: uso fallback procedurale.", this);
            _loggedMissingClickClips = true;
        }
#endif

        return GetUseProceduralFallback() ? _proceduralFallbackClip : null;
    }

    private void ResolveConfig()
    {
        if (clickConfig != null)
            return;

        if (string.IsNullOrWhiteSpace(resourcesConfigPath))
            return;

        clickConfig = Resources.Load<UIButtonClickSfxConfig>(resourcesConfigPath);
    }

    private AudioClip[] GetButtonClickClips()
    {
        return clickConfig != null ? clickConfig.buttonClickClips : buttonClickClips;
    }

    private float GetButtonClickVolume()
    {
        return clickConfig != null ? clickConfig.buttonClickVolume : buttonClickVolume;
    }

    private Vector2 GetButtonClickPitchRange()
    {
        return clickConfig != null ? clickConfig.buttonClickPitchRange : buttonClickPitchRange;
    }

    private float GetClickMinInterval()
    {
        return clickConfig != null ? clickConfig.clickMinInterval : clickMinInterval;
    }

    private float GetRebindInterval()
    {
        return clickConfig != null ? clickConfig.rebindInterval : rebindInterval;
    }

    private bool GetUseProceduralFallback()
    {
        return clickConfig != null ? clickConfig.useProceduralFallback : useProceduralFallback;
    }

    private bool GetWarnMissingClickClips()
    {
        return clickConfig != null ? clickConfig.warnMissingClickClips : warnMissingClickClips;
    }

    private static AudioClip PickRandomValidClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int first = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            int index = (first + i) % clips.Length;
            AudioClip clip = clips[index];
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static AudioClip CreateFallbackClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.04f;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));

        float[] data = new float[sampleCount];
        float baseFreq = 1800f;
        float endFreq = 1100f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float env = Mathf.Exp(-16f * t);
            float freq = Mathf.Lerp(baseFreq, endFreq, t);
            float phase = 2f * Mathf.PI * freq * i / sampleRate;
            data[i] = Mathf.Sin(phase) * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("UIButtonFallbackClick", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
