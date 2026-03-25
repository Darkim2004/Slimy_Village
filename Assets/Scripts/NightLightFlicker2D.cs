using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public class NightLightFlicker2D : MonoBehaviour
{
    private Light2D _light;

    [Header("Attivazione")]
    public bool enableOnlyAtNight = true;
    [Range(0f, 1f)] public float nightStartThreshold = 0.3f;

    [Header("Flicker Macro (lento)")]
    [Min(0f)] public float macroSpeed = 1.2f;
    [Range(0f, 1f)] public float macroAmount = 0.25f;

    [Header("Flicker Micro (veloce)")]
    [Min(0f)] public float microSpeed = 8.5f;
    [Range(0f, 1f)] public float microAmount = 0.08f;

    [Header("Smussatura e limiti")]
    [Min(0f)] public float smoothing = 10f;
    [Min(0f)] public float minIntensity = 0f;

    [Header("Random")]
    public bool randomizePhaseOnStart = true;
    [Min(0f)] public float phaseOffset = 0f;

    private float _smoothedOffset;
    private const float MacroNoiseY = 11.73f;
    private const float MicroNoiseY = 91.17f;

    void Awake()
    {
        _light = GetComponent<Light2D>();

        if (_light == null)
        {
            Debug.LogWarning("NightLightFlicker2D richiede un Light2D sullo stesso GameObject.", this);
            enabled = false;
            return;
        }

        if (randomizePhaseOnStart)
        {
            phaseOffset = Random.value * 100f + Mathf.Abs(GetInstanceID()) * 0.013f;
        }
    }

    void LateUpdate()
    {
        if (_light == null)
        {
            return;
        }

        float baseIntensity = _light.intensity;
        float nightFactor = DayNightScript.NightFactor;

        if (enableOnlyAtNight && nightFactor <= nightStartThreshold)
        {
            _smoothedOffset = 0f;
            _light.intensity = Mathf.Max(minIntensity, baseIntensity);
            return;
        }

        float nightBlend = enableOnlyAtNight
            ? Mathf.InverseLerp(nightStartThreshold, 1f, nightFactor)
            : 1f;

        float t = Time.time + phaseOffset;

        float macro = (Mathf.PerlinNoise(t * macroSpeed, MacroNoiseY) * 2f - 1f) * macroAmount;
        float micro = (Mathf.PerlinNoise(t * microSpeed, MicroNoiseY) * 2f - 1f) * microAmount;

        float rawOffset = baseIntensity * (macro + micro) * nightBlend;
        float lerpFactor = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        _smoothedOffset = Mathf.Lerp(_smoothedOffset, rawOffset, lerpFactor);

        float target = baseIntensity + _smoothedOffset;
        _light.intensity = Mathf.Max(minIntensity, target);
    }
}
