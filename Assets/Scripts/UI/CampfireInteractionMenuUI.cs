using UnityEngine;

/// <summary>
/// Menu de falò essenziale: si chiude con Escape o il tasto di interazione (E).
/// Espone funzioni pubbliche agganciabili dall'Inspector ai bottoni UI.
/// </summary>
public class CampfireInteractionMenuUI : PlaceableInteractionMenuBase
{
    [Header("Behavior")]
    [Tooltip("Permette di chiudere il menù premendo di nuovo il tasto interazione.")]
    [SerializeField] private bool closeWithInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Tooltip("Se true, il menu viene agganciato a un Canvas screen-space gia presente in scena.")]
    [SerializeField] private bool attachToSceneCanvasOnShow = true;

    private PlayerTopDown playerTopDown;
    private DayNightScript dayNightScript;
    private int openedFrame = -1;
    private Canvas runtimeSceneCanvas;

    private void Awake()
    {
        playerTopDown = FindFirstObjectByType<PlayerTopDown>();
        dayNightScript = FindFirstObjectByType<DayNightScript>();
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();

        if (closeWithInteractKey && Input.GetKeyDown(interactKey) && Time.frameCount > openedFrame)
            Hide();
    }

    public override void Show(PlacedObject placedObject)
    {
        EnsureSceneCanvasParent();

        base.Show(placedObject);
        openedFrame = Time.frameCount;

        if (playerTopDown == null)
            playerTopDown = FindFirstObjectByType<PlayerTopDown>();
        
        if (dayNightScript == null)
            dayNightScript = FindFirstObjectByType<DayNightScript>();

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(true);
    }

    public override void Hide()
    {
        if (playerTopDown != null)
            playerTopDown.SetInputLocked(false);

        base.Hide();
    }

    /// <summary>
    /// Da assegnare all'evento OnClick del bottone per impostare il punto di respawn.
    /// </summary>
    public void OnSetRespawnButtonClicked()
    {
        if (playerTopDown != null)
        {
            playerTopDown.SetRespawnPoint(playerTopDown.transform.position);
            Debug.Log("[Campfire] Punto di respawn aggiornato: " + playerTopDown.transform.position);
        }
        else
        {
            Debug.LogWarning("[Campfire] Impossibile trovare il player per impostare il respawn.");
        }
    }

    /// <summary>
    /// Da assegnare all'evento OnClick del bottone per mandare avanti il tempo di 12 ore.
    /// </summary>
    public void OnSkipTimeButtonClicked()
    {
        if (dayNightScript != null)
        {
            dayNightScript.hours += 12;
            if (dayNightScript.hours >= 24)
            {
                dayNightScript.hours -= 24;
                dayNightScript.days += 1;
            }
            Debug.Log($"[Campfire] Tempo saltato di 12 ore. Giorno {dayNightScript.days}, Ore {dayNightScript.hours}:00");
        }
        else
        {
            Debug.LogWarning("[Campfire] Nessun DayNightScript trovato nella scena!");
        }
    }

    private void EnsureSceneCanvasParent()
    {
        if (!attachToSceneCanvasOnShow) return;

        var canvas = FindSceneScreenSpaceCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[CampfireInteractionMenuUI] Nessun Canvas screen-space root trovato in scena. Il menu non puo essere agganciato.", this);
            return;
        }

        runtimeSceneCanvas = canvas;
        if (transform.parent != runtimeSceneCanvas.transform)
            transform.SetParent(runtimeSceneCanvas.transform, false);
    }

    private Canvas FindSceneScreenSpaceCanvas()
    {
        if (runtimeSceneCanvas != null &&
            runtimeSceneCanvas.isRootCanvas &&
            (runtimeSceneCanvas.renderMode == RenderMode.ScreenSpaceOverlay ||
             runtimeSceneCanvas.renderMode == RenderMode.ScreenSpaceCamera))
        {
            return runtimeSceneCanvas;
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas cameraCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (!c.isRootCanvas) continue;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;

            if (cameraCanvas == null && c.renderMode == RenderMode.ScreenSpaceCamera)
                cameraCanvas = c;
        }

        return cameraCanvas;
    }
}
