using System.Collections;
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

    [Header("Respawn")]
    [Tooltip("Secondi di attesa dopo la morte prima del respawn.")]
    [SerializeField] private float respawnDelay = 1f;

    [Tooltip("Raggio di dispersione degli item droppati alla morte.")]
    [SerializeField] private float dropSpreadRadius = 0.3f;

    private bool inputLocked;
    private HotbarEffectManager hotbarEffects;
    private Vector3 respawnPoint;

    protected override void Awake()
    {
        base.Awake();
        health.SetMaxHp(playerMaxHp, refillCurrentHp: true);
    }

    private void Start()
    {
        hotbarEffects = FindFirstObjectByType<HotbarEffectManager>();

        // Inizializza respawnPoint dal WorldGen
        var world = FindFirstObjectByType<WorldGenTilemap>();
        if (world != null && world.HasGenerated)
        {
            respawnPoint = world.WorldSpawnPoint;
            // Posiziona il player allo spawn iniziale
            transform.position = respawnPoint;
        }
        else
        {
            respawnPoint = transform.position;
        }
    }

    /// <summary>
    /// Imposta un punto di respawn personalizzato (es. letto, checkpoint).
    /// </summary>
    public void SetRespawnPoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public Vector3 RespawnPoint => respawnPoint;

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (!locked) return;

        EnterIdle();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    protected override int GetBonusDamage()
    {
        return hotbarEffects != null ? hotbarEffects.WeaponBonusDamage : 0;
    }

    protected override void FreezeDeathAnimation()
    {
        // NON distruggere il GO: avvia la coroutine di respawn
        deathAnimFrozen = true;
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        // Disabilita la sprite e aspetta
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        if (animator != null) animator.enabled = false;

        // Droppa tutti gli item nella posizione di morte
        DropAllItems(transform.position);

        yield return new WaitForSeconds(respawnDelay);

        // Teletrasporta al punto di respawn
        transform.position = respawnPoint;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Resetta salute e stato dell'entità
        health.Revive();
        ResetEntity();

        // Forza l'animazione idle
        facing = Dir.Down;
        PlayAnim();
    }

    /// <summary>
    /// Droppa tutto l'inventario (Hotbar, Main, Armor) come WorldDrop
    /// alla posizione indicata, con offset randomico per ogni item.
    /// </summary>
    private void DropAllItems(Vector3 deathPos)
    {
        var inventory = GetComponentInParent<InventoryModel>();
        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryModel>();
        if (inventory == null) return;

        foreach (var section in inventory.Sections)
        {
            for (int i = 0; i < section.Size; i++)
            {
                var stack = section.GetSlot(i);
                if (stack == null || stack.IsEmpty) continue;

                // Offset randomico dentro un cerchio
                Vector2 rndOffset = UnityEngine.Random.insideUnitCircle * dropSpreadRadius;
                Vector3 dropPos = deathPos + new Vector3(rndOffset.x, rndOffset.y, 0f);

                WorldDrop.Spawn(stack.def, stack.amount, dropPos);

                // Svuota lo slot
                section.SetSlot(i, null);
            }
        }
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
            // Non attaccare se si è in build mode
            if (hotbarEffects != null && hotbarEffects.IsBuildModeRequested)
                return;

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

    // ══════════════════════════════════════════════════════════
    //  Debug Cheats (solo Editor / Development Build)
    // ══════════════════════════════════════════════════════════
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            health.SetHp(1);
            Debug.Log($"[DEBUG] Player HP → 1/{health.MaxHp}");
        }
        else if (Input.GetKeyDown(KeyCode.F10))
        {
            health.SetHp(0);
            Debug.Log("[DEBUG] Player killed!");
        }
        else if (Input.GetKeyDown(KeyCode.F11))
        {
            health.SetHp(health.MaxHp);
            Debug.Log($"[DEBUG] Player HP → {health.MaxHp}/{health.MaxHp}");
        }
    }
#endif

    // ══════════════════════════════════════════════════════════
    //  Gizmo: mostra il respawn point nella Scene view
    // ══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(respawnPoint, 0.35f);
        Gizmos.DrawLine(respawnPoint + Vector3.down * 0.5f, respawnPoint + Vector3.up * 0.5f);
        Gizmos.DrawLine(respawnPoint + Vector3.left * 0.5f, respawnPoint + Vector3.right * 0.5f);
    }
}
