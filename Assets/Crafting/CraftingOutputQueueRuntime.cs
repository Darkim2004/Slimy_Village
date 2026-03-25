using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftingOutputQueueRuntime : MonoBehaviour
{
    [Serializable]
    private class PendingOutput
    {
        public ItemDefinition item;
        public int amount;

        public PendingOutput(ItemDefinition item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }
    }

    [SerializeField] private InventoryModel targetInventory;
    [SerializeField] private List<PendingOutput> pending = new List<PendingOutput>();

    private bool isFlushing;

    public int PendingEntriesCount => pending.Count;

    public static CraftingOutputQueueRuntime GetOrCreate(InventoryModel inventory)
    {
        if (inventory == null) return null;

        var runtime = inventory.GetComponent<CraftingOutputQueueRuntime>();
        if (runtime == null)
            runtime = inventory.gameObject.AddComponent<CraftingOutputQueueRuntime>();

        runtime.Bind(inventory);
        return runtime;
    }

    private void Awake()
    {
        if (targetInventory == null)
            targetInventory = GetComponent<InventoryModel>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(InventoryModel inventory)
    {
        if (targetInventory == inventory) return;

        Unsubscribe();
        targetInventory = inventory;
        Subscribe();
    }

    public void Enqueue(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return;

        if (item.isStackable)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].item == item)
                {
                    pending[i].amount += amount;
                    TryFlush();
                    return;
                }
            }
        }

        pending.Add(new PendingOutput(item, amount));
        TryFlush();
    }

    public void TryFlush()
    {
        if (targetInventory == null || pending.Count == 0 || isFlushing) return;

        isFlushing = true;
        try
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var entry = pending[i];
                if (entry == null || entry.item == null || entry.amount <= 0)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                int before = entry.amount;
                targetInventory.TryAdd(entry.item, entry.amount, out var remainder);
                entry.amount = remainder != null ? remainder.amount : 0;

                if (entry.amount <= 0)
                    pending.RemoveAt(i);
                else if (entry.amount == before)
                    continue;
            }
        }
        finally
        {
            isFlushing = false;
        }
    }

    private void Subscribe()
    {
        if (targetInventory == null) return;

        targetInventory.OnSlotChanged -= HandleInventoryChanged;
        targetInventory.OnBulkChanged -= HandleBulkChanged;
        targetInventory.OnSlotChanged += HandleInventoryChanged;
        targetInventory.OnBulkChanged += HandleBulkChanged;
    }

    private void Unsubscribe()
    {
        if (targetInventory == null) return;

        targetInventory.OnSlotChanged -= HandleInventoryChanged;
        targetInventory.OnBulkChanged -= HandleBulkChanged;
    }

    private void HandleInventoryChanged(InventorySection _, int __)
    {
        TryFlush();
    }

    private void HandleBulkChanged()
    {
        TryFlush();
    }
}
