using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class MusicAudioSource : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 1f;

    private AudioSource source;

    public float BaseVolume
    {
        get { return baseVolume; }
    }

    private void Awake()
    {
        EnsureSource();
        ApplySavedVolume();
    }

    private void OnValidate()
    {
        baseVolume = Mathf.Clamp01(baseVolume);
        EnsureSource();
        ApplySavedVolume();
    }

    public void SetBaseVolume(float value)
    {
        baseVolume = Mathf.Clamp01(value);
        ApplySavedVolume();
    }

    public void ApplySavedVolume()
    {
        ApplyVolume(GlobalAudioVolume.GetSavedMusicVolume());
    }

    public void ApplyVolume(float globalMusicVolume)
    {
        EnsureSource();

        if (source == null)
            return;

        source.volume = Mathf.Clamp01(baseVolume) * Mathf.Clamp01(globalMusicVolume);
    }

    private void EnsureSource()
    {
        if (source == null)
            source = GetComponent<AudioSource>();
    }
}
