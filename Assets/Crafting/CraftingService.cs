using UnityEngine;

public readonly struct CraftAttemptResult
{
    public readonly bool Success;
    public readonly int CraftedCount;
    public readonly int QueuedOutputAmount;
    public readonly string Reason;

    public CraftAttemptResult(bool success, int craftedCount, int queuedOutputAmount, string reason)
    {
        Success = success;
        CraftedCount = craftedCount;
        QueuedOutputAmount = queuedOutputAmount;
        Reason = reason;
    }
}

public static class CraftingService
{
    public static int GetMaxCraftCount(InventoryModel inventory, CraftingRecipeDefinition recipe)
    {
        if (inventory == null || recipe == null || !recipe.IsValidRecipe())
            return 0;

        int maxByIngredients = int.MaxValue;

        var ingredients = recipe.ingredients;
        for (int i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.item == null || ingredient.amount <= 0) return 0;

            int owned = inventory.CountItem(ingredient.item.id);
            int craftableWithThis = owned / ingredient.amount;
            if (craftableWithThis < maxByIngredients)
                maxByIngredients = craftableWithThis;
        }

        return Mathf.Max(0, maxByIngredients);
    }

    public static bool CanCraft(InventoryModel inventory, CraftingRecipeDefinition recipe, int times = 1)
    {
        if (times <= 0) return false;
        return GetMaxCraftCount(inventory, recipe) >= times;
    }

    public static CraftAttemptResult TryCraft(InventoryModel inventory, CraftingRecipeDefinition recipe, int times)
    {
        if (inventory == null)
            return new CraftAttemptResult(false, 0, 0, "Missing inventory");

        if (recipe == null || !recipe.IsValidRecipe())
            return new CraftAttemptResult(false, 0, 0, "Invalid recipe");

        if (times <= 0)
            return new CraftAttemptResult(false, 0, 0, "Invalid craft amount");

        int maxCraftable = GetMaxCraftCount(inventory, recipe);
        if (maxCraftable <= 0)
            return new CraftAttemptResult(false, 0, 0, "Missing ingredients");

        int craftCount = Mathf.Min(times, maxCraftable);
        if (!ConsumeIngredientsAtomically(inventory, recipe, craftCount))
            return new CraftAttemptResult(false, 0, 0, "Atomic consume failed");

        int outputTotal = recipe.outputAmount * craftCount;
        inventory.TryAdd(recipe.outputItem, outputTotal, out var remainder);

        int queued = 0;
        if (remainder != null && remainder.amount > 0)
        {
            queued = remainder.amount;
            var queue = CraftingOutputQueueRuntime.GetOrCreate(inventory);
            queue?.Enqueue(recipe.outputItem, queued);
        }

        return new CraftAttemptResult(true, craftCount, queued, queued > 0 ? "Output queued" : string.Empty);
    }

    private static bool ConsumeIngredientsAtomically(InventoryModel inventory, CraftingRecipeDefinition recipe, int times)
    {
        if (inventory == null || recipe == null || times <= 0) return false;

        // Prima verifica tutti i requisiti, poi consuma in blocco.
        var ingredients = recipe.ingredients;
        for (int i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            int required = ingredient.amount * times;
            if (!inventory.HasItem(ingredient.item.id, required))
                return false;
        }

        for (int i = 0; i < ingredients.Length; i++)
        {
            var ingredient = ingredients[i];
            int required = ingredient.amount * times;
            int removed = inventory.TryRemove(ingredient.item.id, required);
            if (removed != required)
            {
                Debug.LogError($"[CraftingService] Consume mismatch for '{ingredient.item.id}'. Expected {required}, got {removed}.");
                return false;
            }
        }

        return true;
    }
}
