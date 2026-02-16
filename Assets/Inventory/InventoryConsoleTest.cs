using UnityEngine;

/// <summary>
/// Batteria di test dell'inventario eseguibile in Console Unity.
/// Piazza su un GO con <see cref="InventoryModel"/>. Trascina 2 ItemDefinition nell'Inspector.
/// </summary>
[RequireComponent(typeof(InventoryModel))]
[RequireComponent(typeof(InventoryInteractionController))]
public class InventoryConsoleTest : MonoBehaviour
{
    [Header("Trascina qui le ItemDefinition di test")]
    [SerializeField] private ItemDefinition testStackable;   // es: "stone", maxStack 64
    [SerializeField] private ItemDefinition testUnstackable; // es: "iron_sword", maxStack 1

    private InventoryModel inv;
    private InventoryInteractionController ctrl;
    private int passed, failed;

    private void Awake()
    {
        inv  = GetComponent<InventoryModel>();
        ctrl = GetComponent<InventoryInteractionController>();
    }

    private void Start()
    {
        if (testStackable == null || testUnstackable == null)
        {
            return;
        }

        passed = failed = 0;

        Log("═══════════════════════════════════════════");
        Log("   INVENTORY CONSOLE TEST — START");
        Log("═══════════════════════════════════════════");

        Test_AddStackable();
        Test_AddUnstackable();
        Test_TryRemove();
        Test_CountAndHas();
        Test_Overflow();
        Test_MoveOrMerge_ToEmpty();
        Test_MoveOrMerge_Merge();
        Test_MoveOrMerge_Swap();
        Test_SplitHalf();
        Test_TakeOne();
        Test_PlaceOne();
        Test_FindHelpers();
        Test_SlotRef();
        Test_SectionTryAddWithRemainder();
        Test_Clear();

        // ── Controller / Cursor tests ──
        Test_LeftClick_PickUp();
        Test_LeftClick_PlaceDown();
        Test_LeftClick_Merge();
        Test_LeftClick_Swap();
        Test_RightClick_PickHalf();
        Test_RightClick_PlaceOne();
        Test_RightClick_AddOne();
        Test_ReturnCursorToInventory();
        Test_GetSectionByName();

        Log("═══════════════════════════════════════════");
        Log($"   DONE — {passed} passed, {failed} failed");
        Log("═══════════════════════════════════════════");

    }

    // ══════════════════════════════════════════════════════════
    //  Test: Add / Remove / Count
    // ══════════════════════════════════════════════════════════

    private void Test_AddStackable()
    {
        Log("\n── AddStackable ──");
        inv.Clear();

        int left = inv.AddItem(testStackable, 10);
        Assert(left == 0,                             "AddItem(10) should fit");
        Assert(inv.CountItem(testStackable.id) == 10, "Count should be 10");

        left = inv.AddItem(testStackable, 5);
        Assert(left == 0,                             "AddItem(+5) should fit");
        Assert(inv.CountItem(testStackable.id) == 15, "Count should be 15 (merged)");
    }

    private void Test_AddUnstackable()
    {
        Log("\n── AddUnstackable ──");
        inv.Clear();

        int left = inv.AddItem(testUnstackable, 1);
        Assert(left == 0,                     "1st sword should fit");
        Assert(inv.HasItem(testUnstackable.id), "HasItem should be true");

        left = inv.AddItem(testUnstackable, 1);
        Assert(left == 0,                                   "2nd sword should fit (new slot)");
        Assert(inv.CountItem(testUnstackable.id) == 2, "Count should be 2");
    }

    private void Test_TryRemove()
    {
        Log("\n── TryRemove ──");
        inv.Clear();
        inv.AddItem(testStackable, 20);

        int removed = inv.TryRemove(testStackable.id, 7);
        Assert(removed == 7,                             "Should remove 7");
        Assert(inv.CountItem(testStackable.id) == 13, "Should have 13 left");

        removed = inv.TryRemove(testStackable.id, 999);
        Assert(removed == 13,                            "Should remove remaining 13");
        Assert(inv.CountItem(testStackable.id) == 0,  "Should be empty");
    }

    private void Test_CountAndHas()
    {
        Log("\n── Count & Has ──");
        inv.Clear();
        Assert(!inv.HasItem(testStackable.id),            "Empty → HasItem false");
        Assert(inv.CountItem(testStackable.id) == 0, "Empty → Count 0");

        inv.AddItem(testStackable, 3);
        Assert(inv.HasItem(testStackable.id, 3),  "Has 3 → true");
        Assert(!inv.HasItem(testStackable.id, 4), "Has 4 → false");
    }

