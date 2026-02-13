using UnityEngine;

public class MeleeAttack2D : MonoBehaviour
{
    [Header("References")]
    public DamageHitbox2D hitbox; // child AttackHitbox con BoxCollider2D

    [Header("Damage")]
    public int baseDamage = 1;

    [Header("Hitbox sizes")]
    public Vector2 sizeVertical = new(0.6f, 0.9f);
    public Vector2 sizeHorizontal = new(0.9f, 0.6f);

    [Header("Distance per direction")]
    public float distanceUp = 0.35f;
    public float distanceDown = 0.35f;
    public float distanceLeft = 0.35f;
    public float distanceRight = 0.35f;

    [Header("Fine offset per direction (local, after rotation)")]
    public Vector2 fineOffsetUp = Vector2.zero;
    public Vector2 fineOffsetDown = Vector2.zero;
    public Vector2 fineOffsetLeft = Vector2.zero;
    public Vector2 fineOffsetRight = Vector2.zero;

    [Header("Active window (normalized 0..1 of attack duration)")]
    [Range(0f, 1f)] public float hitStart = 0.25f;
    [Range(0f, 1f)] public float hitEnd = 0.55f;

    private float attackDuration;
    private float attackElapsed;
    private bool active;

    private BoxCollider2D box;

    private void Awake()
    {
        if (hitbox == null) return;

        box = hitbox.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.enabled = false;
            // Nota: size/offset li settiamo in PositionHitbox4()
        }

        hitbox.End();
    }

    public void StartAttack(float duration, Vector2 facingDir, int damage)
    {
        attackDuration = Mathf.Max(0.01f, duration);
        attackElapsed = 0f;
        active = false;

        baseDamage = damage;

        PositionHitbox4(facingDir);
        hitbox.End(); // parte spenta
    }

    public void TickAttack(Vector2 facingDir)
    {
        if (hitbox == null || box == null) return;

        attackElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(attackElapsed / attackDuration);

        PositionHitbox4(facingDir);

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

    // =========================
    // Core: 4-dir placement
    // =========================
    private void PositionHitbox4(Vector2 dir)
    {
        if (hitbox == null || box == null) return;

        dir = Snap4(dir);

        // IMPORTANT: teniamo il GameObject della hitbox fermo, così parent/pivot/scale danno meno problemi
        hitbox.transform.localPosition = Vector3.zero;

        // Rotazione a scatti: base DOWN = 0°, RIGHT = -90°, LEFT = +90°, UP = 180°
        float angle = DirToAngle(dir);
        hitbox.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Size: verticale vs orizzontale
        bool horizontal = (dir == Vector2.left || dir == Vector2.right);
        box.size = horizontal ? sizeHorizontal : sizeVertical;

        // Distance + fineOffset (offset è nel local space della hitbox, quindi ruota insieme)
        float dist = GetDistance(dir);
        Vector2 fine = GetFineOffset(dir);

        // “in avanti” nel local della hitbox = Vector2.down (perché DOWN è il riferimento 0°)
        box.offset = Vector2.down * dist + fine;
    }

    private Vector2 Snap4(Vector2 v)
    {
        if (v.sqrMagnitude < 0.001f) return Vector2.down;
        v = v.normalized;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0) ? Vector2.right : Vector2.left;
        else
            return (v.y >= 0) ? Vector2.up : Vector2.down;
    }

    private float DirToAngle(Vector2 dir)
    {
        if (dir == Vector2.down) return 0f;
        if (dir == Vector2.right) return 90f;
        if (dir == Vector2.left) return -90f;
        if (dir == Vector2.up) return 180f;
        return 0f;
    }

    private float GetDistance(Vector2 dir)
    {
        if (dir == Vector2.up) return distanceUp;
        if (dir == Vector2.down) return distanceDown;
        if (dir == Vector2.left) return distanceLeft;
        if (dir == Vector2.right) return distanceRight;
        return distanceDown;
    }

    private Vector2 GetFineOffset(Vector2 dir)
    {
        if (dir == Vector2.up) return fineOffsetUp;
        if (dir == Vector2.down) return fineOffsetDown;
        if (dir == Vector2.left) return fineOffsetLeft;
        if (dir == Vector2.right) return fineOffsetRight;
        return Vector2.zero;
    }

    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (hitbox == null) return;

        BoxCollider2D box = hitbox.GetComponent<BoxCollider2D>();
        if (box == null) return;

        Gizmos.color = Color.green;

        // Matrice = posizione + rotazione della hitbox
        Matrix4x4 matrix = Matrix4x4.TRS(
            hitbox.transform.position,
            hitbox.transform.rotation,
            Vector3.one
        );

        Gizmos.matrix = matrix;

        // Disegna il box usando size e offset REALI del collider
        Gizmos.DrawWireCube(box.offset, box.size);

        Gizmos.matrix = Matrix4x4.identity;
    }
    #endif
}

