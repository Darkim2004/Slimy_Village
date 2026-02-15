using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modello completo dell'inventario di un'entità.
/// Contiene una o più <see cref="InventorySection"/> e pubblica eventi globali.
/// Nessuna dipendenza dalla UI: la UI si iscrive agli eventi.
/// </summary>
public class InventoryModel : MonoBehaviour
{
    // ── Configurazione ──────────────────────────────────────

    [Header("Section sizes")]
    [SerializeField] private int hotbarSize = 9;
    [SerializeField] private int mainSize   = 27;

    // ── Sezioni ─────────────────────────────────────────────

    public InventorySection Hotbar { get; private set; }
    public InventorySection Main   { get; private set; }

    /// <summary>Accesso ordinato a tutte le sezioni (hotbar prima, poi main).</summary>
    public IReadOnlyList<InventorySection> Sections => sections;
    private readonly List<InventorySection> sections = new();

    // ── Eventi globali ──────────────────────────────────────

    /// <summary>Fired quando uno slot qualsiasi cambia: (sezione, indice slot).</summary>
    public event Action<InventorySection, int> OnSlotChanged;

    /// <summary>Fired dopo operazioni bulk (clear, load, sort).</summary>
    public event Action OnBulkChanged;

    // ══════════════════════════════════════════════════════════
    //  Init
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        Hotbar = new InventorySection("Hotbar", InventorySection.SectionType.Hotbar, hotbarSize);
        Main   = new InventorySection("Main",   InventorySection.SectionType.Main,   mainSize);

