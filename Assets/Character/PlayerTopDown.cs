using UnityEngine;

public class PlayerTopDown : EntityBase2D
{
    [Header("Player Stats")]
    [SerializeField] private int playerMaxHp = 20;

    [Header("Input")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode attackKey = KeyCode.J;

    private bool inputLocked;

    protected override void Awake()
    {
        base.Awake();
        health.SetMaxHp(playerMaxHp, refillCurrentHp: true);
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (!locked) return;

        EnterIdle();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    protected override void TickAI()
    {
        if (inputLocked)
        {
            EnterIdle();
            return;
        }

        // Se sei in stati bloccanti, non leggere input
        if (state == State.Hurt || state == State.Death || state == State.Attack)
            return;

        float x = Input.GetAxisRaw(horizontalAxis);
        float y = Input.GetAxisRaw(verticalAxis);
        Vector2 input = new Vector2(x, y);

        bool run = Input.GetKey(runKey);
        bool attackPressed = Input.GetKeyDown(attackKey);

        if (attackPressed)
        {
            StartAttack();
            return;
        }

        if (input.sqrMagnitude < 0.01f)
        {
            EnterIdle();
        }
        else
        {
            Vector2 dir = input.normalized;
            float speed = run ? runSpeed : walkSpeed;
            Vector2 vel = dir * speed;

            if (run) EnterRun(vel);
            else EnterWalk(vel);
        }
    }
}
