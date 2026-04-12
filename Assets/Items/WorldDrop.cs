using UnityEngine;

/// <summary>
/// Oggetto droppato nel mondo con animazione di galleggiamento.
/// Quando il player si avvicina entro <see cref="pickupRadius"/>, viene raccolto nell'inventario.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WorldDrop : MonoBehaviour
{
    private const string WorldDropsRootName = "WorldDropsRoot";
    private static Transform cachedWorldDropsRoot;

    [Header("Item")]
    [Tooltip("Definizione dell'item contenuto in questo drop.")]
    [SerializeField] private ItemDefinition itemDef;
    [SerializeField] private int amount = 1;

    [Tooltip("Scala visiva dell'oggetto a terra (es. 0.6 per renderlo più piccolo).")]
    [SerializeField] private float itemScale = 0.6f;

    [Header("Pickup")]
    [Tooltip("Raggio in unità mondo entro cui il player raccoglie il drop (0.5 = mezza casella).")]
    [SerializeField] private float pickupRadius = 0.5f;

    [Tooltip("Ritardo prima che il drop sia raccoglibile (evita pickup istantaneo).")]
    [SerializeField] private float pickupDelay = 0.4f;

    [Header("Float Animation")]
    [SerializeField] private float floatAmplitude = 0.08f;
    [SerializeField] private float floatSpeed = 2.5f;

    private SpriteRenderer sr;
    private Transform playerTransform;
    private InventoryModel playerInventory;
    private Health playerHealth;
    private float spawnTime;
    private Vector3 basePosition;

    public ItemDefinition ItemDefinition => itemDef;
    public int Amount => amount;

    // ══════════════════════════════════════════════════════════
    //  Factory statica — usata da LootTable.SpawnLoot
    // ══════════════════════════════════════════════════════════

    public static WorldDrop Spawn(ItemDefinition def, int qty, Vector3 position)
    {
        if (def == null || qty <= 0) return null;

        var go = new GameObject($"Drop_{def.id}");
        var dropsRoot = GetOrCreateDropsRoot();
        if (dropsRoot != null)
            go.transform.SetParent(dropsRoot, false);

        go.transform.position = position;

        var drop = go.AddComponent<WorldDrop>();
        drop.itemDef = def;
        drop.amount = qty;

        return drop;
    }

    // ══════════════════════════════════════════════════════════
    //  Lifecycle
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        spawnTime = Time.time;
        basePosition = transform.position;

        // Imposta la dimensione
        transform.localScale = new Vector3(itemScale, itemScale, 1f);

        // Sprite dall'ItemDefinition
        if (itemDef != null && itemDef.icon != null)
            sr.sprite = itemDef.icon;

        // Tiene i drop visibili anche in scene con sorting layer personalizzati.
        sr.sortingOrder = 10;

        // Trova player e il suo inventario
        var player = FindFirstObjectByType<PlayerTopDown>();
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<Health>();
            playerInventory = player.GetComponentInParent<InventoryModel>();
            var playerSprite = player.GetComponentInChildren<SpriteRenderer>();
            if (playerSprite != null)
            {
                sr.sortingLayerID = playerSprite.sortingLayerID;
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, playerSprite.sortingOrder + 1);
            }

            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<InventoryModel>();
        }
    }

    private void Update()
    {
        // Animazione di galleggiamento
        float offset = Mathf.Sin((Time.time - spawnTime) * floatSpeed) * floatAmplitude;
        transform.position = basePosition + new Vector3(0f, offset, 0f);

        // Pickup check
        if (playerTransform == null || playerInventory == null) return;
        if (Time.time - spawnTime < pickupDelay) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist <= pickupRadius)
            TryPickup();
    }

    private void TryPickup()
    {
        if (itemDef == null || amount <= 0) return;

        int leftover = playerInventory.AddItem(itemDef, amount);

        if (leftover < amount)
        {
            // Almeno qualcosa è stato raccolto
            amount = leftover;

            if (amount <= 0)
                Destroy(gameObject);
        }
        // Se leftover == amount, inventario pieno: il drop resta a terra
    }

    // ══════════════════════════════════════════════════════════
    //  Gizmo per debug
    // ══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    private static Transform GetOrCreateDropsRoot()
    {
        if (cachedWorldDropsRoot != null)
            return cachedWorldDropsRoot;

        var existing = GameObject.Find(WorldDropsRootName);
        if (existing != null)
        {
            cachedWorldDropsRoot = existing.transform;
            return cachedWorldDropsRoot;
        }

        var root = new GameObject(WorldDropsRootName);
        cachedWorldDropsRoot = root.transform;
        return cachedWorldDropsRoot;
    }
}
