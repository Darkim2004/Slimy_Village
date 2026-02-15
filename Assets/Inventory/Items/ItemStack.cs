using System;

/// <summary>
/// Rappresenta un singolo slot dell'inventario: definizione + quantità + dati extra opzionali.
/// È una classe (reference type) così i metodi possono mutarla e la UI riceve notifiche.
/// Uno slot vuoto è rappresentato da <c>null</c> oppure da <see cref="IsEmpty"/>.
/// </summary>
[Serializable]
public class ItemStack
{
    public ItemDefinition def;
    public int amount;

    /// <summary>NBT è presente SOLO per item non stackabili. Per stackabili è sempre null.</summary>
    public ItemNBT nbt;

    // ── Costruttori ─────────────────────────────────────────

    public ItemStack() { }

    public ItemStack(ItemDefinition def, int amount, ItemNBT nbt = null)
    {
        this.def    = def;
        this.amount = amount;
        this.nbt    = def != null && !def.isStackable ? (nbt ?? ItemNBT.NewFull()) : null;
    }

    // ── Query ───────────────────────────────────────────────

    public bool IsEmpty => def == null || amount <= 0;

    public int FreeSpace => def != null ? def.EffectiveMaxStack - amount : 0;

    /// <summary>
    /// Due stack possono fondersi solo se:
    /// - stesso ItemDefinition (per id)
    /// - entrambi stackabili (nbt == null su entrambi)
    /// </summary>
    public bool CanStackWith(ItemStack other)
    {
        if (other == null || other.IsEmpty) return false;
        if (this.IsEmpty) return false;
        if (this.def != other.def) return false;           // stesso SO
        if (!this.def.isStackable) return false;            // non stackabile
        if (this.nbt != null || other.nbt != null) return false; // nessun NBT
        return true;
    }

    /// <summary>
    /// Trasferisce quanti più item possibile da <paramref name="source"/> dentro questo stack.
    /// Restituisce il numero di item effettivamente trasferiti.
    /// </summary>
    public int MergeFrom(ItemStack source)
    {
        if (!CanStackWith(source)) return 0;

        int space = FreeSpace;
        int transfer = Math.Min(space, source.amount);
        this.amount   += transfer;
        source.amount -= transfer;
        return transfer;
    }

    /// <summary>Crea una copia indipendente (deep copy dell'NBT).</summary>
    public ItemStack Clone()
    {
        return new ItemStack
        {
            def    = this.def,
            amount = this.amount,
            nbt    = this.nbt != null ? (ItemNBT)this.nbt.Clone() : null,
        };
    }

    public override string ToString()
    {
        if (IsEmpty) return "[empty]";
        string tag = nbt != null ? $" nbt(dur={nbt.durability}/{nbt.maxDurability})" : "";
        return $"[{def.id} x{amount}{tag}]";
    }
}
