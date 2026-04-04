using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestisce il menu pausa attivato con ESC.
/// Blocca il player, ferma il tempo (Time.timeScale), e previene sovrapposizioni con altri menu.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private PlayerTopDown playerTopDown;
    private InventoryToggleController inventoryToggleController;
    private bool isPaused;
    private float timeScaleBeforePause = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        playerTopDown = FindFirstObjectByType<PlayerTopDown>();
        inventoryToggleController = FindFirstObjectByType<InventoryToggleController>();

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ClosePause);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        isPaused = false;
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ClosePause);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);

        // Sicurezza: se il controller viene distrutto in pausa, ripristina timeScale
        if (isPaused)
        {
            Time.timeScale = timeScaleBeforePause;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    private void HandleEscapeKey()
    {
        // Priorità: chiudi menu di interazione prima, poi inventario, poi togglepausausa
        if (playerTopDown != null && playerTopDown.IsAnyInteractionMenuOpenPublic())
        {
            playerTopDown.CloseAllInteractionMenusPublic();
            return;
        }

        if (inventoryToggleController != null && inventoryToggleController.IsOpen)
        {
            inventoryToggleController.SetOpen(false);
            return;
        }

        // Se siamo qui, nessun altro menu è aperto: toggle pausa
        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
            ClosePause();
        else
            TryOpenPause();
    }

    public void TryOpenPause()
    {
        // Non aprire pausa se input è già lockato (es. da interazione menu)
        if (playerTopDown != null && playerTopDown.IsInputLocked)
            return;

        // Non aprire pausa se ci sono menu aperti
        if (playerTopDown != null && playerTopDown.IsAnyInteractionMenuOpenPublic())
            return;

        if (inventoryToggleController != null && inventoryToggleController.IsOpen)
            return;

        OpenPause();
    }

    private void OpenPause()
    {
        if (isPaused) return;

        isPaused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(true);

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);

            // Seleziona il bottone resume per navigazione UI
            if (resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }
    }

    public void ClosePause()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = timeScaleBeforePause;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void OnQuitClicked()
    {
        WorldSaveSystem.Instance?.SaveNow("save-and-quit");
        ClosePause();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnSaveClicked()
    {
        bool success = WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.SaveNow("manual");
        Debug.Log(success ? "[PauseMenu] Manual save completed." : "[PauseMenu] Manual save skipped or failed.");
    }
}
