using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class CampfireLoopPlaylistAudio : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Clip del falo riprodotte in sequenza random per evitare monotonia.")]
    [SerializeField] private AudioClip[] clips;

    [Tooltip("Evita di ripetere subito la stessa clip consecutivamente.")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Playback")]
    [Range(0f, 1f)]
    [Tooltip("Volume base locale del falo (prima del volume globale SFX).")]
    [SerializeField] private float baseVolume = 0.6f;

    [Tooltip("Range di pitch random applicato a ogni clip.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);

    [Min(0f)]
    [Tooltip("Pausa opzionale tra una clip e la successiva.")]
    [SerializeField] private float gapBetweenClips = 0f;

    [Tooltip("Se true, parte automaticamente quando il prefab viene attivato/piazzato.")]
    [SerializeField] private bool playOnEnable = true;

    [Header("AudioSource")]
    [Tooltip("Se true, forza playback 2D (niente attenuazione per distanza).")]
    [SerializeField] private bool force2D = true;

    [Range(0f, 1f)]
    [Tooltip("Usato solo se force2D e false. 0 = 2D, 1 = 3D.")]
    [SerializeField] private float spatialBlend = 0f;

    [Min(0.01f)]
    [Tooltip("Intervallo (secondi) con cui il componente sincronizza il volume col menu principale.")]
    [SerializeField] private float volumeRefreshInterval = 0.2f;

    [Header("Debug")]
    [Tooltip("Se true, logga in editor se non ci sono clip valide configurate.")]
    [SerializeField] private bool warnMissingClips = true;

    private AudioSource _source;
    private Coroutine _loopRoutine;
    private int _lastClipIndex = -1;
    private bool _loggedMissingClips;
    private float _nextVolumeRefreshAt;

    private void Awake()
    {
        EnsureAudioSource();
        ConfigureAudioSource();
    }

    private void OnEnable()
    {
        if (!playOnEnable)
            return;

        StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void OnValidate()
    {
        if (pitchRange.x <= 0f) pitchRange.x = 0.1f;
        if (pitchRange.y <= 0f) pitchRange.y = 0.1f;
        if (volumeRefreshInterval < 0.01f) volumeRefreshInterval = 0.01f;
        if (gapBetweenClips < 0f) gapBetweenClips = 0f;

        EnsureAudioSource();
        ConfigureAudioSource();
    }

    public void StartLoop()
    {
        EnsureAudioSource();
        ConfigureAudioSource();

        if (_loopRoutine != null)
            StopCoroutine(_loopRoutine);

        _loopRoutine = StartCoroutine(PlayLoopRoutine());
    }

    public void StopLoop()
    {
        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        if (_source != null && _source.isPlaying)
            _source.Stop();
    }

    private System.Collections.IEnumerator PlayLoopRoutine()
    {
        while (enabled)
        {
            int clipIndex = SelectNextClipIndex();
            if (clipIndex < 0)
            {
#if UNITY_EDITOR
                if (warnMissingClips && !_loggedMissingClips)
                {
                    Debug.LogWarning("[CampfireLoopPlaylistAudio] Nessuna clip falo valida configurata.", this);
                    _loggedMissingClips = true;
                }
#endif
                yield return null;
                continue;
            }

            AudioClip clip = clips[clipIndex];
            if (clip == null)
            {
                yield return null;
                continue;
            }

            _loggedMissingClips = false;
            _lastClipIndex = clipIndex;

            float minPitch = Mathf.Max(0.1f, Mathf.Min(pitchRange.x, pitchRange.y));
            float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
            float pitch = Random.Range(minPitch, maxPitch);

            _source.pitch = pitch;
            _source.clip = clip;
            RefreshVolume(force: true);
            _source.Play();

            float duration = clip.length / Mathf.Abs(pitch);
            float endTime = Time.time + Mathf.Max(0.01f, duration);

            while (enabled && _source != null && Time.time < endTime)
            {
                RefreshVolume(force: false);
                yield return null;
            }

            if (gapBetweenClips > 0f)
                yield return new WaitForSeconds(gapBetweenClips);
            else
                yield return null;
        }
    }

    private void RefreshVolume(bool force)
    {
        if (_source == null)
            return;

        if (!force && Time.time < _nextVolumeRefreshAt)
            return;

        float globalSfx = GlobalAudioVolume.GetSavedSfxVolume();
        _source.volume = Mathf.Clamp01(baseVolume) * globalSfx;
        _nextVolumeRefreshAt = Time.time + Mathf.Max(0.01f, volumeRefreshInterval);
    }

    private int SelectNextClipIndex()
    {
        if (clips == null || clips.Length == 0)
            return -1;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return -1;

        if (!avoidImmediateRepeat || validCount == 1)
            return PickRandomValidIndex(-1);

        return PickRandomValidIndex(_lastClipIndex);
    }

    private int PickRandomValidIndex(int forbiddenIndex)
    {
        int first = Random.Range(0, clips.Length);
        int fallback = -1;

        for (int i = 0; i < clips.Length; i++)
        {
            int index = (first + i) % clips.Length;
            if (clips[index] == null)
                continue;

            if (fallback < 0)
                fallback = index;

            if (index != forbiddenIndex)
                return index;
        }

        return fallback;
    }

    private void EnsureAudioSource()
    {
        if (_source == null)
            _source = GetComponent<AudioSource>();

        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();
    }

    private void ConfigureAudioSource()
    {
        if (_source == null)
            return;

        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = force2D ? 0f : Mathf.Clamp01(spatialBlend);
        _source.dopplerLevel = 0f;
    }
}
