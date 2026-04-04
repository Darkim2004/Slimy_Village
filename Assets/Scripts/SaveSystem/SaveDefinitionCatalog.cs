using System.Collections.Generic;
using UnityEngine;

public static class SaveDefinitionCatalog
{
    private static readonly Dictionary<string, ItemDefinition> ItemsById = new Dictionary<string, ItemDefinition>();
    private static readonly Dictionary<string, PlaceableDefinition> PlaceablesById = new Dictionary<string, PlaceableDefinition>();
    private static readonly Dictionary<string, EntityDefinition> EntitiesById = new Dictionary<string, EntityDefinition>();
    private static bool initialized;

    public static void Refresh()
    {
        ItemsById.Clear();
        PlaceablesById.Clear();
        EntitiesById.Clear();

        var items = Resources.FindObjectsOfTypeAll<ItemDefinition>();
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item == null)
                continue;

            string key = GetItemId(item);
            if (string.IsNullOrEmpty(key) || ItemsById.ContainsKey(key))
                continue;

            ItemsById.Add(key, item);

            if (item.placeableData != null)
            {
                string placeableKey = GetPlaceableId(item.placeableData);
                if (!string.IsNullOrEmpty(placeableKey) && !PlaceablesById.ContainsKey(placeableKey))
                    PlaceablesById.Add(placeableKey, item.placeableData);
            }
        }

        var placeables = Resources.FindObjectsOfTypeAll<PlaceableDefinition>();
        for (int i = 0; i < placeables.Length; i++)
        {
            var placeable = placeables[i];
            if (placeable == null)
                continue;

            string key = GetPlaceableId(placeable);
            if (string.IsNullOrEmpty(key) || PlaceablesById.ContainsKey(key))
                continue;

            PlaceablesById.Add(key, placeable);
        }

        var entities = Resources.FindObjectsOfTypeAll<EntityDefinition>();
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (entity == null)
                continue;

            string key = GetEntityId(entity);
            if (string.IsNullOrEmpty(key) || EntitiesById.ContainsKey(key))
                continue;

            EntitiesById.Add(key, entity);
        }

        initialized = true;
    }

    public static bool TryResolveItem(string id, out ItemDefinition item)
    {
        EnsureInitialized();
        return ItemsById.TryGetValue(id, out item);
    }

    public static bool TryResolvePlaceable(string id, out PlaceableDefinition placeable)
    {
        EnsureInitialized();
        return PlaceablesById.TryGetValue(id, out placeable);
    }

    public static bool TryResolveEntityDefinition(string id, out EntityDefinition definition)
    {
        EnsureInitialized();
        return EntitiesById.TryGetValue(id, out definition);
    }

    public static string GetItemId(ItemDefinition item)
    {
        if (item == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(item.id))
            return item.id.Trim();

        return item.name;
    }

    public static string GetPlaceableId(PlaceableDefinition placeable)
    {
        if (placeable == null)
            return string.Empty;

        return placeable.name;
    }

    public static string GetEntityId(EntityDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        return definition.name;
    }

    private static void EnsureInitialized()
    {
        if (!initialized)
            Refresh();
    }
}
