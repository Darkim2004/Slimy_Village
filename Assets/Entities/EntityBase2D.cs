using System.Collections.Generic;
using System.Linq;
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

    [Header("Hit Audio (Non-Player Entities)")]
    [Tooltip("Se true, riproduce un suono quando questa entita viene colpita (escluso PlayerTopDown).")]
    public bool enableEntityHitSfx = true;

    [Tooltip("Clip casuali riprodotte quando l'entita subisce danno.")]
    public AudioClip[] entityHitSfxClips;

    [Range(0f, 1f)]
    [Tooltip("Volume base locale del suono hit entita (prima del volume globale SFX).")]
    public float entityHitSfxVolume = 1f;

    [Tooltip("Range di pitch random per i colpi alle entita.")]
    public Vector2 entityHitSfxPitchRange = new Vector2(0.95f, 1.05f);

    [Min(0f)]
    [Tooltip("Intervallo minimo tra due suoni hit della stessa entita.")]
    public float entityHitSfxMinInterval = 0.02f;

    [Tooltip("Se true, logga in editor se mancano clip hit entita configurate.")]
    public bool warnMissingEntityHitSfx = true;

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
    private readonly HashSet<string> missingStateLogged = new();
    private float _lastEntityHitSfxTime = float.NegativeInfinity;
    private bool _loggedMissingEntityHitSfx;

    // movimento “proposto” dalla AI/derivata (in world space)
    protected Vector2 desiredVelocity = Vector2.zero;
    private bool aiExternallyPaused;

    public bool IsAIPaused => aiExternallyPaused;

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
        health.onDeath += HandleDropLoot;
        health.onHurtBy += HandleEntityHitAudio;

        EnterIdle();
    }

    protected virtual void OnDestroy()
    {
        if (health == null)
            return;

        health.onHurt -= EnterHurt;
        health.onDeath -= EnterDeath;
        health.onDeath -= HandleDropLoot;
        health.onHurtBy -= HandleEntityHitAudio;
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

        // Mantiene il listener di drop unico anche dopo re-inizializzazioni.
        health.onDeath -= HandleDropLoot;
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

        if (aiExternallyPaused)
        {
            if (state != State.Idle)
                EnterIdle();

            PlayAnim();
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
        if (aiExternallyPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

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

    public void SetAIPaused(bool paused)
    {
        if (aiExternallyPaused == paused)
            return;

        aiExternallyPaused = paused;

        if (aiExternallyPaused)
        {
            EnterIdle();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        OnExternalPauseStateChanged(aiExternallyPaused);
    }

    protected virtual void OnExternalPauseStateChanged(bool paused)
    {
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

    [Header("Death cleanup")]
    [Tooltip("Tempo extra dopo la fine dell'anim di morte prima di distruggere il GO.")]
    public float destroyDelayAfterDeath = 0.15f;

    protected virtual void FreezeDeathAnimation()
    {
        deathAnimFrozen = true;

        // Disattiva l'animator e lo sprite per evitare residui visivi
        if (animator != null) animator.enabled = false;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        Destroy(gameObject, destroyDelayAfterDeath);
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

            int dmg = melee.baseDamage + GetBonusDamage();
            melee.StartAttack(stateTimer, dir, dmg);
        }
    }

    /// <summary>
    /// Danno bonus da aggiungere al baseDamage durante l'attacco.
    /// Le derivate possono sovrascrivere per integrare armi della hotbar, buff, ecc.
    /// </summary>
    protected virtual int GetBonusDamage() => 0;

    protected virtual void OnAttackFinished()
    {
        if (melee != null) melee.EndAttack();
        EnterIdle();
    }

    protected bool IsTimedState(State s) =>
        s == State.Hurt || s == State.Death || s == State.Attack;

    /// <summary>
    /// Resetta l'entità allo stato iniziale (Idle). Riabilita tutti i componenti
    /// disattivati durante la morte (animator, collider, rigidbody, sprite).
    /// Da chiamare dopo aver revivificato la Health.
    /// </summary>
    protected virtual void ResetEntity()
    {
        state = State.Idle;
        deathAnimFrozen = false;
        currentAnim = "";
        desiredVelocity = Vector2.zero;

        if (animator != null) animator.enabled = true;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        if (rb != null) rb.simulated = true;
    }

    protected Dir ResolveFacing(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0) ? Dir.Right : Dir.Left;
        else
            return (v.y >= 0) ? Dir.Up : Dir.Down;
    }

    protected void PlayAnim()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

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

        int stateHash = Animator.StringToHash(clip);
        if (!animator.HasState(0, stateHash))
        {
            // Logga una sola volta per clip mancante per evitare spam in console.
            if (missingStateLogged.Add(clip))
            {
                string controllerName = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "<none>";

                string available = string.Join(", ",
                    animator.runtimeAnimatorController.animationClips
                        .Where(c => c != null)
                        .Select(c => c.name)
                        .Distinct()
                        .OrderBy(n => n));

                Debug.LogError(
                    $"[{name}] Missing animator state '{clip}' in controller '{controllerName}'. " +
                    $"State={state}, Facing={facing}, LockedDir={lockedDir}. Available clips: {available}",
                    this);
            }

            return;
        }

        animator.Play(clip, 0, 0f);
        currentAnim = clip;
    }

    protected float GetClipOrFallback(string baseName, Dir d, float fallback)
    {
        string clip = $"{baseName}_{d.ToString().ToLower()}";
        return clipLength.TryGetValue(clip, out float len) ? len : fallback;
    }

    private void HandleEntityHitAudio(GameObject attacker)
    {
        if (this is PlayerTopDown)
            return;

        if (!enableEntityHitSfx)
            return;

        if (entityHitSfxClips == null || entityHitSfxClips.Length == 0)
        {
#if UNITY_EDITOR
            if (warnMissingEntityHitSfx && !_loggedMissingEntityHitSfx)
            {
                Debug.LogWarning("[EntityBase2D] Entity hit audio non configurato: assegna entityHitSfxClips.", this);
                _loggedMissingEntityHitSfx = true;
            }
#endif
            return;
        }

        if (Time.time < _lastEntityHitSfxTime + Mathf.Max(0f, entityHitSfxMinInterval))
            return;

        AudioClip clip = PickRandomValidClip(entityHitSfxClips);
        if (clip == null)
            return;

        float minPitch = Mathf.Max(0.1f, Mathf.Min(entityHitSfxPitchRange.x, entityHitSfxPitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(entityHitSfxPitchRange.x, entityHitSfxPitchRange.y));
        float pitch = UnityEngine.Random.Range(minPitch, maxPitch);

        GlobalAudioVolume.PlaySfx2D(clip, transform.position, Mathf.Clamp01(entityHitSfxVolume), pitch);
        _lastEntityHitSfxTime = Time.time;
    }

    private static AudioClip PickRandomValidClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int first = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            int index = (first + i) % clips.Length;
            AudioClip clip = clips[index];
            if (clip != null)
                return clip;
        }

        return null;
    }
}