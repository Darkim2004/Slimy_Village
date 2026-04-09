using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldGenTilemap : MonoBehaviour
{
    private const string PrefPendingWorldName = "MainMenu.PendingWorldName";
    private const string PrefPendingWorldSeed = "MainMenu.PendingWorldSeed";
    private const string PrefPendingWorldId = "MainMenu.PendingWorldId";
    private const string PrefHasPendingWorldCreation = "MainMenu.HasPendingWorldCreation";

    [Header("Tilemaps (same Grid)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap decorTilemap;
    [SerializeField] private Tilemap treeCollisionTilemap;

    // --- Cached generated data (for spawners/queries) ---
    private TileData[,] _generated;
    private bool _hasGenerated;

    public bool HasGenerated => _hasGenerated;
    public int Width => width;
    public int Height => height;
    public Tilemap GroundTilemap => groundTilemap;

    /// <summary>
    /// Punto di spawn iniziale del player, calcolato durante Generate().
    /// Si trova sempre su una cella di terra (Plains o Snowy), mai Ocean.
    /// </summary>
    public Vector3 WorldSpawnPoint { get; private set; }

    public enum Biome { Ocean, Plains, Snomy }

    [Serializable]
    public class StructurePrefabEntry
    {
        public GameObject prefab;

        [Min(1)]
        public Vector2Int size = Vector2Int.one;

        public Vector3 spawnOffset = Vector3.zero;
    }

    [Serializable]
    public class FixedChestLootEntry
    {
        public ItemDefinition item;

        [Min(1)]
        public int amount = 1;
    }

    [Serializable]
    public class StructureGroupDefinition
    {
        public string groupName = "Ruins";
        public bool enabled = true;
        public Biome spawnBiome = Biome.Plains;

        [Min(1)] public int minStructures = 3;
        [Min(1)] public int maxStructures = 6;

        [Min(1)]
        [Tooltip("Distanza minima in celle (Chebyshev) tra le strutture del gruppo.")]
        public int minStructureDistanceCells = 1;

        [Min(1)]
        [Tooltip("Distanza massima in celle tra le strutture del gruppo.")]
        public int maxStructureDistanceCells = 8;

        [Tooltip("Se true, tenta prima il piazzamento delle strutture piu grandi per ridurre i casi in cui vengono bloccate dalle piccole.")]
        public bool placeLargerStructuresFirst = true;

        [Min(1)]
        [Tooltip("Numero massimo di tentativi per trovare un anchor valido per il gruppo.")]
        public int anchorAttempts = 120;

        [Min(1)]
        [Tooltip("Tentativi per piazzare ciascuna struttura del gruppo.")]
        public int placementAttemptsPerStructure = 80;

        [Min(1)]
        [Tooltip("Numero di retry completi del planner per questo gruppo (nuovo seed locale ad ogni retry).")]
        public int planningRetries = 2;

        [Min(1)]
        [Tooltip("Raggio massimo in celle per trovare una cella valida per la chest vicino al centro gruppo.")]
        public int chestSearchRadius = 8;

        [Header("Structure Pool")]
        public StructurePrefabEntry[] structurePrefabs;

        [Header("Center Chest")]
        public PlaceableDefinition chestPlaceable;
        public FixedChestLootEntry[] chestFixedLoot;
    }

    [Header("World Size (prototype, no chunks yet)")]
    [Min(1)] public int width = 128;
    [Min(1)] public int height = 128;

    [Header("Seed & Offset")]
    public int seed = 12345;
    public Vector2 offset; // useful to "move" the world without changing seed

    // -----------------------
    // Tiles - Ground
    // -----------------------
    [Header("Tiles - Ground Variants (at least 1 each)")]
    public TileBase[] oceanTiles;   // fallback if oceanBands not set
    public TileBase[] plainsTiles;
    public TileBase[] snomyTiles;

    [Header("Tiles - Minor Decor Variants (white noise)")]
    public TileBase[] grassTiles;   // Plains minor decor

    [Header("Tiles - Collision (Trees)")]
    public TileBase treeCollisionTile;
    public TileBase snowTreeCollisionTile;

    // -----------------------
    // Prefabs - Props (y-sorted)
    // -----------------------
    [Header("Prefabs - Props (Y-sorted)")]
    [Tooltip("Prefab per alberi (SpriteRenderer + tuo YSort sul prefab).")]
    public GameObject treePrefab;

    [Tooltip("Prefab per alberi innevati (SpriteRenderer + tuo YSort sul prefab).")]
    public GameObject snowTreePrefab;

    [Tooltip("Prefab per cespugli di pianura (SpriteRenderer + tuo YSort sul prefab).")]
    public GameObject bushPrefab;

    [Tooltip("Prefab per rocce grandi (spawnate con regole dedicate).")]
    public GameObject rockPrefab;

    [Tooltip("Parent opzionale dove mettere tutti i props spawnati (consigliato: Empty 'Props').")]
    public Transform propsParent;

    [Header("Structure Groups")]
    [Tooltip("Se true, genera gruppi di strutture non distruttibili con chest centrale.")]
    public bool spawnStructureGroups = true;

    [Tooltip("Collision tile usata per bloccare le celle occupate dalle strutture. Fallback: treeCollisionTile.")]
    public TileBase structureCollisionTile;

    [Tooltip("Collision tile usata per bloccare la cella/area chest. Fallback: structureCollisionTile/treeCollisionTile.")]
    public TileBase chestCollisionTile;

    [Tooltip("Definizioni dei gruppi strutture (es. Rovine Plains, Rovine di ghiaccio Snomy).")]
    public StructureGroupDefinition[] structureGroups;

    [Tooltip("Offset di spawn per alberi (per correggere pivot/ancoraggio).")]
    public Vector3 treeSpawnOffset = Vector3.zero;

    [Tooltip("Offset di spawn per alberi innevati (per correggere pivot/ancoraggio).")]
    public Vector3 snowTreeSpawnOffset = Vector3.zero;

    [Tooltip("Offset di spawn per cespugli (per correggere pivot/ancoraggio).")]
    public Vector3 bushSpawnOffset = Vector3.zero;

    [Tooltip("Offset di spawn per rocce prefab (per quando le implementerai).")]
    public Vector3 rockSpawnOffset = Vector3.zero;

    [Header("Decor Placement Jitter")]
    [Tooltip("Se true, applica un leggero offset casuale (deterministico) ai decor prefab spawnati.")]
    public bool decorJitterEnabled = true;

    [Min(0f)]
    [Tooltip("Offset massimo assoluto su asse X per i decor prefab (in unità mondo).")]
    public float decorJitterMaxX = 0.12f;

    [Min(0f)]
    [Tooltip("Offset massimo assoluto su asse Y per i decor prefab (in unità mondo).")]
    public float decorJitterMaxY = 0.08f;

    // -----------------------
    // NEW: Tree variants + anti-monotony
    // -----------------------
    [Header("Tree Variants (Option A)")]
    [Tooltip("Se assegnato, lo script sostituisce la sprite del prefab con una di queste varianti (deterministico).")]
    public Sprite[] treeVariantSprites;

    [Tooltip("Se true, applica flipX deterministico per variare l'aspetto.")]
    public bool treeAllowFlipX = true;

    [Tooltip("Se true, applica una scala deterministica nell'intervallo sotto (per evitare monotonia).")]
    public bool treeAllowScale = true;

    [Tooltip("Intervallo di scala applicato all'albero (es. 0.95..1.05).")]
    public Vector2 treeScaleRange = new Vector2(0.95f, 1.05f);

    [Header("Snow Tree Variants (Option A)")]
    [Tooltip("Se assegnato, lo script sostituisce la sprite del prefab innevato con una di queste varianti (deterministico).")]
    public Sprite[] snowTreeVariantSprites;

    [Tooltip("Se true, applica flipX deterministico per variare l'aspetto (alberi innevati).")]
    public bool snowTreeAllowFlipX = true;

    [Tooltip("Se true, applica una scala deterministica nell'intervallo sotto (alberi innevati).")]
    public bool snowTreeAllowScale = true;

    [Tooltip("Intervallo di scala applicato all'albero innevato (es. 0.95..1.05).")]
    public Vector2 snowTreeScaleRange = new Vector2(0.95f, 1.05f);

    [Header("Bush Variants (Option A)")]
    [Tooltip("Se assegnato, lo script sostituisce la sprite del prefab del cespuglio con una di queste varianti (deterministico).")]
    public Sprite[] bushVariantSprites;

    [Tooltip("Se true, applica flipX deterministico per variare l'aspetto dei cespugli.")]
    public bool bushAllowFlipX = true;

    [Tooltip("Se true, applica una scala deterministica nell'intervallo sotto (per evitare monotonia).")]
    public bool bushAllowScale = true;

    [Tooltip("Intervallo di scala applicato al cespuglio (es. 0.9..1.1).")]
    public Vector2 bushScaleRange = new Vector2(0.9f, 1.1f);

    [Header("Rock Prefab Variants (Option A)")]
    [Tooltip("Se assegnato, lo script sostituisce la sprite del prefab con una di queste varianti (deterministico).")]
    public Sprite[] rockVariantSprites;

    [Tooltip("Se true, applica flipX deterministico per variare l'aspetto.")]
    public bool rockAllowFlipX = true;

    [Tooltip("Se true, applica una scala deterministica nell'intervallo sotto (per evitare monotonia).")]
    public bool rockAllowScale = true;

    [Tooltip("Intervallo di scala applicato alla roccia (es. 0.9..1.1).")]
    public Vector2 rockScaleRange = new Vector2(0.9f, 1.1f);

    [Header("Harvestable Nodes")]
    [Tooltip("Se true, alberi, cespugli e rocce spawnati diventano distruttibili con HP.")]
    public bool makeSpawnedPropsHarvestable = true;

    [Min(1)] public int treeHitPoints = 3;
    [Min(1)] public int snowTreeHitPoints = 3;
    [Min(1)] public int bushHitPoints = 2;
    [Min(1)] public int rockHitPoints = 5;

    [Tooltip("Loot table per alberi normali (opzionale).")]
    public LootTable treeLootTable;

    [Tooltip("Loot table per alberi innevati (opzionale).")]
    public LootTable snowTreeLootTable;

    [Tooltip("Loot table per cespugli (opzionale).")]
    public LootTable bushLootTable;

    [Tooltip("Loot table per rocce (opzionale).")]
    public LootTable rockLootTable;

    [Min(0)]
    [Tooltip("Livello harvesting richiesto per abbattere alberi normali.")]
    public int treeRequiredHarvestLevel = 1;

    [Min(0)]
    [Tooltip("Livello harvesting richiesto per abbattere alberi innevati.")]
    public int snowTreeRequiredHarvestLevel = 1;

    [Min(0)]
    [Tooltip("Livello harvesting richiesto per distruggere cespugli (0 = anche a mani nude).")]
    public int bushRequiredHarvestLevel = 0;

    [Min(0)]
    [Tooltip("Livello harvesting richiesto per rompere rocce.")]
    public int rockRequiredHarvestLevel = 2;

    [Header("Height Noise (Ocean vs Land)")]
    [Tooltip("Higher = more detail; lower = larger blobs")]
    public float heightScale = 6f;
    [Range(1, 10)] public int heightOctaves = 5;
    [Range(0.1f, 0.9f)] public float heightPersistence = 0.5f;
    [Range(1.2f, 4f)] public float heightLacunarity = 2f;
    [Range(0f, 1f)] public float seaLevel = 0.45f;

    [Header("Biome Noise (Plains vs Snomy)")]
    [Tooltip("Lower = larger biomes. Usually lower than heightScale.")]
    public float biomeScale = 2f;
    [Range(1, 10)] public int biomeOctaves = 3;
    [Range(0.1f, 0.9f)] public float biomePersistence = 0.5f;
    [Range(1.2f, 4f)] public float biomeLacunarity = 2f;
    [Range(0f, 1f)] public float snomyThreshold = 0.65f;

    [Header("Decor Density")]
    [Range(0f, 1f)]
    [Tooltip("Chance per erba tile nelle plains (fallback semplice).")]
    public float grassChance = 0.18f;

    [Header("Tree Patches (Plains)")]
    public float treePatchScale = 1.2f;
    [Range(0f, 1f)] public float treePatchThreshold = 0.72f; // alto = poche patch
    [Range(0f, 1f)] public float treeInPatchChance = 0.10f;  // densità dentro patch
    public float treeScatterScale = 14f;

    [Header("Snow Tree Patches (Snomy)")]
    public float snowTreePatchScale = 1.2f;
    [Range(0f, 1f)] public float snowTreePatchThreshold = 0.72f; // alto = poche patch
    [Range(0f, 1f)] public float snowTreeInPatchChance = 0.10f;  // densità dentro patch
    public float snowTreeScatterScale = 14f;

    [Header("Bush Patches (Plains)")]
    [Tooltip("Basso = macchie più grandi di cespugli.")]
    public float bushPatchScale = 1.35f;
    [Range(0f, 1f)] public float bushPatchThreshold = 0.74f;
    [Range(0f, 1f)] public float bushInPatchChance = 0.12f;
    public float bushScatterScale = 13f;

    [Header("Big Rock Patches (Plains)")]
    [Tooltip("Basso = macchie grandi di rocce prefab.")]
    public float rockPrefabPatchScale = 1.1f;
    [Range(0f, 1f)] public float rockPrefabPatchThreshold = 0.78f;
    [Range(0f, 1f)] public float rockPrefabInPatchChance = 0.06f;
    public float rockPrefabScatterScale = 12f;

    [Header("Rules")]
    [Tooltip("If true, do not place decor on cells adjacent to ocean (nice for shorelines).")]
    public bool avoidDecorNearOcean = true;

    [Tooltip("If true, prevents too many trees touching each other (simple neighborhood check).")]
    public bool avoidDecorClumps = true;

    [Range(0, 2)]
    public int clumpRadius = 1;

    private enum GroundType { Ocean, Plains, Snomy }
    private enum DecorType { None, Tree, SnowTree, Bush, BigRock, Grass }

    private struct TileData
    {
        public GroundType ground;
        public DecorType decor;
    }

    private struct PlannedStructureSpawn
    {
        public GameObject prefab;
        public Vector2Int origin;
        public Vector2Int size;
        public Vector3 spawnOffset;
    }

    private struct PlannedChestSpawn
    {
        public StructureGroupDefinition group;
        public Vector2Int origin;
        public Vector2Int size;
    }

    private struct GroupStructurePlacement
    {
        public StructurePrefabEntry entry;
        public Vector2Int origin;
        public Vector2 center;
    }

    private readonly List<PlannedStructureSpawn> _plannedStructureSpawns = new List<PlannedStructureSpawn>();
    private readonly List<PlannedChestSpawn> _plannedChestSpawns = new List<PlannedChestSpawn>();
    private bool[,] _reservedStructureCells;

    [Header("Auto Generate")]
    public bool generateOnStart = true;
    private void Start()
    {
        ApplyPendingWorldCreationSettings();

        if (generateOnStart)
            Generate();
    }

    private void ApplyPendingWorldCreationSettings()
    {
        if (PlayerPrefs.GetInt(PrefHasPendingWorldCreation, 0) != 1)
            return;

        string pendingWorldName = PlayerPrefs.GetString(PrefPendingWorldName, string.Empty);
        seed = PlayerPrefs.GetInt(PrefPendingWorldSeed, seed);

        PlayerPrefs.DeleteKey(PrefPendingWorldName);
        PlayerPrefs.DeleteKey(PrefPendingWorldSeed);
        PlayerPrefs.DeleteKey(PrefPendingWorldId);
        PlayerPrefs.DeleteKey(PrefHasPendingWorldCreation);
        PlayerPrefs.Save();

        Debug.Log("[WorldGenTilemap] Applying pending world setup. Name: '" + pendingWorldName + "' Seed: " + seed, this);
    }


    public void Generate()
    {
        ValidateRefs();
        ClearSpawnedProps();

        Vector2 worldOffset = offset;

        float[,] heightMap = GenerateNoiseMap(width, height, heightScale, heightOctaves, heightPersistence, heightLacunarity, worldOffset, seed);
        float[,] biomeMap = GenerateNoiseMap(width, height, biomeScale, biomeOctaves, biomePersistence, biomeLacunarity, worldOffset + new Vector2(777, 333), seed + 1);

        // --- TREES: patch mask + scatter ---
        float[,] treePatchMap = GenerateNoiseMap(width, height, treePatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2011, 3011), seed + 20);
        float[,] treeScatterMap = GenerateNoiseMap(width, height, treeScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2012, 3012), seed + 21);

        // --- BUSHES: patch mask + scatter ---
        float[,] bushPatchMap = GenerateNoiseMap(width, height, bushPatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2021, 3021), seed + 24);
        float[,] bushScatterMap = GenerateNoiseMap(width, height, bushScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2022, 3022), seed + 25);

        // --- SNOW TREES: patch mask + scatter ---
        float[,] snowTreePatchMap = GenerateNoiseMap(width, height, snowTreePatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2111, 3111), seed + 22);
        float[,] snowTreeScatterMap = GenerateNoiseMap(width, height, snowTreeScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2112, 3112), seed + 23);

        // --- BIG ROCKS: patch mask + scatter ---
        float[,] rockPatchMap = GenerateNoiseMap(width, height, rockPrefabPatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2031, 3031), seed + 40);
        float[,] rockScatterMap = GenerateNoiseMap(width, height, rockPrefabScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2032, 3032), seed + 41);

        // Build tile data first (so rules can look around)
        _generated = new TileData[width, height];
        TileData[,] data = _generated;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float h = heightMap[x, y];

            if (h < seaLevel)
            {
                data[x, y] = new TileData { ground = GroundType.Ocean, decor = DecorType.None };
                continue;
            }

            float b = biomeMap[x, y];
            GroundType g = (b >= snomyThreshold) ? GroundType.Snomy : GroundType.Plains;

            data[x, y] = new TileData { ground = g, decor = DecorType.None };
        }

        // Structure groups first, so decorations become lower-priority and never block them.
        PlanStructureGroups(data);

        // Decor pass (patch-aware)
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (data[x, y].ground == GroundType.Ocean) continue;

            // Keep planned structure/chest footprints clear from later decor generation.
            if (_reservedStructureCells != null && _reservedStructureCells[x, y])
                continue;

            if (avoidDecorNearOcean && IsAdjacentToOcean(data, x, y))
                continue;

            DecorType d = PickDecorPatched(
                data[x, y].ground,
                x, y,
                treePatchMap, treeScatterMap,
                bushPatchMap, bushScatterMap,
                snowTreePatchMap, snowTreeScatterMap,
                rockPatchMap, rockScatterMap
            );

            if (d == DecorType.Tree || d == DecorType.SnowTree || d == DecorType.Bush || d == DecorType.BigRock)
            {
                if (avoidDecorClumps && HasNearbySameDecor(data, x, y, d, clumpRadius))
                    d = DecorType.None;
            }

            data[x, y].decor = d;
        }

        RenderGeneratedData(data);
    }

    public WorldGridData CreateGridSnapshot()
    {
        var snapshot = new WorldGridData();
        snapshot.width = width;
        snapshot.height = height;
        snapshot.seed = seed;

        int total = Mathf.Max(0, width * height);
        snapshot.ground = new int[total];
        snapshot.decor = new int[total];

        if (_generated == null || _generated.GetLength(0) != width || _generated.GetLength(1) != height)
            return snapshot;

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                snapshot.ground[index] = (int)_generated[x, y].ground;
                snapshot.decor[index] = (int)_generated[x, y].decor;
                index++;
            }
        }

        return snapshot;
    }

    public bool TryApplyGridSnapshot(WorldGridData snapshot)
    {
        if (snapshot == null)
            return false;

        if (snapshot.width <= 0 || snapshot.height <= 0)
            return false;

        int total = snapshot.width * snapshot.height;
        if (snapshot.ground == null || snapshot.decor == null)
            return false;

        if (snapshot.ground.Length != total || snapshot.decor.Length != total)
            return false;

        width = snapshot.width;
        height = snapshot.height;
        seed = snapshot.seed;

        var data = new TileData[width, height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                data[x, y] = new TileData
                {
                    ground = DeserializeGround(snapshot.ground[index]),
                    decor = DeserializeDecor(snapshot.decor[index])
                };

                index++;
            }
        }

        _generated = data;
        PlanStructureGroups(data);
        RenderGeneratedData(data);
        return true;
    }

    private void RenderGeneratedData(TileData[,] data)
    {
        if (data == null)
            return;

        groundTilemap.ClearAllTiles();
        decorTilemap.ClearAllTiles();
        if (treeCollisionTilemap != null)
            treeCollisionTilemap.ClearAllTiles();

        ClearSpawnedProps();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3Int cell = new Vector3Int(x, y, 0);

            TileBase groundTile;
            if (data[x, y].ground == GroundType.Ocean)
            {
                groundTile = PickVariant(oceanTiles, x, y, 101);
            }
            else
            {
                groundTile = data[x, y].ground switch
                {
                    GroundType.Plains => PickVariant(plainsTiles, x, y, 102),
                    GroundType.Snomy => PickVariant(snomyTiles, x, y, 103),
                    _ => PickVariant(plainsTiles, x, y, 102)
                };
            }

            groundTilemap.SetTile(cell, groundTile);

            if (data[x, y].decor == DecorType.Tree)
            {
                SpawnTreePrefab(cell);
                if (treeCollisionTilemap != null && treeCollisionTile != null)
                    treeCollisionTilemap.SetTile(cell, treeCollisionTile);
                continue;
            }

            if (data[x, y].decor == DecorType.SnowTree)
            {
                SpawnSnowTreePrefab(cell);
                if (treeCollisionTilemap != null && snowTreeCollisionTile != null)
                    treeCollisionTilemap.SetTile(cell, snowTreeCollisionTile);
                continue;
            }

            if (data[x, y].decor == DecorType.Bush)
            {
                SpawnBushPrefab(cell);
                continue;
            }

            if (data[x, y].decor == DecorType.BigRock)
            {
                SpawnRockPrefab(cell);
                continue;
            }

            TileBase decorTile = data[x, y].decor switch
            {
                DecorType.Grass => PickVariant(grassTiles, x, y, 205),
                _ => null
            };

            if (decorTile != null)
                decorTilemap.SetTile(cell, decorTile);
        }

        SpawnPlannedStructureGroups();

        groundTilemap.CompressBounds();
        decorTilemap.CompressBounds();
        if (treeCollisionTilemap != null)
            treeCollisionTilemap.CompressBounds();

        _hasGenerated = true;
        WorldSpawnPoint = FindWorldSpawnPoint();
    }

    private void PlanStructureGroups(TileData[,] data)
    {
        _plannedStructureSpawns.Clear();
        _plannedChestSpawns.Clear();
        _reservedStructureCells = null;

        if (!spawnStructureGroups || data == null || structureGroups == null || structureGroups.Length == 0)
            return;

        _reservedStructureCells = new bool[width, height];

        int worldSeed = seed;
        HashSet<Biome> plannedBiomes = new HashSet<Biome>();
        for (int i = 0; i < structureGroups.Length; i++)
        {
            StructureGroupDefinition group = structureGroups[i];
            if (!IsValidStructureGroup(group))
                continue;

            if (plannedBiomes.Contains(group.spawnBiome))
                continue;

            int groupSeed = unchecked(worldSeed * 397 ^ (i * 486187739) ^ 0x5f3759df);
            int retries = Mathf.Max(1, group.planningRetries);
            bool planned = false;

            for (int retry = 0; retry < retries && !planned; retry++)
            {
                int retrySeed = unchecked(groupSeed ^ (retry * 92821));
                System.Random rng = new System.Random(retrySeed);
                planned = TryPlanSingleStructureGroup(data, group, rng);
            }

            if (planned)
                plannedBiomes.Add(group.spawnBiome);

            if (!planned)
                Debug.LogWarning("[WorldGen] Impossibile piazzare il gruppo strutture '" + group.groupName + "'.", this);
        }
    }

    private bool TryPlanSingleStructureGroup(TileData[,] data, StructureGroupDefinition group, System.Random rng)
    {
        GroundType targetGround = ToGroundType(group.spawnBiome);
        if (targetGround == GroundType.Ocean)
            return false;

        List<Vector2Int> anchorCandidates = BuildAnchorCandidates(data, targetGround);
        if (anchorCandidates.Count == 0)
            return false;

        ShuffleInPlace(anchorCandidates, rng);

        List<StructurePrefabEntry> pool = BuildValidStructurePool(group);
        if (pool.Count == 0)
            return false;

        int minStructures = Mathf.Max(1, group.minStructures);
        int maxStructures = Mathf.Max(minStructures, group.maxStructures);
        int targetCount = rng.Next(minStructures, maxStructures + 1);

        int anchorAttempts = Mathf.Min(Mathf.Max(1, group.anchorAttempts), anchorCandidates.Count);
        int placeAttempts = Mathf.Max(1, group.placementAttemptsPerStructure);
        int minDistance = Mathf.Max(1, group.minStructureDistanceCells);
        int maxDistance = Mathf.Max(1, group.maxStructureDistanceCells);

        for (int anchorTry = 0; anchorTry < anchorAttempts; anchorTry++)
        {
            Vector2Int anchor = anchorCandidates[anchorTry];

            List<GroupStructurePlacement> placements = new List<GroupStructurePlacement>(targetCount);

            List<StructurePrefabEntry> placementOrder = BuildStructurePlacementOrder(
                pool,
                targetCount,
                rng,
                group.placeLargerStructuresFirst
            );

            for (int slot = 0; slot < placementOrder.Count; slot++)
            {
                bool isFirstPlacement = placements.Count == 0;

                bool added = TryPlaceStructureAroundAnchor(
                    data,
                    targetGround,
                    pool,
                    placements,
                    anchor,
                    maxDistance,
                    minDistance,
                    placeAttempts,
                    rng,
                    isFirstPlacement,
                    placementOrder[slot]
                );

                // Fallback: se l'entry pianificata non entra, prova una scelta libera dal pool.
                if (!added)
                {
                    added = TryPlaceStructureAroundAnchor(
                        data,
                        targetGround,
                        pool,
                        placements,
                        anchor,
                        maxDistance,
                        minDistance,
                        placeAttempts,
                        rng,
                        isFirstPlacement,
                        null
                    );
                }

                if (!added)
                    break;
            }

            int safetyBudget = Mathf.Max(placeAttempts * targetCount, placeAttempts);
            while (placements.Count < targetCount && safetyBudget > 0)
            {
                bool added = TryPlaceStructureAroundAnchor(
                    data,
                    targetGround,
                    pool,
                    placements,
                    anchor,
                    maxDistance,
                    minDistance,
                    placeAttempts,
                    rng,
                    isFirst: placements.Count == 0,
                    forcedEntry: null
                );

                safetyBudget--;
                if (!added && safetyBudget <= 0)
                    break;
            }

            if (placements.Count < minStructures)
                continue;

            if (!TryPlanChestForGroup(data, group, targetGround, placements, out Vector2Int chestOrigin, out Vector2Int chestSize))
                continue;

            CommitGroupPlan(data, group, placements, chestOrigin, chestSize);
            return true;
        }

        return false;
    }

    private bool TryPlanChestForGroup(
        TileData[,] data,
        StructureGroupDefinition group,
        GroundType targetGround,
        List<GroupStructurePlacement> placements,
        out Vector2Int chestOrigin,
        out Vector2Int chestSize)
    {
        chestOrigin = Vector2Int.zero;
        chestSize = Vector2Int.one;

        if (group == null || group.chestPlaceable == null || group.chestPlaceable.placedPrefab == null)
            return false;

        chestSize = ClampSize(group.chestPlaceable.size);

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < placements.Count; i++)
            centroid += placements[i].center;

        centroid /= Mathf.Max(1, placements.Count);
        Vector2Int centroidCell = new Vector2Int(Mathf.RoundToInt(centroid.x), Mathf.RoundToInt(centroid.y));

        int searchRadius = Mathf.Max(0, group.chestSearchRadius);
        int dynamicRadius = Mathf.Max(1, group.maxStructureDistanceCells) + Mathf.Max(chestSize.x, chestSize.y);
        searchRadius = Mathf.Max(searchRadius, dynamicRadius);
        for (int radius = 0; radius <= searchRadius; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                        continue;

                    Vector2Int center = new Vector2Int(centroidCell.x + dx, centroidCell.y + dy);
                    Vector2Int candidateOrigin = CenterToOrigin(center, chestSize);

                    if (!CanPlaceArea(data, targetGround, candidateOrigin, chestSize, allowBlockingGrass: true))
                        continue;

                    if (OverlapsAnyStructure(candidateOrigin, chestSize, placements))
                        continue;

                    chestOrigin = candidateOrigin;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryPlaceStructureAroundAnchor(
        TileData[,] data,
        GroundType targetGround,
        List<StructurePrefabEntry> pool,
        List<GroupStructurePlacement> placements,
        Vector2Int anchor,
        int maxDistance,
        int minDistance,
        int attempts,
        System.Random rng,
        bool isFirst,
        StructurePrefabEntry forcedEntry)
    {
        int tries = Mathf.Max(1, attempts);
        for (int attempt = 0; attempt < tries; attempt++)
        {
            StructurePrefabEntry entry = forcedEntry ?? pool[rng.Next(pool.Count)];
            Vector2Int size = ClampSize(entry.size);

            Vector2Int center;
            if (isFirst && attempt == 0)
            {
                center = anchor;
            }
            else
            {
                center = RandomCenterAroundAnchor(anchor, maxDistance, rng);
            }

            Vector2Int origin = CenterToOrigin(center, size);

            if (!CanPlaceArea(data, targetGround, origin, size, allowBlockingGrass: true))
                continue;

            if (!RespectsStructureDistanceRules(origin, size, placements, minDistance, maxDistance))
                continue;

            GroupStructurePlacement placement = new GroupStructurePlacement
            {
                entry = entry,
                origin = origin,
                center = GetRectCenter(origin, size)
            };

            placements.Add(placement);
            return true;
        }

        return false;
    }

    private void CommitGroupPlan(
        TileData[,] data,
        StructureGroupDefinition group,
        List<GroupStructurePlacement> placements,
        Vector2Int chestOrigin,
        Vector2Int chestSize)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            GroupStructurePlacement p = placements[i];
            ReserveArea(data, p.origin, ClampSize(p.entry.size));

            _plannedStructureSpawns.Add(new PlannedStructureSpawn
            {
                prefab = p.entry.prefab,
                origin = p.origin,
                size = ClampSize(p.entry.size),
                spawnOffset = p.entry.spawnOffset
            });
        }

        ReserveArea(data, chestOrigin, chestSize);
        _plannedChestSpawns.Add(new PlannedChestSpawn
        {
            group = group,
            origin = chestOrigin,
            size = chestSize
        });
    }

    private void SpawnPlannedStructureGroups()
    {
        if (_plannedStructureSpawns.Count == 0 && _plannedChestSpawns.Count == 0)
            return;

        TileBase structureBlockTile = structureCollisionTile != null ? structureCollisionTile : treeCollisionTile;
        TileBase chestBlockTile = chestCollisionTile != null
            ? chestCollisionTile
            : (structureCollisionTile != null ? structureCollisionTile : treeCollisionTile);

        for (int i = 0; i < _plannedStructureSpawns.Count; i++)
        {
            PlannedStructureSpawn planned = _plannedStructureSpawns[i];
            if (planned.prefab == null)
                continue;

            Vector3 spawnPos = GetAreaWorldCenter(planned.origin, planned.size) + planned.spawnOffset;
            Transform parent = propsParent != null ? propsParent : transform;
            GameObject go = Instantiate(planned.prefab, spawnPos, Quaternion.identity, parent);
            RemoveHarvestableIfAny(go);

            if (treeCollisionTilemap != null && structureBlockTile != null)
                FillCollisionArea(planned.origin, planned.size, structureBlockTile);
        }

        for (int i = 0; i < _plannedChestSpawns.Count; i++)
        {
            PlannedChestSpawn plannedChest = _plannedChestSpawns[i];
            SpawnPlannedChest(plannedChest);

            if (treeCollisionTilemap != null && chestBlockTile != null)
                FillCollisionArea(plannedChest.origin, plannedChest.size, chestBlockTile);
        }
    }

    private void SpawnPlannedChest(PlannedChestSpawn planned)
    {
        StructureGroupDefinition group = planned.group;
        if (group == null || group.chestPlaceable == null || group.chestPlaceable.placedPrefab == null)
            return;

        Vector3 spawnPos = GetAreaWorldCenter(planned.origin, planned.size) + group.chestPlaceable.spawnOffset;
        Transform parent = propsParent != null ? propsParent : transform;
        GameObject go = Instantiate(group.chestPlaceable.placedPrefab, spawnPos, Quaternion.identity, parent);
        RemoveHarvestableIfAny(go);

        PlacedObject placed = go.GetComponent<PlacedObject>();
        if (placed == null)
            placed = go.AddComponent<PlacedObject>();

        placed.Initialize(group.chestPlaceable, planned.origin, planned.size);

        if (group.chestPlaceable.useYSort)
        {
            YSort ySort = go.GetComponent<YSort>();
            if (ySort == null && go.GetComponentInChildren<YSort>() == null)
                ySort = go.AddComponent<YSort>();

            if (ySort != null)
                ySort.orderOffset = group.chestPlaceable.ySortOrderOffset;
        }
        else
        {
            YSort ySort = go.GetComponent<YSort>();
            if (ySort != null)
                Destroy(ySort);
        }

        ChestInventoryStorage chest = go.GetComponent<ChestInventoryStorage>();
        if (chest == null)
            chest = go.AddComponent<ChestInventoryStorage>();

        FillChestWithFixedLoot(chest, group.chestFixedLoot);
    }

    private void FillChestWithFixedLoot(ChestInventoryStorage chest, FixedChestLootEntry[] loot)
    {
        if (chest == null || chest.Section == null || loot == null)
            return;

        chest.Section.Clear();

        for (int i = 0; i < loot.Length; i++)
        {
            FixedChestLootEntry entry = loot[i];
            if (entry == null || entry.item == null || entry.amount <= 0)
                continue;

            chest.Section.TryAdd(new ItemStack(entry.item, entry.amount));
        }
    }

    private void RemoveHarvestableIfAny(GameObject go)
    {
        if (go == null) return;

        HarvestableNode[] nodes = go.GetComponentsInChildren<HarvestableNode>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            HarvestableNode node = nodes[i];
            if (node == null) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(node);
            else
#endif
                Destroy(node);
        }
    }

    private bool IsValidStructureGroup(StructureGroupDefinition group)
    {
        if (group == null || !group.enabled)
            return false;

        if (group.chestPlaceable == null || group.chestPlaceable.placedPrefab == null)
            return false;

        List<StructurePrefabEntry> pool = BuildValidStructurePool(group);
        return pool.Count > 0;
    }

    private List<StructurePrefabEntry> BuildValidStructurePool(StructureGroupDefinition group)
    {
        List<StructurePrefabEntry> pool = new List<StructurePrefabEntry>();
        if (group == null || group.structurePrefabs == null)
            return pool;

        for (int i = 0; i < group.structurePrefabs.Length; i++)
        {
            StructurePrefabEntry entry = group.structurePrefabs[i];
            if (entry == null || entry.prefab == null)
                continue;

            entry.size = ClampSize(entry.size);
            pool.Add(entry);
        }

        return pool;
    }

    private bool CanPlaceArea(TileData[,] data, GroundType targetGround, Vector2Int origin, Vector2Int size, bool allowBlockingGrass)
    {
        for (int y = origin.y; y < origin.y + size.y; y++)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                if (!IsInside(x, y))
                    return false;

                if (data[x, y].ground != targetGround)
                    return false;

                if (_reservedStructureCells != null && _reservedStructureCells[x, y])
                    return false;

                DecorType decor = data[x, y].decor;
                if (IsSolidDecor(decor))
                    return false;

                if (!allowBlockingGrass && decor == DecorType.Grass)
                    return false;
            }
        }

        return true;
    }

    private bool RespectsStructureDistanceRules(
        Vector2Int origin,
        Vector2Int size,
        List<GroupStructurePlacement> placements,
        int minDistance,
        int maxDistance)
    {
        if (placements == null || placements.Count == 0)
            return true;

        Vector2 center = GetRectCenter(origin, size);

        for (int i = 0; i < placements.Count; i++)
        {
            GroupStructurePlacement placed = placements[i];
            Vector2Int placedSize = ClampSize(placed.entry.size);

            int chebyshev = RectChebyshevDistance(origin, size, placed.origin, placedSize);
            if (chebyshev < minDistance)
                return false;

            if (Vector2.Distance(center, placed.center) > maxDistance)
                return false;
        }

        return true;
    }

    private static int GetStructureEntryArea(StructurePrefabEntry entry)
    {
        if (entry == null)
            return 1;

        Vector2Int size = ClampSize(entry.size);
        return Mathf.Max(1, size.x * size.y);
    }

    private List<StructurePrefabEntry> BuildStructurePlacementOrder(
        List<StructurePrefabEntry> pool,
        int targetCount,
        System.Random rng,
        bool placeLargerFirst)
    {
        List<StructurePrefabEntry> order = new List<StructurePrefabEntry>(Mathf.Max(0, targetCount));
        if (pool == null || pool.Count == 0 || targetCount <= 0)
            return order;

        if (placeLargerFirst)
        {
            List<StructurePrefabEntry> sorted = new List<StructurePrefabEntry>(pool);
            sorted.Sort((a, b) =>
            {
                int areaA = GetStructureEntryArea(a);
                int areaB = GetStructureEntryArea(b);
                return areaB.CompareTo(areaA);
            });

            // First pass: try largest unique prefabs first.
            int firstPass = Mathf.Min(targetCount, sorted.Count);
            for (int i = 0; i < firstPass; i++)
                order.Add(sorted[i]);

            // Fill remaining slots with random picks (can repeat).
            for (int i = firstPass; i < targetCount; i++)
                order.Add(sorted[rng.Next(sorted.Count)]);

            return order;
        }

        for (int i = 0; i < targetCount; i++)
            order.Add(pool[rng.Next(pool.Count)]);

        return order;
    }

    private bool OverlapsAnyStructure(Vector2Int origin, Vector2Int size, List<GroupStructurePlacement> placements)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            GroupStructurePlacement p = placements[i];
            if (RectanglesOverlap(origin, size, p.origin, ClampSize(p.entry.size)))
                return true;
        }

        return false;
    }

    private void ReserveArea(TileData[,] data, Vector2Int origin, Vector2Int size)
    {
        for (int y = origin.y; y < origin.y + size.y; y++)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                if (!IsInside(x, y))
                    continue;

                if (_reservedStructureCells != null)
                    _reservedStructureCells[x, y] = true;

                if (data[x, y].decor == DecorType.Grass)
                    data[x, y].decor = DecorType.None;
            }
        }
    }

    private void FillCollisionArea(Vector2Int origin, Vector2Int size, TileBase tile)
    {
        if (treeCollisionTilemap == null || tile == null)
            return;

        for (int y = origin.y; y < origin.y + size.y; y++)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                if (!IsInside(x, y))
                    continue;

                treeCollisionTilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private bool IsCellSuitableForGroup(TileData[,] data, int x, int y, GroundType targetGround)
    {
        if (!IsInside(x, y))
            return false;

        if (data[x, y].ground != targetGround)
            return false;

        if (_reservedStructureCells != null && _reservedStructureCells[x, y])
            return false;

        return !IsSolidDecor(data[x, y].decor);
    }

    private List<Vector2Int> BuildAnchorCandidates(TileData[,] data, GroundType targetGround)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsCellSuitableForGroup(data, x, y, targetGround))
                    result.Add(new Vector2Int(x, y));
            }
        }

        return result;
    }

    private static void ShuffleInPlace<T>(List<T> list, System.Random rng)
    {
        if (list == null || rng == null)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private GroundType ToGroundType(Biome biome)
    {
        return biome switch
        {
            Biome.Plains => GroundType.Plains,
            Biome.Snomy => GroundType.Snomy,
            _ => GroundType.Ocean
        };
    }

    private static bool IsSolidDecor(DecorType decor)
    {
        return decor == DecorType.Tree ||
               decor == DecorType.SnowTree ||
               decor == DecorType.Bush ||
               decor == DecorType.BigRock;
    }

    private static Vector2Int ClampSize(Vector2Int size)
    {
        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    private static Vector2 GetRectCenter(Vector2Int origin, Vector2Int size)
    {
        return new Vector2(
            origin.x + (size.x - 1) * 0.5f,
            origin.y + (size.y - 1) * 0.5f
        );
    }

    private static Vector2Int CenterToOrigin(Vector2Int center, Vector2Int size)
    {
        int ox = Mathf.RoundToInt(center.x - (size.x - 1) * 0.5f);
        int oy = Mathf.RoundToInt(center.y - (size.y - 1) * 0.5f);
        return new Vector2Int(ox, oy);
    }

    private static bool RectanglesOverlap(Vector2Int aOrigin, Vector2Int aSize, Vector2Int bOrigin, Vector2Int bSize)
    {
        int aMinX = aOrigin.x;
        int aMaxX = aOrigin.x + aSize.x - 1;
        int aMinY = aOrigin.y;
        int aMaxY = aOrigin.y + aSize.y - 1;

        int bMinX = bOrigin.x;
        int bMaxX = bOrigin.x + bSize.x - 1;
        int bMinY = bOrigin.y;
        int bMaxY = bOrigin.y + bSize.y - 1;

        bool separated = aMaxX < bMinX || bMaxX < aMinX || aMaxY < bMinY || bMaxY < aMinY;
        return !separated;
    }

    private static int RectChebyshevDistance(Vector2Int aOrigin, Vector2Int aSize, Vector2Int bOrigin, Vector2Int bSize)
    {
        int aMinX = aOrigin.x;
        int aMaxX = aOrigin.x + aSize.x - 1;
        int aMinY = aOrigin.y;
        int aMaxY = aOrigin.y + aSize.y - 1;

        int bMinX = bOrigin.x;
        int bMaxX = bOrigin.x + bSize.x - 1;
        int bMinY = bOrigin.y;
        int bMaxY = bOrigin.y + bSize.y - 1;

        int dx = 0;
        if (aMaxX < bMinX) dx = bMinX - aMaxX;
        else if (bMaxX < aMinX) dx = aMinX - bMaxX;

        int dy = 0;
        if (aMaxY < bMinY) dy = bMinY - aMaxY;
        else if (bMaxY < aMinY) dy = aMinY - bMaxY;

        return Mathf.Max(dx, dy);
    }

    private static Vector2Int RandomCenterAroundAnchor(Vector2Int anchor, int maxDistance, System.Random rng)
    {
        int range = Mathf.Max(1, maxDistance);
        int dx = 0;
        int dy = 0;

        for (int guard = 0; guard < 20; guard++)
        {
            dx = rng.Next(-range, range + 1);
            dy = rng.Next(-range, range + 1);

            int chebyshev = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            if (chebyshev >= 1 && chebyshev <= range)
                break;
        }

        return new Vector2Int(anchor.x + dx, anchor.y + dy);
    }

    private Vector3 GetAreaWorldCenter(Vector2Int origin, Vector2Int size)
    {
        Vector3 bottomLeft = CellCenterWorld(origin.x, origin.y);
        Vector3 topRight = CellCenterWorld(origin.x + size.x - 1, origin.y + size.y - 1);
        return (bottomLeft + topRight) * 0.5f;
    }

    private static GroundType DeserializeGround(int value)
    {
        if (value < 0 || value > (int)GroundType.Snomy)
            return GroundType.Ocean;

        return (GroundType)value;
    }

    private static DecorType DeserializeDecor(int value)
    {
        if (value < 0 || value > (int)DecorType.Grass)
            return DecorType.None;

        return (DecorType)value;
    }

    /// <summary>
    /// Cerca una cella valida per lo spawn del player il piu vicino possibile a world (0,0),
    /// espandendo la ricerca ad anelli verso l'esterno.
    /// La cella deve essere terra (non Ocean) e non bloccata (alberi/rocce).
    /// </summary>
    private Vector3 FindWorldSpawnPoint()
    {
        Vector3Int worldOriginCell = groundTilemap != null
            ? groundTilemap.WorldToCell(Vector3.zero)
            : new Vector3Int(0, 0, 0);

        int sx = Mathf.Clamp(worldOriginCell.x, 0, Mathf.Max(0, width - 1));
        int sy = Mathf.Clamp(worldOriginCell.y, 0, Mathf.Max(0, height - 1));

        // Prova prima la cella mappa corrispondente a world (0,0).
        if (IsValidSpawnCell(sx, sy))
            return CellCenterWorld(sx, sy);

        // Ricerca ad anelli (distanza crescente da world 0,0).
        int maxRadius = Mathf.Max(width, height);
        for (int r = 1; r < maxRadius; r++)
        {
            int minX = sx - r;
            int maxX = sx + r;
            int minY = sy - r;
            int maxY = sy + r;

            // Bordo alto + basso
            for (int x = minX; x <= maxX; x++)
            {
                if (IsValidSpawnCell(x, minY))
                    return CellCenterWorld(x, minY);

                if (minY != maxY && IsValidSpawnCell(x, maxY))
                    return CellCenterWorld(x, maxY);
            }

            // Bordo sinistro + destro (senza ricontrollare gli angoli)
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                if (IsValidSpawnCell(minX, y))
                    return CellCenterWorld(minX, y);

                if (minX != maxX && IsValidSpawnCell(maxX, y))
                    return CellCenterWorld(maxX, y);
            }
        }

        // Fallback: cella interna piu vicina a world (0,0).
        Debug.LogWarning("[WorldGen] Nessuna cella valida per spawn! Uso la cella piu vicina a world (0,0).");
        return CellCenterWorld(sx, sy);
    }

    private bool IsValidSpawnCell(int x, int y)
    {
        if (!IsInside(x, y)) return false;
        if (!IsLandCell(x, y)) return false;
        if (IsBlockedCell(x, y)) return false;
        return true;
    }

    public void Clear()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (decorTilemap != null) decorTilemap.ClearAllTiles();
        if (treeCollisionTilemap != null) treeCollisionTilemap.ClearAllTiles();
        _plannedStructureSpawns.Clear();
        _plannedChestSpawns.Clear();
        _reservedStructureCells = null;
        ClearSpawnedProps();
    }

    private void SpawnTreePrefab(Vector3Int cell)
    {
        if (treePrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + treeSpawnOffset + GetDecorJitter(cell, 12001, 12002);
        Transform parent = propsParent != null ? propsParent : transform;

        GameObject go = Instantiate(treePrefab, pos, Quaternion.identity, parent);

        // 1) scegli variante sprite (Option A)
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && treeVariantSprites != null && treeVariantSprites.Length > 0)
        {
            int idx = Hash(cell.x, cell.y, 7777) % treeVariantSprites.Length;
            sr.sprite = treeVariantSprites[idx];
        }

        // 2) flipX deterministico
        if (sr != null && treeAllowFlipX)
        {
            bool flip = (Hash(cell.x, cell.y, 8888) & 1) == 1;
            sr.flipX = flip;
        }

        if (treeAllowScale)
        {
            float minS = Mathf.Min(treeScaleRange.x, treeScaleRange.y);
            float maxS = Mathf.Max(treeScaleRange.x, treeScaleRange.y);
            minS = Mathf.Max(0.01f, minS);
            maxS = Mathf.Max(minS, maxS);

            float t = White01(cell.x, cell.y, 9999); // 0..1
            float s = Mathf.Lerp(minS, maxS, t);

            go.transform.localScale = new Vector3(s, s, 1f);
        }

        ConfigureHarvestableNode(go, treeHitPoints, treeLootTable, treeRequiredHarvestLevel);
    }

    private void SpawnBushPrefab(Vector3Int cell)
    {
        if (bushPrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + bushSpawnOffset + GetDecorJitter(cell, 13001, 13002);
        Transform parent = propsParent != null ? propsParent : transform;

        GameObject go = Instantiate(bushPrefab, pos, Quaternion.identity, parent);

        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && bushVariantSprites != null && bushVariantSprites.Length > 0)
        {
            int idx = Hash(cell.x, cell.y, 47777) % bushVariantSprites.Length;
            sr.sprite = bushVariantSprites[idx];
        }

        if (sr != null && bushAllowFlipX)
        {
            bool flip = (Hash(cell.x, cell.y, 48888) & 1) == 1;
            sr.flipX = flip;
        }

        if (bushAllowScale)
        {
            float minS = Mathf.Min(bushScaleRange.x, bushScaleRange.y);
            float maxS = Mathf.Max(bushScaleRange.x, bushScaleRange.y);
            minS = Mathf.Max(0.01f, minS);
            maxS = Mathf.Max(minS, maxS);

            float t = White01(cell.x, cell.y, 49999);
            float s = Mathf.Lerp(minS, maxS, t);

            go.transform.localScale = new Vector3(s, s, 1f);
        }

        EnsureBushHitCollider(go);

        ConfigureHarvestableNode(go, bushHitPoints, bushLootTable, bushRequiredHarvestLevel);
    }

    private void SpawnSnowTreePrefab(Vector3Int cell)
    {
        if (snowTreePrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + snowTreeSpawnOffset + GetDecorJitter(cell, 14001, 14002);
        Transform parent = propsParent != null ? propsParent : transform;

        GameObject go = Instantiate(snowTreePrefab, pos, Quaternion.identity, parent);

        // 1) scegli variante sprite (Option A)
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && snowTreeVariantSprites != null && snowTreeVariantSprites.Length > 0)
        {
            int idx = Hash(cell.x, cell.y, 27777) % snowTreeVariantSprites.Length;
            sr.sprite = snowTreeVariantSprites[idx];
        }

        // 2) flipX deterministico
        if (sr != null && snowTreeAllowFlipX)
        {
            bool flip = (Hash(cell.x, cell.y, 28888) & 1) == 1;
            sr.flipX = flip;
        }

        // 3) scala deterministica
        if (snowTreeAllowScale)
        {
            float minS = Mathf.Min(snowTreeScaleRange.x, snowTreeScaleRange.y);
            float maxS = Mathf.Max(snowTreeScaleRange.x, snowTreeScaleRange.y);
            minS = Mathf.Max(0.01f, minS);
            maxS = Mathf.Max(minS, maxS);

            float t = White01(cell.x, cell.y, 29999); // 0..1
            float s = Mathf.Lerp(minS, maxS, t);

            go.transform.localScale = new Vector3(s, s, 1f);
        }

        ConfigureHarvestableNode(go, snowTreeHitPoints, snowTreeLootTable, snowTreeRequiredHarvestLevel);
    }

    private void SpawnRockPrefab(Vector3Int cell)
    {
        if (rockPrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + rockSpawnOffset + GetDecorJitter(cell, 15001, 15002);
        Transform parent = propsParent != null ? propsParent : transform;

        GameObject go = Instantiate(rockPrefab, pos, Quaternion.identity, parent);

        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && rockVariantSprites != null && rockVariantSprites.Length > 0)
        {
            int idx = Hash(cell.x, cell.y, 17777) % rockVariantSprites.Length;
            sr.sprite = rockVariantSprites[idx];
        }

        // 2) flipX deterministico
        if (sr != null && rockAllowFlipX)
        {
            bool flip = (Hash(cell.x, cell.y, 18888) & 1) == 1;
            sr.flipX = flip;
        }

        // 3) scala deterministica
        if (rockAllowScale)
        {
            float minS = Mathf.Min(rockScaleRange.x, rockScaleRange.y);
            float maxS = Mathf.Max(rockScaleRange.x, rockScaleRange.y);
            minS = Mathf.Max(0.01f, minS);
            maxS = Mathf.Max(minS, maxS);

            float t = White01(cell.x, cell.y, 19999);
            float s = Mathf.Lerp(minS, maxS, t);

            go.transform.localScale = new Vector3(s, s, 1f);
        }

        ConfigureHarvestableNode(go, rockHitPoints, rockLootTable, rockRequiredHarvestLevel);
    }

    private void ConfigureHarvestableNode(GameObject go, int hp, LootTable lootTable, int requiredHarvestLevel)
    {
        if (!makeSpawnedPropsHarvestable || go == null)
            return;

        HarvestableNode node = go.GetComponent<HarvestableNode>();
        if (node == null)
            node = go.AddComponent<HarvestableNode>();

        node.requiredHarvestLevel = Mathf.Max(0, requiredHarvestLevel);
        node.requireHarvestTool = node.requiredHarvestLevel > 0;
        node.destroyOnDeath = true;
        node.lootTable = lootTable;
        node.SetMaxHpAndReset(Mathf.Max(1, hp));
    }

    private void ClearSpawnedProps()
    {
        Transform parent = propsParent != null ? propsParent : null;
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(parent.GetChild(i).gameObject);
#else
            Destroy(parent.GetChild(i).gameObject);
#endif
        }
    }

    private void EnsureBushHitCollider(GameObject go)
    {
        if (go == null) return;

        Collider2D col = go.GetComponentInChildren<Collider2D>();
        if (col == null)
            col = go.AddComponent<BoxCollider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    // -----------------------
    // Deterministic helpers
    // -----------------------
    private int Hash(int x, int y, int salt)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + x;
            h = h * 31 + y;
            h = h * 31 + salt;
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
            return h & int.MaxValue;
        }
    }

    private float White01(int x, int y, int salt)
    {
        return (Hash(x, y, salt) % 100000) / 100000f;
    }

    private float WhiteSigned(int x, int y, int salt)
    {
        return White01(x, y, salt) * 2f - 1f;
    }

    private Vector3 GetDecorJitter(Vector3Int cell, int saltX, int saltY)
    {
        if (!decorJitterEnabled)
            return Vector3.zero;

        float maxX = Mathf.Max(0f, decorJitterMaxX);
        float maxY = Mathf.Max(0f, decorJitterMaxY);
        if (maxX <= 0f && maxY <= 0f)
            return Vector3.zero;

        float jx = WhiteSigned(cell.x, cell.y, saltX) * maxX;
        float jy = WhiteSigned(cell.x, cell.y, saltY) * maxY;
        return new Vector3(jx, jy, 0f);
    }

    private TileBase PickVariant(TileBase[] variants, int x, int y, int salt)
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];

        int idx = Hash(x, y, salt) % variants.Length;
        return variants[idx];
    }

    private bool HasAny(TileBase[] variants) => variants != null && variants.Length > 0;

    // -----------------------
    // Decor selection (PATCHED)
    // -----------------------
    private DecorType PickDecorPatched(
        GroundType ground,
        int x, int y,
        float[,] treePatchMap, float[,] treeScatterMap,
        float[,] bushPatchMap, float[,] bushScatterMap,
        float[,] snowTreePatchMap, float[,] snowTreeScatterMap,
        float[,] rockPatchMap, float[,] rockScatterMap
    )
    {
        // 1) PLAINS
        if (ground == GroundType.Plains)
        {
            // TREES: patch mask + scatter (solo se hai assegnato il prefab)
            if (treePrefab != null)
            {
                float patch = treePatchMap[x, y];
                float scatter = treeScatterMap[x, y];

                if (patch > treePatchThreshold && scatter < treeInPatchChance)
                    return DecorType.Tree;
            }

            // BUSHES: patch mask + scatter (cespugli di pianura)
            if (bushPrefab != null)
            {
                float patch = bushPatchMap[x, y];
                float scatter = bushScatterMap[x, y];

                if (patch > bushPatchThreshold && scatter < bushInPatchChance)
                    return DecorType.Bush;
            }

            // BIG ROCKS: patch mask + scatter (prefab)
            if (rockPrefab != null)
            {
                float patch = rockPatchMap[x, y];
                float scatter = rockScatterMap[x, y];

                if (patch > rockPrefabPatchThreshold && scatter < rockPrefabInPatchChance)
                    return DecorType.BigRock;
            }

            // MINOR: GRASS (white noise)
            if (HasAny(grassTiles))
            {
                float w = White01(x, y, 9001);
                if (w < grassChance)
                    return DecorType.Grass;
            }

            return DecorType.None;
        }

        // 2) SNOMY
        if (ground == GroundType.Snomy)
        {
            // SNOW TREES: patch mask + scatter (solo se hai assegnato il prefab)
            if (snowTreePrefab != null)
            {
                float patch = snowTreePatchMap[x, y];
                float scatter = snowTreeScatterMap[x, y];

                if (patch > snowTreePatchThreshold && scatter < snowTreeInPatchChance)
                    return DecorType.SnowTree;
            }

            return DecorType.None;
        }

        return DecorType.None;
    }

    // -----------------------
    // Decor rules
    // -----------------------
    private bool IsAdjacentToOcean(TileData[,] data, int x, int y)
    {
        return IsOcean(data, x + 1, y) ||
               IsOcean(data, x - 1, y) ||
               IsOcean(data, x, y + 1) ||
               IsOcean(data, x, y - 1);
    }

    private bool IsOcean(TileData[,] data, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return false;
        return data[x, y].ground == GroundType.Ocean;
    }

    private bool HasNearbySameDecor(TileData[,] data, int x, int y, DecorType d, int radius)
    {
        for (int yy = y - radius; yy <= y + radius; yy++)
        for (int xx = x - radius; xx <= x + radius; xx++)
        {
            if (xx == x && yy == y) continue;
            if (xx < 0 || yy < 0 || xx >= width || yy >= height) continue;
            if (data[xx, yy].decor == d) return true;
        }
        return false;
    }

    // -----------------------
    // Noise (Perlin multi-octave)
    // -----------------------
    private static float[,] GenerateNoiseMap(
        int width, int height,
        float scale,
        int octaves,
        float persistence,
        float lacunarity,
        Vector2 offset,
        int seed
    )
    {
        float[,] map = new float[width, height];

        if (scale <= 0f) scale = 0.0001f;

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offX = prng.Next(-100000, 100000) + offset.x;
            float offY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offX, offY);
        }

        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (x / (float)width) * scale * frequency + octaveOffsets[i].x;
                float sampleY = (y / (float)height) * scale * frequency + octaveOffsets[i].y;

                float perlin = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f; // [-1,1]
                noiseValue += perlin * amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            if (noiseValue < minVal) minVal = noiseValue;
            if (noiseValue > maxVal) maxVal = noiseValue;

            map[x, y] = noiseValue;
        }

        // Normalize to [0,1]
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            map[x, y] = Mathf.InverseLerp(minVal, maxVal, map[x, y]);

        return map;
    }

    public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    public bool IsOceanCell(int x, int y)
    {
        if (!_hasGenerated || !IsInside(x, y)) return false;
        return _generated[x, y].ground == GroundType.Ocean;
    }

    public bool IsLandCell(int x, int y)
    {
        if (!_hasGenerated || !IsInside(x, y)) return false;
        return _generated[x, y].ground != GroundType.Ocean;
    }

    public Biome GetBiome(int x, int y)
    {
        if (!_hasGenerated || !IsInside(x, y)) return Biome.Ocean;

        return _generated[x, y].ground switch
        {
            GroundType.Ocean => Biome.Ocean,
            GroundType.Plains => Biome.Plains,
            GroundType.Snomy => Biome.Snomy,
            _ => Biome.Ocean
        };
    }

    public bool IsBlockedCell(int x, int y)
    {
        if (treeCollisionTilemap == null) return false;
        var cell = new Vector3Int(x, y, 0);
        return treeCollisionTilemap.GetTile(cell) != null;
    }

    public Vector3 CellCenterWorld(int x, int y)
    {
        return groundTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }

    private void ValidateRefs()
    {
        if (groundTilemap == null) throw new Exception("Assign Ground Tilemap in inspector.");
        if (decorTilemap == null) throw new Exception("Assign Decor Tilemap in inspector.");

        if (!HasAny(plainsTiles) || !HasAny(snomyTiles))
            throw new Exception("Assign ground tile arrays (at least 1 each): plainsTiles, snomyTiles.");

        if (!HasAny(oceanTiles))
            throw new Exception("Assign oceanTiles (at least 1).");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WorldGenTilemap))]
public class WorldGenTilemapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (WorldGenTilemap)target;

        GUILayout.Space(8);

        if (GUILayout.Button("Generate"))
            gen.Generate();

        if (GUILayout.Button("Clear"))
            gen.Clear();

        if (GUILayout.Button("Regenerate (Clear + Generate)"))
        {
            gen.Clear();
            gen.Generate();
        }
    }
}
#endif