using UnityEngine;
using UnityEngine.Rendering.Universal; // <- importantissimo per Light2D

[RequireComponent(typeof(Light2D))]
public class NightLight2D : MonoBehaviour
{
    private Light2D _light;

    [Header("Intensità")]
    public float dayIntensity = 0.2f;   // quasi spenta di giorno
    public float nightIntensity = 5f;   // forte di notte

    void Awake()
    {
        _light = GetComponent<Light2D>();
    }

    void Update()
    {
        // 0 = giorno, 1 = notte (dal tuo DayNightScript)
        float t = DayNightScript.NightFactor;

        float targetIntensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        _light.intensity = targetIntensity;
    }
}