using UnityEngine;

public class GenericAreaSpawner2D : MonoBehaviour
{
    [Header("Refs")]
    public WorldGenTilemap world;
    public Transform player;
    public Camera camOverride; // se vuoto usa Camera.main (con CinemachineBrain)

    [Header("Catalog")]
    public SpawnEntry[] entries;

    [Header("Rules")]
    public SpawnRules rules = new SpawnRules();

    [Header("Spawner")]
    public int maxAlive = 12;
    public float spawnInterval = 2.0f;
    public int triesPerTick = 30;

    private float timer;

    private void Awake()
    {
        timer = spawnInterval;
    }

    private void Update()
    {
        if (world == null || player == null) { GameDebug.Log(GameDebugCategory.Spawner, "Spawner: missing world/player"); return; }
        if (!world.HasGenerated) { GameDebug.Log(GameDebugCategory.Spawner, "Spawner: world not generated"); return; }
        if (entries == null || entries.Length == 0) { GameDebug.Log(GameDebugCategory.Spawner, "Spawner: no entries"); return; }

        if (transform.childCount >= maxAlive) { GameDebug.Log(GameDebugCategory.Spawner, "Spawner: maxAlive reached"); return; }

        timer -= Time.deltaTime;
        if (timer > 0f) { return; }
        timer = spawnInterval;

        Vector3Int playerCell = world.GroundTilemap.WorldToCell(player.position);
        int half = Mathf.Max(1, rules.windowSize / 2);
        int minD2 = rules.minDistanceFromPlayerCells * rules.minDistanceFromPlayerCells;

        Camera cam = camOverride != null ? camOverride : Camera.main;

        for (int i = 0; i < triesPerTick; i++)
        {
            int x = Random.Range(playerCell.x - half, playerCell.x + half);
            int y = Random.Range(playerCell.y - half, playerCell.y + half);

            int dx = x - playerCell.x;
            int dy = y - playerCell.y;
            if (dx * dx + dy * dy < minD2) { continue; }

            if (!world.IsInside(x, y)) { continue; }
            if (!world.IsLandCell(x, y)) { continue; }
            if (world.IsBlockedCell(x, y)) { continue; }

            var biome = world.GetBiome(x, y);
            var entry = PickEntryFor(biome);
            if (entry == null) { continue; }

            Vector3 pos = world.CellCenterWorld(x, y);

            if (!rules.IsOffscreen(pos, cam)) { continue; }

            if (rules.avoidRadius > 0f &&
                Physics2D.OverlapCircle(pos, rules.avoidRadius, rules.avoidMask) != null)
            { continue; }

            var go = Instantiate(entry.prefab, pos, Quaternion.identity, transform);

            // inizializzazione generica (se il prefab la supporta)
            var init = go.GetComponent<ISpawnInitializable>();
            if (init != null) init.Initialize(entry.definition);

            return;
        }
    }

    private SpawnEntry PickEntryFor(WorldGenTilemap.Biome biome)
    {
        float total = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null || e.definition == null) continue;

            if (!IsBiomeAllowed(e.definition, biome)) continue;
            if (!IsTimeAllowed(e.definition.TimeRule)) continue;

            total += Mathf.Max(0f, e.definition.Weight);
        }

        if (total <= 0f) return null;

        float r = Random.value * total;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null || e.definition == null) continue;

            if (!IsBiomeAllowed(e.definition, biome)) continue;
            if (!IsTimeAllowed(e.definition.TimeRule)) continue;

            r -= Mathf.Max(0f, e.definition.Weight);
            if (r <= 0f) return e;
        }

        return null;
    }

    private bool IsBiomeAllowed(EntityDefinition def, WorldGenTilemap.Biome biome)
    {
        var allowed = def.AllowedBiomes;
        if (allowed == null || allowed.Length == 0) return true;

        for (int i = 0; i < allowed.Length; i++)
            if (allowed[i] == biome) return true;

        return false;
    }

    private bool IsTimeAllowed(SpawnTimeRule rule)
    {
        if (rule == SpawnTimeRule.Always)
            return true;

        // DayNightScript.NightFactor: 0 = giorno, 1 = notte.
        // Soglia 0.5 per trattare alba/tramonto in modo bilanciato.
        bool isNight = DayNightScript.NightFactor >= 0.5f;

        if (rule == SpawnTimeRule.Night)
            return isNight;

        if (rule == SpawnTimeRule.Day)
            return !isNight;

        return true;
    }
}