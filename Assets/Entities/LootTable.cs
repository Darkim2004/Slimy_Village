using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public GameObject itemPrefab;
    [Range(0f, 1f)] public float chance = 0.5f;
    public int min = 1;
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
            if (d.itemPrefab == null) continue;
            if (Random.value > d.chance) continue;

            int lo = Mathf.Min(d.min, d.max);
            int hi = Mathf.Max(d.min, d.max);
            int qty = Random.Range(lo, hi + 1);

            for (int i = 0; i < qty; i++)
                Object.Instantiate(d.itemPrefab, position, Quaternion.identity);
        }
    }
}