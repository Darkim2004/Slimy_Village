using UnityEngine;

[System.Serializable]
public class LootEntry
{
    [Tooltip("Item da droppare.")]
    public ItemDefinition item;

    [Range(0f, 1f)]
    [Tooltip("Probabilità che questo drop avvenga (0 = mai, 1 = sempre).")]
    public float chance = 0.5f;

    [Min(1)]
    [Tooltip("Quantità minima droppata (se il roll ha successo).")]
    public int min = 1;

    [Min(1)]
    [Tooltip("Quantità massima droppata (se il roll ha successo).")]
    public int max = 1;
}

[CreateAssetMenu(menuName = "Game/Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    public LootEntry[] drops;

    public void SpawnLoot(Vector3 position)
    {
        if (drops == null) return;

        foreach (var d in drops)
        {
            if (d.item == null) continue;
            if (Random.value > d.chance) continue;

            int lo = Mathf.Min(d.min, d.max);
            int hi = Mathf.Max(d.min, d.max);
            int qty = Random.Range(lo, hi + 1);

            // Offset leggero per non sovrapporre tutti i drop
            Vector2 offset = Random.insideUnitCircle * 0.3f;
            Vector3 spawnPos = position + new Vector3(offset.x, offset.y, 0f);

            WorldDrop.Spawn(d.item, qty, spawnPos);
        }
    }
}