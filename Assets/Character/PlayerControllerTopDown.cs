using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerControllerTopDown : MonoBehaviour
{
    public enum PlayerState
    {
        Idle, Walk, Run,
        Attack,
        Hurt, Death
    }

    public enum FacingDir { Up, Down, Left, Right }

    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 4.5f;

    [Header("Combat / Health")]
    public int maxHp = 5;
    public float hurtDurationFallback = 0.25f;   // usato se non trova la clip
    public float deathDurationFallback = 0.8f;   // usato se non trova la clip

    [Header("Input (old Input Manager)")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode attackKey = KeyCode.J;

    private Rigidbody2D rb;
    private Animator animator;

    private PlayerState state = PlayerState.Idle;
    private FacingDir facing = FacingDir.Down;

    // Direzione “lockata” all'inizio di attack/hurt/death per coerenza animazione
    private FacingDir lockedDir;

    private Vector2 moveInput;
    private bool runHeld;
    private bool attackPressedThisFrame;
    private bool inputLocked;

    private int hp;

    // Timer di stato (attack/hurt/death). Usiamo la durata della clip se esiste.
    private float stateTimer = 0f;

    // Cache clip lengths (per non cercare ogni volta)
    private readonly Dictionary<string, float> clipLength = new();

    // Per evitare di chiamare Play() ogni frame
    private string currentAnim = "";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        hp = maxHp;

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        CacheClipLengths();
        lockedDir = facing;
    }

    private void CacheClipLengths()
    {
        clipLength.Clear();
        if (animator.runtimeAnimatorController == null) return;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            // se nomi duplicati, tieni il primo
            if (!clipLength.ContainsKey(clip.name))
                clipLength.Add(clip.name, clip.length);
        }
    }

    private void Update()
    {
        ReadInput();

        // Priorità: Death > Hurt > Attack > Locomotion
        if (hp <= 0 && state != PlayerState.Death)
        {
            EnterDeath();
        }

        // Aggiorna timer stato (attack/hurt/death)
        if (IsTimedState(state))
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                // Se Death finisce, resti comunque in Death.
                if (state != PlayerState.Death)
                    EnterLocomotionFromInput();
            }
        }
        else
        {
            // Se non sei in timed state, gestisci attacco o locomotion
            if (attackPressedThisFrame)
            {
                EnterAttackFromInput();
            }
            else
            {
                EnterLocomotionFromInput();
            }
        }

        // Aggiorna animazione
        PlayAnimationForCurrentState();
    }

    private void FixedUpdate()
    {
        // Movimento consentito solo in Idle/Walk/Run (attack/hurt/death bloccano)
        if (state == PlayerState.Walk || state == PlayerState.Run)
        {
            float speed = (state == PlayerState.Run) ? runSpeed : walkSpeed;
            Vector2 v = moveInput.normalized * speed;
            rb.linearVelocity = v;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void ReadInput()
    {
        if (inputLocked)
        {
            moveInput = Vector2.zero;
            runHeld = false;
            attackPressedThisFrame = false;
            return;
        }

        float x = Input.GetAxisRaw(horizontalAxis);
        float y = Input.GetAxisRaw(verticalAxis);
        moveInput = new Vector2(x, y);

        runHeld = Input.GetKey(runKey);
        attackPressedThisFrame = Input.GetKeyDown(attackKey);

        // Aggiorna facing SOLO se ti stai muovendo e NON sei in stati che lockano dir
        if (moveInput.sqrMagnitude > 0.001f && !LocksFacing(state))
        {
            facing = ResolveFacing(moveInput);
        }
    }

    private FacingDir ResolveFacing(Vector2 v)
    {
        // 4 direzioni: scegli asse dominante
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return (v.x >= 0) ? FacingDir.Right : FacingDir.Left;
        else
            return (v.y >= 0) ? FacingDir.Up : FacingDir.Down;
    }

    private void EnterLocomotionFromInput()
    {
        if (hp <= 0) { EnterDeath(); return; }

        if (moveInput.sqrMagnitude <= 0.001f)
            SetState(PlayerState.Idle);
        else
            SetState(runHeld ? PlayerState.Run : PlayerState.Walk);
    }

    private void EnterAttackFromInput()
    {
        if (hp <= 0) { EnterDeath(); return; }
        if (state == PlayerState.Hurt || state == PlayerState.Death) return;

        lockedDir = facing;

        // Ferma subito il movimento quando si attacca
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        SetTimedState(PlayerState.Attack, GetClipOrFallback("attack", lockedDir, 0.35f));
    }

    private void EnterHurt()
    {
        if (state == PlayerState.Death) return;

        // Hurt interrompe attack: basta entrare in Hurt (priorità alta)
        lockedDir = facing;
        SetTimedState(PlayerState.Hurt, GetClipOrFallback("hurt", lockedDir, hurtDurationFallback));
    }

    private void EnterDeath()
    {
        lockedDir = facing;
        SetTimedState(PlayerState.Death, GetClipOrFallback("death", lockedDir, deathDurationFallback));
    }

    private void SetState(PlayerState newState)
    {
        if (state == newState) return;
        state = newState;
    }

    private void SetTimedState(PlayerState newState, float duration)
    {
        state = newState;
        stateTimer = Mathf.Max(0.01f, duration);
    }

    private bool IsTimedState(PlayerState s) =>
        s == PlayerState.Attack ||
        s == PlayerState.Hurt || s == PlayerState.Death;

    private bool LocksFacing(PlayerState s) =>
        s == PlayerState.Attack ||
        s == PlayerState.Hurt || s == PlayerState.Death;

    private void PlayAnimationForCurrentState()
    {
        FacingDir dirToUse = LocksFacing(state) ? lockedDir : facing;

        string baseName = state switch
        {
            PlayerState.Idle => "idle",
            PlayerState.Walk => "walk",
            PlayerState.Run => "run",
            PlayerState.Attack => "attack",
            PlayerState.Hurt => "hurt",
            PlayerState.Death => "death",
            _ => "idle"
        };

        string dirName = dirToUse.ToString().ToLower(); // up/down/left/right
        string clipName = $"{baseName}_{dirName}";

        if (clipName == currentAnim) return;

        // Prova a playare; se lo stato non esiste nel controller, non succede nulla.
        animator.Play(clipName, 0, 0f);
        currentAnim = clipName;
    }

    private float GetClipOrFallback(string baseName, FacingDir dir, float fallback)
    {
        string dirName = dir.ToString().ToLower();
        string clipName = $"{baseName}_{dirName}";
        return clipLength.TryGetValue(clipName, out float len) ? len : fallback;
    }

    // === API PUBBLICA per danno ===
    public void TakeDamage(int amount)
    {
        if (state == PlayerState.Death) return;

        hp -= Mathf.Abs(amount);
        if (hp <= 0)
        {
            hp = 0;
            EnterDeath();
        }
        else
        {
            EnterHurt();
        }
    }

    // === API PUBBLICA per lock input (inventario/UI) ===
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        if (!locked) return;

        moveInput = Vector2.zero;
        runHeld = false;
        attackPressedThisFrame = false;
        rb.linearVelocity = Vector2.zero;

        if (state == PlayerState.Walk || state == PlayerState.Run)
            SetState(PlayerState.Idle);
    }

    public int CurrentHp => hp;
}