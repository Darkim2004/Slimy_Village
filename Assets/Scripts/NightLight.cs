using UnityEngine;
using UnityEngine.Rendering.Universal; // <- importantissimo per Light2D

[RequireComponent(typeof(Light2D))]
public class NightLight2D : MonoBehaviour
{
    private Light2D _light;
    public float CurrentBaseIntensity { get; private set; }

    [Header("Intensita")]
    public float dayIntensity = 0.2f;   // quasi spenta di giorno
    public float nightIntensity = 5f;   // forte di notte

    void Awake()
    {
        _light = GetComponent<Light2D>();

        if (_light == null)
        {
            Debug.LogWarning("NightLight2D richiede un Light2D sullo stesso GameObject.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (_light == null)
        {
            return;
        }

        // 0 = giorno, 1 = notte (dal tuo DayNightScript)
        float t = DayNightScript.NightFactor;

        CurrentBaseIntensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        _light.intensity = CurrentBaseIntensity;
    }
}