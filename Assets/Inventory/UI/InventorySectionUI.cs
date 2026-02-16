using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Costruisce e aggiorna la UI di una sezione inventario (Hotbar/Main/Chest).
/// Instanzia uno slot prefab per ogni indice e aggiorna solo gli slot coinvolti via evento.
/// </summary>
public class InventorySectionUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private InventoryModel inventory;
    [SerializeField] private InventoryInteractionController controller;
    [SerializeField] private string sectionName = "Main";

    [Header("UI")]
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private InventorySlotUI slotPrefab;

    private readonly List<InventorySlotUI> slotViews = new();
    private InventorySection section;
    private Coroutine delayedBuildRoutine;
    private bool warningLogged;

    private void OnEnable()
    {
        TryBuild();

        if (inventory != null)
            inventory.OnSlotChanged += HandleSlotChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnSlotChanged -= HandleSlotChanged;

        if (delayedBuildRoutine != null)
        {
            StopCoroutine(delayedBuildRoutine);
            delayedBuildRoutine = null;
        }
    }

    [ContextMenu("Rebuild Slots")]
    public void RebuildSlots()
    {
        ClearInstantiated();
        TryBuild();
    }

    public void RefreshAll()
    {
        for (int i = 0; i < slotViews.Count; i++)
            slotViews[i].Refresh();
    }

    private void TryBuild()
    {
        // Auto-find missing references
        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();
        if (controller == null)
            controller = FindFirstObjectByType<InventoryInteractionController>();

        if (inventory == null || controller == null || slotsRoot == null || slotPrefab == null)
        {
            GameDebug.Warning(GameDebugCategory.Inventory,
                $"[InventorySectionUI] TryBuild aborted on '{name}': " +
                $"inventory={inventory != null}, controller={controller != null}, " +
                $"slotsRoot={slotsRoot != null}, slotPrefab={slotPrefab != null}", this);
            return;
        }

        if (slotViews.Count > 0)
        {
            RefreshAll();
            return;
        }

        GameDebug.Log(GameDebugCategory.Inventory, $"[InventorySectionUI] TryBuild '{sectionName}' on {name} using inventory={inventory.name} sections={inventory.Sections.Count}", this);
        section = inventory.GetSection(sectionName);
        if (section == null)
        {
            if (delayedBuildRoutine == null && isActiveAndEnabled)
                delayedBuildRoutine = StartCoroutine(DelayedBuildRetry());

            if (!warningLogged)
                warningLogged = true;
            return;
        }

        warningLogged = false;

        for (int i = 0; i < section.Size; i++)
        {
            var slotView = Instantiate(slotPrefab, slotsRoot);
            slotView.name = $"Slot_{sectionName}_{i}";
            slotView.Bind(inventory, controller, section, i);
            slotViews.Add(slotView);
        }
    }

    private System.Collections.IEnumerator DelayedBuildRetry()
    {
        const int maxFrames = 30;
        for (int i = 0; i < maxFrames; i++)
        {
            if (!isActiveAndEnabled) break;

            section = inventory != null ? inventory.GetSection(sectionName) : null;
            if (section != null)
            {
                delayedBuildRoutine = null;
                warningLogged = false;
                TryBuild();
                yield break;
            }

            yield return null;
        }

        delayedBuildRoutine = null;
    }

    private string GetAvailableSections()
    {
        if (inventory == null || inventory.Sections == null || inventory.Sections.Count == 0)
            return "<none>";

        var names = new List<string>(inventory.Sections.Count);
        for (int i = 0; i < inventory.Sections.Count; i++)
        {
            var sec = inventory.Sections[i];
            if (sec != null)
                names.Add(sec.sectionName);
        }

        return names.Count > 0 ? string.Join(", ", names) : "<none>";
    }

    private void HandleSlotChanged(InventorySection changedSection, int changedIndex)
    {
        if (section == null || changedSection == null) return;
        if (changedSection != section) return;
        if (changedIndex < 0 || changedIndex >= slotViews.Count) return;

        slotViews[changedIndex].Refresh();
    }

    private void ClearInstantiated()
    {
        for (int i = slotViews.Count - 1; i >= 0; i--)
        {
            var slotView = slotViews[i];
            if (slotView == null) continue;

            if (Application.isPlaying)
                Destroy(slotView.gameObject);
            else
                DestroyImmediate(slotView.gameObject);
        }

        slotViews.Clear();
    }
}
