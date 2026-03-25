using UnityEngine;

[DisallowMultipleComponent]
public class HarvestableNode : MonoBehaviour
{
    [Header("Health")]
    [Min(1)] public int maxHp = 3;

    [Header("Loot (optional)")]
    [Tooltip("LootTable usata quando il nodo viene distrutto.")]
    public LootTable lootTable;

    [Header("Rules")]
    [Tooltip("Se true, riceve danno solo se l'attaccante ha un tool valido equipaggiato.")]
    public bool requireHarvestTool = true;

    [Tooltip("Se true, distrugge il GameObject quando gli HP arrivano a zero.")]
    public bool destroyOnDeath = true;

    public bool IsDestroyed { get; private set; }

    private int _currentHp;

    private void Awake()
    {
        _currentHp = Mathf.Max(1, maxHp);
        IsDestroyed = false;
    }

    public void SetMaxHpAndReset(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        _currentHp = maxHp;
        IsDestroyed = false;
    }

    public bool TryTakeDamage(int amount, GameObject attacker)
    {
        if (IsDestroyed) return false;
        if (requireHarvestTool && !HasValidHarvestTool(attacker)) return false;

        int effective = Mathf.Max(1, Mathf.Abs(amount));
        _currentHp -= effective;

        if (_currentHp <= 0)
        {
            _currentHp = 0;
            IsDestroyed = true;

            if (lootTable != null)
                lootTable.SpawnLoot(transform.position);

            if (destroyOnDeath)
                Destroy(gameObject);
        }

        return true;
    }

    private bool HasValidHarvestTool(GameObject attacker)
    {
        if (attacker == null) return false;

        HotbarEffectManager manager = attacker.GetComponentInChildren<HotbarEffectManager>();
        if (manager == null)
            manager = attacker.GetComponentInParent<HotbarEffectManager>();

        if (manager == null) return false;
        return manager.IsHarvestToolEquipped;
    }

    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
    }
}
