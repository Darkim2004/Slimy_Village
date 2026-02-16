using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Debug/Debug Settings", fileName = "GameDebugSettings")]
public class GameDebugSettings : ScriptableObject
{
    [Serializable]
    public class CategoryToggle
    {
        public GameDebugCategory category = GameDebugCategory.General;
        public bool enabled = true;
    }

    [Header("Global")]
    [SerializeField] private bool loggingEnabled = true;

    [Tooltip("Se true, le categorie NON presenti nella lista sono attive. Se false, solo quelle in lista con enabled=true loggano.")]
    [SerializeField] private bool defaultCategoryEnabled = false;

    [Header("Category Toggles")]
    [SerializeField] private List<CategoryToggle> categoryToggles = new();

    public bool LoggingEnabled => loggingEnabled;

    public bool IsCategoryEnabled(GameDebugCategory category)
    {
        for (int i = 0; i < categoryToggles.Count; i++)
        {
            var entry = categoryToggles[i];
            if (entry != null && entry.category == category)
                return entry.enabled;
        }

        return defaultCategoryEnabled;
    }
}
