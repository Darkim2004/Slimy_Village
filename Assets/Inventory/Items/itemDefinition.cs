using UnityEngine;

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

    [Header("Visuals")]
    public Sprite icon;

    [Header("Stacking")]
    [Tooltip("Se false → maxStack viene forzato a 1 e NBT è permesso.")]
    public bool isStackable = true;

    [Min(1)]
    [Tooltip("Quantità massima per slot (ignorato se isStackable = false).")]
    public int maxStack = 64;

    /// <summary>Stack effettivo: 1 se non stackabile, altrimenti maxStack.</summary>
    public int EffectiveMaxStack => isStackable ? Mathf.Max(1, maxStack) : 1;

    private void OnValidate()
    {
        if (!isStackable) maxStack = 1;
        if (maxStack < 1) maxStack = 1;
    }
}
