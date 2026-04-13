using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 3;

    public System.Action onHurt;
    public System.Action onDeath;
    public System.Action<int, int> onHpChanged;

    /// <summary>
    /// Invocato quando l'entità subisce danno da un attaccante noto.
    /// Il parametro è il GameObject root dell'attaccante (può essere null).
    /// </summary>
    public System.Action<GameObject> onHurtBy;

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

    public void TakeDamage(int amount) => TakeDamage(amount, null);

    public void TakeDamage(int amount, GameObject attacker)
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
            onHurtBy?.Invoke(attacker);
            onDeath?.Invoke();
        }
        else
        {
            onHpChanged?.Invoke(hp, maxHp);
            onHurt?.Invoke();
            onHurtBy?.Invoke(attacker);
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

    public void Revive()
    {
        hp = maxHp;
        dead = false;
        onHpChanged?.Invoke(hp, maxHp);
    }

    /// <summary>
    /// Imposta direttamente gli HP a un valore specifico (per debug/cheat).
    /// Se il valore è <= 0, scatena la morte. Se è inferiore al corrente, scatena hurt.
    /// </summary>
    public void SetHp(int value)
    {
        if (dead && value > 0) { Revive(); return; }
        if (dead) return;

        int previous = hp;
        hp = Mathf.Clamp(value, 0, maxHp);
        onHpChanged?.Invoke(hp, maxHp);

        if (hp <= 0)
        {
            hp = 0;
            dead = true;
            onDeath?.Invoke();
        }
        else if (hp < previous)
        {
            onHurt?.Invoke();
        }
    }

    public bool IsDead => dead;
    public int CurrentHp => hp;
    public int MaxHp => maxHp;
}