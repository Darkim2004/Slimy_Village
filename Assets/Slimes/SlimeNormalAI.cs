using UnityEngine;

public class SlimeNormalAI : EntityBase2D
{
    [Header("Wander behaviour")]
    public float wanderRadius = 3.5f;

    public Vector2 idleTimeRange = new Vector2(0.6f, 1.8f);
    public Vector2 moveTimeRange = new Vector2(0.8f, 2.0f);

    [Range(0f, 1f)]
    public float runChance = 0.15f;

    [Header("Aggro (override in SlimeDefinition)")]
    [Tooltip("Layer delle entità che possono essere colpite dall'AoE.")]
    public LayerMask aoeDamageTargets;

    private Vector2 homePos;
    private Vector2 targetPos;
    private float decisionTimer;

    // ── Aggro ──
    private Transform aggroTarget;
    private bool hitboxPending;   // danno in attesa del delay
    private float hitboxDelayTimer;
    private float attackCooldownTimer;

    // ── Shortcut alla definition castata ──
    private SlimeDefinition slimeDef;

    protected override void Awake()
    {
        base.Awake();

        homePos = transform.position;
        targetPos = homePos;

        decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        EnterIdle();

        // Iscriviti all'evento per sapere chi ci ha colpito
        health.onHurtBy += OnHurtBy;
    }

    public override void Initialize(EntityDefinition def)
    {
        base.Initialize(def);
        slimeDef = def as SlimeDefinition;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.onHurtBy -= OnHurtBy;
    }

    // ───────────────────────────── Aggro callback ──────────────────────────────
    private void OnHurtBy(GameObject attacker)
    {
        if (attacker == null) return;

        // Prende aggro solo se non ne ha già uno, o se è lo stesso attaccante
        if (aggroTarget == null)
            aggroTarget = attacker.transform;
    }

    // ───────────────────────────── AI principale ──────────────────────────────
    protected override void TickAI()
    {
        // During attack: countdown del delay hitbox
        if (state == State.Attack)
        {
            if (hitboxPending)
            {
                hitboxDelayTimer -= Time.deltaTime;
                if (hitboxDelayTimer <= 0f)
                {
                    hitboxPending = false;
                    ApplyAoEDamage();
                }
            }
            return; // stato bloccante
        }

        // Altri stati bloccanti (hurt, death)
        if (IsTimedState(state))
            return;

        // Cooldown attacco
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // Se abbiamo un aggro target valido → modalità combattimento
        if (aggroTarget != null)
        {
            if (!IsTargetValid())
            {
                ClearAggro();
            }
            else
            {
                TickCombat();
                return;
            }
        }

        // ── Wander normale (invariato) ──
        TickWander();
    }

    // ───────────────────────────── Combattimento ──────────────────────────────
    private void TickCombat()
    {
        float aggroRange = slimeDef != null ? slimeDef.aggroRange : 5f;
        float atkRadius  = slimeDef != null ? slimeDef.attackRadius : 1.2f;
        float chaseSpd   = slimeDef != null ? slimeDef.chaseSpeed : 2.5f;

        Vector2 toTarget = (Vector2)aggroTarget.position - rb.position;
        float dist = toTarget.magnitude;

        // Fuori dal leash range → perdi aggro
        if (dist > aggroRange)
        {
            ClearAggro();
            return;
        }

        // Abbastanza vicino per attaccare?
        if (dist <= atkRadius)
        {
            if (attackCooldownTimer <= 0f)
            {
                PerformAoEAttack();
            }
            else
            {
                // Aspetta il cooldown stando fermo
                EnterIdle();
            }
            return;
        }

        // Chase: corri verso il bersaglio
        Vector2 vel = toTarget.normalized * chaseSpd;
        EnterRun(vel);
    }

    // ───────────────────────────── Attacco AoE ────────────────────────────────
    private void PerformAoEAttack()
    {
        float cooldown = slimeDef != null ? slimeDef.attackCooldown : 1.5f;
        float delay    = slimeDef != null ? slimeDef.attackHitboxDelay : 0.15f;

        // Imposta la direzione verso il bersaglio per l'animazione
        Vector2 dir = (Vector2)aggroTarget.position - rb.position;
        if (dir.sqrMagnitude > 0.001f)
            facing = ResolveFacing(dir);

        // Entra nello stato Attack (animazione)
        lockedDir = facing;
        state = State.Attack;
        stateTimer = GetClipOrFallback(attackName, lockedDir, 0.35f);
        desiredVelocity = Vector2.zero;

        // Avvia il countdown: il danno verrà applicato dopo 'delay' secondi
        hitboxPending = true;
        hitboxDelayTimer = delay;

        attackCooldownTimer = cooldown;
    }

    /// <summary>Applica il danno AoE. Chiamato dopo il delay.</summary>
    private void ApplyAoEDamage()
    {
        float atkRadius = slimeDef != null ? slimeDef.attackRadius : 1.2f;
        int   atkDamage = slimeDef != null ? slimeDef.attackDamage : 1;

        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, atkRadius);
        foreach (var hit in hits)
        {
            if (hit.isTrigger) continue;

            if (aoeDamageTargets.value != 0 &&
                ((1 << hit.gameObject.layer) & aoeDamageTargets) == 0)
                continue;

            if (hit.transform.root == transform.root) continue;

            var hp = hit.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDead)
                hp.TakeDamage(atkDamage, gameObject);
        }
    }

    protected override void OnAttackFinished()
    {
        // Non chiamiamo melee.EndAttack() perché non usiamo MeleeAttack2D
        hitboxPending = false;  // sicurezza: annulla il pending se il timer finisce prima del delay
        EnterIdle();
    }

    // ───────────────────────────── Wander ─────────────────────────────────────
    private void TickWander()
    {
        decisionTimer -= Time.deltaTime;

        // Continua movimento verso il target
        if (state == State.Walk || state == State.Run)
        {
            Vector2 pos = rb.position;
            Vector2 to = targetPos - pos;

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

        // Nuova decisione
        float r = Random.value;

        if (r < 0.40f)
        {
            EnterIdle();
            decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            return;
        }

        // Scegli nuova destinazione
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

    // ───────────────────────────── Helpers ─────────────────────────────────────
    private bool IsTargetValid()
    {
        if (aggroTarget == null) return false;

        var targetHealth = aggroTarget.GetComponentInParent<Health>();
        if (targetHealth != null && targetHealth.IsDead) return false;

        return true;
    }

    private void ClearAggro()
    {
        aggroTarget = null;

        // Torna alla posizione home per il wander
        targetPos = homePos;
        decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        EnterIdle();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float atkRadius  = slimeDef != null ? slimeDef.attackRadius : 1.2f;
        float aggroRange = slimeDef != null ? slimeDef.aggroRange : 5f;

        // Attack radius (rosso)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, atkRadius);

        // Aggro range (giallo)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        // Wander radius (verde)
        Vector2 home = Application.isPlaying ? homePos : (Vector2)transform.position;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(home, wanderRadius);
    }
#endif
}