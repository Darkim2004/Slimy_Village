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

    [Min(0)]
    [Tooltip("Livello di raccolta richiesto per danneggiare questo nodo. Se > 0, sovrascrive requireHarvestTool.")]
    public int requiredHarvestLevel = 0;

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

        int effectiveRequired = requiredHarvestLevel > 0
            ? requiredHarvestLevel
            : (requireHarvestTool ? 1 : 0);

        if (effectiveRequired > 0 && GetAttackerHarvestToolLevel(attacker) < effectiveRequired)
            return false;

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

    private int GetAttackerHarvestToolLevel(GameObject attacker)
    {
        if (attacker == null) return 0;

        HotbarEffectManager manager = attacker.GetComponentInChildren<HotbarEffectManager>();
        if (manager == null)
            manager = attacker.GetComponentInParent<HotbarEffectManager>();

        if (manager == null) return 0;
        return manager.HarvestToolLevel;
    }

    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        if (requiredHarvestLevel < 0) requiredHarvestLevel = 0;
    }
}
