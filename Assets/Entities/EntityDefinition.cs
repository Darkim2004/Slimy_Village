using UnityEngine;

public enum SpawnTimeRule
{
    Always,
    Day,
    Night
}

public abstract class EntityDefinition : ScriptableObject
{
    [Header("Spawn")]
    [SerializeField] private SpawnTimeRule timeRule = SpawnTimeRule.Always;

    [Tooltip("Biomi dove può spawnare. Se vuoto: ovunque.")]
    [SerializeField] private WorldGenTilemap.Biome[] allowedBiomes;

    [Tooltip("Peso nella scelta random (più alto = più probabile).")]
    [SerializeField] private float weight = 1f;

    [Header("Loot (optional)")]
    [SerializeField] private LootTable lootTable;

    [Header("Animations (optional, data-driven)")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    public SpawnTimeRule TimeRule => timeRule;
    public WorldGenTilemap.Biome[] AllowedBiomes => allowedBiomes;
    public float Weight => weight;

    public LootTable LootTable => lootTable;
    public RuntimeAnimatorController AnimatorController => animatorController;

    protected void ApplySpawnDefaults(SpawnTimeRule rule, WorldGenTilemap.Biome[] biomes, float spawnWeight)
    {
        timeRule = rule;
        allowedBiomes = biomes;
        weight = Mathf.Max(0f, spawnWeight);
    }
}
