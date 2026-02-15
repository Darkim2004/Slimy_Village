using System;
using UnityEngine;

/// <summary>
/// Dati extra per item NON stackabili (armi, strumenti, armature…).
/// Stackabili non devono mai avere un NBT: se nbt == null → l'item è stackabile.
/// </summary>
[Serializable]
public class ItemNBT : ICloneable
{
    [Header("Durability")]
    [Min(0)] public int durability;
    [Min(1)] public int maxDurability = 100;

    // --- Puoi aggiungere qui altri campi in futuro ---
    // public int enchantmentLevel;
    // public Color tint;

    /// <summary>Durabilità normalizzata 0…1.</summary>
    public float DurabilityRatio =>
        maxDurability > 0 ? (float)durability / maxDurability : 0f;

    public bool IsBroken => durability <= 0;

    /// <summary>Deep copy — ogni istanza di item ha il suo NBT indipendente.</summary>
    public object Clone()
    {
        return new ItemNBT
        {
            durability    = this.durability,
            maxDurability = this.maxDurability,
        };
    }

    /// <summary>Factory per un NBT "nuovo", piena durabilità.</summary>
    public static ItemNBT NewFull(int maxDur = 100)
    {
        return new ItemNBT
        {
            durability    = maxDur,
            maxDurability = maxDur,
        };
    }
}
