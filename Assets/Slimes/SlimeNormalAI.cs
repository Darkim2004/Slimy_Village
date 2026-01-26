using UnityEngine;

public class SlimeNormalAI : EntityBase2D
{
    [Header("Wander behaviour")]
    public float wanderRadius = 3.5f;

    public Vector2 idleTimeRange = new Vector2(0.6f, 1.8f);
    public Vector2 moveTimeRange = new Vector2(0.8f, 2.0f);

    [Range(0f, 1f)]
    public float runChance = 0.15f;

    private Vector2 homePos;
    private Vector2 targetPos;
    private float decisionTimer;

    protected override void Awake()
    {
        base.Awake();

        homePos = transform.position;
        targetPos = homePos;

        decisionTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        EnterIdle();
    }

    protected override void TickAI()
    {
        // Stati bloccanti (hurt, death, attack)
        if (IsTimedState(state))
            return;

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

        Vector2 dir = targetPos - rb.position;
        Vector2 velocity = dir.sqrMagnitude > 0.001f
            ? dir.normalized * spd
            : Vector2.zero;

        if (willRun)
            EnterRun(velocity);
        else
            EnterWalk(velocity);

        decisionTimer = Random.Range(moveTimeRange.x, moveTimeRange.y);
    }
}