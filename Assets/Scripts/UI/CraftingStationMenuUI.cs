using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftingStationMenuUI : PlaceableInteractionMenuBase
{
    [Header("References")]
    [SerializeField] private InventoryModel playerInventory;
    [SerializeField] private PlayerTopDown playerTopDown;
    [SerializeField] private RectTransform recipesRoot;
    [SerializeField] private CraftingRecipeRowUI recipeRowPrefab;

    [Header("Behavior")]
    [SerializeField] private bool closeWithInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool attachToSceneCanvasOnShow = true;

    [Header("State")]
    [SerializeField] private int selectedRecipeIndex = -1;

    private readonly List<CraftingRecipeDefinition> activeRecipes = new List<CraftingRecipeDefinition>();
    private readonly Dictionary<CraftingRecipeDefinition, int> craftableByRecipe = new Dictionary<CraftingRecipeDefinition, int>();
    private readonly List<CraftingRecipeRowUI> recipeRows = new List<CraftingRecipeRowUI>();

    private int openedFrame = -1;
    private Canvas runtimeSceneCanvas;

    public event Action OnRecipesChanged;
    public event Action<CraftAttemptResult, CraftingRecipeDefinition> OnCraftExecuted;

    public IReadOnlyList<CraftingRecipeDefinition> ActiveRecipes => activeRecipes;
    public int SelectedRecipeIndex => selectedRecipeIndex;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        UnsubscribeInventoryEvents();
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
        ResolveReferences();
        EnsureSceneCanvasParent();

        base.Show(placedObject);
        openedFrame = Time.frameCount;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(true);

        SubscribeInventoryEvents();
        ResolveRecipesFromContext(placedObject);
        RefreshCraftableStates();
    }

    public override void Hide()
    {
        if (playerTopDown != null)
            playerTopDown.SetInputLocked(false);

        UnsubscribeInventoryEvents();
        base.Hide();
    }

    public void SelectRecipeByIndex(int index)
    {
        if (index < 0 || index >= activeRecipes.Count)
        {
            selectedRecipeIndex = -1;
            OnRecipesChanged?.Invoke();
            return;
        }

        selectedRecipeIndex = index;
        RefreshRecipeRows();
        OnRecipesChanged?.Invoke();
    }

    public int GetMaxCraftable(int index)
    {
        if (index < 0 || index >= activeRecipes.Count) return 0;

        var recipe = activeRecipes[index];
        return recipe != null && craftableByRecipe.TryGetValue(recipe, out int amount)
            ? amount
            : 0;
    }

    public bool CraftSelectedX1() => CraftSelectedAmount(1);
    public bool CraftSelectedX5() => CraftSelectedAmount(5);
    public bool CraftSelectedX10() => CraftSelectedAmount(10);

    public bool CraftSelectedMax()
    {
        if (selectedRecipeIndex < 0 || selectedRecipeIndex >= activeRecipes.Count)
            return false;

        int max = GetMaxCraftable(selectedRecipeIndex);
        if (max <= 0) return false;

        return CraftSelectedAmount(max);
    }

    public bool CraftSelectedAmount(int amount)
    {
        if (playerInventory == null || amount <= 0) return false;
        if (selectedRecipeIndex < 0 || selectedRecipeIndex >= activeRecipes.Count) return false;

        var recipe = activeRecipes[selectedRecipeIndex];
        var result = CraftingService.TryCraft(playerInventory, recipe, amount);

        RefreshCraftableStates();
        OnCraftExecuted?.Invoke(result, recipe);
        return result.Success;
    }

    public bool CraftRecipeByIndex(int index, int amount)
    {
        SelectRecipeByIndex(index);
        return CraftSelectedAmount(amount);
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<InventoryModel>();

        if (playerTopDown == null)
            playerTopDown = FindFirstObjectByType<PlayerTopDown>();
    }

    private void ResolveRecipesFromContext(PlacedObject placedObject)
    {
        activeRecipes.Clear();
        craftableByRecipe.Clear();

        if (placedObject == null)
        {
            selectedRecipeIndex = -1;
            ClearRecipeRows();
            OnRecipesChanged?.Invoke();
            return;
        }

        var station = placedObject.GetComponent<CraftingStationRecipes>();
        if (station == null || !station.HasRecipes)
        {
            selectedRecipeIndex = -1;
            ClearRecipeRows();
            OnRecipesChanged?.Invoke();
            return;
        }

        var recipes = station.Recipes;
        for (int i = 0; i < recipes.Count; i++)
        {
            if (recipes[i] != null)
                activeRecipes.Add(recipes[i]);
        }

        if (activeRecipes.Count == 0)
            selectedRecipeIndex = -1;
        else if (selectedRecipeIndex < 0 || selectedRecipeIndex >= activeRecipes.Count)
            selectedRecipeIndex = 0;

        RebuildRecipeRows();
        OnRecipesChanged?.Invoke();
    }

    private void RefreshCraftableStates()
    {
        craftableByRecipe.Clear();
        if (playerInventory == null)
        {
            OnRecipesChanged?.Invoke();
            return;
        }

        for (int i = 0; i < activeRecipes.Count; i++)
        {
            var recipe = activeRecipes[i];
            if (recipe == null) continue;
            craftableByRecipe[recipe] = CraftingService.GetMaxCraftCount(playerInventory, recipe);
        }

        RefreshRecipeRows();
        OnRecipesChanged?.Invoke();
    }

    private void RebuildRecipeRows()
    {
        if (recipesRoot == null || recipeRowPrefab == null)
            return;

        ClearRecipeRows();

        for (int i = 0; i < activeRecipes.Count; i++)
        {
            var row = Instantiate(recipeRowPrefab, recipesRoot, false);
            row.Bind(this, i);
            recipeRows.Add(row);
        }

        RefreshRecipeRows();
    }

    private void ClearRecipeRows()
    {
        for (int i = 0; i < recipeRows.Count; i++)
        {
            if (recipeRows[i] != null)
                Destroy(recipeRows[i].gameObject);
        }

        recipeRows.Clear();
    }

    private void RefreshRecipeRows()
    {
        int rowCount = recipeRows.Count;
        if (rowCount == 0) return;

        for (int i = 0; i < rowCount; i++)
        {
            if (i >= activeRecipes.Count || recipeRows[i] == null)
                continue;

            var recipe = activeRecipes[i];
            int maxCraftable = 0;
            if (recipe != null && craftableByRecipe.TryGetValue(recipe, out int value))
                maxCraftable = value;

            recipeRows[i].Refresh(recipe, maxCraftable, i == selectedRecipeIndex);
        }
    }

    private void SubscribeInventoryEvents()
    {
        if (playerInventory == null) return;

        playerInventory.OnSlotChanged -= HandleInventorySlotChanged;
        playerInventory.OnBulkChanged -= HandleInventoryBulkChanged;
        playerInventory.OnSlotChanged += HandleInventorySlotChanged;
        playerInventory.OnBulkChanged += HandleInventoryBulkChanged;
    }

    private void UnsubscribeInventoryEvents()
    {
        if (playerInventory == null) return;

        playerInventory.OnSlotChanged -= HandleInventorySlotChanged;
        playerInventory.OnBulkChanged -= HandleInventoryBulkChanged;
    }

    private void HandleInventorySlotChanged(InventorySection _, int __)
    {
        RefreshCraftableStates();
    }

    private void HandleInventoryBulkChanged()
    {
        RefreshCraftableStates();
    }

    private void EnsureSceneCanvasParent()
    {
        if (!attachToSceneCanvasOnShow) return;

        var canvas = FindSceneScreenSpaceCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[CraftingStationMenuUI] Nessun Canvas screen-space root trovato in scena.", this);
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
            if (c == null || !c.isRootCanvas) continue;

            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;

            if (cameraCanvas == null && c.renderMode == RenderMode.ScreenSpaceCamera)
                cameraCanvas = c;
        }

        return cameraCanvas;
    }
}
