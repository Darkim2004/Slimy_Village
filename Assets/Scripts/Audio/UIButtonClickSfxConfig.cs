using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio/UI Button Click SFX Config", fileName = "UIButtonClickSfxConfig")]
public class UIButtonClickSfxConfig : ScriptableObject
{
    [Header("Button Click Audio")]
    [Tooltip("Clip usate per il click dei button UI. Se vuoto, il service puo usare il fallback procedurale.")]
    public AudioClip[] buttonClickClips;

    [Range(0f, 1f)]
    [Tooltip("Volume base locale del click UI (prima del volume globale SFX).")]
    public float buttonClickVolume = 0.5f;

    [Tooltip("Range di pitch random per variare il click UI.")]
    public Vector2 buttonClickPitchRange = new Vector2(0.98f, 1.02f);

    [Min(0f)]
    [Tooltip("Intervallo minimo globale tra due click audio UI.")]
    public float clickMinInterval = 0.02f;

    [Header("Binding")]
    [Min(0.05f)]
    [Tooltip("Ogni quanto rivalutare nuovi button in scena.")]
    public float rebindInterval = 0.5f;

    [Tooltip("Se true, usa automaticamente un click breve procedurale quando mancano clip assegnate.")]
    public bool useProceduralFallback = true;

    [Header("Debug")]
    [Tooltip("Logga warning una sola volta se non ci sono clip click assegnate.")]
    public bool warnMissingClickClips = true;

    private void OnValidate()
    {
        if (buttonClickVolume < 0f) buttonClickVolume = 0f;
        if (buttonClickVolume > 1f) buttonClickVolume = 1f;

        if (buttonClickPitchRange.x <= 0f) buttonClickPitchRange.x = 0.1f;
        if (buttonClickPitchRange.y <= 0f) buttonClickPitchRange.y = 0.1f;

        if (clickMinInterval < 0f) clickMinInterval = 0f;
        if (rebindInterval < 0.05f) rebindInterval = 0.05f;
    }
}
