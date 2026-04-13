using UnityEngine;

public class AnimalPassiveAI : EntityBase2D
{
    [Header("Fallback Wander")]
    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackWanderRadius = 4f;

    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public Vector2 fallbackIdleTimeRange = new Vector2(0.6f, 1.8f);

    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public Vector2 fallbackMoveTimeRange = new Vector2(0.8f, 2f);

    [Range(0f, 1f)]
    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackWanderRunChance = 0.2f;

    [Header("Fallback Flee")]
    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackFleeDuration = 3f;

    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackFleeSpeedMultiplier = 1.5f;

    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackFleePreferredDistance = 5f;

    [Tooltip("Usato solo se non viene assegnata una AnimalDefinition.")]
    public float fallbackFleeRetriggerCooldown = 0.1f;

    [Header("Navigation")]
    [Tooltip("Se true, evita l'oceano durante wander/fuga (richiede WorldGenTilemap in scena).")]
    public bool avoidOcean = true;

    [Header("Debug")]
    [Tooltip("Se attivo, disegna i raggi di wander/fuga anche quando non selezionato.")]
    public bool drawDebugGizmosAlways;

    protected AnimalDefinition animalDef;

    protected Vector2 homePos;
    protected Vector2 wanderTargetPos;

    private float decisionTimer;
    private float fleeTimer;
    private float fleeRetriggerTimer;

    private Transform fleeFrom;
    private Vector2 lastFleeDir = Vector2.up;

    private WorldGenTilemap worldGen;

    protected override void Awake()
    {
        base.Awake();

        homePos = rb.position;
        wanderTargetPos = homePos;
        decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);

        health.onHurtBy += OnHurtBy;

