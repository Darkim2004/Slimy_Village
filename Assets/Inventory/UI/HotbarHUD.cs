using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller della hotbar HUD sempre visibile a schermo.
/// Gestisce la selezione dello slot attivo (tasti 1-9 / scroll) e
/// delega la visualizzazione degli slot a <see cref="InventorySectionUI"/>.
/// </summary>
[RequireComponent(typeof(InventorySectionUI))]
[RequireComponent(typeof(CanvasGroup))]
public class HotbarHUD : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    [Header("References")]
    [Tooltip("InventorySectionUI sullo stesso GO (auto-rilevato).")]
    [SerializeField] private InventorySectionUI hotbarSectionUI;

    [Tooltip("InventoryModel di riferimento. Se null, viene cercato automaticamente.")]
    [SerializeField] private InventoryModel inventory;

    [Tooltip("Controller interazione inventario. Se null, viene cercato automaticamente.")]
    [SerializeField] private InventoryInteractionController interactionController;

    [Tooltip("Prefab di slot da usare per costruire la hotbar.")]
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Selection")]
    [Tooltip("Indice iniziale dello slot selezionato (0-based).")]
    [SerializeField] private int selectedIndex;

    [Header("Visual")]
    [Tooltip("GameObject che funge da cornice di selezione. Verrà spostato sopra lo slot attivo.")]
    [SerializeField] private RectTransform selectionFrame;

    private InventorySlotUI[] slotViews;
    private int hotbarSize;

    public int SelectedIndex => selectedIndex;

    /// <summary>Restituisce lo stack attualmente selezionato nella hotbar (può essere null/vuoto).</summary>
    public ItemStack SelectedStack
    {
        get
        {
            if (inventory == null || inventory.Hotbar == null) return null;
            return inventory.Hotbar.GetSlot(selectedIndex);
        }
    }

    // ── Eventi ──────────────────────────────────────────────

    /// <summary>
    /// Fired quando cambia l'item attivo della hotbar (cambio slot o contenuto dello slot).
    /// Il parametro è lo stack attivo corrente (può essere null se vuoto).
    /// </summary>
    public event Action<ItemStack> OnActiveItemChanged;

    private InventoryToggleController toggleController;

    /// <summary>Cache dell'ultimo stack notificato, per evitare notifiche duplicate.</summary>
    private ItemStack lastNotifiedStack;

    private void Start()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();

        if (interactionController == null)
            interactionController = FindFirstObjectByType<InventoryInteractionController>();

        if (hotbarSectionUI == null)
            hotbarSectionUI = GetComponent<InventorySectionUI>();

        toggleController = FindFirstObjectByType<InventoryToggleController>();

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        EnsureLayoutComponent();
        ConfigureSectionUI();

        hotbarSize = inventory != null && inventory.Hotbar != null ? inventory.Hotbar.Size : 9;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, hotbarSize - 1));

        // Iscriviti ai cambiamenti dello slot della hotbar
        if (inventory != null && inventory.Hotbar != null)
            inventory.Hotbar.OnSlotChanged += OnHotbarSlotChanged;
    }

    private void OnDestroy()
    {
        if (inventory != null && inventory.Hotbar != null)
            inventory.Hotbar.OnSlotChanged -= OnHotbarSlotChanged;
    }

    private void OnHotbarSlotChanged(int slotIndex)
    {
        if (slotIndex == selectedIndex)
            NotifyActiveItemChanged();
    }

    private void LateUpdate()
    {
        // Aggiorna la cache slot se non ancora pronta
        if (slotViews == null || slotViews.Length == 0)
        {
            CacheSlotViews();
            if (slotViews != null && slotViews.Length > 0)
                ApplySelection();
        }

        // Blocca interazione mouse quando l'inventario è chiuso
        bool inventoryOpen = toggleController != null && toggleController.IsOpen;
        canvasGroup.interactable = inventoryOpen;
        canvasGroup.blocksRaycasts = inventoryOpen;

        HandleNumberKeys();
        HandleScrollWheel();
    }

    private void HandleNumberKeys()
    {
        for (int i = 0; i < Mathf.Min(hotbarSize, 9); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SetSelected(i);
                return;
            }
        }
    }

    private void HandleScrollWheel()
    {
        if (hotbarSize <= 0) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        int dir = scroll > 0f ? -1 : 1;
        int next = (selectedIndex + dir + hotbarSize) % hotbarSize;
        SetSelected(next);
    }

    public void SetSelected(int index)
    {
        if (index < 0 || index >= hotbarSize) return;

        selectedIndex = index;
        ApplySelection();
        NotifyActiveItemChanged();
    }

    /// <summary>Notifica i listener solo se l'item attivo è effettivamente cambiato.</summary>
    private void NotifyActiveItemChanged()
    {
        var current = SelectedStack;

        // Evita notifiche duplicate se lo stack non è cambiato
        if (ReferenceEquals(current, lastNotifiedStack)) return;

        lastNotifiedStack = current;
        OnActiveItemChanged?.Invoke(current);
    }

    private void ApplySelection()
    {
        if (slotViews == null) return;

        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null)
                slotViews[i].SetHighlight(i == selectedIndex);
        }

        // Sposta la cornice di selezione
        if (selectionFrame != null &&
            selectedIndex < slotViews.Length && slotViews[selectedIndex] != null)
        {
            selectionFrame.SetParent(slotViews[selectedIndex].transform, false);
            selectionFrame.anchoredPosition = Vector2.zero;
            selectionFrame.SetAsLastSibling();
        }
    }

    private void CacheSlotViews()
    {
        if (hotbarSectionUI == null) return;

        slotViews = hotbarSectionUI.GetSlotViews();
    }

    private void ConfigureSectionUI()
    {
        if (hotbarSectionUI == null || slotPrefab == null)
        {
            Debug.LogWarning("[HotbarHUD] Mancano riferimenti: InventorySectionUI o slotPrefab.", this);
            return;
        }

        hotbarSectionUI.Configure(inventory, interactionController, "Hotbar", transform, slotPrefab);
    }

    private void EnsureLayoutComponent()
    {
        if (GetComponent<LayoutGroup>() != null) return;

        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }
}
