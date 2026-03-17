using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu interazione chest: mostra due inventari 5x6 affiancati.
/// A sinistra inventario player, a destra inventario chest.
/// </summary>
public class ChestInteractionMenuUI : PlaceableInteractionMenuBase
{
    [Header("References")]
    [SerializeField] private InventoryInteractionController interactionController;
    [SerializeField] private InventoryModel playerInventory;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private string playerSectionName = "Main";

    [Header("Matrices Root")]
    [Tooltip("Root della matrice player (sinistra). Deve essere assegnato in scena/prefab.")]
    [SerializeField] private RectTransform playerSlotsRoot;
    [Tooltip("Root della matrice chest (destra). Deve essere assegnato in scena/prefab.")]
    [SerializeField] private RectTransform chestSlotsRoot;

    [Header("Layout")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 6;
    [SerializeField] private Vector2 cellSize = new Vector2(56f, 56f);
    [SerializeField] private Vector2 spacing = new Vector2(6f, 6f);

    [Header("Behavior")]
    [Tooltip("Permette di chiudere la chest premendo di nuovo il tasto interazione.")]
    [SerializeField] private bool closeWithInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Se true, il menu viene sempre agganciato a un Canvas UI e mostrato in posizione fissa sullo schermo.")]
    [SerializeField] private bool forceScreenSpace = true;
    [Tooltip("Canvas UI principale da usare come parent del menu (consigliato: HUD root canvas).")]
    [SerializeField] private Canvas screenSpaceRootCanvas;
    [SerializeField] private Vector2 anchoredScreenPosition = Vector2.zero;

    private readonly List<InventorySlotUI> playerSlotViews = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> chestSlotViews = new List<InventorySlotUI>();
    private InventorySection activePlayerSection;
    private InventorySection activeChestSection;
    private RectTransform selfRect;
    private int openedFrame = -1;

    private void Awake()
    {
        selfRect = transform as RectTransform;
        ResolveReferences();
        EnsureMatricesRoots();
        ValidateMatricesRoots();
        SetupSlotsRootGrid(playerSlotsRoot);
        SetupSlotsRootGrid(chestSlotsRoot);
    }

    private void OnDestroy()
    {
        UnsubscribeSections();
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
        if (placedObject == null)
        {
            Hide();
            return;
        }

        ResolveReferences();

        var storage = placedObject.GetComponent<ChestInventoryStorage>();
        if (storage == null)
            storage = placedObject.gameObject.AddComponent<ChestInventoryStorage>();

        var chestSection = storage.Section;
        var playerSection = ResolvePlayerSection();

        EnsureMatricesRoots();

        BindChestSection(chestSection);
        BindPlayerSection(playerSection);

        EnsureScreenSpacePresentation();

        base.Show(placedObject);
        openedFrame = Time.frameCount;
    }

    public override void Hide()
    {
        interactionController?.TryReturnCursorToInventory();
        base.Hide();
    }

    private void ResolveReferences()
    {
        if (interactionController == null)
            interactionController = FindFirstObjectByType<InventoryInteractionController>();

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<InventoryModel>();
    }

    private void ValidateMatricesRoots()
    {
        if (playerSlotsRoot == null || chestSlotsRoot == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] Roots mancanti anche dopo il setup runtime.", this);
        }
    }

    private void EnsureMatricesRoots()
    {
        if (playerSlotsRoot != null && chestSlotsRoot != null)
            return;

        if (selfRect == null)
            selfRect = transform as RectTransform;

        if (selfRect == null)
            return;

        selfRect.sizeDelta = new Vector2(820f, 520f);

        RectTransform container = transform.Find("SlotsContainer") as RectTransform;
        if (container == null)
        {
            var containerGO = new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGO.transform.SetParent(transform, false);
            container = containerGO.GetComponent<RectTransform>();
            container.anchorMin = new Vector2(0.5f, 0.5f);
            container.anchorMax = new Vector2(0.5f, 0.5f);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = Vector2.zero;
            container.sizeDelta = new Vector2(760f, 420f);

            var layout = containerGO.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        if (playerSlotsRoot == null)
            playerSlotsRoot = CreateMatrixRoot("PlayerSlotsRoot", container);

        if (chestSlotsRoot == null)
            chestSlotsRoot = CreateMatrixRoot("ChestSlotsRoot", container);
    }

    private RectTransform CreateMatrixRoot(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 360f);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 360f;
        le.preferredHeight = 360f;

        return rt;
    }

    private InventorySection ResolvePlayerSection()
    {
        if (playerInventory == null) return null;

        if (!string.IsNullOrWhiteSpace(playerSectionName))
        {
            var namedSection = playerInventory.GetSection(playerSectionName);
            if (namedSection != null) return namedSection;
        }

        if (playerInventory.Main != null) return playerInventory.Main;
        if (playerInventory.Hotbar != null) return playerInventory.Hotbar;
        return null;
    }

