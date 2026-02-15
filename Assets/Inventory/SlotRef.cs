/// <summary>
/// Riferimento univoco a uno slot: sezione + indice.
/// Funziona sia con inventario del player che con chest esterne.
/// </summary>
public struct SlotRef
{
    public InventorySection section;
    public int              index;

    public SlotRef(InventorySection section, int index)
    {
        this.section = section;
        this.index   = index;
    }

    // ── Shortcut ────────────────────────────────────────────

    public bool       IsValid => section != null && index >= 0 && index < section.Size;
    public ItemStack  Stack   => IsValid ? section.GetSlot(index) : null;
    public bool       IsEmpty => Stack == null || Stack.IsEmpty;

    public void Set(ItemStack stack)
    {
        if (IsValid) section.SetSlot(index, stack);
    }

    public override string ToString() =>
        IsValid ? $"{section.sectionName}[{index}]" : "(invalid)";
}