    private void Test_Overflow()
    {
        Log("\n── Overflow ──");
        inv.Clear();

        int left = inv.AddItem(testStackable, 99999);
        Assert(left >= 0, "Shouldn't crash");
        Log($"  99999 → left={left}");
    }

    // ══════════════════════════════════════════════════════════
    //  Test: Slot Operations
    // ══════════════════════════════════════════════════════════

    private void Test_MoveOrMerge_ToEmpty()
    {
        Log("\n── MoveOrMerge (→ empty) ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 10));

        var from = inv.Hotbar.RefAt(0);
        var to   = inv.Hotbar.RefAt(3);
        inv.MoveOrMerge(from, to);

        Assert(from.IsEmpty,               "Source should be empty");
        Assert(!to.IsEmpty,                "Dest should have items");
        Assert(to.Stack.amount == 10, "Dest should have 10");
    }

    private void Test_MoveOrMerge_Merge()
    {
        Log("\n── MoveOrMerge (merge) ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 30));
        inv.Hotbar.SetSlot(1, new ItemStack(testStackable, 20));

        var from = inv.Hotbar.RefAt(0);
        var to   = inv.Hotbar.RefAt(1);
        inv.MoveOrMerge(from, to);

        int max = testStackable.EffectiveMaxStack; // 64
        if (30 + 20 <= max)
        {
            Assert(from.IsEmpty,                "Source empty after full merge");
            Assert(to.Stack.amount == 50,  "Dest has 50");
        }
        else
        {
            Assert(to.Stack.amount == max,            "Dest capped at max");
            Assert(from.Stack.amount == 50 - max, "Source has remainder");
        }
    }

    private void Test_MoveOrMerge_Swap()
    {
        Log("\n── MoveOrMerge (swap) ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable,   5));
        inv.Hotbar.SetSlot(1, new ItemStack(testUnstackable, 1));

        var from = inv.Hotbar.RefAt(0);
        var to   = inv.Hotbar.RefAt(1);
        inv.MoveOrMerge(from, to);

        Assert(from.Stack.def == testUnstackable, "Slot0 now has sword");
        Assert(to.Stack.def   == testStackable,   "Slot1 now has stone");
    }

    private void Test_SplitHalf()
    {
        Log("\n── SplitHalf ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 10));

        var slot  = inv.Hotbar.RefAt(0);
        var taken = inv.SplitHalf(slot);

        Assert(taken != null,             "Should return taken stack");
        Assert(taken.amount == 5,         "Taken should be 5 (half of 10)");
        Assert(slot.Stack.amount == 5,    "Remaining should be 5");

        // Dispari: 7 → prende 4, resta 3
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 7));
        taken = inv.SplitHalf(inv.Hotbar.RefAt(0));
        Assert(taken.amount == 4,                        "Taken ceil(7/2)=4");
        Assert(inv.Hotbar.GetSlot(0).amount == 3, "Remaining 3");

        // Singolo: 1 → prende 1, slot vuoto
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 1));
        taken = inv.SplitHalf(inv.Hotbar.RefAt(0));
        Assert(taken.amount == 1,                    "Taken 1");
        Assert(inv.Hotbar.RefAt(0).IsEmpty, "Slot now empty");
    }

    private void Test_TakeOne()
    {
        Log("\n── TakeOne ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 5));

        var slot  = inv.Hotbar.RefAt(0);
        var taken = inv.TakeOne(slot);

        Assert(taken != null && taken.amount == 1,       "Took 1");
        Assert(slot.Stack.amount == 4,                   "4 left");

        // Prendi l'ultimo
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 1));
        taken = inv.TakeOne(inv.Hotbar.RefAt(0));
        Assert(taken.amount == 1,                    "Took last 1");
        Assert(inv.Hotbar.RefAt(0).IsEmpty, "Slot now empty");

