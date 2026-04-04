using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldSaveSystem : MonoBehaviour
{
    private const string WorldsFolderName = "worlds";

    private const string PrefPendingWorldName = "MainMenu.PendingWorldName";
    private const string PrefPendingWorldSeed = "MainMenu.PendingWorldSeed";
    private const string PrefPendingWorldId = "MainMenu.PendingWorldId";
    private const string PrefHasPendingWorldCreation = "MainMenu.HasPendingWorldCreation";

    private const string PrefCurrentWorldId = "MainMenu.CurrentWorldId";
    private const string PrefCurrentWorldName = "MainMenu.CurrentWorldName";

    [SerializeField] private bool autosaveEnabled = true;
    [SerializeField] private float autosaveIntervalSeconds = 60f;
    [SerializeField] private bool verboseLogs;

    public static WorldSaveSystem Instance { get; private set; }

    private WorldGenTilemap worldGen;
    private bool initialized;
    private bool saveInProgress;
    private float autosaveTimer;

    private string currentWorldId;
    private string currentWorldName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureInstanceInGameScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || scene.name != "Game")
            return;

        EnsureInstanceInGameScene();
    }

    private static void EnsureInstanceInGameScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Game")
            return;

        if (UnityEngine.Object.FindFirstObjectByType<WorldSaveSystem>() != null)
            return;

        var go = new GameObject("WorldSaveSystem");
        go.AddComponent<WorldSaveSystem>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private IEnumerator Start()
    {
        SaveDefinitionCatalog.Refresh();

        worldGen = FindFirstObjectByType<WorldGenTilemap>();
        while (worldGen == null || !worldGen.HasGenerated)
        {
            worldGen = FindFirstObjectByType<WorldGenTilemap>();
            yield return null;
        }

        InitializeWorldContext();

        autosaveTimer = Mathf.Max(10f, autosaveIntervalSeconds);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !autosaveEnabled)
            return;

        autosaveTimer -= Time.unscaledDeltaTime;
        if (autosaveTimer > 0f)
            return;

        SaveNow("autosave");
        autosaveTimer = Mathf.Max(10f, autosaveIntervalSeconds);
    }

    public bool SaveNow(string reason = "manual")
    {
        if (saveInProgress || worldGen == null)
            return false;

        saveInProgress = true;
        bool success = false;

        try
        {
            if (string.IsNullOrEmpty(currentWorldId))
                currentWorldId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(currentWorldName))
                currentWorldName = "New World";

            string worldDir = GetWorldDirectory(currentWorldId);
            EnsureDirectory(worldDir);

            var metadata = BuildMetadata();
            var grid = worldGen.CreateGridSnapshot();
            var entities = CaptureDynamicEntities();

            WriteJsonAtomic(Path.Combine(worldDir, "metadata.json"), JsonUtility.ToJson(metadata, true));
            WriteJsonAtomic(Path.Combine(worldDir, "grid.json"), JsonUtility.ToJson(grid, true));
            WriteJsonAtomic(Path.Combine(worldDir, "entities.json"), JsonUtility.ToJson(entities, true));

            PlayerPrefs.SetString(PrefCurrentWorldId, currentWorldId);
            PlayerPrefs.SetString(PrefCurrentWorldName, currentWorldName);
            PlayerPrefs.Save();

            if (verboseLogs)
                Debug.Log("[WorldSaveSystem] Save completed (" + reason + ") for worldId=" + currentWorldId, this);

            success = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WorldSaveSystem] Save failed: " + ex.Message, this);
        }
        finally
        {
            saveInProgress = false;
        }

        return success;
    }

    public bool LoadCurrentWorld()
    {
        if (string.IsNullOrEmpty(currentWorldId))
            return false;

        string worldDir = GetWorldDirectory(currentWorldId);
        string metadataPath = Path.Combine(worldDir, "metadata.json");
        string gridPath = Path.Combine(worldDir, "grid.json");
        string entitiesPath = Path.Combine(worldDir, "entities.json");

        if (!File.Exists(gridPath))
            return false;

        try
        {
            if (File.Exists(metadataPath))
            {
                var metadata = JsonUtility.FromJson<WorldMetadataData>(File.ReadAllText(metadataPath));
                if (metadata != null)
                {
                    currentWorldName = string.IsNullOrWhiteSpace(metadata.displayName) ? currentWorldName : metadata.displayName;
                }
            }

            var grid = JsonUtility.FromJson<WorldGridData>(File.ReadAllText(gridPath));
            if (grid == null || !worldGen.TryApplyGridSnapshot(grid))
                return false;

            if (File.Exists(entitiesPath))
            {
                var entities = JsonUtility.FromJson<DynamicEntitiesData>(File.ReadAllText(entitiesPath));
                if (entities != null)
                    ApplyDynamicEntities(entities);
            }

            if (verboseLogs)
                Debug.Log("[WorldSaveSystem] Loaded worldId=" + currentWorldId, this);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WorldSaveSystem] Load failed: " + ex.Message, this);
            return false;
        }
    }

    private void InitializeWorldContext()
    {
        bool hasPendingWorld = PlayerPrefs.GetInt(PrefHasPendingWorldCreation, 0) == 1;

        if (hasPendingWorld)
        {
            currentWorldId = PlayerPrefs.GetString(PrefPendingWorldId, string.Empty);
            if (string.IsNullOrWhiteSpace(currentWorldId))
                currentWorldId = Guid.NewGuid().ToString("N");

            currentWorldName = PlayerPrefs.GetString(PrefPendingWorldName, "New World");
            if (string.IsNullOrWhiteSpace(currentWorldName))
                currentWorldName = "New World";

            PlayerPrefs.SetString(PrefCurrentWorldId, currentWorldId);
            PlayerPrefs.SetString(PrefCurrentWorldName, currentWorldName);
            PlayerPrefs.Save();

            SaveNow("new-world-bootstrap");
            return;
        }

        currentWorldId = PlayerPrefs.GetString(PrefCurrentWorldId, string.Empty);
        currentWorldName = PlayerPrefs.GetString(PrefCurrentWorldName, "New World");

        if (!string.IsNullOrWhiteSpace(currentWorldId) && HasSavedWorld(currentWorldId))
        {
            if (LoadCurrentWorld())
                return;
        }

        if (string.IsNullOrWhiteSpace(currentWorldId))
            currentWorldId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(currentWorldName))
            currentWorldName = "New World";

        PlayerPrefs.SetString(PrefCurrentWorldId, currentWorldId);
        PlayerPrefs.SetString(PrefCurrentWorldName, currentWorldName);
        PlayerPrefs.Save();

        SaveNow("bootstrap-new-id");
    }

    private bool HasSavedWorld(string worldId)
    {
        string worldDir = GetWorldDirectory(worldId);
        return File.Exists(Path.Combine(worldDir, "metadata.json"))
            && File.Exists(Path.Combine(worldDir, "grid.json"));
    }

    private WorldMetadataData BuildMetadata()
    {
        string now = DateTime.UtcNow.ToString("O");
        string worldDir = GetWorldDirectory(currentWorldId);
        string metadataPath = Path.Combine(worldDir, "metadata.json");

        var metadata = new WorldMetadataData();
        metadata.worldId = currentWorldId;
        metadata.displayName = currentWorldName;
        metadata.seed = worldGen != null ? worldGen.seed : 0;
        metadata.lastPlayedAtUtc = now;
        metadata.gameVersion = Application.version;

        if (File.Exists(metadataPath))
        {
            try
            {
                var existing = JsonUtility.FromJson<WorldMetadataData>(File.ReadAllText(metadataPath));
                metadata.createdAtUtc = existing != null && !string.IsNullOrEmpty(existing.createdAtUtc)
                    ? existing.createdAtUtc
                    : now;
            }
            catch
            {
                metadata.createdAtUtc = now;
            }
        }
        else
        {
            metadata.createdAtUtc = now;
        }

        return metadata;
    }

    private DynamicEntitiesData CaptureDynamicEntities()
    {
        var data = new DynamicEntitiesData();

        CapturePlayer(data);
        CapturePlacedObjects(data);
        CaptureWorldDrops(data);
        CaptureAiEntities(data);

        return data;
    }

    private void CapturePlayer(DynamicEntitiesData data)
    {
        var player = FindFirstObjectByType<PlayerTopDown>();
        if (player == null)
            return;

        var playerState = new PlayerStateData();
        playerState.positionX = player.transform.position.x;
        playerState.positionY = player.transform.position.y;
        playerState.positionZ = player.transform.position.z;

        Vector3 respawn = player.RespawnPoint;
        playerState.respawnX = respawn.x;
        playerState.respawnY = respawn.y;
        playerState.respawnZ = respawn.z;

        var health = player.GetComponent<Health>();
        playerState.currentHp = health != null ? health.CurrentHp : 1;

        InventoryModel inventory = player.GetComponentInParent<InventoryModel>();
        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();

        if (inventory != null)
            playerState.inventory = SerializeInventory(inventory);

        data.player = playerState;
    }

    private void CapturePlacedObjects(DynamicEntitiesData data)
    {
        var placedObjects = FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
        for (int i = 0; i < placedObjects.Length; i++)
        {
            var placed = placedObjects[i];
            if (placed == null || placed.definition == null)
                continue;

            var dto = new PlacedObjectData();
            dto.instanceId = placed.PersistentInstanceId;
            dto.placeableId = SaveDefinitionCatalog.GetPlaceableId(placed.definition);
            dto.originX = placed.gridOrigin.x;
            dto.originY = placed.gridOrigin.y;
            dto.sizeX = placed.gridSize.x;
            dto.sizeY = placed.gridSize.y;

            var chest = placed.GetComponent<ChestInventoryStorage>();
            if (chest != null && chest.Section != null)
                dto.chestSection = SerializeSection(chest.Section);

            data.placedObjects.Add(dto);
        }
    }

    private void CaptureWorldDrops(DynamicEntitiesData data)
    {
        var drops = FindObjectsByType<WorldDrop>(FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            var drop = drops[i];
            if (drop == null || drop.ItemDefinition == null || drop.Amount <= 0)
                continue;

            var dto = new WorldDropData();
            dto.itemId = SaveDefinitionCatalog.GetItemId(drop.ItemDefinition);
            dto.amount = drop.Amount;
            dto.positionX = drop.transform.position.x;
            dto.positionY = drop.transform.position.y;
            dto.positionZ = drop.transform.position.z;
            data.worldDrops.Add(dto);
        }
    }

    private void CaptureAiEntities(DynamicEntitiesData data)
    {
        var entities = FindObjectsByType<EntityBase2D>(FindObjectsSortMode.None);
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (entity == null || entity is PlayerTopDown || entity.definition == null)
                continue;

            var health = entity.GetComponent<Health>();
            var dto = new AiEntityData();
            dto.definitionId = SaveDefinitionCatalog.GetEntityId(entity.definition);
            dto.currentHp = health != null ? health.CurrentHp : 1;
            dto.positionX = entity.transform.position.x;
            dto.positionY = entity.transform.position.y;
            dto.positionZ = entity.transform.position.z;
            data.aiEntities.Add(dto);
        }
    }

    private void ApplyDynamicEntities(DynamicEntitiesData data)
    {
        if (data == null)
            return;

        ApplyPlayerState(data.player);
        RestorePlacedObjects(data.placedObjects);
        RestoreWorldDrops(data.worldDrops);
        RestoreAiEntities(data.aiEntities);
    }

    private void ApplyPlayerState(PlayerStateData playerData)
    {
        if (playerData == null)
            return;

        var player = FindFirstObjectByType<PlayerTopDown>();
        if (player == null)
            return;

        player.transform.position = new Vector3(playerData.positionX, playerData.positionY, playerData.positionZ);
        player.SetRespawnPoint(new Vector3(playerData.respawnX, playerData.respawnY, playerData.respawnZ));

        var health = player.GetComponent<Health>();
        if (health != null)
            health.SetHp(playerData.currentHp);

        InventoryModel inventory = player.GetComponentInParent<InventoryModel>();
        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();

        if (inventory != null && playerData.inventory != null)
            DeserializeInventory(inventory, playerData.inventory);
    }

    private void RestorePlacedObjects(List<PlacedObjectData> placedObjects)
    {
        var existing = FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                Destroy(existing[i].gameObject);
        }

        var grid = FindFirstObjectByType<PlacementGrid>();
        if (grid != null)
            grid.Initialize();

        if (placedObjects == null)
            return;

        for (int i = 0; i < placedObjects.Count; i++)
        {
            var dto = placedObjects[i];
            if (dto == null)
                continue;

            if (!SaveDefinitionCatalog.TryResolvePlaceable(dto.placeableId, out var placeable) || placeable == null || placeable.placedPrefab == null)
                continue;

            Vector2Int origin = new Vector2Int(dto.originX, dto.originY);
            Vector2Int size = new Vector2Int(Mathf.Max(1, dto.sizeX), Mathf.Max(1, dto.sizeY));

            Vector3 spawnPos;
            if (grid != null)
                spawnPos = grid.GetAreaWorldCenter(origin, size) + placeable.spawnOffset;
            else
                spawnPos = new Vector3(origin.x + 0.5f, origin.y + 0.5f, 0f) + placeable.spawnOffset;

            GameObject go = Instantiate(placeable.placedPrefab, spawnPos, Quaternion.identity);

            var placed = go.GetComponent<PlacedObject>();
            if (placed == null)
                placed = go.AddComponent<PlacedObject>();

            placed.Initialize(placeable, origin, size);
            placed.SetPersistentInstanceId(dto.instanceId);

            if (go.GetComponent<YSort>() == null && go.GetComponentInChildren<YSort>() == null)
                go.AddComponent<YSort>();

            if (grid != null)
                grid.OccupyCells(origin, size);

            if (dto.chestSection != null)
            {
                var chest = go.GetComponent<ChestInventoryStorage>();
                if (chest == null)
                    chest = go.AddComponent<ChestInventoryStorage>();

                DeserializeSection(chest.Section, dto.chestSection);
            }
        }
    }

    private void RestoreWorldDrops(List<WorldDropData> drops)
    {
        var existingDrops = FindObjectsByType<WorldDrop>(FindObjectsSortMode.None);
        for (int i = 0; i < existingDrops.Length; i++)
        {
            if (existingDrops[i] != null)
                Destroy(existingDrops[i].gameObject);
        }

        if (drops == null)
            return;

        for (int i = 0; i < drops.Count; i++)
        {
            var dto = drops[i];
            if (dto == null || dto.amount <= 0)
                continue;

            if (!SaveDefinitionCatalog.TryResolveItem(dto.itemId, out var item) || item == null)
                continue;

            WorldDrop.Spawn(item, dto.amount, new Vector3(dto.positionX, dto.positionY, dto.positionZ));
        }
    }

    private void RestoreAiEntities(List<AiEntityData> entities)
    {
        var existing = FindObjectsByType<EntityBase2D>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            var entity = existing[i];
            if (entity == null || entity is PlayerTopDown)
                continue;

            Destroy(entity.gameObject);
        }

        if (entities == null)
            return;

        var spawnCatalog = BuildSpawnerCatalog();

        for (int i = 0; i < entities.Count; i++)
        {
            var dto = entities[i];
            if (dto == null || string.IsNullOrWhiteSpace(dto.definitionId))
                continue;

            if (!spawnCatalog.TryGetValue(dto.definitionId, out var spawnInfo))
                continue;

            GameObject go = Instantiate(spawnInfo.prefab, new Vector3(dto.positionX, dto.positionY, dto.positionZ), Quaternion.identity, spawnInfo.parent);

            var init = go.GetComponent<ISpawnInitializable>();
            if (init != null)
                init.Initialize(spawnInfo.definition);

            var health = go.GetComponent<Health>();
            if (health != null)
                health.SetHp(dto.currentHp);
        }
    }

    private Dictionary<string, SpawnCatalogEntry> BuildSpawnerCatalog()
    {
        var catalog = new Dictionary<string, SpawnCatalogEntry>();
        var spawners = FindObjectsByType<GenericAreaSpawner2D>(FindObjectsSortMode.None);

        for (int i = 0; i < spawners.Length; i++)
        {
            var spawner = spawners[i];
            if (spawner == null || spawner.entries == null)
                continue;

            for (int j = 0; j < spawner.entries.Length; j++)
            {
                var entry = spawner.entries[j];
                if (entry == null || entry.definition == null || entry.prefab == null)
                    continue;

                string key = SaveDefinitionCatalog.GetEntityId(entry.definition);
                if (string.IsNullOrWhiteSpace(key) || catalog.ContainsKey(key))
                    continue;

                catalog.Add(key, new SpawnCatalogEntry
                {
                    definition = entry.definition,
                    prefab = entry.prefab,
                    parent = spawner.transform
                });
            }
        }

        return catalog;
    }

    private InventoryData SerializeInventory(InventoryModel inventory)
    {
        var data = new InventoryData();
        if (inventory == null)
            return data;

        var sections = inventory.Sections;
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (section == null)
                continue;

            data.sections.Add(SerializeSection(section));
        }

        return data;
    }

    private InventorySectionData SerializeSection(InventorySection section)
    {
        var data = new InventorySectionData();
        data.sectionName = section.sectionName;
        data.size = section.Size;

        for (int i = 0; i < section.Size; i++)
        {
            var stack = section.GetSlot(i);
            if (stack == null || stack.IsEmpty || stack.def == null)
                continue;

            var slot = new InventorySlotData();
            slot.index = i;
            slot.itemId = SaveDefinitionCatalog.GetItemId(stack.def);
            slot.amount = stack.amount;

            if (stack.nbt != null)
            {
                slot.nbt = new ItemNbtData();
                slot.nbt.durability = stack.nbt.durability;
                slot.nbt.maxDurability = stack.nbt.maxDurability;
            }

            data.slots.Add(slot);
        }

        return data;
    }

    private void DeserializeInventory(InventoryModel inventory, InventoryData data)
    {
        if (inventory == null || data == null)
            return;

        inventory.Clear();

        for (int i = 0; i < data.sections.Count; i++)
        {
            var sectionData = data.sections[i];
            if (sectionData == null)
                continue;

            var section = inventory.GetSection(sectionData.sectionName);
            if (section == null)
                continue;

            DeserializeSection(section, sectionData);
        }
    }

    private void DeserializeSection(InventorySection section, InventorySectionData data)
    {
        if (section == null || data == null)
            return;

        for (int i = 0; i < section.Size; i++)
            section.SetSlot(i, null);

        if (data.slots == null)
            return;

        for (int i = 0; i < data.slots.Count; i++)
        {
            var slotData = data.slots[i];
            if (slotData == null || slotData.index < 0 || slotData.index >= section.Size)
                continue;

            if (!SaveDefinitionCatalog.TryResolveItem(slotData.itemId, out var itemDef) || itemDef == null)
                continue;

            ItemNBT nbt = null;
            if (slotData.nbt != null)
            {
                nbt = new ItemNBT();
                nbt.durability = slotData.nbt.durability;
                nbt.maxDurability = Mathf.Max(1, slotData.nbt.maxDurability);
            }

            var stack = new ItemStack(itemDef, Mathf.Max(1, slotData.amount), nbt);
            section.SetSlot(slotData.index, stack);
        }
    }

    private static string GetWorldsRootDirectory()
    {
        return Path.Combine(Application.persistentDataPath, WorldsFolderName);
    }

    private static string GetWorldDirectory(string worldId)
    {
        return Path.Combine(GetWorldsRootDirectory(), worldId);
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static void WriteJsonAtomic(string targetPath, string json)
    {
        string tempPath = targetPath + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(targetPath))
            File.Delete(targetPath);

        File.Move(tempPath, targetPath);
    }

    private struct SpawnCatalogEntry
    {
        public EntityDefinition definition;
        public GameObject prefab;
        public Transform parent;
    }
}
