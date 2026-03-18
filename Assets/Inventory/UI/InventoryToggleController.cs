using UnityEngine;

/// <summary>
/// Gestisce apertura/chiusura inventario con tasto toggle e lock movimento player.
/// Da mettere su un GameObject sempre attivo (non sul panel che viene nascosto).
/// </summary>
public class InventoryToggleController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    [Header("UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private bool startOpened;

    [Header("Player Lock")]
    [SerializeField] private PlayerTopDown playerTopDown;

    [Header("Inventory")]
    [SerializeField] private InventoryInteractionController interactionController;

    public bool IsOpen { get; private set; }

    /// <summary>True se il controller è sullo stesso GO del panel (situazione da evitare ma gestita).</summary>
    private bool isSelfPanel;

    private void Awake()
    {
        if (playerTopDown == null)
            playerTopDown = FindFirstObjectByType<PlayerTopDown>();

        if (interactionController == null)
            interactionController = FindFirstObjectByType<InventoryInteractionController>();

        // Rileva se il controller vive sullo stesso GO del panel
        isSelfPanel = inventoryPanel != null && inventoryPanel == gameObject;

        if (isSelfPanel)
            GameDebug.Warning(GameDebugCategory.Inventory,
                "[InventoryToggle] Il controller è sullo stesso GameObject del panel! " +
                "Nasconderò solo i figli per non disattivare me stesso. " +
                "Per setup pulito, sposta questo componente su un GO sempre attivo.", this);

        SetOpen(startOpened, true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            // Non permettere l'apertura se l'input è già bloccato da altri menu (es. Chest)
            if (!IsOpen && playerTopDown != null && playerTopDown.IsInputLocked)
            {
                return;
            }

            GameDebug.Log(GameDebugCategory.Inventory, $"[InventoryToggle] Key '{toggleKey}' pressed → toggling (was {(IsOpen ? "open" : "closed")})", this);
            Toggle();
        }
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open, bool force = false)
    {
        if (!force && IsOpen == open) return;

        IsOpen = open;

        if (inventoryPanel != null)
        {
            if (isSelfPanel)
            {
                // Non disattivare il GO stesso, solo i figli
                foreach (Transform child in inventoryPanel.transform)
                    child.gameObject.SetActive(IsOpen);
            }
            else
            {
                inventoryPanel.SetActive(IsOpen);
            }
        }
        else
            GameDebug.Warning(GameDebugCategory.Inventory, "[InventoryToggle] inventoryPanel is null — nothing to show/hide!", this);

        if (!IsOpen && interactionController != null)
            interactionController.TryReturnCursorToInventory();

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(IsOpen);
    }
}