        RegisterSection(Hotbar);
        RegisterSection(Main);
    }

    // ══════════════════════════════════════════════════════════
    //  API: Add / Remove / Count
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Aggiunge un item all'inventario (hotbar → main).
    /// <paramref name="remainder"/>: ciò che non è entrato (null = tutto OK).
    /// Restituisce true se almeno 1 unità è stata inserita.
    /// </summary>
    public bool TryAdd(ItemDefinition def, int amount, out ItemStack remainder, ItemNBT nbt = null)
    {
        remainder = null;
        if (def == null || amount <= 0) return false;

        var stack = new ItemStack(def, amount, nbt);
        bool any = false;

        foreach (var sec in sections)
        {
            if (sec.TryAdd(stack, out var left))
                any = true;

            if (stack.amount <= 0) break;
        }

        if (stack.amount > 0)
            remainder = new ItemStack(def, stack.amount, nbt);

        return any;
    }

    /// <summary>Overload semplificato — restituisce quantità non inserita.</summary>
    public int AddItem(ItemDefinition def, int amount, ItemNBT nbt = null)
    {
        TryAdd(def, amount, out var rem, nbt);
        return rem != null ? rem.amount : 0;
    }

    /// <summary>
    /// Rimuove fino a <paramref name="amount"/> unità del dato item (per crafting/consumo).
    /// Restituisce la quantità effettivamente rimossa.
    /// </summary>
    public int TryRemove(string defId, int amount)
    {
        int removed = 0;
        foreach (var sec in sections)
        {
            removed += sec.TryRemove(defId, amount - removed);
            if (removed >= amount) break;
        }
        return removed;
    }

    /// <summary>Alias retrocompatibile.</summary>
    public int RemoveItem(string defId, int amount) => TryRemove(defId, amount);

    /// <summary>Controlla se l'inventario contiene almeno <paramref name="amount"/> unità.</summary>
    public bool HasItem(string defId, int amount = 1) => CountItem(defId) >= amount;

    /// <summary>Conta il totale di un item in tutto l'inventario.</summary>
    public int CountItem(string defId)
    {
        int total = 0;
        foreach (var sec in sections)
            total += sec.Count(defId);
        return total;
    }

    /// <summary>Svuota tutto l'inventario.</summary>
    public void Clear()
    {
        foreach (var sec in sections)
            sec.Clear();
        OnBulkChanged?.Invoke();
    }

    // ══════════════════════════════════════════════════════════
    //  API: Slot Operations (per UI / drag&drop)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Sposta o fonde lo stack da <paramref name="from"/> a <paramref name="to"/>.
    /// Se non può fondere → swap.
    /// </summary>
    public void MoveOrMerge(SlotRef from, SlotRef to)
    {
        if (!from.IsValid || !to.IsValid) return;
        if (from.section == to.section && from.index == to.index) return;

        var srcStack = from.Stack;
        var dstStack = to.Stack;

        // Caso 1: destinazione vuota → sposta
        if (dstStack == null || dstStack.IsEmpty)
        {
            to.Set(srcStack);
            from.Set(null);
            return;
        }

        // Caso 2: stessi item stackabili → merge (e avanzo resta in from)
        if (dstStack.CanStackWith(srcStack))
        {
            dstStack.MergeFrom(srcStack);
            if (srcStack.IsEmpty) from.Set(null);
            else from.Set(srcStack); // notifica l'aggiornamento
            to.Set(dstStack);
            return;
        }

        // Caso 3: swap
        from.Set(dstStack);
        to.Set(srcStack);
    }

    /// <summary>
    /// Prende metà dello stack (arrotondato per eccesso) e lo restituisce.
    /// Lo slot originale tiene il resto.
    /// Utile per click destro "prendi metà".
    /// </summary>
    public ItemStack SplitHalf(SlotRef slot)
    {
        if (!slot.IsValid) return null;

        var stack = slot.Stack;
        if (stack == null || stack.IsEmpty) return null;

        int half = (stack.amount + 1) / 2; // ceil division
        int keep = stack.amount - half;

        var taken = stack.Clone();
        taken.amount = half;

        if (keep <= 0)
            slot.Set(null);
        else
        {
            stack.amount = keep;
            slot.Set(stack);
        }

        return taken;
    }

    /// <summary>
    /// Preleva 1 unità dallo slot. Restituisce lo stack di 1 (o null se vuoto).
    /// </summary>
    public ItemStack TakeOne(SlotRef slot)
    {
        if (!slot.IsValid) return null;

        var stack = slot.Stack;
        if (stack == null || stack.IsEmpty) return null;

        var taken = stack.Clone();
        taken.amount = 1;

        stack.amount -= 1;
        if (stack.amount <= 0)
            slot.Set(null);
        else
            slot.Set(stack);

        return taken;
    }

    /// <summary>
    /// Piazza 1 unità dalla <paramref name="hand"/> nello slot.
    /// Se lo slot è vuoto, crea un nuovo stack di 1.
    /// Se è compatibile, aggiunge 1 (se c'è spazio).
    /// Restituisce true se ha piazzato.
    /// </summary>
    public bool PlaceOne(SlotRef slot, ItemStack hand)
    {
        if (!slot.IsValid || hand == null || hand.IsEmpty) return false;

        var dstStack = slot.Stack;

        // Slot vuoto → piazza 1
        if (dstStack == null || dstStack.IsEmpty)
        {
            var placed = hand.Clone();
            placed.amount = 1;
            slot.Set(placed);
            hand.amount -= 1;
            return true;
        }

        // Slot compatibile con spazio → aggiungi 1
        if (dstStack.CanStackWith(hand) && dstStack.FreeSpace > 0)
        {
            dstStack.amount += 1;
            hand.amount     -= 1;
            slot.Set(dstStack);
            return true;
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════
    //  Helpers: ricerca globale
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Trova il primo slot (cross-section) che contiene <paramref name="defId"/>
    /// e ha ancora spazio libero. Restituisce un SlotRef invalid se non trovato.
    /// </summary>
    public SlotRef FindFirstSlotWithSpace(string defId)
    {
        foreach (var sec in sections)
        {
            int i = sec.FindFirstSlotWithSpace(defId);
            if (i >= 0) return sec.RefAt(i);
        }
        return default;
    }

    /// <summary>
    /// Trova il primo slot vuoto (cross-section).
    /// Restituisce un SlotRef invalid se non trovato.
    /// </summary>
    public SlotRef FindFirstEmptySlot()
    {
        foreach (var sec in sections)
        {
            int i = sec.FindFirstEmptySlot();
            if (i >= 0) return sec.RefAt(i);
        }
        return default;
    }

    // ══════════════════════════════════════════════════════════
    //  Sezioni dinamiche (es. chest)
    // ══════════════════════════════════════════════════════════

    /// <summary>Aggiunge una sezione custom e registra i suoi eventi.</summary>
    public void AddSection(InventorySection section)
    {
        if (section == null) return;
        RegisterSection(section);
    }

    /// <summary>Rimuove una sezione custom.</summary>
    public void RemoveSection(InventorySection section)
    {
        if (section == null) return;
        sections.Remove(section);
    }

    // ── Internal ────────────────────────────────────────────

    private void RegisterSection(InventorySection sec)
    {
        sections.Add(sec);
        sec.OnSlotChanged += idx => OnSlotChanged?.Invoke(sec, idx);
    }

    /// <summary>Stampa in console il contenuto completo (per debug).</summary>
    public void DebugDump()
    {
        foreach (var sec in sections)
        {
            Debug.Log($"── {sec.sectionName} ({sec.Size} slots) ──");
            for (int i = 0; i < sec.Size; i++)
            {
                var s = sec.GetSlot(i);
                if (s != null && !s.IsEmpty)
                    Debug.Log($"  [{i}] {s}");
            }
        }
    }
}
