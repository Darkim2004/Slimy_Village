using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHp = 3;

    public System.Action onHurt;
    public System.Action onDeath;

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

        hp -= Mathf.Abs(amount);
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