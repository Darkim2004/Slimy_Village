using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper di test per assegnare rapidamente item a un personaggio con <see cref="InventoryModel"/>.
/// Usalo solo in fase di sviluppo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryModel))]
public class InventoryTestItemGiver : MonoBehaviour
{
    [Serializable]
    private struct TestItemEntry
    {
        public ItemDefinition item;

        [Min(1)]
        public int amount;
    }

    [Header("Items to grant")]
    [SerializeField] private List<TestItemEntry> items = new();

    [Header("When to grant")]
    [SerializeField] private bool grantOnStart = true;
    [SerializeField] private bool grantOnKeyPress;
    [SerializeField] private KeyCode grantKey = KeyCode.F6;

    [Header("Options")]
    [SerializeField] private bool clearInventoryBeforeGrant;

    private InventoryModel inventory;

    private void Awake()
    {
        inventory = GetComponent<InventoryModel>();
    }

    private void Start()
    {
        if (grantOnStart)
            GrantConfiguredItems();
    }

    private void Update()
    {
        if (!grantOnKeyPress)
            return;

        if (Input.GetKeyDown(grantKey))
            GrantConfiguredItems();
    }

    [ContextMenu("Grant Configured Items")]
    public void GrantConfiguredItems()
    {
        if (inventory == null)
            inventory = GetComponent<InventoryModel>();

        if (inventory == null)
        {
            Debug.LogWarning("[InventoryTestItemGiver] InventoryModel non trovato.", this);
            return;
        }

        if (clearInventoryBeforeGrant)
            inventory.Clear();

        for (int index = 0; index < items.Count; index++)
        {
            var entry = items[index];
            if (entry.item == null || entry.amount <= 0)
                continue;

            int leftover = inventory.AddItem(entry.item, entry.amount);
            if (leftover > 0)
            {
                Debug.LogWarning(
                    $"[InventoryTestItemGiver] Inventario pieno: '{entry.item.displayName}' aggiunto parzialmente. Rimanenti: {leftover}.",
                    this);
                continue;
            }

            Debug.Log(
                $"[InventoryTestItemGiver] Aggiunti {entry.amount}x '{entry.item.displayName}' a {name}.",
                this);
        }
    }
}