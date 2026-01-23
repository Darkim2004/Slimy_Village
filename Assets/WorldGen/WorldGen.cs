using System;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldGenTilemap : MonoBehaviour
{
    [Header("Tilemaps (same Grid)")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap decorTilemap;

    [Header("World Size (prototype, no chunks yet)")]
    [Min(1)] public int width = 128;
    [Min(1)] public int height = 128;

    [Header("Seed & Offset")]
    public int seed = 12345;
    public Vector2 offset; // useful to "move" the world without changing seed

    // -----------------------
    // Ocean palette bands (shallow/deep palettes)
    // -----------------------
    [Serializable]
    public class PaletteBand
    {
        public string name;
        [Range(0f, 1f)] public float minInclusive = 0f;
        [Range(0f, 1f)] public float maxExclusive = 1f;

        [Header("Tiles for this band (variants)")]
        public TileBase[] tiles;

        [Header("Clustering inside this band")]
        [Tooltip("Lower = bigger patches. Higher = smaller patches.")]
        public float clusterScale = 1.2f;

        [Range(0f, 1f)]
        [Tooltip("0 = no clustering (pure hashed pick). 1 = strong clustering.")]
        public float clusterStrength = 0.85f;

        [Tooltip("Salt to avoid repeating patterns between bands")]
        public int salt = 1000;
    }

    [Header("Ocean Palettes (bands by depth)")]
    [Tooltip("Depth 0 = near shore / shallow, Depth 1 = deep. Bands choose which tile palette to use.")]
    public PaletteBand[] oceanBands;

    [Header("Ocean Depth Source")]
    [Tooltip("Use heightMap to derive depth: shallow near seaLevel, deep where height is very low.")]
    public bool oceanDepthFromHeight = true;

    [Tooltip("Optional extra noise to vary depth patterns in the ocean (adds variety).")]
    public bool oceanDepthAddNoise = false;

    [Range(0f, 1f)]
    [Tooltip("If oceanDepthAddNoise is true, this is how much noise blends in (0 = only height, 1 = only noise).")]
    public float oceanDepthNoiseBlend = 0.35f;

    [Tooltip("Lower = bigger deep/shallow regions if using noise depth.")]
    public float oceanDepthNoiseScale = 1.2f;

    // -----------------------
    // Tiles - Ground
    // -----------------------
    [Header("Tiles - Ground Variants (at least 1 each)")]
    public TileBase[] oceanTiles;   // fallback if oceanBands not set
    public TileBase[] plainsTiles;
    public TileBase[] desertTiles;

    // -----------------------
    // Tiles - Decor (tile-based)
    // -----------------------
    [Header("Tiles - Decor Variants (tile-based)")]
    public TileBase[] flowerTiles;
    public TileBase[] cactusTiles;
    public TileBase[] rockTiles;

    [Header("Tiles - Minor Decor Variants (white noise)")]
    public TileBase[] grassTiles;   // Plains minor decor
    public TileBase[] pebbleTiles;  // Desert minor decor

    // -----------------------
    // Prefabs - Props (y-sorted)
    // -----------------------
    [Header("Prefabs - Props (Y-sorted)")]
    [Tooltip("Prefab per alberi (SpriteRenderer + tuo YSort sul prefab).")]
    public GameObject treePrefab;

    [Tooltip("Prefab per rocce grandi (spawnate con regole dedicate).")]
    public GameObject rockPrefab;

    [Tooltip("Parent opzionale dove mettere tutti i props spawnati (consigliato: Empty 'Props').")]
    public Transform propsParent;

    [Tooltip("Offset di spawn per alberi (per correggere pivot/ancoraggio).")]
    public Vector3 treeSpawnOffset = Vector3.zero;

    [Tooltip("Offset di spawn per rocce prefab (per quando le implementerai).")]
    public Vector3 rockSpawnOffset = Vector3.zero;

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

    [Header("Rock Prefab Variants (Option A)")]
    [Tooltip("Se assegnato, lo script sostituisce la sprite del prefab con una di queste varianti (deterministico).")]
    public Sprite[] rockVariantSprites;

    [Tooltip("Se true, applica flipX deterministico per variare l'aspetto.")]
    public bool rockAllowFlipX = true;

    [Tooltip("Se true, applica una scala deterministica nell'intervallo sotto (per evitare monotonia).")]
    public bool rockAllowScale = true;

    [Tooltip("Intervallo di scala applicato alla roccia (es. 0.9..1.1).")]
    public Vector2 rockScaleRange = new Vector2(0.9f, 1.1f);

    [Header("Height Noise (Ocean vs Land)")]
    [Tooltip("Higher = more detail; lower = larger blobs")]
    public float heightScale = 6f;
    [Range(1, 10)] public int heightOctaves = 5;
    [Range(0.1f, 0.9f)] public float heightPersistence = 0.5f;
    [Range(1.2f, 4f)] public float heightLacunarity = 2f;
    [Range(0f, 1f)] public float seaLevel = 0.45f;

    [Header("Biome Noise (Plains vs Desert)")]
    [Tooltip("Lower = larger biomes. Usually lower than heightScale.")]
    public float biomeScale = 2f;
    [Range(1, 10)] public int biomeOctaves = 3;
    [Range(0.1f, 0.9f)] public float biomePersistence = 0.5f;
    [Range(1.2f, 4f)] public float biomeLacunarity = 2f;
    [Range(0f, 1f)] public float desertThreshold = 0.65f;

    [Header("Decor Noise (fallback / rocks)")]
    [Tooltip("Higher = more variation in decor placement")]
    public float decorScale = 12f;

    [Header("Decor Density")]
    [Range(0f, 1f)]
    [Tooltip("Chance per rocce tile nel deserto (fallback semplice).")]
    public float rockChance = 0.05f;

    [Header("Minor Decor Chances (white noise)")]
    [Range(0f, 1f)] public float grassChance = 0.18f;
    [Range(0f, 1f)] public float pebbleChance = 0.12f;

    [Header("Flower Patches (Plains)")]
    public float flowerPatchScale = 1.6f;                 // basso = macchie grandi
    [Range(0f, 1f)] public float flowerPatchMin = 0.65f;  // da qui in su inizia ad apparire
    [Range(0f, 1f)] public float flowerBaseChance = 0.18f;// densità massima al centro patch
    public float flowerScatterScale = 18f;                // alto = dettagli piccoli nello scatter

    [Header("Tree Patches (Plains)")]
    public float treePatchScale = 1.2f;
    [Range(0f, 1f)] public float treePatchThreshold = 0.72f; // alto = poche patch
    [Range(0f, 1f)] public float treeInPatchChance = 0.10f;  // densità dentro patch
    public float treeScatterScale = 14f;

    [Header("Cactus Patches (Desert)")]
    public float cactusPatchScale = 1.3f;
    [Range(0f, 1f)] public float cactusPatchThreshold = 0.70f;
    [Range(0f, 1f)] public float cactusInPatchChance = 0.10f;
    public float cactusScatterScale = 14f;

    [Header("Big Rock Patches (Desert)")]
    [Tooltip("Basso = macchie grandi di rocce prefab.")]
    public float rockPrefabPatchScale = 1.1f;
    [Range(0f, 1f)] public float rockPrefabPatchThreshold = 0.78f;
    [Range(0f, 1f)] public float rockPrefabInPatchChance = 0.06f;
    public float rockPrefabScatterScale = 12f;

    [Header("Rules")]
    [Tooltip("If true, do not place decor on cells adjacent to ocean (nice for shorelines).")]
    public bool avoidDecorNearOcean = true;

    [Tooltip("If true, prevents too many trees/cactus touching each other (simple neighborhood check).")]
    public bool avoidDecorClumps = true;

    [Range(0, 2)]
    public int clumpRadius = 1;

    private enum GroundType { Ocean, Plains, Desert }
    private enum DecorType { None, Tree, BigRock, Flower, Cactus, Rock, Grass, Pebble }

    private struct TileData
    {
        public GroundType ground;
        public DecorType decor;
    }

    public void Generate()
    {
        ValidateRefs();
        ClearSpawnedProps();

        Vector2 worldOffset = offset;

        float[,] heightMap = GenerateNoiseMap(width, height, heightScale, heightOctaves, heightPersistence, heightLacunarity, worldOffset, seed);
        float[,] biomeMap = GenerateNoiseMap(width, height, biomeScale, biomeOctaves, biomePersistence, biomeLacunarity, worldOffset + new Vector2(777, 333), seed + 1);
        float[,] decorMap = GenerateNoiseMap(width, height, decorScale, 2, 0.5f, 2f, worldOffset + new Vector2(999, 111), seed + 2);

        // Optional extra ocean depth noise (only used if oceanDepthAddNoise)
        float[,] oceanDepthNoiseMap = null;
        if (oceanDepthAddNoise)
        {
            oceanDepthNoiseMap = GenerateNoiseMap(
                width, height,
                oceanDepthNoiseScale,
                2, 0.5f, 2f,
                worldOffset + new Vector2(5000, 6000), seed + 99
            );
        }

        // --- FLOWERS: patch + scatter (soft density) ---
        float[,] flowerPatchMap = GenerateNoiseMap(width, height, flowerPatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2001, 3001), seed + 10);
        float[,] flowerScatterMap = GenerateNoiseMap(width, height, flowerScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2002, 3002), seed + 11);

        // --- TREES: patch mask + scatter ---
        float[,] treePatchMap = GenerateNoiseMap(width, height, treePatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2011, 3011), seed + 20);
        float[,] treeScatterMap = GenerateNoiseMap(width, height, treeScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2012, 3012), seed + 21);

        // --- CACTUS: patch mask + scatter ---
        float[,] cactusPatchMap = GenerateNoiseMap(width, height, cactusPatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2021, 3021), seed + 30);
        float[,] cactusScatterMap = GenerateNoiseMap(width, height, cactusScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2022, 3022), seed + 31);

        // --- BIG ROCKS: patch mask + scatter ---
        float[,] rockPatchMap = GenerateNoiseMap(width, height, rockPrefabPatchScale, 2, 0.5f, 2f, worldOffset + new Vector2(2031, 3031), seed + 40);
        float[,] rockScatterMap = GenerateNoiseMap(width, height, rockPrefabScatterScale, 2, 0.5f, 2f, worldOffset + new Vector2(2032, 3032), seed + 41);

        // Build tile data first (so rules can look around)
        TileData[,] data = new TileData[width, height];

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
            GroundType g = (b >= desertThreshold) ? GroundType.Desert : GroundType.Plains;

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
                flowerPatchMap, flowerScatterMap,
                treePatchMap, treeScatterMap,
                cactusPatchMap, cactusScatterMap,
                rockPatchMap, rockScatterMap,
                decorMap
            );

            if (d == DecorType.Tree || d == DecorType.Cactus || d == DecorType.BigRock)
            {
                if (avoidDecorClumps && HasNearbySameDecor(data, x, y, d, clumpRadius))
                    d = DecorType.None;
            }

            data[x, y].decor = d;
        }

        // Render
        groundTilemap.ClearAllTiles();
        decorTilemap.ClearAllTiles();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector3Int cell = new Vector3Int(x, y, 0);

            // --- ground ---
            TileBase groundTile;
            if (data[x, y].ground == GroundType.Ocean)
            {
                float depth01 = ComputeOceanDepth01(heightMap[x, y], oceanDepthNoiseMap, x, y);
                groundTile = PickOceanTile(depth01, x, y);

                // Fallback if bands not configured
                if (groundTile == null)
                    groundTile = PickVariant(oceanTiles, x, y, 101);
            }
            else
            {
                groundTile = data[x, y].ground switch
                {
                    GroundType.Plains => PickVariant(plainsTiles, x, y, 102),
                    GroundType.Desert => PickVariant(desertTiles, x, y, 103),
                    _ => PickVariant(plainsTiles, x, y, 102)
                };
            }

            groundTilemap.SetTile(cell, groundTile);

            // --- decor / props ---
            if (data[x, y].decor == DecorType.Tree)
            {
                SpawnTreePrefab(cell);
                continue; // non mettere tile albero
            }

            if (data[x, y].decor == DecorType.BigRock)
            {
                SpawnRockPrefab(cell);
                continue; // non mettere tile roccia grande
            }

            // (Per ora le Rock restano tile-based. rockPrefab è pronto ma non usato.)
            TileBase decorTile = data[x, y].decor switch
            {
                DecorType.Flower => PickVariant(flowerTiles, x, y, 202),
                DecorType.Cactus => PickVariant(cactusTiles, x, y, 203),
                DecorType.Rock => PickVariant(rockTiles, x, y, 204),
                DecorType.Grass => PickVariant(grassTiles, x, y, 205),
                DecorType.Pebble => PickVariant(pebbleTiles, x, y, 206),
                _ => null
            };

            if (decorTile != null)
                decorTilemap.SetTile(cell, decorTile);
        }

        // CompressBounds DOPO aver piazzato le tile
        groundTilemap.CompressBounds();
        decorTilemap.CompressBounds();
    }

    public void Clear()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (decorTilemap != null) decorTilemap.ClearAllTiles();
        ClearSpawnedProps();
    }

    private void SpawnTreePrefab(Vector3Int cell)
    {
        if (treePrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + treeSpawnOffset;
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

        // 3) scala deterministica (±5% o quello che imposti)
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
    }

    private void SpawnRockPrefab(Vector3Int cell)
    {
        if (rockPrefab == null) return;

        Vector3 pos = groundTilemap.GetCellCenterWorld(cell) + rockSpawnOffset;
        Transform parent = propsParent != null ? propsParent : transform;

        GameObject go = Instantiate(rockPrefab, pos, Quaternion.identity, parent);

        // 1) scegli variante sprite (Option A)
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

    // -----------------------
    // Ocean depth + palette picking
    // -----------------------
    private float ComputeOceanDepth01(float heightVal01, float[,] oceanNoiseMap, int x, int y)
    {
        float fromHeight = 0f;

        if (oceanDepthFromHeight)
        {
            // height near seaLevel => shallow(0), height very low => deep(1)
            fromHeight = Mathf.InverseLerp(seaLevel, 0f, heightVal01);
        }

        if (oceanDepthAddNoise && oceanNoiseMap != null)
        {
            float n = oceanNoiseMap[x, y]; // 0..1
            float t = Mathf.Clamp01(oceanDepthNoiseBlend);
            return oceanDepthFromHeight ? Mathf.Lerp(fromHeight, n, t) : n;
        }

        return oceanDepthFromHeight ? fromHeight : 0f;
    }

    private TileBase PickOceanTile(float depth01, int x, int y)
    {
        if (oceanBands == null || oceanBands.Length == 0) return null;

        PaletteBand band = null;
        for (int i = 0; i < oceanBands.Length; i++)
        {
            var b = oceanBands[i];
            if (b == null) continue;

            if (depth01 >= b.minInclusive && depth01 < b.maxExclusive)
            {
                band = b;
                break;
            }
        }

        // fallback: last band
        if (band == null) band = oceanBands[oceanBands.Length - 1];
        if (band == null) return null;

        return PickClusteredVariant(band.tiles, x, y, band.salt, band.clusterScale, band.clusterStrength);
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

    private TileBase PickVariant(TileBase[] variants, int x, int y, int salt)
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];

        int idx = Hash(x, y, salt) % variants.Length;
        return variants[idx];
    }

    // clustered variant selection (for ocean palettes)
    private TileBase PickClusteredVariant(TileBase[] variants, int x, int y, int salt, float clusterScale, float clusterStrength)
    {
        if (variants == null || variants.Length == 0) return null;
        if (variants.Length == 1) return variants[0];

        float r = (Hash(x, y, salt) % 100000) / 100000f; // 0..1 (deterministic)
        float c = ClusterValue(x, y, clusterScale, salt); // 0..1
        float mixed = Mathf.Lerp(r, c, Mathf.Clamp01(clusterStrength));

        int idx = Mathf.FloorToInt(mixed * variants.Length);
        if (idx >= variants.Length) idx = variants.Length - 1;
        return variants[idx];
    }

    private float ClusterValue(int x, int y, float scale, int salt)
    {
        if (scale <= 0f) scale = 0.0001f;

        // Use offset so moving the world also moves clusters
        float sx = (x + offset.x + salt * 0.001f) / 100f * scale;
        float sy = (y + offset.y + salt * 0.001f) / 100f * scale;

        return Mathf.PerlinNoise(sx, sy);
    }

    private bool HasAny(TileBase[] variants) => variants != null && variants.Length > 0;

    // -----------------------
    // Decor selection (PATCHED)
    // -----------------------
    private DecorType PickDecorPatched(
        GroundType ground,
        int x, int y,
        float[,] flowerPatchMap, float[,] flowerScatterMap,
        float[,] treePatchMap, float[,] treeScatterMap,
        float[,] cactusPatchMap, float[,] cactusScatterMap,
        float[,] rockPatchMap, float[,] rockScatterMap,
        float[,] fallbackDecorMap
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

            // BIG ROCKS: patch mask + scatter (prefab)
            if (rockPrefab != null)
            {
                float patch = rockPatchMap[x, y];
                float scatter = rockScatterMap[x, y];

                if (patch > rockPrefabPatchThreshold && scatter < rockPrefabInPatchChance)
                    return DecorType.BigRock;
            }

            // FLOWERS: soft density from patch (borders fade out)
            if (HasAny(flowerTiles))
            {
                float patch = flowerPatchMap[x, y];
                float scatter = flowerScatterMap[x, y];

                float intensity = Mathf.InverseLerp(flowerPatchMin, 1f, patch); // 0..1
                float finalChance = flowerBaseChance * intensity;

                if (scatter < finalChance)
                    return DecorType.Flower;
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

        // 2) DESERT
        if (ground == GroundType.Desert)
        {
            // CACTUS: patch mask + scatter
            if (HasAny(cactusTiles))
            {
                float patch = cactusPatchMap[x, y];
                float scatter = cactusScatterMap[x, y];

                if (patch > cactusPatchThreshold && scatter < cactusInPatchChance)
                    return DecorType.Cactus;
            }

            // BIG ROCKS: patch mask + scatter (prefab)
            if (rockPrefab != null)
            {
                float patch = rockPatchMap[x, y];
                float scatter = rockScatterMap[x, y];

                if (patch > rockPrefabPatchThreshold && scatter < rockPrefabInPatchChance)
                    return DecorType.BigRock;
            }

            // ROCK: fallback (tile-based)
            if (HasAny(rockTiles))
            {
                float v = fallbackDecorMap[x, y];
                if (v < rockChance) return DecorType.Rock;
            }

            // MINOR: PEBBLES (white noise)
            if (HasAny(pebbleTiles))
            {
                float w = White01(x, y, 9002);
                if (w < pebbleChance)
                    return DecorType.Pebble;
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

    private void ValidateRefs()
    {
        if (groundTilemap == null) throw new Exception("Assign Ground Tilemap in inspector.");
        if (decorTilemap == null) throw new Exception("Assign Decor Tilemap in inspector.");

        if (!HasAny(plainsTiles) || !HasAny(desertTiles))
            throw new Exception("Assign ground tile arrays (at least 1 each): plainsTiles, desertTiles.");

        bool hasOceanBands = oceanBands != null && oceanBands.Length > 0;
        if (!hasOceanBands && !HasAny(oceanTiles))
            throw new Exception("Assign oceanTiles OR configure oceanBands (with tiles inside each band).");
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