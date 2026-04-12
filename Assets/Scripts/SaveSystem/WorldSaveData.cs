using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldMetadataData
{
    public string schemaVersion = "2";
    public string worldId;
    public string displayName;
    public int seed;
    public string createdAtUtc;
    public string lastPlayedAtUtc;
    public string gameVersion;
    public bool ritualPlatformUnlocked;
    public bool aegisDefeated;
    public bool aegisIntroPlayed;
    public AegisStateData aegisState;
}

[Serializable]
public sealed class AegisStateData
{
    public bool defeated;
    public bool aegisIntroPlayed;
    public List<AegisPillarStateData> pillars = new List<AegisPillarStateData>();
}

[Serializable]
public sealed class AegisPillarStateData
{
    public string pillarName;
    public int hitsTaken;
    public bool disabled;
}

[Serializable]
public sealed class WorldGridData
{
    public int width;
    public int height;
    public int seed;
    public int[] ground;
    public int[] decor;
}

[Serializable]
public sealed class DynamicEntitiesData
{
    public PlayerStateData player;
    public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
    public List<WorldDropData> worldDrops = new List<WorldDropData>();
    public List<AiEntityData> aiEntities = new List<AiEntityData>();
}

[Serializable]
public sealed class PlayerStateData
{
    public float positionX;
    public float positionY;
    public float positionZ;
    public float respawnX;
    public float respawnY;
    public float respawnZ;
    public int currentHp;
    public InventoryData inventory;
}

[Serializable]
public sealed class InventoryData
{
    public List<InventorySectionData> sections = new List<InventorySectionData>();
}

[Serializable]
public sealed class InventorySectionData
{
    public string sectionName;
    public int size;
    public List<InventorySlotData> slots = new List<InventorySlotData>();
}

[Serializable]
public sealed class InventorySlotData
{
    public int index;
    public string itemId;
    public int amount;
    public ItemNbtData nbt;
}

[Serializable]
public sealed class ItemNbtData
{
    public int durability;
    public int maxDurability;
}

[Serializable]
public sealed class PlacedObjectData
{
    public string instanceId;
    public string placeableId;
    public int originX;
    public int originY;
    public int sizeX;
    public int sizeY;
    public bool hasWorldPosition;
    public float worldX;
    public float worldY;
    public float worldZ;
    public InventorySectionData chestSection;
}

[Serializable]
public sealed class WorldDropData
{
    public string itemId;
    public int amount;
    public float positionX;
    public float positionY;
    public float positionZ;
}

[Serializable]
public sealed class AiEntityData
{
    public string definitionId;
    public int currentHp;
    public float positionX;
    public float positionY;
    public float positionZ;
}
