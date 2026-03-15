using UnityEngine;

/// <summary>
/// Preview semi-trasparente dell'oggetto da piazzare.
/// Segue la posizione del mouse agganciandosi alla griglia.
/// Colore verde se la posizione è valida, rosso se invalida.
/// </summary>
public class PlacementGhost : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Colore quando la posizione è valida.")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);

    [Tooltip("Colore quando la posizione è invalida.")]
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.4f);

    [Header("Sorting")]
    [Tooltip("Sorting layer per il ghost.")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Ordine nel sorting layer (alto = davanti).")]
    [SerializeField] private int sortingOrderOffset = 1000;

    private SpriteRenderer spriteRenderer;
    private PlaceableDefinition currentDefinition;
    private bool isValid;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrderOffset;

        // Inizia nascosto
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Mostra il ghost con la sprite della definizione indicata.
    /// </summary>
    public void Show(PlaceableDefinition definition)
    {
        if (definition == null)
        {
            Hide();
            return;
        }

        currentDefinition = definition;

        Sprite ghostSprite = definition.GetGhostSprite();
        spriteRenderer.sprite = ghostSprite;

        gameObject.SetActive(true);
        SetValid(true);
    }

    /// <summary>
    /// Nasconde il ghost.
    /// </summary>
    public void Hide()
    {
        currentDefinition = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Aggiorna la posizione del ghost (snap alla griglia) e lo stato valido/invalido.
    /// </summary>
    public void UpdatePosition(Vector3 worldPos, bool canPlace)
    {
        if (!gameObject.activeSelf) return;

        transform.position = worldPos;
        SetValid(canPlace);
    }

    /// <summary>
    /// Imposta il colore del ghost in base alla validità.
    /// </summary>
    public void SetValid(bool valid)
    {
        isValid = valid;
        spriteRenderer.color = valid ? validColor : invalidColor;
    }

    /// <summary>
    /// True se il ghost è attualmente visibile.
    /// </summary>
    public bool IsActive => gameObject.activeSelf;

    /// <summary>
    /// La definizione corrente mostrata dal ghost.
    /// </summary>
    public PlaceableDefinition CurrentDefinition => currentDefinition;
}
