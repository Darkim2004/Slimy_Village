using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Health))]
public abstract class EntityBase2D : MonoBehaviour, ISpawnInitializable
{
    public enum State { Idle, Walk, Run, Hurt, Death, Attack }
    public enum Dir { Up, Down, Left, Right }

    [Header("Definition (set by spawner)")]
    public EntityDefinition definition;

    [Header("Movement")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 2.8f;

    [Header("Hurt/Death")]
    public float hurtDurationFallback = 0.25f;
    public float deathDurationFallback = 0.8f;

    [Header("Animation base names")]
    public string idleName = "idle";
    public string walkName = "walk";
    public string runName = "run";
    public string hurtName = "hurt";
    public string deathName = "death";
    public string attackName = "attack"; // per dopo

    protected Rigidbody2D rb;
    protected Animator animator;
    protected Health health;

    protected MeleeAttack2D melee;


    protected State state = State.Idle;
    protected Dir facing = Dir.Down;
    protected Dir lockedDir = Dir.Down;

    protected float stateTimer;
    protected string currentAnim = "";
    protected bool deathAnimFrozen;

    protected readonly Dictionary<string, float> clipLength = new();

    // movimento “proposto” dalla AI/derivata (in world space)
    protected Vector2 desiredVelocity = Vector2.zero;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
        melee = GetComponent<MeleeAttack2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        CacheClipLengths();

        health.onHurt += EnterHurt;
        health.onDeath += EnterDeath;

        EnterIdle();
    }

    public virtual void Initialize(EntityDefinition def)
    {
        definition = def;

        // Animazioni data-driven (opzionale)
        if (definition != null && definition.AnimatorController != null)
        {
            animator.runtimeAnimatorController = definition.AnimatorController;
            CacheClipLengths();
        }

        // Drop loot generico (opzionale)
        health.onDeath -= HandleDropLoot; // evita doppioni
        health.onDeath += HandleDropLoot;
    }

    private void HandleDropLoot()
    {
        if (definition != null && definition.LootTable != null)
            definition.LootTable.SpawnLoot(transform.position);
    }

    protected virtual void CacheClipLengths()
    {
        clipLength.Clear();
        if (animator.runtimeAnimatorController == null) return;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && !clipLength.ContainsKey(clip.name))
                clipLength.Add(clip.name, clip.length);
    }

    protected virtual void Update()
    {
        if (state == State.Death)
        {
            if (!deathAnimFrozen)
            {
                PlayAnim();

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    FreezeDeathAnimation();
            }
            return;
        }

        // Lascia alla derivata decidere desiredVelocity e/o cambiare stato
        TickAI();

        if (state == State.Attack && melee != null)
        {
            Vector2 dir = lockedDir switch
            {
                Dir.Up => Vector2.up,
                Dir.Down => Vector2.down,
                Dir.Left => Vector2.left,
                Dir.Right => Vector2.right,
                _ => Vector2.down
            };

            melee.TickAttack(dir);
        }

        // timer stati “bloccanti”
        if (IsTimedState(state))
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                if (state == State.Hurt) EnterIdle();
                else if (state == State.Attack) OnAttackFinished();
            }
        }

        PlayAnim();
    }

    protected virtual void FixedUpdate()
    {
        if (state == State.Walk || state == State.Run)
        {
            rb.linearVelocity = desiredVelocity;
            if (desiredVelocity.sqrMagnitude > 0.001f)
                facing = ResolveFacing(desiredVelocity);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // --- Questa è la “porta” per tutte le AI
    // La derivata deve impostare desiredVelocity e decidere tra Idle/Walk/Run/Attack
    protected abstract void TickAI();

    protected void EnterIdle()
    {
        state = State.Idle;
        desiredVelocity = Vector2.zero;
    }

    protected void EnterWalk(Vector2 velocity)
    {
        state = State.Walk;
        desiredVelocity = velocity;
    }

    protected void EnterRun(Vector2 velocity)
    {
        state = State.Run;
        desiredVelocity = velocity;
    }

    protected void EnterHurt()
    {
        if (state == State.Death) return;

        lockedDir = facing;
        state = State.Hurt;
        stateTimer = GetClipOrFallback(hurtName, lockedDir, hurtDurationFallback);
        desiredVelocity = Vector2.zero;
    }

    protected void EnterDeath()
    {
        lockedDir = facing;
        state = State.Death;
        stateTimer = GetClipOrFallback(deathName, lockedDir, deathDurationFallback);
        deathAnimFrozen = false;
        desiredVelocity = Vector2.zero;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;
    }

    protected virtual void FreezeDeathAnimation()
    {
        if (animator == null) return;

        string clip = $"{deathName}_{lockedDir.ToString().ToLower()}";
        animator.Play(clip, 0, 0.999f);
        animator.Update(0f);
        animator.enabled = false;
        deathAnimFrozen = true;
    }

    protected virtual void StartAttack()
    {
        lockedDir = facing;
        state = State.Attack;
        stateTimer = GetClipOrFallback(attackName, lockedDir, 0.35f);
        desiredVelocity = Vector2.zero;

        if (melee != null)
        {
            Vector2 dir = lockedDir switch
            {
                Dir.Up => Vector2.up,
                Dir.Down => Vector2.down,
                Dir.Left => Vector2.left,
                Dir.Right => Vector2.right,
                _ => Vector2.down
            };

            int dmg = melee.baseDamage; // per ora base
            melee.StartAttack(stateTimer, dir, dmg);
        }
    }

    protected virtual void OnAttackFinished()
    {
        if (melee != null) melee.EndAttack();
        EnterIdle();
    }

    protected bool IsTimedState(State s) =>
        s == State.Hurt || s == State.Death || s == State.Attack;

    protected Dir ResolveFacing(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0) ? Dir.Right : Dir.Left;
        else
            return (v.y >= 0) ? Dir.Up : Dir.Down;
    }

    protected void PlayAnim()
    {
        Dir d = (state == State.Hurt || state == State.Death || state == State.Attack) ? lockedDir : facing;

        string baseName = state switch
        {
            State.Idle => idleName,
            State.Walk => walkName,
            State.Run => runName,
            State.Hurt => hurtName,
            State.Death => deathName,
            State.Attack => attackName,
            _ => idleName
        };

        string clip = $"{baseName}_{d.ToString().ToLower()}";
        if (clip == currentAnim) return;

        animator.Play(clip, 0, 0f);
        currentAnim = clip;
    }

    protected float GetClipOrFallback(string baseName, Dir d, float fallback)
    {
        string clip = $"{baseName}_{d.ToString().ToLower()}";
        return clipLength.TryGetValue(clip, out float len) ? len : fallback;
    }
}