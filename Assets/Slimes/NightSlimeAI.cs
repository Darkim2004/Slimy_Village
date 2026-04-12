using UnityEngine;

public class SNightSlimeAI : EntityBase2D
{
    [Header("Wander behaviour")]
    public float wanderRadius = 3.5f;
    public Vector2 idleTimeRange = new Vector2(0.6f, 1.8f);
    public Vector2 moveTimeRange = new Vector2(0.8f, 2.0f);

    [Range(0f, 1f)]
    public float runChance = 0.15f;

    [Header("Aggressive Attack")]
    [Tooltip("Layer delle entita che possono essere colpite dall'attacco.")]
    public LayerMask attackDamageTargets;

    [Tooltip("Intervallo di aggiornamento della ricerca del player.")]
    public float targetRefreshInterval = 0.4f;

    [Header("Debug")]
    [Tooltip("Se attivo, disegna i gizmo dell'attacco sempre, anche senza selezionare il GameObject.")]
    public bool drawDebugHitboxesAlways;

    private Vector2 homePos;
    private Vector2 targetPos;
    private float decisionTimer;

    private Transform playerTarget;
    private float targetRefreshTimer;

    private bool hitboxPending;
    private float hitboxDelayTimer;
    private float attackCooldownTimer;

    private SlimeDefinition slimeDef;

    protected override void Awake()
    {
        base.Awake();

        // Usa il campo definition ereditato dalla classe padre
        slimeDef = definition as SlimeDefinition;

        homePos = transform.position;
        targetPos = homePos;
        decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        EnterIdle();
    }

    public override void Initialize(EntityDefinition def)
    {
        base.Initialize(def);
        slimeDef = def as SlimeDefinition;
    }

    /// <summary>
    /// Forza immediatamente il target di aggro (utile per summon boss).
    /// </summary>
    public void ForceAggroTarget(Transform target)
    {
        if (target == null)
            return;

        playerTarget = target;
        targetRefreshTimer = targetRefreshInterval;
        attackCooldownTimer = 0f;
    }

    protected override void TickAI()
    {
        if (state == State.Attack)
        {
            if (hitboxPending)
            {
                hitboxDelayTimer -= Time.deltaTime;
                if (hitboxDelayTimer <= 0f)
                {
                    hitboxPending = false;
                    ApplyAttackDamage();
                }
            }
            return;
        }

        if (IsTimedState(state))
            return;

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (!EnsurePlayerTarget())
        {
            TickWander();
            return;
        }

        TickCombat();
    }

    private bool EnsurePlayerTarget()
    {
        float aggroRange = slimeDef != null ? slimeDef.aggroRange : 5f;

        if (playerTarget != null)
        {
            var targetHealth = playerTarget.GetComponentInParent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                playerTarget = null;
            }
            else
            {
                float dist = Vector2.Distance(rb.position, playerTarget.position);
                if (dist > aggroRange)
                    playerTarget = null;
            }

            if (playerTarget != null)
                return true;
        }

        targetRefreshTimer -= Time.deltaTime;
        if (targetRefreshTimer > 0f)
            return false;

        targetRefreshTimer = targetRefreshInterval;

        var player = FindFirstObjectByType<PlayerTopDown>();
        if (player != null)
        {
            float dist = Vector2.Distance(rb.position, player.transform.position);
            if (dist <= aggroRange)
            {
                playerTarget = player.transform;
                return true;
            }
        }

