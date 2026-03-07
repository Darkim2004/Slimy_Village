using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 3;

    public System.Action onHurt;
    public System.Action onDeath;

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
            onDeath?.Invoke();
        }
        else
        {
            onHurt?.Invoke();
        }
    }

    public bool IsDead => dead;
    public int CurrentHp => hp;
}