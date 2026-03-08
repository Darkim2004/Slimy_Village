using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 3;

    public System.Action onHurt;
    public System.Action onDeath;
    public System.Action<int, int> onHpChanged;

    /// <summary>
    /// Se assegnato, la difesa dell'armatura viene sottratta al danno ricevuto (minimo 1).
    /// </summary>
    [Tooltip("Riferimento opzionale all'inventario per leggere la difesa armatura.")]
    public InventoryModel inventory;

    private int hp;
    private bool dead;

    private void Awake()
    {
        hp = maxHp;
        dead = false;
    }

    private void Start()
    {
        onHpChanged?.Invoke(hp, maxHp);
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;

        int defense = inventory != null ? inventory.ArmorDefense : 0;
        int effective = Mathf.Max(1, Mathf.Abs(amount) - defense);

        hp -= effective;
        if (hp <= 0)
        {
            hp = 0;
            dead = true;
            onHpChanged?.Invoke(hp, maxHp);
            onDeath?.Invoke();
        }
        else
        {
            onHpChanged?.Invoke(hp, maxHp);
            onHurt?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (dead) return;

        hp = Mathf.Min(hp + Mathf.Abs(amount), maxHp);
        onHpChanged?.Invoke(hp, maxHp);
    }

    public void SetMaxHp(int value, bool refillCurrentHp)
    {
        maxHp = Mathf.Max(1, value);

        if (refillCurrentHp)
            hp = maxHp;
        else
            hp = Mathf.Clamp(hp, 0, maxHp);

        onHpChanged?.Invoke(hp, maxHp);
    }

    public bool IsDead => dead;
    public int CurrentHp => hp;
    public int MaxHp => maxHp;
}