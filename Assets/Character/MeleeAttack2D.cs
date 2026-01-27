using UnityEngine;

public class MeleeAttack2D : MonoBehaviour
{
    [Header("References")]
    public DamageHitbox2D hitbox; // assegna il child AttackHitbox

    [Header("Damage")]
    public int baseDamage = 1;

    [Header("Hitbox placement")]
    public float distance = 0.35f;     // quanto davanti all’entità
    public Vector2 boxSize = new(0.6f, 0.6f);

    [Header("Active window (normalized 0..1 of attack duration)")]
    [Range(0f, 1f)] public float hitStart = 0.25f;
    [Range(0f, 1f)] public float hitEnd   = 0.55f;

    private float attackDuration;
    private float attackElapsed;
    private bool active;

    private BoxCollider2D box;

    private void Awake()
    {
        if (hitbox != null)
        {
            box = hitbox.GetComponent<BoxCollider2D>();
            if (box != null) box.size = boxSize;
            hitbox.End();
        }
    }

    public void StartAttack(float duration, Vector2 facingDir, int damage)
    {
        attackDuration = Mathf.Max(0.01f, duration);
        attackElapsed = 0f;
        active = false;

        PositionHitbox(facingDir);

        if (box != null) box.size = boxSize;
        hitbox.End(); // parte spenta
        baseDamage = damage;
    }

    public void TickAttack(Vector2 facingDir)
    {
        if (hitbox == null) return;

        attackElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(attackElapsed / attackDuration);

        PositionHitbox(facingDir);

        bool shouldBeActive = (t >= hitStart && t <= hitEnd);

        if (shouldBeActive && !active)
        {
            active = true;
            hitbox.Begin(transform, baseDamage);
        }
        else if (!shouldBeActive && active)
        {
            active = false;
            hitbox.End();
        }
    }

    public void EndAttack()
    {
        active = false;
        if (hitbox != null) hitbox.End();
    }

    private void PositionHitbox(Vector2 dir)
    {
        if (hitbox == null) return;

        if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
        dir = dir.normalized;

        hitbox.transform.localPosition = (Vector3)(dir * distance);
    }
}