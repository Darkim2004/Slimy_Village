using UnityEngine;

/// <summary>
/// Controller principale del sistema di piazzamento blocchi.
/// Gestisce l'input del mouse, la validazione tramite PlacementGrid,
/// la preview ghost e l'istanziazione degli oggetti piazzati.
/// 
/// Ascolta HotbarEffectManager.OnBuildModeChanged per entrare/uscire
/// dalla modalità costruzione.
/// </summary>
public class BuildingSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlacementGrid per validare le posizioni.")]
    [SerializeField] private PlacementGrid placementGrid;

    [Tooltip("PlacementGhost per la preview.")]
    [SerializeField] private PlacementGhost placementGhost;

    [Tooltip("HotbarEffectManager per sapere quando si è in build mode.")]
    [SerializeField] private HotbarEffectManager hotbarEffects;

    [Tooltip("InventoryModel per rimuovere gli item piazzati.")]
    [SerializeField] private InventoryModel inventory;

    [Tooltip("HotbarHUD per sapere quale slot è selezionato.")]
    [SerializeField] private HotbarHUD hotbarHUD;

    [Tooltip("Camera principale (usa Camera.main se null).")]
    [SerializeField] private Camera mainCamera;

    [Header("Placed Objects")]
    [Tooltip("Parent per gli oggetti piazzati. Se null, usa la root della scena.")]
    [SerializeField] private Transform placedObjectsParent;

    // Stato corrente
    private bool _buildModeActive;
    private PlaceableDefinition _currentPlaceable;
    private Vector2Int _currentCell;
    private bool _currentCellValid;

    private void Start()
    {
        // Auto-find references se non assegnate
        if (hotbarEffects == null)
            hotbarEffects = FindFirstObjectByType<HotbarEffectManager>();

        if (placementGrid == null)
            placementGrid = FindFirstObjectByType<PlacementGrid>();

        if (placementGhost == null)
            placementGhost = FindFirstObjectByType<PlacementGhost>();

        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();

        if (hotbarHUD == null)
            hotbarHUD = FindFirstObjectByType<HotbarHUD>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Iscriviti agli eventi
        if (hotbarEffects != null)
        {
            hotbarEffects.OnBuildModeChanged += OnBuildModeChanged;
            hotbarEffects.OnCategoryChanged += OnCategoryChanged;
        }

        // Controlla stato iniziale
        if (hotbarEffects != null && hotbarEffects.IsBuildModeRequested)
            EnterBuildMode();
    }

    private void OnDestroy()
    {
        if (hotbarEffects != null)
        {
            hotbarEffects.OnBuildModeChanged -= OnBuildModeChanged;
            hotbarEffects.OnCategoryChanged -= OnCategoryChanged;
        }
    }

    private void Update()
    {
        if (!_buildModeActive) return;

        UpdateGhostPosition();

        // Piazzamento con tasto destro del mouse
        if (Input.GetMouseButtonDown(1))
            TryPlace();
    }

    // ══════════════════════════════════════════════════════════
    //  Build Mode Enter / Exit
    // ══════════════════════════════════════════════════════════

    private void OnBuildModeChanged(bool active)
    {
        if (active)
            EnterBuildMode();
        else
            ExitBuildMode();
    }

    private void OnCategoryChanged(ItemCategory category)
    {
        // Aggiorna la definizione piazzabile quando l'item attivo cambia
        if (_buildModeActive)
            RefreshPlaceableDefinition();
    }

    private void EnterBuildMode()
    {
        _buildModeActive = true;
        RefreshPlaceableDefinition();
    }

    private void ExitBuildMode()
    {
        _buildModeActive = false;
        _currentPlaceable = null;

        if (placementGhost != null)
            placementGhost.Hide();
    }

    private void RefreshPlaceableDefinition()
    {
        var def = hotbarEffects != null ? hotbarEffects.ActiveItemDef : null;

        if (def != null && def.IsBuilding && def.placeableData != null)
        {
            _currentPlaceable = def.placeableData;

            if (placementGhost != null)
                placementGhost.Show(_currentPlaceable);
        }
        else
        {
            _currentPlaceable = null;

            if (placementGhost != null)
                placementGhost.Hide();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Ghost Update
    // ══════════════════════════════════════════════════════════

    private void UpdateGhostPosition()
    {
        if (_currentPlaceable == null || placementGrid == null || placementGhost == null)
            return;

        // Posizione del mouse in world space
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // Calcola la cella pivot (dove il cursore punta)
        Vector2Int mouseCell = placementGrid.WorldToCell(mouseWorld);

        // L'origine dell'area occupata = mouseCell - pivotOffset
        _currentCell = mouseCell - _currentPlaceable.pivotOffset;

        // Valida il piazzamento
        _currentCellValid = placementGrid.CanPlace(_currentCell, _currentPlaceable.size);

        // Posizione del ghost: centro dell'area + spawnOffset
        Vector3 ghostPos = placementGrid.GetAreaWorldCenter(_currentCell, _currentPlaceable.size);
        ghostPos += _currentPlaceable.spawnOffset;

        placementGhost.UpdatePosition(ghostPos, _currentCellValid);
    }

    // ══════════════════════════════════════════════════════════
    //  Placement
    // ══════════════════════════════════════════════════════════

    private void TryPlace()
    {
        if (_currentPlaceable == null) return;
        if (!_currentCellValid) return;

        // Controlla di avere l'item nell'inventario
        var activeStack = hotbarHUD != null ? hotbarHUD.SelectedStack : null;
        if (activeStack == null || activeStack.IsEmpty) return;

        var def = activeStack.def;
        if (def == null || def.placeableData != _currentPlaceable) return;

        // Piazza l'oggetto
        PlaceObject(_currentPlaceable, _currentCell);

        // Rimuovi 1 item dall'inventario (dallo slot attivo della hotbar)
        ConsumeOneFromHotbar();
    }

    private void PlaceObject(PlaceableDefinition placeable, Vector2Int origin)
    {
        if (placeable.placedPrefab == null)
        {
            Debug.LogWarning("[BuildingSystem] PlaceableDefinition non ha un prefab assegnato!");
            return;
        }

        // Calcola la posizione di spawn
        Vector3 spawnPos = placementGrid.GetAreaWorldCenter(origin, placeable.size);
        spawnPos += placeable.spawnOffset;

        // Istanzia il prefab
        Transform parent = placedObjectsParent != null ? placedObjectsParent : null;
        GameObject go = Instantiate(placeable.placedPrefab, spawnPos, Quaternion.identity, parent);

        // Aggiungi/configura PlacedObject
        PlacedObject placedObj = go.GetComponent<PlacedObject>();
        if (placedObj == null)
            placedObj = go.AddComponent<PlacedObject>();

        placedObj.Initialize(placeable, origin, placeable.size);

        // Aggiungi YSort se non presente
        if (go.GetComponent<YSort>() == null && go.GetComponentInChildren<YSort>() == null)
            go.AddComponent<YSort>();

        // Occupa le celle sulla griglia
        placementGrid.OccupyCells(origin, placeable.size);
    }

    private void ConsumeOneFromHotbar()
    {
        if (hotbarHUD == null || inventory == null) return;

        int slotIndex = hotbarHUD.SelectedIndex;
        var section = inventory.Hotbar;
        if (section == null) return;

        var stack = section.GetSlot(slotIndex);
        if (stack == null || stack.IsEmpty) return;

        stack.amount -= 1;
        if (stack.amount <= 0)
            section.SetSlot(slotIndex, null);
        else
            section.SetSlot(slotIndex, stack); // notifica la UI
    }

    // ══════════════════════════════════════════════════════════
    //  Public API
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// True se il sistema è attualmente in modalità costruzione.
    /// </summary>
    public bool IsBuildModeActive => _buildModeActive;

    /// <summary>
    /// La definizione piazzabile corrente (null se non in build mode).
    /// </summary>
    public PlaceableDefinition CurrentPlaceable => _currentPlaceable;
}