        EnterIdle();
    }

    public override void Initialize(EntityDefinition def)
    {
        base.Initialize(def);
        animalDef = def as AnimalDefinition;
    }

    protected override void OnDestroy()
    {
        if (health != null)
            health.onHurtBy -= OnHurtBy;

        base.OnDestroy();
    }

    protected override void TickAI()
    {
        if (IsTimedState(state))
            return;

        if (fleeRetriggerTimer > 0f)
            fleeRetriggerTimer -= Time.deltaTime;

        if (fleeTimer > 0f)
        {
            TickFlee();
            return;
        }

        TickWander();
    }

    private void OnHurtBy(GameObject attacker)
    {
        if (fleeRetriggerTimer > 0f)
            return;

        if (attacker != null)
            fleeFrom = attacker.transform;

        fleeTimer = Mathf.Max(0.1f, GetFleeDuration());
        fleeRetriggerTimer = Mathf.Max(0f, GetFleeRetriggerCooldown());
    }

    protected virtual void TickWander()
    {
        decisionTimer -= Time.deltaTime;

        if (state == State.Walk || state == State.Run)
        {
            Vector2 to = wanderTargetPos - rb.position;
            if (to.sqrMagnitude < 0.02f)
            {
                EnterIdle();
                decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
                return;
            }

            float speed = state == State.Run ? runSpeed : walkSpeed;
            Vector2 velocity = to.normalized * speed;

            if (!CanMoveTo(velocity))
            {
                EnterIdle();
                decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
                return;
            }

            if (state == State.Run)
                EnterRun(velocity);
            else
                EnterWalk(velocity);
        }

        if (decisionTimer > 0f)
            return;

        if (Random.value < 0.40f)
        {
            EnterIdle();
            decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
            return;
        }

        Vector2 candidate = homePos + Random.insideUnitCircle * Mathf.Max(0.1f, GetWanderRadius());

        if (avoidOcean && IsOceanCell(candidate))
        {
            EnterIdle();
            decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
            return;
        }

        wanderTargetPos = candidate;

        bool willRun = Random.value < GetWanderRunChance();
        float speedNew = willRun ? runSpeed : walkSpeed;

        Vector2 moveDir = wanderTargetPos - rb.position;
        Vector2 velocityNew = moveDir.sqrMagnitude > 0.001f ? moveDir.normalized * speedNew : Vector2.zero;

        if (!CanMoveTo(velocityNew))
        {
            EnterIdle();
            decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
            return;
        }

        if (willRun)
            EnterRun(velocityNew);
        else
            EnterWalk(velocityNew);

        decisionTimer = Random.Range(GetMoveTimeRange().x, GetMoveTimeRange().y);
    }

    protected virtual void TickFlee()
    {
        fleeTimer -= Time.deltaTime;

        if (fleeTimer <= 0f)
        {
            fleeTimer = 0f;
            fleeFrom = null;
            EnterIdle();
            decisionTimer = Random.Range(GetIdleTimeRange().x, GetIdleTimeRange().y);
            return;
        }

        Vector2 fleeDir = ComputeFleeDirection();
        if (fleeDir.sqrMagnitude <= 0.0001f)
            fleeDir = lastFleeDir.sqrMagnitude > 0.0001f ? lastFleeDir : Vector2.up;

        lastFleeDir = fleeDir.normalized;

        float fleeSpeed = runSpeed * Mathf.Max(1f, GetFleeSpeedMultiplier());
        Vector2 fleeVelocity = lastFleeDir * fleeSpeed;

        if (!CanMoveTo(fleeVelocity))
        {
            Vector2 side = Vector2.Perpendicular(lastFleeDir).normalized;
            Vector2 sideVelA = side * fleeSpeed;
            Vector2 sideVelB = -side * fleeSpeed;

            if (CanMoveTo(sideVelA))
            {
                EnterRun(sideVelA);
                return;
            }

            if (CanMoveTo(sideVelB))
            {
                EnterRun(sideVelB);
                return;
            }

            EnterIdle();
            return;
        }

        EnterRun(fleeVelocity);
    }

    private Vector2 ComputeFleeDirection()
    {
        if (fleeFrom == null)
            return lastFleeDir;

        Vector2 away = rb.position - (Vector2)fleeFrom.position;

        float preferredDistance = Mathf.Max(0.5f, GetFleePreferredDistance());
        if (away.sqrMagnitude >= preferredDistance * preferredDistance)
            return away.normalized;

        return away.sqrMagnitude > 0.0001f ? away.normalized : lastFleeDir;
    }

    protected bool CanMoveTo(Vector2 velocity)
    {
        if (!avoidOcean)
            return true;

        if (velocity.sqrMagnitude <= 0.0001f)
            return true;

        var world = GetWorldGen();
        if (world == null || world.GroundTilemap == null)
            return true;

        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;
        Vector3Int nextCell = world.GroundTilemap.WorldToCell(nextPosition);
        return world.IsLandCell(nextCell.x, nextCell.y);
    }

    protected bool IsOceanCell(Vector2 worldPos)
    {
        var world = GetWorldGen();
        if (world == null || world.GroundTilemap == null)
            return false;

        Vector3Int cell = world.GroundTilemap.WorldToCell(worldPos);
        return world.IsOceanCell(cell.x, cell.y);
    }

    private WorldGenTilemap GetWorldGen()
    {
        if (worldGen == null)
            worldGen = FindFirstObjectByType<WorldGenTilemap>();

        return worldGen;
    }

    protected virtual float GetWanderRadius()
    {
        return animalDef != null ? animalDef.wanderRadius : fallbackWanderRadius;
    }

    protected virtual Vector2 GetIdleTimeRange()
    {
        return animalDef != null ? animalDef.idleTimeRange : fallbackIdleTimeRange;
    }

    protected virtual Vector2 GetMoveTimeRange()
    {
        return animalDef != null ? animalDef.moveTimeRange : fallbackMoveTimeRange;
    }

    protected virtual float GetWanderRunChance()
    {
        return animalDef != null ? animalDef.wanderRunChance : fallbackWanderRunChance;
    }

    protected virtual float GetFleeDuration()
    {
        return animalDef != null ? animalDef.fleeDuration : fallbackFleeDuration;
    }

    protected virtual float GetFleeSpeedMultiplier()
    {
        return animalDef != null ? animalDef.fleeSpeedMultiplier : fallbackFleeSpeedMultiplier;
    }

    protected virtual float GetFleePreferredDistance()
    {
        return animalDef != null ? animalDef.fleePreferredDistance : fallbackFleePreferredDistance;
    }

    protected virtual float GetFleeRetriggerCooldown()
    {
        return animalDef != null ? animalDef.fleeRetriggerCooldown : fallbackFleeRetriggerCooldown;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawDebugGizmosAlways)
            DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        Vector2 center = Application.isPlaying ? homePos : (Vector2)transform.position;

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.18f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0.1f, GetWanderRadius()));

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.22f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.5f, GetFleePreferredDistance()));

        if (Application.isPlaying && fleeTimer > 0f)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + lastFleeDir * 1.2f);
        }
    }
#endif
}
