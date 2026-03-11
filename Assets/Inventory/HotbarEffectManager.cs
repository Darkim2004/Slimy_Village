using System;
using UnityEngine;

/// <summary>
/// Gestisce gli effetti gameplay dello slot attivo della hotbar.
/// Si iscrive a <see cref="HotbarHUD.OnActiveItemChanged"/> e mantiene
/// lo stato corrente (arma equipaggiata, modalità costruzione, ecc.).
/// Espone proprietà e eventi per i sistemi di gioco (combattimento, building).
/// </summary>
public class HotbarEffectManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("HotbarHUD da monitorare. Se null, viene cercato automaticamente.")]
    [SerializeField] private HotbarHUD hotbarHUD;

    // ── Stato corrente ──────────────────────────────────────

    /// <summary>Categoria dell'item attualmente nello slot attivo.</summary>
    public ItemCategory ActiveCategory { get; private set; } = ItemCategory.None;

    /// <summary>Danno bonus dell'arma nello slot attivo (0 se non è un'arma).</summary>
    public int WeaponBonusDamage { get; private set; }

    /// <summary>True se lo slot attivo contiene un item di tipo Building.</summary>
    public bool IsBuildModeRequested { get; private set; }

    /// <summary>La definizione dell'item attivo (null se slot vuoto).</summary>
    public ItemDefinition ActiveItemDef { get; private set; }

    // ── Eventi ──────────────────────────────────────────────

    /// <summary>Fired quando cambia la categoria dell'item attivo.</summary>
    public event Action<ItemCategory> OnCategoryChanged;

    /// <summary>Fired quando si entra/esce dalla modalità costruzione.</summary>
    public event Action<bool> OnBuildModeChanged;

    // ══════════════════════════════════════════════════════════
    //  Init
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        if (hotbarHUD == null)
            hotbarHUD = FindFirstObjectByType<HotbarHUD>();

        if (hotbarHUD != null)
            hotbarHUD.OnActiveItemChanged += HandleActiveItemChanged;

        // Applica lo stato iniziale
        HandleActiveItemChanged(hotbarHUD != null ? hotbarHUD.SelectedStack : null);
    }

    private void OnDestroy()
    {
        if (hotbarHUD != null)
            hotbarHUD.OnActiveItemChanged -= HandleActiveItemChanged;
    }

    // ══════════════════════════════════════════════════════════
    //  Core
    // ══════════════════════════════════════════════════════════

    private void HandleActiveItemChanged(ItemStack stack)
    {
        var def = (stack != null && !stack.IsEmpty) ? stack.def : null;

        ActiveItemDef = def;

        var newCategory = def != null ? def.category : ItemCategory.None;
        int newDamage = (def != null && def.IsWeapon) ? def.attackDamage : 0;
        bool newBuild = def != null && def.IsBuilding;

        bool categoryChanged = newCategory != ActiveCategory;
        bool buildChanged = newBuild != IsBuildModeRequested;

        ActiveCategory = newCategory;
        WeaponBonusDamage = newDamage;
        IsBuildModeRequested = newBuild;

        if (categoryChanged)
            OnCategoryChanged?.Invoke(ActiveCategory);

        if (buildChanged)
            OnBuildModeChanged?.Invoke(IsBuildModeRequested);
    }
}
