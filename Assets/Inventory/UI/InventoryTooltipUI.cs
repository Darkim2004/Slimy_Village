using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tooltip che mostra il nome dell'item quando il mouse resta sopra uno slot per un certo tempo.
/// Creato a runtime, non richiede prefab: basta un Canvas nella scena.
/// Il rettangolo di sfondo si adatta automaticamente alla lunghezza del testo.
/// </summary>
public class InventoryTooltipUI : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float delay = 0.4f;

    [Header("Visual")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private bool preserveSpriteColors = true;
    [Range(0f, 1f)]
    [SerializeField] private float backgroundSpriteAlpha = 1f;
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color textColor       = Color.white;
    [SerializeField] private Font tooltipFont;
    [SerializeField] private int   fontSize         = 18;
    [SerializeField] private Vector2 padding        = new Vector2(12f, 6f);
    [SerializeField] private Vector2 offset         = new Vector2(0f, 12f);

    // ── Singleton leggero ──────────────────────────────────
    private static InventoryTooltipUI instance;
    public  static InventoryTooltipUI Instance => instance;

    // ── Componenti runtime ─────────────────────────────────
    private GameObject tooltipGO;
    private RectTransform tooltipRect;
    private Text label;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    // ── Stato ──────────────────────────────────────────────
    private float hoverTimer;
    private bool  isShowing;
    private string pendingText;

    private void Awake()
    {
        instance = this;
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;

        BuildTooltip();
        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ══════════════════════════════════════════════════════════
    //  API pubblica
    // ══════════════════════════════════════════════════════════

    /// <summary>Inizia il timer; se scade mostra il tooltip col testo dato.</summary>
    public void RequestShow(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        pendingText = text;
        hoverTimer  = 0f;

        if (isShowing)
            ApplyText(text);
    }

    /// <summary>Nasconde immediatamente il tooltip e resetta il timer.</summary>
    public void Hide()
    {
        hoverTimer  = 0f;
        isShowing   = false;
        pendingText = null;
        if (tooltipGO != null)
            tooltipGO.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════
    //  Update
    // ══════════════════════════════════════════════════════════

    private void LateUpdate()
    {
        if (pendingText == null) return;

        if (!isShowing)
        {
            hoverTimer += Time.unscaledDeltaTime;
            if (hoverTimer < delay) return;

            ApplyText(pendingText);
            isShowing = true;
            tooltipGO.SetActive(true);
        }

        FollowMouse();
        ClampToScreen();
    }

    // ══════════════════════════════════════════════════════════
    //  Costruzione runtime del tooltip
    // ══════════════════════════════════════════════════════════

    private void BuildTooltip()
    {
        tooltipGO = new GameObject("InventoryTooltip");
        tooltipGO.transform.SetParent(transform, false);

        // RectTransform
        tooltipRect = tooltipGO.AddComponent<RectTransform>();
        tooltipRect.pivot = new Vector2(0.5f, 0f);

        // Sfondo
        var bg = tooltipGO.AddComponent<Image>();
        bg.raycastTarget = false;

        if (backgroundSprite != null)
        {
            bg.sprite = backgroundSprite;
            bg.type   = Image.Type.Simple;
            bg.preserveAspect = false;

            if (preserveSpriteColors)
                bg.color = new Color(1f, 1f, 1f, backgroundSpriteAlpha);
            else
                bg.color = backgroundColor;
        }
        else
        {
            bg.color = backgroundColor;
        }

        // Layout: padding automatico attorno al testo
        var hlg = tooltipGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(
            Mathf.RoundToInt(padding.x), Mathf.RoundToInt(padding.x),
            Mathf.RoundToInt(padding.y), Mathf.RoundToInt(padding.y));
        hlg.childAlignment    = TextAnchor.MiddleCenter;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Content size fitter → il rettangolo si adatta al contenuto
        var csf = tooltipGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Canvas proprio per stare sempre in cima
        var tipCanvas = tooltipGO.AddComponent<Canvas>();
        tipCanvas.overrideSorting = true;
        tipCanvas.sortingOrder    = 600;

        tooltipGO.AddComponent<GraphicRaycaster>();

        // Testo figlio
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(tooltipGO.transform, false);
        textGO.AddComponent<RectTransform>();

        label = textGO.AddComponent<Text>();
        label.font          = tooltipFont != null
            ? tooltipFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize      = fontSize;
        label.color         = textColor;
        label.alignment     = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow   = VerticalWrapMode.Overflow;
    }

    private void ApplyText(string text)
    {
        if (label != null)
            label.text = text;
    }

    private void FollowMouse()
    {
        if (tooltipRect == null || canvasRect == null) return;

        Vector2 screenPos = (Vector2)Input.mousePosition + offset;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out var localPoint);

        tooltipRect.localPosition = localPoint;
    }

    private void ClampToScreen()
    {
        if (tooltipRect == null || canvasRect == null) return;

        // Forza un rebuild del layout prima di leggere le dimensioni
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

        Vector3[] corners = new Vector3[4];
        tooltipRect.GetWorldCorners(corners);

        Vector3[] canvasCorners = new Vector3[4];
        canvasRect.GetWorldCorners(canvasCorners);

        float minX = canvasCorners[0].x, maxX = canvasCorners[2].x;
        float minY = canvasCorners[0].y, maxY = canvasCorners[2].y;

        Vector3 pos = tooltipRect.position;

        float tipMinX = corners[0].x, tipMaxX = corners[2].x;
        float tipMinY = corners[0].y, tipMaxY = corners[2].y;

        if (tipMaxX > maxX) pos.x -= tipMaxX - maxX;
        if (tipMinX < minX) pos.x += minX - tipMinX;
        if (tipMaxY > maxY) pos.y -= tipMaxY - maxY;
        if (tipMinY < minY) pos.y += minY - tipMinY;

        tooltipRect.position = pos;
    }
}
