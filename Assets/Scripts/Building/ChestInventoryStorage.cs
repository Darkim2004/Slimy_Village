using UnityEngine;

/// <summary>
/// Storage inventario dedicato alla chest piazzata nel mondo.
/// Ogni istanza mantiene i propri slot indipendenti.
/// </summary>
public class ChestInventoryStorage : MonoBehaviour
{
    [Header("Chest Inventory")]
    [Min(1)]
    [SerializeField] private int slotCount = 30;
    [SerializeField] private string sectionName = "Chest";

    private InventorySection section;

    public InventorySection Section
    {
        get
        {
            EnsureInitialized();
            return section;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (section != null) return;

        int safeSize = Mathf.Max(1, slotCount);
        section = new InventorySection(sectionName, InventorySection.SectionType.Chest, safeSize);
    }
}
