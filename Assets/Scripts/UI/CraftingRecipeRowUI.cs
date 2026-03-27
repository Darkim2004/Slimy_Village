using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CraftingRecipeRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text craftableCountText;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectedHighlight;

    [Header("Output")]
    [SerializeField] private CraftingItemIconAmountUI outputSlot;

    [Header("Ingredients")]
    [SerializeField] private RectTransform ingredientsRoot;
    [SerializeField] private CraftingItemIconAmountUI ingredientSlotPrefab;

    private CraftingStationMenuUI owner;
    private int recipeIndex;
    private readonly List<CraftingItemIconAmountUI> ingredientSlots = new List<CraftingItemIconAmountUI>();

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClick);
    }

    public void Bind(CraftingStationMenuUI owner, int recipeIndex)
    {
        this.owner = owner;
        this.recipeIndex = recipeIndex;
    }

    public void Refresh(CraftingRecipeDefinition recipe, int maxCraftable, bool isSelected)
    {
        if (recipe == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = recipe.DisplayName;

        if (craftableCountText != null)
            craftableCountText.text = maxCraftable > 0 ? $"x{maxCraftable}" : "-";

        if (selectedHighlight != null)
            selectedHighlight.SetActive(isSelected);

        if (selectButton != null)
            selectButton.interactable = maxCraftable > 0;

        RefreshOutput(recipe);
        RefreshIngredients(recipe);
    }

    private void RefreshOutput(CraftingRecipeDefinition recipe)
    {
        if (outputSlot == null) return;
        outputSlot.Refresh(recipe.outputItem, recipe.outputAmount);
    }

    private void RefreshIngredients(CraftingRecipeDefinition recipe)
    {
        if (ingredientsRoot == null || ingredientSlotPrefab == null)
            return;

        if (recipe == null || recipe.ingredients == null)
        {
            for (int i = 0; i < ingredientSlots.Count; i++)
            {
                ingredientSlots[i].Refresh(null, 0);
                ingredientSlots[i].gameObject.SetActive(false);
            }

            return;
        }

        int validCount = CountValidIngredients(recipe);
        EnsureIngredientSlots(validCount);

        int slotIndex = 0;
        var ingredients = recipe.ingredients;
        for (int i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.item == null || ingredient.amount <= 0) continue;

            var slot = ingredientSlots[slotIndex++];
            slot.gameObject.SetActive(true);
            slot.Refresh(ingredient.item, ingredient.amount);
        }

        for (int i = slotIndex; i < ingredientSlots.Count; i++)
        {
            ingredientSlots[i].Refresh(null, 0);
            ingredientSlots[i].gameObject.SetActive(false);
        }
    }

    private int CountValidIngredients(CraftingRecipeDefinition recipe)
    {
        if (recipe == null || recipe.ingredients == null) return 0;

        int count = 0;
        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            var ingredient = recipe.ingredients[i];
            if (ingredient.item != null && ingredient.amount > 0)
                count++;
        }

        return count;
    }

    private void EnsureIngredientSlots(int requiredCount)
    {
        while (ingredientSlots.Count < requiredCount)
        {
            var slot = Instantiate(ingredientSlotPrefab, ingredientsRoot);
            ingredientSlots.Add(slot);
        }

        for (int i = 0; i < ingredientSlots.Count; i++)
            ingredientSlots[i].gameObject.SetActive(i < requiredCount);
    }

    private void HandleClick()
    {
        owner?.SelectRecipeByIndex(recipeIndex);
    }
}