        // Slot vuoto
        taken = inv.TakeOne(inv.Hotbar.RefAt(0));
        Assert(taken == null, "Empty slot → null");
    }

    private void Test_PlaceOne()
    {
        Log("\n── PlaceOne ──");
        inv.Clear();

        var hand = new ItemStack(testStackable, 10);

        // In slot vuoto
        var slot = inv.Hotbar.RefAt(0);
        bool ok = inv.PlaceOne(slot, hand);
        Assert(ok,                       "Should place in empty slot");
        Assert(slot.Stack.amount == 1,   "Slot now 1");
        Assert(hand.amount == 9,         "Hand now 9");

        // In slot compatibile
        ok = inv.PlaceOne(slot, hand);
        Assert(ok,                       "Should merge +1");
        Assert(slot.Stack.amount == 2,   "Slot now 2");
        Assert(hand.amount == 8,         "Hand now 8");

        // In slot con item diverso → false
        inv.Hotbar.SetSlot(1, new ItemStack(testUnstackable, 1));
        ok = inv.PlaceOne(inv.Hotbar.RefAt(1), hand);
        Assert(!ok, "Different item → no place");
    }

    // ══════════════════════════════════════════════════════════
    //  Test: Helpers
    // ══════════════════════════════════════════════════════════

    private void Test_FindHelpers()
    {
        Log("\n── FindFirstSlotWithSpace / FindFirstEmptySlot ──");
        inv.Clear();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 10));

        var found = inv.FindFirstSlotWithSpace(testStackable.id);
        Assert(found.IsValid,         "Should find slot with space");
        Assert(found.index == 0,      "Should be slot 0");

        var empty = inv.FindFirstEmptySlot();
        Assert(empty.IsValid,         "Should find empty slot");
        Assert(empty.index == 1,      "First empty should be 1 (slot 0 occupied)");

        // Per section
        int secIdx = inv.Hotbar.FindFirstSlotWithSpace(testStackable.id);
        Assert(secIdx == 0, "Section-level: slot 0");
        int secEmpty = inv.Hotbar.FindFirstEmptySlot();
        Assert(secEmpty == 1, "Section-level: first empty = 1");
    }

    private void Test_SlotRef()
    {
        Log("\n── SlotRef ──");
        inv.Clear();
        inv.Hotbar.SetSlot(2, new ItemStack(testStackable, 7));

        var r = inv.Hotbar.RefAt(2);
        Assert(r.IsValid,              "RefAt(2) valid");
        Assert(!r.IsEmpty,             "Not empty");
        Assert(r.Stack.amount == 7,    "Amount 7");

        var bad = new SlotRef(null, 0);
        Assert(!bad.IsValid, "Null section → invalid");

        var oob = inv.Hotbar.RefAt(999);
        Assert(!oob.IsValid, "Out of bounds → invalid");
    }

    private void Test_SectionTryAddWithRemainder()
    {
        Log("\n── Section.TryAdd with remainder ──");
        // Crea una mini sezione da 2 slot
        var tiny = new InventorySection("Tiny", InventorySection.SectionType.Other, 2);

        var stack = new ItemStack(testStackable, 200);
        bool any = tiny.TryAdd(stack, out var rem);

        Assert(any, "Should insert something");
        int maxPossible = testStackable.EffectiveMaxStack * 2;
        int expectedRem = 200 - maxPossible;
        if (expectedRem > 0)
        {
            Assert(rem != null,                   "Remainder should exist");
            Assert(rem.amount == expectedRem, $"Remainder={rem?.amount}, expected {expectedRem}");
        }
        Log($"  200 into 2 slots (max {testStackable.EffectiveMaxStack}): rem={rem?.amount ?? 0}");
    }

    private void Test_Clear()
    {
        Log("\n── Clear ──");
        inv.AddItem(testStackable, 50);
        inv.Clear();
        Assert(inv.CountItem(testStackable.id) == 0, "Should be 0 after clear");
    }

    // ══════════════════════════════════════════════════════════
    //  Test: InventoryInteractionController
    // ══════════════════════════════════════════════════════════

    private void Test_LeftClick_PickUp()
    {
        Log("\n── LeftClick: Pick Up ──");
        inv.Clear();
        ctrl.ClearCursor();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 10));

        ctrl.OnLeftClick(inv.Hotbar.RefAt(0));

        Assert(inv.Hotbar.RefAt(0).IsEmpty,       "Slot should be empty after pick up");
        Assert(!ctrl.CursorEmpty,                  "Cursor should hold items");
        Assert(ctrl.CursorStack.amount == 10,      "Cursor should have 10");
        Assert(ctrl.CursorStack.def == testStackable, "Cursor def matches");
    }

    private void Test_LeftClick_PlaceDown()
    {
        Log("\n── LeftClick: Place Down ──");
        inv.Clear();
        ctrl.ClearCursor();
        ctrl.SetCursor(new ItemStack(testStackable, 5));

        ctrl.OnLeftClick(inv.Hotbar.RefAt(0));

        Assert(ctrl.CursorEmpty,                   "Cursor should be empty");
        Assert(!inv.Hotbar.RefAt(0).IsEmpty,       "Slot should have items");
        Assert(inv.Hotbar.GetSlot(0).amount == 5,  "Slot should have 5");
    }

    private void Test_LeftClick_Merge()
    {
        Log("\n── LeftClick: Merge ──");
        inv.Clear();
        ctrl.ClearCursor();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 30));
        ctrl.SetCursor(new ItemStack(testStackable, 20));

        ctrl.OnLeftClick(inv.Hotbar.RefAt(0));

        int max = testStackable.EffectiveMaxStack;
        if (50 <= max)
        {
            Assert(ctrl.CursorEmpty,                      "Cursor empty after full merge");
            Assert(inv.Hotbar.GetSlot(0).amount == 50,    "Slot has 50");
        }
        else
        {
            Assert(inv.Hotbar.GetSlot(0).amount == max,   "Slot capped at max");
            Assert(ctrl.CursorStack.amount == 50 - max,   "Cursor has remainder");
        }
    }

    private void Test_LeftClick_Swap()
    {
        Log("\n── LeftClick: Swap ──");
        inv.Clear();
        ctrl.ClearCursor();
        inv.Hotbar.SetSlot(0, new ItemStack(testUnstackable, 1));
        ctrl.SetCursor(new ItemStack(testStackable, 5));

        ctrl.OnLeftClick(inv.Hotbar.RefAt(0));

        Assert(ctrl.CursorStack.def == testUnstackable,   "Cursor now has sword");
        Assert(inv.Hotbar.GetSlot(0).def == testStackable, "Slot now has stone");
        Assert(inv.Hotbar.GetSlot(0).amount == 5,          "Slot amount 5");
    }

    private void Test_RightClick_PickHalf()
    {
        Log("\n── RightClick: Pick Half ──");
        inv.Clear();
        ctrl.ClearCursor();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 10));

        ctrl.OnRightClick(inv.Hotbar.RefAt(0));

        Assert(!ctrl.CursorEmpty,                  "Cursor should have items");
        Assert(ctrl.CursorStack.amount == 5,       "Cursor has half (5)");
        Assert(inv.Hotbar.GetSlot(0).amount == 5,  "Slot has remaining 5");
    }

    private void Test_RightClick_PlaceOne()
    {
        Log("\n── RightClick: Place 1 in empty ──");
        inv.Clear();
        ctrl.ClearCursor();
        ctrl.SetCursor(new ItemStack(testStackable, 10));

        ctrl.OnRightClick(inv.Hotbar.RefAt(0));

        Assert(inv.Hotbar.GetSlot(0).amount == 1,   "Slot should have 1");
        Assert(ctrl.CursorStack.amount == 9,         "Cursor should have 9");
    }

    private void Test_RightClick_AddOne()
    {
        Log("\n── RightClick: Add 1 to compatible ──");
        inv.Clear();
        ctrl.ClearCursor();
        inv.Hotbar.SetSlot(0, new ItemStack(testStackable, 3));
        ctrl.SetCursor(new ItemStack(testStackable, 10));

        ctrl.OnRightClick(inv.Hotbar.RefAt(0));

        Assert(inv.Hotbar.GetSlot(0).amount == 4,    "Slot should have 4");
        Assert(ctrl.CursorStack.amount == 9,          "Cursor should have 9");
    }

    private void Test_ReturnCursorToInventory()
    {
        Log("\n── ReturnCursorToInventory ──");
        inv.Clear();
        ctrl.ClearCursor();
        ctrl.SetCursor(new ItemStack(testStackable, 15));

        bool ok = ctrl.TryReturnCursorToInventory();
        Assert(ok,                                       "Should return all");
        Assert(ctrl.CursorEmpty,                         "Cursor should be empty");
        Assert(inv.CountItem(testStackable.id) == 15,    "Inventory should have 15");
    }

    private void Test_GetSectionByName()
    {
        Log("\n── GetSection / GetSlot / SetSlot by name ──");
        inv.Clear();

        Assert(inv.GetSection("Hotbar") == inv.Hotbar,  "GetSection('Hotbar') returns Hotbar");
        Assert(inv.GetSection("Main") == inv.Main,      "GetSection('Main') returns Main");
        Assert(inv.GetSection("Nonexistent") == null,   "Unknown section → null");

        inv.SetSlot("Hotbar", 0, new ItemStack(testStackable, 7));
        var got = inv.GetSlot("Hotbar", 0);
        Assert(got != null && got.amount == 7,           "GetSlot by name works");

        var r = inv.RefAt("Hotbar", 0);
        Assert(r.IsValid && r.Stack.amount == 7,         "RefAt by name works");
    }

    // ══════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════

    private void Assert(bool condition, string msg)
    {
        if (condition)
        {
            passed++;
        }
        else
        {
            failed++;
        }
    }

    private void Log(string msg) { }
}