        return false;
    }

    private void TickCombat()
    {
        float atkRange = GetAttackRange();
        float chaseSpd = slimeDef != null ? slimeDef.chaseSpeed : 2.5f;

        Vector2 toTarget = (Vector2)playerTarget.position - rb.position;
        float dist = toTarget.magnitude;

        if (dist <= atkRange)
        {
            if (attackCooldownTimer <= 0f)
                PerformAttack();
            else
                EnterIdle();

            return;
        }

        Vector2 vel = toTarget.normalized * chaseSpd;
        EnterRun(vel);
    }

    private void TickWander()
    {
        decisionTimer -= Time.deltaTime;

        if (state == State.Walk || state == State.Run)
        {
            Vector2 to = targetPos - rb.position;

            if (to.sqrMagnitude < 0.02f)
            {
                EnterIdle();
                decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
                return;
            }

            float speed = (state == State.Run) ? runSpeed : walkSpeed;
            Vector2 vel = to.normalized * speed;

            if (state == State.Run)
                EnterRun(vel);
            else
                EnterWalk(vel);
        }

        if (decisionTimer > 0f)
            return;

        float r = Random.value;
        if (r < 0.40f)
        {
            EnterIdle();
            decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            return;
        }

        targetPos = homePos + Random.insideUnitCircle * wanderRadius;

        bool willRun = Random.value < runChance;
        float spd = willRun ? runSpeed : walkSpeed;

        Vector2 moveDir = targetPos - rb.position;
        Vector2 velocity = moveDir.sqrMagnitude > 0.001f
            ? moveDir.normalized * spd
            : Vector2.zero;

        if (willRun)
            EnterRun(velocity);
        else
            EnterWalk(velocity);

        decisionTimer = Random.Range(moveTimeRange.x, moveTimeRange.y);
    }

    private void PerformAttack()
    {
        float cooldown = slimeDef != null ? slimeDef.attackCooldown : 1.5f;
        float delay = slimeDef != null ? slimeDef.attackHitboxDelay : 0.15f;

        Vector2 dir = (Vector2)playerTarget.position - rb.position;
        if (dir.sqrMagnitude > 0.001f)
            facing = ResolveFacing(dir);

        lockedDir = facing;
        state = State.Attack;
        stateTimer = GetClipOrFallback(attackName, lockedDir, 0.35f);
        desiredVelocity = Vector2.zero;

        hitboxPending = true;
        hitboxDelayTimer = delay;
        attackCooldownTimer = cooldown;
    }

    private void ApplyAttackDamage()
    {
        if (IsDirectionalAttack())
            ApplyDirectionalDamage();
        else
            ApplyAoEDamage();
    }

    private void ApplyAoEDamage()
    {
        float atkRadius = slimeDef != null ? slimeDef.attackRadius : 1.2f;
        int atkDamage = slimeDef != null ? slimeDef.attackDamage : 1;

        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, atkRadius);
        foreach (var hit in hits)
        {
            if (hit.isTrigger) continue;

            if (attackDamageTargets.value != 0 &&
                ((1 << hit.gameObject.layer) & attackDamageTargets) == 0)
                continue;

            if (hit.transform.root == transform.root) continue;

            var hp = hit.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(atkDamage, gameObject);
        }
    }

    private void ApplyDirectionalDamage()
    {
        float atkLength = slimeDef != null ? Mathf.Max(0.1f, slimeDef.directionalLength) : 1.6f;
        float atkWidth = slimeDef != null ? Mathf.Max(0.1f, slimeDef.directionalWidth) : 0.9f;
        float forwardOffset = slimeDef != null ? slimeDef.directionalForwardOffset : 0f;
        int atkDamage = slimeDef != null ? slimeDef.attackDamage : 1;

        Vector2 dir = DirToVector(lockedDir);
        Vector2 center = rb.position + dir * (atkLength * 0.5f + forwardOffset);

        Vector2 size = (lockedDir == Dir.Left || lockedDir == Dir.Right)
            ? new Vector2(atkLength, atkWidth)
            : new Vector2(atkWidth, atkLength);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (var hit in hits)
        {
            if (hit.isTrigger) continue;

            if (attackDamageTargets.value != 0 &&
                ((1 << hit.gameObject.layer) & attackDamageTargets) == 0)
                continue;

            if (hit.transform.root == transform.root) continue;

            var hp = hit.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(atkDamage, gameObject);
        }
    }

    protected override void OnAttackFinished()
    {
        hitboxPending = false;
        EnterIdle();
    }

    protected override void OnExternalPauseStateChanged(bool paused)
    {
        if (!paused)
            return;

        hitboxPending = false;
        hitboxDelayTimer = 0f;
        attackCooldownTimer = 0f;
        playerTarget = null;
        targetRefreshTimer = 0f;
        EnterIdle();
    }

    private bool IsDirectionalAttack()
    {
        return slimeDef != null && slimeDef.attackMode == SlimeAttackMode.Directional;
    }

    private float GetAttackRange()
    {
        if (IsDirectionalAttack())
        {
            float length = Mathf.Max(0.1f, slimeDef.directionalLength);
            float forwardOffset = Mathf.Max(0f, slimeDef.directionalForwardOffset);
            return length + forwardOffset;
        }

        return slimeDef != null ? slimeDef.attackRadius : 1.2f;
    }

    private static Vector2 DirToVector(Dir d)
    {
        return d switch
        {
            Dir.Up => Vector2.up,
            Dir.Down => Vector2.down,
            Dir.Left => Vector2.left,
            Dir.Right => Vector2.right,
            _ => Vector2.down
        };
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawDebugHitboxesAlways)
            DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        float atkRadius = slimeDef != null ? slimeDef.attackRadius : 1.2f;
        float aggroRange = slimeDef != null ? slimeDef.aggroRange : 5f;

        if (IsDirectionalAttack())
        {
            float atkLength = Mathf.Max(0.1f, slimeDef.directionalLength);
            float atkWidth = Mathf.Max(0.1f, slimeDef.directionalWidth);
            float forwardOffset = slimeDef.directionalForwardOffset;

            Dir drawDir = Application.isPlaying ? lockedDir : facing;
            Vector2 dir = DirToVector(drawDir);
            Vector2 center = (Vector2)transform.position + dir * (atkLength * 0.5f + forwardOffset);
            Vector2 size = (drawDir == Dir.Left || drawDir == Dir.Right)
                ? new Vector2(atkLength, atkWidth)
                : new Vector2(atkWidth, atkLength);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, size);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, atkRadius);
        }

        if (Application.isPlaying && playerTarget != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Vector2 home = Application.isPlaying ? homePos : (Vector2)transform.position;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(home, wanderRadius);
    }
#endif
}
