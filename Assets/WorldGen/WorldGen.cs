using System;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldGenTilemap : MonoBehaviour
{
    private const string PrefPendingWorldName = "MainMenu.PendingWorldName";
    private const string PrefPendingWorldSeed = "MainMenu.PendingWorldSeed";
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

        // Decor pass (patch-aware)
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (data[x, y].ground == GroundType.Ocean) continue;

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

        // Render
        groundTilemap.ClearAllTiles();
        decorTilemap.ClearAllTiles();
        if (treeCollisionTilemap != null)
            treeCollisionTilemap.ClearAllTiles();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3Int cell = new Vector3Int(x, y, 0);

            // --- ground ---
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

            // --- decor / props ---
            if (data[x, y].decor == DecorType.Tree)
            {
                SpawnTreePrefab(cell);
                if (treeCollisionTilemap != null && treeCollisionTile != null)
                    treeCollisionTilemap.SetTile(cell, treeCollisionTile);
                continue; // non mettere tile albero
            }

            if (data[x, y].decor == DecorType.SnowTree)
            {
                SpawnSnowTreePrefab(cell);
                if (treeCollisionTilemap != null && snowTreeCollisionTile != null)
                    treeCollisionTilemap.SetTile(cell, snowTreeCollisionTile);
                continue; // non mettere tile albero
            }

            if (data[x, y].decor == DecorType.Bush)
            {
                SpawnBushPrefab(cell);
                continue; // non mettere tile cespuglio
            }

            if (data[x, y].decor == DecorType.BigRock)
            {
                SpawnRockPrefab(cell);
                continue; // non mettere tile roccia grande
            }

            TileBase decorTile = data[x, y].decor switch
            {
                DecorType.Grass => PickVariant(grassTiles, x, y, 205),
                _ => null
            };

            if (decorTile != null)
                decorTilemap.SetTile(cell, decorTile);
        }

        // CompressBounds DOPO aver piazzato le tile
        groundTilemap.CompressBounds();
        decorTilemap.CompressBounds();
        if (treeCollisionTilemap != null)
            treeCollisionTilemap.CompressBounds();

        _hasGenerated = true;

        // Calcola il punto di spawn del player
        WorldSpawnPoint = FindWorldSpawnPoint();
    }

    /// <summary>
    /// Cerca una cella valida per lo spawn del player partendo dal centro della mappa
    /// e muovendosi a spirale verso l'esterno. La cella deve essere terra (non Ocean),
    /// non bloccata (alberi/rocce) e non adiacente all'oceano.
    /// </summary>
    private Vector3 FindWorldSpawnPoint()
    {
        int cx = width / 2;
        int cy = height / 2;

        // Prova il centro prima
        if (IsValidSpawnCell(cx, cy))
            return CellCenterWorld(cx, cy);

        // Spirale dal centro verso l'esterno
        int maxRadius = Mathf.Max(width, height);
        for (int r = 1; r < maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int[] dyValues = (Mathf.Abs(dx) == r)
                    ? GetRange(-r, r)
                    : new int[] { -r, r };

                foreach (int dy in dyValues)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (IsValidSpawnCell(x, y))
                        return CellCenterWorld(x, y);
                }
            }
        }

        // Fallback: centro mappa
        Debug.LogWarning("[WorldGen] Nessuna cella valida per spawn! Usando il centro della mappa.");
        return CellCenterWorld(cx, cy);
    }

    private bool IsValidSpawnCell(int x, int y)
    {
        if (!IsInside(x, y)) return false;
        if (!IsLandCell(x, y)) return false;
        if (IsBlockedCell(x, y)) return false;
        if (_generated != null && IsAdjacentToOcean(_generated, x, y)) return false;
        return true;
    }

    private static int[] GetRange(int from, int to)
    {
        int count = to - from + 1;
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = from + i;
        return result;
    }

    public void Clear()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (decorTilemap != null) decorTilemap.ClearAllTiles();
        if (treeCollisionTilemap != null) treeCollisionTilemap.ClearAllTiles();
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