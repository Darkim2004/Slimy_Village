using System;
using UnityEngine;

[Serializable]
public struct CraftingIngredient
{
    public ItemDefinition item;
    [Min(1)] public int amount;

    public string ItemId => item != null ? item.id : string.Empty;
}

[CreateAssetMenu(menuName = "Game/Crafting/Recipe Definition")]
public class CraftingRecipeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Id univoco della ricetta (facoltativo ma consigliato).")]
    public string recipeId;

    [Tooltip("Nome visualizzato in UI. Se vuoto usa il nome dell'output.")]
    public string displayName;

    [TextArea(1, 3)]
    public string description;

    [Header("Input")]
    public CraftingIngredient[] ingredients;

    [Header("Output")]
    public ItemDefinition outputItem;
    [Min(1)] public int outputAmount = 1;

    [Header("Visual")]
    [Tooltip("Se nullo, la UI puo mostrare l'icona dell'output.")]
    public Sprite icon;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;
            return outputItem != null && !string.IsNullOrWhiteSpace(outputItem.displayName)
                ? outputItem.displayName
                : name;
        }
    }

    public bool IsValidRecipe()
    {
        if (outputItem == null || outputAmount <= 0) return false;
        if (ingredients == null || ingredients.Length == 0) return false;

        for (int i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.item == null || ingredient.amount <= 0)
                return false;
        }

        return true;
    }

    private void OnValidate()
    {
        if (outputAmount < 1) outputAmount = 1;
    }
}
