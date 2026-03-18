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

    [Header("Player Lock")]
    [SerializeField] private PlayerTopDown playerTopDown;

    [Header("Matrices Root")]
    [Tooltip("Root della matrice player (sinistra). Deve essere assegnato in scena/prefab.")]
    [SerializeField] private RectTransform playerSlotsRoot;
    [Tooltip("Root della matrice chest (destra). Deve essere assegnato in scena/prefab.")]
    [SerializeField] private RectTransform chestSlotsRoot;
    [Tooltip("Root della matrice hotbar player. Deve essere assegnato in scena/prefab.")]
    [SerializeField] private RectTransform playerHotbarSlotsRoot;

    [Header("Layout")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 6;
    [SerializeField] private Vector2 cellSize = new Vector2(56f, 56f);
    [SerializeField] private Vector2 spacing = new Vector2(6f, 6f);

    [Header("Container Layout")]
    [Tooltip("Se true, organizza gli slot fianco a fianco. Se false, puoi posizionarli manualmente o usare i parametri sotto.")]
    [SerializeField] private bool useHorizontalLayout = true;
    [SerializeField] private TextAnchor containerChildAlignment = TextAnchor.MiddleCenter;
    [SerializeField] private float containerSpacing = 24f;
    [SerializeField] private int containerPaddingLeft = 0;
    [SerializeField] private int containerPaddingRight = 0;
    [SerializeField] private int containerPaddingTop = 0;
    [SerializeField] private int containerPaddingBottom = 0;

    [Header("Free Positioning (se Horizontal Layout disattivato)")]
    [SerializeField] private Vector2 playerMenuPosition = new Vector2(-250f, 0f);
    [SerializeField] private Vector2 chestMenuPosition = new Vector2(250f, 0f);

    [Header("Behavior")]
    [Tooltip("Permette di chiudere la chest premendo di nuovo il tasto interazione.")]
    [SerializeField] private bool closeWithInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Se true, il menu viene agganciato a un Canvas screen-space gia presente in scena.")]
    [SerializeField] private bool attachToSceneCanvasOnShow = true;

    [Header("Root Stretch")]
    [Tooltip("Configura il root del menu in stretch (fullscreen con bordi) una sola volta in Awake.")]
    [SerializeField] private bool configureRootStretchOnAwake = true;
    [Tooltip("Margini root: x=Left, y=Right, z=Top, w=Bottom.")]
    [SerializeField] private Vector4 rootMargins = new Vector4(24f, 24f, 24f, 24f);

    private readonly List<InventorySlotUI> playerSlotViews = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> chestSlotViews = new List<InventorySlotUI>();
    private readonly List<InventorySlotUI> hotbarSlotViews = new List<InventorySlotUI>();
    private InventorySection activePlayerSection;
    private InventorySection activeChestSection;
    private InventorySection activeHotbarSection;
    private RectTransform selfRect;
    private Canvas runtimeSceneCanvas;
    private int openedFrame = -1;

    private void Awake()
    {
        selfRect = transform as RectTransform;
        ResolveReferences();
        ApplyRootStretchLayoutOnce();
        EnsureMatricesRoots();
        ValidateMatricesRoots();
        SetupSlotsRootGrid(playerSlotsRoot, false);
        SetupSlotsRootGrid(chestSlotsRoot, false);
        SetupSlotsRootGrid(playerHotbarSlotsRoot, true);
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
        BindHotbarSection(playerInventory?.Hotbar);

        EnsureSceneCanvasParent();

        base.Show(placedObject);
        openedFrame = Time.frameCount;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(true);
    }

    public override void Hide()
    {
        if (playerTopDown != null)
            playerTopDown.SetInputLocked(false);

        interactionController?.TryReturnCursorToInventory();
        base.Hide();
    }

    private void ResolveReferences()
    {
        if (interactionController == null)
            interactionController = FindFirstObjectByType<InventoryInteractionController>();

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<InventoryModel>();

        if (playerTopDown == null)
            playerTopDown = FindFirstObjectByType<PlayerTopDown>();
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
        if (selfRect == null)
            selfRect = transform as RectTransform;

        if (selfRect == null)
            return;

        RectTransform container = transform.Find("SlotsContainer") as RectTransform;
        if (container == null)
        {
            var containerGO = new GameObject("SlotsContainer", typeof(RectTransform));
            containerGO.transform.SetParent(transform, false);
            container = containerGO.GetComponent<RectTransform>();
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.pivot = new Vector2(0.5f, 0.5f);
            container.offsetMin = new Vector2(24f, 24f);
            container.offsetMax = new Vector2(-24f, -24f);
        }

        ConfigureContainerLayout(container);

        if (playerSlotsRoot == null)
            playerSlotsRoot = CreateMatrixRoot("PlayerSlotsRoot", container);

        if (chestSlotsRoot == null)
            chestSlotsRoot = CreateMatrixRoot("ChestSlotsRoot", container);
            
        if (playerHotbarSlotsRoot == null)
            playerHotbarSlotsRoot = CreateMatrixRoot("PlayerHotbarSlotsRoot", container);

        ApplyFreePositions();
    }

    private void ApplyFreePositions()
    {
        if (useHorizontalLayout) return;

        if (playerSlotsRoot != null)
            playerSlotsRoot.anchoredPosition = playerMenuPosition;

        if (chestSlotsRoot != null)
            chestSlotsRoot.anchoredPosition = chestMenuPosition;
    }

    private void ConfigureContainerLayout(RectTransform container)
    {
        if (container == null) return;

        var layout = container.GetComponent<HorizontalLayoutGroup>();

        if (useHorizontalLayout)
        {
            if (layout == null)
                layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = Mathf.Max(0f, containerSpacing);
            layout.childAlignment = containerChildAlignment;
            layout.padding = new RectOffset(
                Mathf.Max(0, containerPaddingLeft),
                Mathf.Max(0, containerPaddingRight),
                Mathf.Max(0, containerPaddingTop),
                Mathf.Max(0, containerPaddingBottom));
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            if (layout != null)
            {
                if (Application.isPlaying)
                    Destroy(layout);
                else
                    DestroyImmediate(layout);
            }
        }
    }

    private RectTransform CreateMatrixRoot(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

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

    private void SetupSlotsRootGrid(RectTransform slotsRoot, bool isHotbar)
    {
        if (slotsRoot == null) return;

        var grid = slotsRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            // Se esiste già, l'utente lo ha configurato manualmente dall'Inspector.
            // Rispettiamo le sue impostazioni in modo da poter ridimensionare gli slot graficamente.
            return;
        }

        grid = slotsRoot.gameObject.AddComponent<GridLayoutGroup>();

        if (isHotbar)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
        }
        else
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
        }
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

        RebuildSlotViews(playerInventory, activePlayerSection, playerSlotsRoot, playerSlotViews, "PlayerSlot", true);
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

        RebuildSlotViews(null, activeChestSection, chestSlotsRoot, chestSlotViews, "ChestSlot", true);
    }

    private void BindHotbarSection(InventorySection section)
    {
        if (section == null || playerHotbarSlotsRoot == null) return;

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

        if (activeHotbarSection != null)
            activeHotbarSection.OnSlotChanged -= HandleHotbarSlotChanged;

        activeHotbarSection = section;
        activeHotbarSection.OnSlotChanged += HandleHotbarSlotChanged;

        RebuildSlotViews(playerInventory, activeHotbarSection, playerHotbarSlotsRoot, hotbarSlotViews, "HotbarSlot", false);
    }

    private void RebuildSlotViews(InventoryModel sourceModel, InventorySection section, RectTransform root, List<InventorySlotUI> targetList, string namePrefix, bool useFixedGrid = true)
    {
        if (root == null) return;

        ClearSlotViews(targetList);

        int totalCount = useFixedGrid ? (rows * columns) : section.Size;
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

    private void HandleHotbarSlotChanged(int index)
    {
        if (index < 0 || index >= hotbarSlotViews.Count) return;
        hotbarSlotViews[index].Refresh();
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

        if (activeHotbarSection != null)
            activeHotbarSection.OnSlotChanged -= HandleHotbarSlotChanged;
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

    private void ApplyRootStretchLayoutOnce()
    {
        if (!configureRootStretchOnAwake) return;
        if (selfRect == null)
            selfRect = transform as RectTransform;

        if (selfRect == null)
            return;

        selfRect.anchorMin = Vector2.zero;
        selfRect.anchorMax = Vector2.one;
        selfRect.pivot = new Vector2(0.5f, 0.5f);
        selfRect.offsetMin = new Vector2(rootMargins.x, rootMargins.w);
        selfRect.offsetMax = new Vector2(-rootMargins.y, -rootMargins.z);
    }

    private void EnsureSceneCanvasParent()
    {
        if (!attachToSceneCanvasOnShow) return;

        var canvas = FindSceneScreenSpaceCanvas();
        if (canvas == null)
        {
            Debug.LogError("[ChestInteractionMenuUI] Nessun Canvas screen-space root trovato in scena. Il menu non puo essere agganciato.", this);
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