    private void SetupSlotsRootGrid(RectTransform slotsRoot)
    {
        if (slotsRoot == null) return;

        var grid = slotsRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = slotsRoot.gameObject.AddComponent<GridLayoutGroup>();

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = slotsRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = slotsRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void BindPlayerSection(InventorySection section)
    {
        if (section == null || playerSlotsRoot == null) return;

        if (slotPrefab == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] Slot prefab non assegnato.", this);
            return;
        }

        if (interactionController == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] InventoryInteractionController non trovato.", this);
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] InventoryModel player non trovato.", this);
            return;
        }

        if (activePlayerSection != null)
            activePlayerSection.OnSlotChanged -= HandlePlayerSlotChanged;

        activePlayerSection = section;
        activePlayerSection.OnSlotChanged += HandlePlayerSlotChanged;

        RebuildSlotViews(playerInventory, activePlayerSection, playerSlotsRoot, playerSlotViews, "PlayerSlot");
    }

    private void BindChestSection(InventorySection section)
    {
        if (section == null || chestSlotsRoot == null) return;

        if (slotPrefab == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] Slot prefab non assegnato.", this);
            return;
        }

        if (interactionController == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] InventoryInteractionController non trovato.", this);
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] InventoryModel player non trovato.", this);
            return;
        }

        if (activeChestSection != null)
            activeChestSection.OnSlotChanged -= HandleChestSlotChanged;

        activeChestSection = section;
        activeChestSection.OnSlotChanged += HandleChestSlotChanged;

        RebuildSlotViews(null, activeChestSection, chestSlotsRoot, chestSlotViews, "ChestSlot");
    }

    private void RebuildSlotViews(InventoryModel sourceModel, InventorySection section, RectTransform root, List<InventorySlotUI> targetList, string namePrefix)
    {
        if (root == null) return;

        ClearSlotViews(targetList);

        int totalCount = rows * columns;
        for (int i = 0; i < totalCount; i++)
        {
            var slot = Instantiate(slotPrefab, root);
            slot.name = namePrefix + "_" + i;

            if (i < section.Size)
            {
                slot.Bind(sourceModel, interactionController, section, i);
            }
            else
            {
                // Slot extra solo visuale per completare la griglia 5x6.
                slot.Bind(null, null, null, -1);
            }

            targetList.Add(slot);
        }

        RefreshSlotList(targetList);
    }

    private void HandlePlayerSlotChanged(int index)
    {
        if (index < 0 || index >= playerSlotViews.Count) return;
        playerSlotViews[index].Refresh();
    }

    private void HandleChestSlotChanged(int index)
    {
        if (index < 0 || index >= chestSlotViews.Count) return;
        chestSlotViews[index].Refresh();
    }

    private void RefreshSlotList(List<InventorySlotUI> slots)
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].Refresh();
    }

    private void UnsubscribeSections()
    {
        if (activePlayerSection != null)
            activePlayerSection.OnSlotChanged -= HandlePlayerSlotChanged;

        if (activeChestSection != null)
            activeChestSection.OnSlotChanged -= HandleChestSlotChanged;
    }

    private void ClearSlotViews(List<InventorySlotUI> slots)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == null) continue;
            Destroy(slots[i].gameObject);
        }

        slots.Clear();
    }

    private void EnsureScreenSpacePresentation()
    {
        if (!forceScreenSpace) return;

        var canvas = FindOrCreateScreenSpaceCanvas();

        if (transform.parent != canvas.transform)
            transform.SetParent(canvas.transform, false);

        if (selfRect == null)
            selfRect = transform as RectTransform;

        if (selfRect != null)
        {
            selfRect.anchorMin = new Vector2(0.5f, 0.5f);
            selfRect.anchorMax = new Vector2(0.5f, 0.5f);
            selfRect.pivot = new Vector2(0.5f, 0.5f);
            selfRect.anchoredPosition = anchoredScreenPosition;
        }

        var rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = gameObject.AddComponent<Canvas>();

        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 550;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    private Canvas FindOrCreateScreenSpaceCanvas()
    {
        if (screenSpaceRootCanvas != null)
        {
            if (screenSpaceRootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                screenSpaceRootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                return screenSpaceRootCanvas;
            }

            Debug.LogWarning("[ChestInteractionMenuUI] Il canvas assegnato non e screen-space, uso fallback automatico.", this);
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas cameraCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (!c.isRootCanvas) continue; // Evita canvas nidificati (es. tooltip, popup secondari).

            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;

            if (cameraCanvas == null && c.renderMode == RenderMode.ScreenSpaceCamera)
                cameraCanvas = c;
        }

        if (cameraCanvas != null)
            return cameraCanvas;

        var canvasGO = new GameObject("InteractionUICanvas_Auto");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        Debug.LogWarning("[ChestInteractionMenuUI] Canvas screen-space non trovato: creato automaticamente.", this);
        screenSpaceRootCanvas = canvas;
        return canvas;
    }
}
