using System;
using UnityEngine;

/// <summary>
/// Controller che gestisce le interazioni utente con l'inventario (click sinistro/destro).
/// Mantiene lo stato del "cursor" (cosa il giocatore tiene in mano) e parla con InventoryModel.
/// La UI si iscrive a <see cref="OnCursorChanged"/> per aggiornare la visualizzazione.
/// Nessuna dipendenza da oggetti UI: solo logica pura.
/// </summary>
[RequireComponent(typeof(InventoryModel))]
public class InventoryInteractionController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  Stato: Cursor Stack
    // ══════════════════════════════════════════════════════════

    /// <summary>Lo stack attualmente "in mano" al giocatore (null = mano vuota).</summary>
    private ItemStack cursorStack;

    /// <summary>True se il cursore è vuoto (nulla in mano).</summary>
    public bool CursorEmpty => cursorStack == null || cursorStack.IsEmpty;

    /// <summary>Restituisce lo stack attualmente in mano (può essere null).</summary>
    public ItemStack CursorStack => cursorStack;

    /// <summary>Fired ogni volta che il cursor cambia (stack o null).</summary>
    public event Action<ItemStack> OnCursorChanged;

    // ── Riferimenti ─────────────────────────────────────────

    private InventoryModel model;

    // ══════════════════════════════════════════════════════════
    //  Init
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        model = GetComponent<InventoryModel>();
    }

    // ══════════════════════════════════════════════════════════
    //  API: Cursor
    // ══════════════════════════════════════════════════════════

    /// <summary>Imposta lo stack in mano al cursore.</summary>
    public void SetCursor(ItemStack stack)
    {
        cursorStack = (stack != null && stack.IsEmpty) ? null : stack;
        OnCursorChanged?.Invoke(cursorStack);
    }

    /// <summary>Svuota il cursore.</summary>
    public void ClearCursor()
    {
        cursorStack = null;
        OnCursorChanged?.Invoke(null);
    }

    // ══════════════════════════════════════════════════════════
    //  API: Left Click
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Click sinistro su uno slot. Implementa 4 casi:
    /// <list type="number">
    ///   <item>Mano vuota + slot pieno → PRENDI (pick up tutto lo stack)</item>
    ///   <item>Mano piena + slot vuoto → POSA (place tutto lo stack)</item>
    ///   <item>Mano piena + slot compatibile → MERGE (fondi, avanzo resta in mano)</item>
    ///   <item>Mano piena + slot incompatibile → SWAP (scambia mano ↔ slot)</item>
    /// </list>
    /// </summary>
    public void OnLeftClick(SlotRef slot)
    {
        if (!slot.IsValid) return;

        var slotStack = slot.Stack;
        bool slotEmpty  = slotStack == null || slotStack.IsEmpty;

        // ── Caso 1: PRENDI ──────────────────────────────────
        if (CursorEmpty && !slotEmpty)
        {
            SetCursor(slotStack);
            slot.Set(null);
            return;
        }

        // ── Caso 2: POSA ────────────────────────────────────
        if (!CursorEmpty && slotEmpty)
        {
            slot.Set(cursorStack);
            ClearCursor();
            return;
        }

        // Entrambi pieni
        if (!CursorEmpty && !slotEmpty)
        {
            // ── Caso 3: MERGE ───────────────────────────────
            if (slotStack.CanStackWith(cursorStack))
            {
                int transferred = slotStack.MergeFrom(cursorStack);
                if (transferred > 0)
                {
                    // Aggiorna lo slot (notifica evento)
                    slot.Set(slotStack);

                    // Aggiorna il cursore
                    if (cursorStack.IsEmpty)
                        ClearCursor();
                    else
                        SetCursor(cursorStack);
                }
                return;
            }

            // ── Caso 4: SWAP ────────────────────────────────
            var oldCursor = cursorStack;
            SetCursor(slotStack);
            slot.Set(oldCursor);
            return;
        }

        // Mano vuota + slot vuoto → nulla da fare
    }

    // ══════════════════════════════════════════════════════════
    //  API: Right Click
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Click destro su uno slot. Implementa 3 casi:
    /// <list type="number">
    ///   <item>Mano vuota + slot pieno → PRENDI METÀ (pick up ceil(n/2))</item>
    ///   <item>Mano piena + slot vuoto → POSA 1 unità</item>
    ///   <item>Mano piena + slot compatibile con spazio → AGGIUNGI 1 unità</item>
    /// </list>
    /// Se mano piena + slot incompatibile → nessuna azione (oppure swap come variante).
    /// </summary>
    public void OnRightClick(SlotRef slot)
    {
        if (!slot.IsValid) return;

        var slotStack = slot.Stack;
        bool slotEmpty  = slotStack == null || slotStack.IsEmpty;

        // ── Caso 1: PRENDI METÀ ─────────────────────────────
        if (CursorEmpty && !slotEmpty)
        {
            var taken = model.SplitHalf(slot);
            if (taken != null)
                SetCursor(taken);
            return;
        }

        // ── Caso 2: POSA 1 ─────────────────────────────────
        if (!CursorEmpty && slotEmpty)
        {
            PlaceOneFromCursor(slot);
            return;
        }

        // ── Caso 3: AGGIUNGI 1 SE COMPATIBILE ──────────────
        if (!CursorEmpty && !slotEmpty)
        {
            if (slotStack.CanStackWith(cursorStack) && slotStack.FreeSpace > 0)
            {
                PlaceOneFromCursor(slot);
            }
            // Se incompatibile o pieno → nessuna azione
            return;
        }

        // Mano vuota + slot vuoto → nulla
    }

    // ══════════════════════════════════════════════════════════
    //  API: Pointer (opzionale, per highlight UI)
    // ══════════════════════════════════════════════════════════

    /// <summary>Fired quando il puntatore entra in uno slot (per highlight visivo).</summary>
    public event Action<SlotRef> OnPointerEnter;

    /// <summary>Fired quando il puntatore esce da uno slot.</summary>
    public event Action<SlotRef> OnPointerExit;

    /// <summary>Da chiamare dalla UI quando il mouse entra in uno slot.</summary>
    public void NotifyPointerEnter(SlotRef slot) => OnPointerEnter?.Invoke(slot);

    /// <summary>Da chiamare dalla UI quando il mouse esce da uno slot.</summary>
    public void NotifyPointerExit(SlotRef slot) => OnPointerExit?.Invoke(slot);

    // ══════════════════════════════════════════════════════════
    //  Utility: Drop cursor
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Tenta di reinserire il cursor nell'inventario (es. alla chiusura della UI).
    /// Restituisce true se tutto è stato reinserito.
    /// </summary>
    public bool TryReturnCursorToInventory()
    {
        if (CursorEmpty) return true;

        model.TryAdd(cursorStack.def, cursorStack.amount, out var remainder, cursorStack.nbt);

        if (remainder == null || remainder.IsEmpty)
        {
            ClearCursor();
            return true;
        }

        SetCursor(remainder);
        return false;
    }

    // ══════════════════════════════════════════════════════════
    //  Internal
    // ══════════════════════════════════════════════════════════

    /// <summary>Piazza 1 unità dal cursore nello slot.</summary>
    private void PlaceOneFromCursor(SlotRef slot)
    {
        if (model.PlaceOne(slot, cursorStack))
        {
            if (cursorStack.IsEmpty)
                ClearCursor();
            else
                SetCursor(cursorStack); // notifica aggiornamento quantità
        }
    }
}
