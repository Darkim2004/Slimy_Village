using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerTopDown : EntityBase2D
{
    [Header("Player Stats")]
    [SerializeField] private int playerMaxHp = 20;

    [Header("Input")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode attackKey = KeyCode.J;
    public KeyCode interactKey = KeyCode.E;

    [Header("Interaction")]
    [Tooltip("Raggio di rilevamento per gli oggetti interattivi.")]
    [SerializeField] private float interactRadius = 1.0f;
    [Tooltip("UI fallback se il PlaceableDefinition non ha un menu specifico.")]
    [SerializeField] private WorldInteractionMenuPlaceholderUI interactionMenu;
    [Tooltip("UI menu usato per leggere i libri dalla hotbar. Se null, viene cercato/creato automaticamente.")]
    [SerializeField] private BookReadingMenuUI bookReadingMenu;
    [Tooltip("Parent runtime per i menu specifici instanziati da prefab. Se null usa il primo Canvas trovato.")]
    [SerializeField] private Transform interactionMenusRoot;
    private PlacedObject currentInteractable;
    private PlaceableInteractionMenuBase activeInteractionMenu;
    private BookReadingMenuUI activeBookMenu;
    private readonly Dictionary<PlaceableInteractionMenuBase, PlaceableInteractionMenuBase> menuInstances =
        new Dictionary<PlaceableInteractionMenuBase, PlaceableInteractionMenuBase>();
    private readonly Dictionary<BookReadingMenuUI, BookReadingMenuUI> bookMenuInstances =
        new Dictionary<BookReadingMenuUI, BookReadingMenuUI>();
    private WorldGenTilemap worldGen;

    [Header("Respawn")]
    [Tooltip("Secondi di attesa dopo la morte prima del respawn.")]
    [SerializeField] private float respawnDelay = 1f;

    [Tooltip("Raggio di dispersione degli item droppati alla morte.")]
    [SerializeField] private float dropSpreadRadius = 0.3f;

    private bool inputLocked;
    public bool IsInputLocked => inputLocked;

    private HotbarEffectManager hotbarEffects;
    private HotbarHUD hotbarHUD;
    private InventoryModel inventoryModel;
    private Vector3 respawnPoint;

    protected override void Awake()
    {
        base.Awake();
        health.SetMaxHp(playerMaxHp, refillCurrentHp: true);
    }

    private void Start()
    {
        hotbarEffects = FindFirstObjectByType<HotbarEffectManager>();
        hotbarHUD = FindFirstObjectByType<HotbarHUD>();
        inventoryModel = GetComponentInParent<InventoryModel>();
        if (inventoryModel == null)
            inventoryModel = FindFirstObjectByType<InventoryModel>();

        worldGen = FindFirstObjectByType<WorldGenTilemap>();

        if (interactionMenu == null)
            interactionMenu = FindFirstObjectByType<WorldInteractionMenuPlaceholderUI>();

        if (bookReadingMenu == null)
            bookReadingMenu = FindFirstObjectByType<BookReadingMenuUI>();

        // Inizializza respawnPoint dal WorldGen
        if (worldGen != null && worldGen.HasGenerated)
        {
            respawnPoint = worldGen.WorldSpawnPoint;
            // Posiziona il player allo spawn iniziale
            transform.position = respawnPoint;
        }
        else
        {
            respawnPoint = transform.position;
        }
    }

    private WorldGenTilemap GetWorldGen()
    {
        if (worldGen == null)
            worldGen = FindFirstObjectByType<WorldGenTilemap>();

        return worldGen;
    }

    private bool CanMoveTo(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= 0.0001f)
            return true;

        var world = GetWorldGen();
        if (world == null || world.GroundTilemap == null)
            return true;

        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;
        Vector3Int nextCell = world.GroundTilemap.WorldToCell(nextPosition);
        return world.IsLandCell(nextCell.x, nextCell.y);
    }

    private void OnDestroy()
    {
        foreach (var menu in menuInstances.Values)
        {
            if (menu != null)
                Destroy(menu.gameObject);
        }

        foreach (var menu in bookMenuInstances.Values)
        {
            if (menu != null)
                Destroy(menu.gameObject);
        }

        menuInstances.Clear();
        bookMenuInstances.Clear();
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

    /// <summary>
    /// Espone query sul fatto che qualunque menu di interazione sia aperto,
    /// per uso esterno (es. PauseMenuController per priorità ESC).
    /// </summary>
    public bool IsAnyInteractionMenuOpenPublic()
    {
        return IsAnyInteractionMenuOpen();
    }

    /// <summary>
    /// Chiude tutti i menu di interazione attivi (chest, campfire, tooltip, ecc.).
    /// Per uso esterno (es. PauseMenuController per priorità ESC).
    /// </summary>
    public void CloseAllInteractionMenusPublic()
    {
        CloseAllInteractionMenus();
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
        UpdateInteraction();

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
        bool attackPressed = Input.GetKeyDown(attackKey) || IsMouseAttackPressed();

        if (attackPressed)
        {
            // Non attaccare se si è in build mode
            if (hotbarEffects != null && hotbarEffects.IsBuildModeRequested)
                return;

            if (TryConsumeActiveFood())
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

            if (!CanMoveTo(vel))
            {
                EnterIdle();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
                return;
            }

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
    //  Interactions
    // ══════════════════════════════════════════════════════════
    private void UpdateInteraction()
    {
        bool mouseInteractPressed = IsMouseInteractPressed();
        bool interactPressed = Input.GetKeyDown(interactKey) || mouseInteractPressed;

        if (state == State.Death || state == State.Hurt)
        {
            CloseActiveInteractionMenu();
            CloseBookMenus();
            if (WorldInteractionTooltipUI.Instance != null) WorldInteractionTooltipUI.Instance.Hide();
            return;
        }

        if (inputLocked)
        {
            // Evitiamo di chiudere i menu di interazione già attivi (come la chest),
            // altrimenti si chiuderebbero nello stesso frame in cui bloccano il player.
            CloseBookMenus();
            if (WorldInteractionTooltipUI.Instance != null) WorldInteractionTooltipUI.Instance.Hide();
            return;
        }

        if (TryHandleBookInteraction(mouseInteractPressed))
        {
            if (WorldInteractionTooltipUI.Instance != null)
                WorldInteractionTooltipUI.Instance.Hide();
            return;
        }

        // Toggle robusto: se qualunque menu di interazione è aperto,
        // premere E lo richiude anche se il riferimento attivo non è più valido.
        if (interactPressed && IsAnyInteractionMenuOpen())
        {
            CloseAllInteractionMenus();
            if (WorldInteractionTooltipUI.Instance != null)
                WorldInteractionTooltipUI.Instance.Hide();
            return;
        }

        PlacedObject closestInteractable = FindClosestInteractable();

        if (activeInteractionMenu != null && !activeInteractionMenu.gameObject.activeInHierarchy)
        {
            activeInteractionMenu = null;
        }

        if (activeInteractionMenu != null && activeInteractionMenu.IsOpen)
        {
            if (interactPressed)
                CloseActiveInteractionMenu();

            if (WorldInteractionTooltipUI.Instance != null)
                WorldInteractionTooltipUI.Instance.Hide();
            return;
        }

        currentInteractable = closestInteractable;

        if (currentInteractable != null)
        {
            if (WorldInteractionTooltipUI.Instance != null)
            {
                string text = currentInteractable.definition.interactionText;
                if (string.IsNullOrWhiteSpace(text))
                    text = currentInteractable.definition.name;

                WorldInteractionTooltipUI.Instance.Show(text, currentInteractable.transform);
            }

            if (interactPressed)
            {
                var menuToOpen = ResolveMenuFor(currentInteractable);

                if (menuToOpen != null)
                {
                    activeInteractionMenu = menuToOpen;
                    activeInteractionMenu.Show(currentInteractable);
                }
            }
        }
        else
        {
            if (WorldInteractionTooltipUI.Instance != null)
            {
                WorldInteractionTooltipUI.Instance.Hide();
            }
        }
    }

    private bool IsMouseAttackPressed()
    {
        if (!Input.GetMouseButtonDown(0))
            return false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        return true;
    }

    private bool IsMouseInteractPressed()
    {
        if (!Input.GetMouseButtonDown(1))
            return false;

        if (hotbarEffects != null && hotbarEffects.IsBuildModeRequested)
            return false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        return true;
    }

    private bool TryHandleBookInteraction(bool rightClickPressed)
    {
        var activeDef = hotbarEffects != null ? hotbarEffects.ActiveItemDef : null;
        bool isBookSelected = activeDef != null && activeDef.IsBook;
        bool rawRightClickPressed = Input.GetMouseButtonDown(1);

        if (!isBookSelected)
        {
            if (activeBookMenu != null)
                activeBookMenu.Hide();
            activeBookMenu = null;
            return false;
        }

        if (activeBookMenu != null && activeBookMenu.IsOpen)
        {
            activeBookMenu.SetBook(activeDef);

            if (rightClickPressed || rawRightClickPressed)
            {
                activeBookMenu.Hide();
                activeBookMenu = null;
                return true;
            }

            // Quando il libro è aperto blocchiamo le interazioni col mondo.
            return true;
        }

        if (!rightClickPressed)
            return false;

        activeBookMenu = ResolveBookMenuFor(activeDef);
        if (activeBookMenu == null)
            return false;

        activeBookMenu.Show(activeDef);
        return true;
    }

    private BookReadingMenuUI ResolveBookMenuFor(ItemDefinition bookDef)
    {
        if (bookDef == null || !bookDef.IsBook)
            return null;

        if (bookDef.bookMenuPrefab != null)
            return GetOrCreateBookMenuInstance(bookDef.bookMenuPrefab);

        if (bookReadingMenu == null)
            bookReadingMenu = FindFirstObjectByType<BookReadingMenuUI>();

        if (bookReadingMenu == null)
        {
            var go = new GameObject("BookReadingMenuUI_Runtime");

            Transform parent = interactionMenusRoot;
            if (parent == null)
            {
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    parent = canvas.transform;
            }

            if (parent != null)
                go.transform.SetParent(parent, false);

            bookReadingMenu = go.AddComponent<BookReadingMenuUI>();
        }

        return bookReadingMenu;
    }

    private BookReadingMenuUI GetOrCreateBookMenuInstance(BookReadingMenuUI menuPrefab)
    {
        if (menuPrefab == null)
            return null;

        if (bookMenuInstances.TryGetValue(menuPrefab, out var instance) && instance != null)
            return instance;

        Transform parent = interactionMenusRoot;
        if (parent == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                parent = canvas.transform;
        }

        instance = Instantiate(menuPrefab, parent);
        instance.gameObject.name = menuPrefab.gameObject.name + "_Runtime";
        instance.Hide();

        bookMenuInstances[menuPrefab] = instance;
        return instance;
    }

    private void CloseBookMenus()
    {
        if (activeBookMenu != null && activeBookMenu.IsOpen)
            activeBookMenu.Hide();

        if (bookReadingMenu != null && bookReadingMenu.IsOpen)
            bookReadingMenu.Hide();

        foreach (var menu in bookMenuInstances.Values)
        {
            if (menu != null && menu.IsOpen)
                menu.Hide();
        }

        activeBookMenu = null;
    }

    private bool TryConsumeActiveFood()
    {
        var activeDef = hotbarEffects != null ? hotbarEffects.ActiveItemDef : null;
        if (activeDef == null || !activeDef.IsFood)
            return false;

        if (health == null || health.IsDead || health.CurrentHp >= health.MaxHp)
            return false;

        if (inventoryModel == null || hotbarHUD == null || inventoryModel.Hotbar == null)
            return false;

        int selectedIndex = hotbarHUD.SelectedIndex;
        var selectedStack = inventoryModel.Hotbar.GetSlot(selectedIndex);
        if (selectedStack == null || selectedStack.IsEmpty || selectedStack.def != activeDef)
            return false;

        selectedStack.amount -= 1;
        if (selectedStack.amount <= 0)
            inventoryModel.Hotbar.SetSlot(selectedIndex, null);
        else
            inventoryModel.Hotbar.SetSlot(selectedIndex, selectedStack);

        health.Heal(activeDef.healAmount);
        return true;
    }

    private PlaceableInteractionMenuBase ResolveMenuFor(PlacedObject interactable)
    {
        if (interactable == null || interactable.definition == null)
            return null;

        var menuPrefab = interactable.definition.interactionMenuPrefab;
        if (menuPrefab != null)
            return GetOrCreateMenuInstance(menuPrefab);

        if (interactionMenu == null)
            interactionMenu = WorldInteractionMenuPlaceholderUI.Instance;

        return interactionMenu;
    }

    private PlaceableInteractionMenuBase GetOrCreateMenuInstance(PlaceableInteractionMenuBase menuPrefab)
    {
        if (menuPrefab == null)
            return null;

        if (menuInstances.TryGetValue(menuPrefab, out var instance) && instance != null)
            return instance;

        Transform parent = interactionMenusRoot;
        if (parent == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                parent = canvas.transform;
        }

        instance = Instantiate(menuPrefab, parent);
        instance.gameObject.name = menuPrefab.gameObject.name + "_Runtime";
        instance.Hide();

        menuInstances[menuPrefab] = instance;
        return instance;
    }

    private void CloseActiveInteractionMenu()
    {
        if (activeInteractionMenu == null)
            return;

        activeInteractionMenu.Hide();
        activeInteractionMenu = null;
    }

    private bool IsAnyInteractionMenuOpen()
    {
        if (activeBookMenu != null && activeBookMenu.IsOpen)
            return true;

        if (bookReadingMenu != null && bookReadingMenu.IsOpen)
            return true;

        foreach (var menu in bookMenuInstances.Values)
        {
            if (menu != null && menu.IsOpen)
                return true;
        }

        if (activeInteractionMenu != null && activeInteractionMenu.IsOpen)
            return true;

        if (interactionMenu != null && interactionMenu.IsOpen)
            return true;

        foreach (var menu in menuInstances.Values)
        {
            if (menu != null && menu.IsOpen)
                return true;
        }

        return false;
    }

    private void CloseAllInteractionMenus()
    {
        CloseBookMenus();

        CloseActiveInteractionMenu();

        if (interactionMenu != null && interactionMenu.IsOpen)
            interactionMenu.Hide();

        foreach (var menu in menuInstances.Values)
        {
            if (menu != null && menu.IsOpen)
                menu.Hide();
        }

        activeInteractionMenu = null;
    }

    private PlacedObject FindClosestInteractable()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactRadius);
        PlacedObject closestInteractable = null;
        float minDst = float.MaxValue;

        foreach (var col in colliders)
        {
            var placedObj = col.GetComponentInParent<PlacedObject>();
            if (!IsInteractable(placedObj))
                continue;

            float dst = Vector2.Distance(transform.position, placedObj.transform.position);
            if (dst < minDst)
            {
                minDst = dst;
                closestInteractable = placedObj;
            }
        }

        // Fallback: oggetti senza collider non entrano in OverlapCircleAll,
        // quindi facciamo un controllo per distanza sui PlacedObject in scena.
        if (closestInteractable == null)
        {
            var allPlacedObjects = FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
            foreach (var placedObj in allPlacedObjects)
            {
                if (!IsInteractable(placedObj))
                    continue;

                float dst = Vector2.Distance(transform.position, placedObj.transform.position);
                if (dst > interactRadius)
                    continue;

                if (dst < minDst)
                {
                    minDst = dst;
                    closestInteractable = placedObj;
                }
            }
        }

        return closestInteractable;
    }

    private static bool IsInteractable(PlacedObject placedObj)
    {
        return placedObj != null &&
               placedObj.definition != null &&
               placedObj.definition.canInteract;
    }

    // ══════════════════════════════════════════════════════════
    //  Gizmo: mostra il respawn point nella Scene view
    // ══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(respawnPoint, 0.35f);
        Gizmos.DrawLine(respawnPoint + Vector3.down * 0.5f, respawnPoint + Vector3.up * 0.5f);
        Gizmos.DrawLine(respawnPoint + Vector3.left * 0.5f, respawnPoint + Vector3.right * 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
