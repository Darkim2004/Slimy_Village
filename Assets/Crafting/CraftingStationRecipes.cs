using System.Collections.Generic;
using UnityEngine;

public class CraftingStationRecipes : MonoBehaviour
{
    [Header("Station Context")]
    [Tooltip("Tag logico del contesto (es. workbench, campfire, alchemy).")]
    [SerializeField] private string contextId = "workbench";

    [SerializeField] private CraftingRecipeDefinition[] recipes;

    public string ContextId => contextId;
    public IReadOnlyList<CraftingRecipeDefinition> Recipes => recipes;

    public bool HasRecipes => recipes != null && recipes.Length > 0;
}
