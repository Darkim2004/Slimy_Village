using UnityEngine;

public class PlayerTopDown : EntityBase2D
{
    [Header("Input")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode attackKey = KeyCode.J;

    protected override void TickAI()
    {
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
