using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Inventory Crafting")]
    [SerializeField] private Button craftButtonInventory;
    [SerializeField] private GameObject inventoryTitleObject;
    [SerializeField] private CraftingStationMenuUI inventoryCraftingMenuPrefab;
    [SerializeField] private CraftingRecipeDefinition[] inventoryCraftingRecipes;

    [Header("Player Lock")]
    [SerializeField] private PlayerTopDown playerTopDown;

    [Header("Inventory")]
    [SerializeField] private InventoryInteractionController interactionController;

    private readonly List<GameObject> craftingHiddenChildren = new List<GameObject>();
    private CraftingStationMenuUI inventoryCraftingMenuInstance;
    private bool isCraftingModeOpen;

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

        ResolveInventoryCraftingReferences();
        RegisterCraftButtonListener();

        SetOpen(startOpened, true);
    }

    private void OnDestroy()
    {
        UnregisterCraftButtonListener();
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

            // Non permettere l'apertura se il gioco è in pausa
            var pauseController = FindFirstObjectByType<PauseMenuController>();
            if (pauseController != null && pauseController.IsPaused)
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

        if (!open)
            CloseInventoryCraftingMode();

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

    public void ToggleInventoryCraftingMode()
    {
        if (!IsOpen)
            return;

        ResolveInventoryCraftingReferences();

        if (isCraftingModeOpen)
        {
            CloseInventoryCraftingMode();
            return;
        }

        OpenInventoryCraftingMode();
    }

    private void OpenInventoryCraftingMode()
    {
        if (!EnsureInventoryCraftingMenu())
            return;

        if (inventoryCraftingRecipes == null || inventoryCraftingRecipes.Length == 0)
        {
            GameDebug.Warning(GameDebugCategory.Inventory,
                "[InventoryToggle] Nessuna ricetta assegnata per il crafting da inventario.", this);
        }

        inventoryCraftingMenuInstance.SetExternalRecipes(inventoryCraftingRecipes);
        inventoryCraftingMenuInstance.Show(null);
        HideInventoryChildrenForCrafting();
        isCraftingModeOpen = true;
    }

    private void CloseInventoryCraftingMode()
    {
        if (inventoryCraftingMenuInstance != null && inventoryCraftingMenuInstance.IsOpen)
            inventoryCraftingMenuInstance.Hide();

        RestoreInventoryChildrenAfterCrafting();
        isCraftingModeOpen = false;
    }

    private bool EnsureInventoryCraftingMenu()
    {
        if (inventoryCraftingMenuInstance != null)
            return true;

        if (inventoryCraftingMenuPrefab == null)
        {
            GameDebug.Warning(GameDebugCategory.Inventory,
                "[InventoryToggle] inventoryCraftingMenuPrefab non assegnato.", this);
            return false;
        }

        var parent = inventoryPanel != null ? inventoryPanel.transform : transform;
        inventoryCraftingMenuInstance = Instantiate(inventoryCraftingMenuPrefab, parent, false);
        inventoryCraftingMenuInstance.gameObject.name = inventoryCraftingMenuPrefab.gameObject.name + "_InventoryRuntime";
        inventoryCraftingMenuInstance.SetAttachToSceneCanvasOnShow(false);
        inventoryCraftingMenuInstance.SetControlPlayerInputLock(false);
        inventoryCraftingMenuInstance.SetCloseBehaviors(false, false);
        inventoryCraftingMenuInstance.Hide();
        return true;
    }

    private void HideInventoryChildrenForCrafting()
    {
        if (inventoryPanel == null)
            return;

        craftingHiddenChildren.Clear();

        var keepCraftButton = craftButtonInventory != null ? craftButtonInventory.gameObject : null;
        var keepTitle = inventoryTitleObject;
        var keepMenu = inventoryCraftingMenuInstance != null ? inventoryCraftingMenuInstance.gameObject : null;

        foreach (Transform child in inventoryPanel.transform)
        {
            var childObject = child.gameObject;
            if (childObject == keepCraftButton || childObject == keepTitle || childObject == keepMenu)
                continue;

            if (!childObject.activeSelf)
                continue;

            craftingHiddenChildren.Add(childObject);
            childObject.SetActive(false);
        }
    }

    private void RestoreInventoryChildrenAfterCrafting()
    {
        for (int i = 0; i < craftingHiddenChildren.Count; i++)
        {
            var childObject = craftingHiddenChildren[i];
            if (childObject != null)
                childObject.SetActive(true);
        }

        craftingHiddenChildren.Clear();
    }

    private void ResolveInventoryCraftingReferences()
    {
        if (inventoryPanel == null)
            return;

        if (craftButtonInventory == null)
        {
            var craftButtonTransform = inventoryPanel.transform.Find("CraftButtonInventory");
            if (craftButtonTransform != null)
                craftButtonInventory = craftButtonTransform.GetComponent<Button>();
        }

        if (inventoryTitleObject == null)
        {
            var inventoryTitleTransform = inventoryPanel.transform.Find("Inventory");
            if (inventoryTitleTransform != null)
                inventoryTitleObject = inventoryTitleTransform.gameObject;
        }
    }

    private void RegisterCraftButtonListener()
    {
        if (craftButtonInventory == null)
        {
            GameDebug.Warning(GameDebugCategory.Inventory,
                "[InventoryToggle] CraftButtonInventory non trovato: impossibile agganciare il toggle crafting.", this);
            return;
        }

        craftButtonInventory.onClick.RemoveListener(ToggleInventoryCraftingMode);
        craftButtonInventory.onClick.AddListener(ToggleInventoryCraftingMode);
    }

    private void UnregisterCraftButtonListener()
    {
        if (craftButtonInventory != null)
            craftButtonInventory.onClick.RemoveListener(ToggleInventoryCraftingMode);
    }
}
