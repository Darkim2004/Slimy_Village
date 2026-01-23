using System;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldGenTilemap : MonoBehaviour
{
    [Header("Tilemaps (same Grid)")]
    [SerializeField] private Tilemap groundTilemapA;
    [SerializeField] private Tilemap groundTilemapB;
    [SerializeField] private Tilemap decorTilemap;

    [Header("World Size (prototype, no chunks yet)")]
    [Min(1)] public int width = 128;
    [Min(1)] public int height = 128;

    [Header("Seed & Offset")]
    public int seed = 12345;
    public Vector2 offset; // useful to "move" the world without changing seed

    // -----------------------
    // NEW: Ocean palette bands (shallow/deep palettes)
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
    // Existing: simple tile variants for other ground types + decor
    // -----------------------
    [Header("Tiles - Ground Variants (at least 1 each)")]
    public TileBase[] oceanTiles;   // fallback if oceanBands not set
    public TileBase[] plainsTiles;
    public TileBase[] desertTiles;

    [Header("Tiles - Decor Variants (optional)")]
    public TileBase[] treeTiles;
    public TileBase[] flowerTiles;
    public TileBase[] cactusTiles;
    public TileBase[] rockTiles;

    [Header("Tiles - Minor Decor Variants (white noise)")]
    public TileBase[] grassTiles;   // Plains minor decor
    public TileBase[] pebbleTiles;  // Desert minor decor

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

    [Header("Decor Noise")]
    [Tooltip("Higher = more variation in decor placement")]
    public float decorScale = 12f;

    [Header("Decor Density (0..1) - legacy (not used by patched spawns)")]
    [Range(0f, 1f)] public float treeChance = 0.08f;
    [Range(0f, 1f)] public float flowerChance = 0.12f;
    [Range(0f, 1f)] public float cactusChance = 0.07f;
    [Range(0f, 1f)] public float rockChance = 0.05f;

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

    [Header("Rules")]
    [Tooltip("If true, do not place decor on cells adjacent to ocean (nice for shorelines).")]
    public bool avoidDecorNearOcean = true;

    [Tooltip("If true, prevents too many trees/cactus touching each other (simple neighborhood check).")]
    public bool avoidDecorClumps = true;

    [Range(0, 2)]
    public int clumpRadius = 1;

    private enum GroundType { Ocean, Plains, Desert }
    private enum DecorType { None, Tree, Flower, Cactus, Rock, Grass, Pebble }

    private struct TileData
    {
        public GroundType ground;
        public DecorType decor;
    }

    public void Generate()
    {
        ValidateRefs();

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
                    decorMap
                );

                if (d == DecorType.Tree || d == DecorType.Cactus)
                {
                    if (avoidDecorClumps && HasNearbySameDecor(data, x, y, d, clumpRadius))
                        d = DecorType.None;
                }

                data[x, y].decor = d;
            }

        // Render
        groundTilemapA.ClearAllTiles();
        groundTilemapB.ClearAllTiles();
        decorTilemap.ClearAllTiles();

        groundTilemapA.CompressBounds();
        groundTilemapB.CompressBounds();
        decorTilemap.CompressBounds();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                // ground
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

                // NEW: checkerboard ground split (prevents chunk sorting overlap artifacts)
                Tilemap targetGround = (((x + y) & 1) == 0) ? groundTilemapA : groundTilemapB;
                targetGround.SetTile(cell, groundTile);

                // decor (variant)
                TileBase decorTile = data[x, y].decor switch
                {
                    DecorType.Tree => PickVariant(treeTiles, x, y, 201),
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
    }

    public void Clear()
    {
        if (groundTilemapA != null) groundTilemapA.ClearAllTiles();
        if (groundTilemapB != null) groundTilemapB.ClearAllTiles();
        if (decorTilemap != null) decorTilemap.ClearAllTiles();
    }

    // -----------------------
    // Ocean depth + palette picking
    // -----------------------
    private float ComputeOceanDepth01(float heightVal01, float[,] oceanNoiseMap, int x, int y)
    {
        // Only meaningful in ocean cells (heightVal01 < seaLevel), but safe anyway.
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
    // Deterministic variants
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

    // NEW: clustered variant selection (for ocean palettes)
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
        float[,] fallbackDecorMap
    )
    {
        // 1) PLAINS
        if (ground == GroundType.Plains)
        {
            // TREES: patch mask + scatter
            if (HasAny(treeTiles))
            {
                float patch = treePatchMap[x, y];
                float scatter = treeScatterMap[x, y];

                if (patch > treePatchThreshold && scatter < treeInPatchChance)
                    return DecorType.Tree;
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

            // ROCK: fallback (simple)
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

    // LEGACY (not used by patched approach, kept for reference)
    private DecorType PickDecor(GroundType ground, float v01)
    {
        if (ground == GroundType.Plains)
        {
            if (HasAny(treeTiles) && v01 < treeChance) return DecorType.Tree;
            if (HasAny(flowerTiles) && v01 < treeChance + flowerChance) return DecorType.Flower;
            return DecorType.None;
        }

        if (ground == GroundType.Desert)
        {
            if (HasAny(cactusTiles) && v01 < cactusChance) return DecorType.Cactus;
            if (HasAny(rockTiles) && v01 < cactusChance + rockChance) return DecorType.Rock;
            return DecorType.None;
        }

        return DecorType.None;
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
        if (groundTilemapA == null) throw new Exception("Assign Ground Tilemap A in inspector.");
        if (groundTilemapB == null) throw new Exception("Assign Ground Tilemap B in inspector.");
        if (decorTilemap == null) throw new Exception("Assign Decor Tilemap in inspector.");

        // Plains/Desert still required
        if (!HasAny(plainsTiles) || !HasAny(desertTiles))
            throw new Exception("Assign ground tile arrays (at least 1 each): plainsTiles, desertTiles.");

        // Ocean must exist either via bands or fallback array
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