using System;

/// <summary>
/// Una sezione (banda) dell'inventario con slot di dimensione fissa.
/// Esempi: Hotbar (9), Main (27), Chest (54).
/// </summary>
[Serializable]
public class InventorySection
{
    public enum SectionType { Hotbar, Main, Equipment, Chest, Other }

    public string       sectionName;
    public SectionType  type;
    public ItemStack[]  slots;

    /// <summary>Evento lanciato quando un singolo slot cambia.</summary>
    public event Action<int> OnSlotChanged;

    // ── Costruttore ─────────────────────────────────────────

    public InventorySection(string name, SectionType type, int size)
    {
        this.sectionName = name;
        this.type        = type;
        this.slots       = new ItemStack[size];
    }

    public int Size => slots.Length;

    // ══════════════════════════════════════════════════════════
    //  API: Add / Remove / Count
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Tenta di aggiungere uno stack nella sezione.
    /// Prima merge su slot compatibili, poi slot vuoti.
    /// <paramref name="remainder"/>: ciò che non è entrato (null se tutto inserito).
    /// Restituisce true se almeno 1 unità è stata inserita.
    /// </summary>
    public bool TryAdd(ItemStack incoming, out ItemStack remainder)
    {
        remainder = null;
        if (incoming == null || incoming.IsEmpty) return false;

        int original  = incoming.amount;
        int remaining = incoming.amount;

        // 1) Merge in slot esistenti dello stesso tipo
        if (incoming.def.isStackable)
        {
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty) continue;
                if (!slots[i].CanStackWith(incoming)) continue;

                int space    = slots[i].FreeSpace;
                int transfer = Math.Min(space, remaining);
                slots[i].amount += transfer;
                remaining       -= transfer;
                NotifySlot(i);
            }
        }

        // 2) Piazza nei primi slot vuoti
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i] != null && !slots[i].IsEmpty) continue;

            int place = Math.Min(remaining, incoming.def.EffectiveMaxStack);
            slots[i] = new ItemStack(incoming.def, place, incoming.nbt);
            remaining -= place;
            NotifySlot(i);
        }

        incoming.amount = remaining;

        if (remaining > 0)
            remainder = new ItemStack(incoming.def, remaining, incoming.nbt);

        return remaining < original;
    }

    /// <summary>Overload retrocompatibile — restituisce la quantità non inserita.</summary>
    public int TryAdd(ItemStack incoming)
    {
        TryAdd(incoming, out _);
        return incoming.amount;
    }

    /// <summary>
    /// Rimuove fino a <paramref name="amount"/> unità di un item con il dato defId.
    /// Restituisce la quantità effettivamente rimossa.
    /// </summary>
    public int TryRemove(string defId, int amount)
    {
        int removed = 0;
        for (int i = 0; i < slots.Length && removed < amount; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty) continue;
            if (slots[i].def.id != defId) continue;

            int take = Math.Min(slots[i].amount, amount - removed);
            slots[i].amount -= take;
            removed += take;

            if (slots[i].amount <= 0) slots[i] = null;
            NotifySlot(i);
        }
        return removed;
    }

    /// <summary>Alias retrocompatibile.</summary>
    public int Remove(string defId, int amount) => TryRemove(defId, amount);

    /// <summary>Conta quante unità di un dato item sono presenti nella sezione.</summary>
    public int Count(string defId)
    {
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].IsEmpty && slots[i].def.id == defId)
                total += slots[i].amount;
        return total;
    }

    // ══════════════════════════════════════════════════════════
    //  API: Slot access
    // ══════════════════════════════════════════════════════════

    /// <summary>Restituisce lo stack allo slot i (può essere null = vuoto).</summary>
    public ItemStack GetSlot(int i) => (i >= 0 && i < slots.Length) ? slots[i] : null;

    /// <summary>Imposta direttamente uno slot (usato da swap / drag&drop).</summary>
    public void SetSlot(int i, ItemStack stack)
    {
        if (i < 0 || i >= slots.Length) return;
        slots[i] = (stack != null && stack.IsEmpty) ? null : stack;
        NotifySlot(i);
    }

    /// <summary>Crea un <see cref="SlotRef"/> per lo slot i di questa sezione.</summary>
    public SlotRef RefAt(int i) => new SlotRef(this, i);

    // ══════════════════════════════════════════════════════════
    //  Helpers: ricerca slot
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Primo slot che contiene <paramref name="defId"/> e ha ancora spazio.
    /// Restituisce -1 se non trovato.
    /// </summary>
    public int FindFirstSlotWithSpace(string defId)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null || s.IsEmpty) continue;
            if (s.def.id != defId) continue;
            if (!s.def.isStackable) continue;
            if (s.FreeSpace > 0) return i;
        }
        return -1;
    }

    /// <summary>Primo slot vuoto. Restituisce -1 se non ce n'è.</summary>
    public int FindFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null || slots[i].IsEmpty) return i;
        return -1;
    }

    /// <summary>Controlla se esiste almeno uno slot libero.</summary>
    public bool HasFreeSlot() => FindFirstEmptySlot() >= 0;

    /// <summary>Svuota tutti gli slot.</summary>
    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = null;
            NotifySlot(i);
        }
    }

    // ── Internal ────────────────────────────────────────────

    private void NotifySlot(int i) => OnSlotChanged?.Invoke(i);
}
