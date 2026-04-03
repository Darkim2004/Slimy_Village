using UnityEngine;

/// <summary>
/// Categoria funzionale di un item. Determina il comportamento quando
/// l'item è nello slot attivo della hotbar.
/// </summary>
public enum ItemCategory
{
    None,
    Weapon,
    Building,
    Consumable,
    Resource,
    Food
}

/// <summary>
/// Definizione immutabile di un tipo di oggetto.
/// Ogni item nel gioco punta a uno di questi asset.
/// </summary>
[CreateAssetMenu(menuName = "Game/Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Identificativo univoco (slug). Es: 'sword_iron', 'potion_hp'.")]
    public string id;

    [Tooltip("Nome leggibile mostrato in UI.")]
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("Category")]
    [Tooltip("Categoria funzionale dell'item. Influenza il comportamento dello slot attivo della hotbar.")]
    public ItemCategory category = ItemCategory.None;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Weapon")]
    [Tooltip("Danno bonus quando l'item è nello slot attivo della hotbar (solo Weapon).")]
    [Min(0)]
    public int attackDamage;

    [Header("Food")]
    [Tooltip("Punti vita recuperati consumando questo item (solo Food).")]
    [Min(0)]
    public int healAmount;

    [Header("Tool")]
    [Tooltip("True se questo item può colpire nodi risorsa (alberi/rocce).")]
    public bool isHarvestTool;

    [Tooltip("Livello del tool per raccolta risorse (ignorato se isHarvestTool è false).")]
    [Min(0)]
    public int harvestToolLevel = 1;

    [Header("Armor")]
    [Tooltip("True se questo item è un'armatura equipaggiabile nello slot armatura.")]
    public bool isArmor;

    [Min(0)]
    [Tooltip("Riduzione danno quando equipaggiata (punti di danno assorbiti).")]
    public int armorDefense;

    [Header("Building")]
    [Tooltip("Dati di piazzamento (solo per Building). Se null, l'item non è piazzabile.")]
    public PlaceableDefinition placeableData;

    [Header("Stacking")]
    [Tooltip("Se false → maxStack viene forzato a 1 e NBT è permesso.")]
    public bool isStackable = true;

    [Min(1)]
    [Tooltip("Quantità massima per slot (ignorato se isStackable = false).")]
    public int maxStack = 64;

    /// <summary>Stack effettivo: 1 se non stackabile, altrimenti maxStack.</summary>
    public int EffectiveMaxStack => isStackable ? Mathf.Max(1, maxStack) : 1;

    /// <summary>True se l'item è un'arma con danno bonus.</summary>
    public bool IsWeapon => category == ItemCategory.Weapon && attackDamage > 0;

    /// <summary>True se l'item è un tool valido per raccolta/mining.</summary>
    public bool IsHarvestTool => isHarvestTool;

    /// <summary>Livello del harvest tool. Ritorna 0 per non-tool, almeno 1 per harvest tools.</summary>
    public int HarvestToolLevel => isHarvestTool ? Mathf.Max(1, harvestToolLevel) : 0;

    /// <summary>True se l'item è un elemento costruibile.</summary>
    public bool IsBuilding => category == ItemCategory.Building;

    /// <summary>True se l'item è un alimento consumabile per recuperare HP.</summary>
    public bool IsFood => category == ItemCategory.Food && healAmount > 0;

    private void OnValidate()
    {
        if (!isStackable) maxStack = 1;
        if (maxStack < 1) maxStack = 1;
        if (harvestToolLevel < 0) harvestToolLevel = 0;
    }
}
