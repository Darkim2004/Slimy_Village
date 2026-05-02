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
    public  static InventoryTooltipUI Instance => GetActiveInstance();

    // ── Componenti runtime ─────────────────────────────────
    private GameObject tooltipGO;
    private RectTransform tooltipRect;
    private Image backgroundImage;
    private HorizontalLayoutGroup layoutGroup;
    private Text label;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    // ── Stato ──────────────────────────────────────────────
    private float hoverTimer;
    private bool  isShowing;
    private string pendingText;

    private void Awake()
    {
        RegisterInstance();
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;

        BuildTooltip();
        Hide();
    }

    private void OnEnable()
    {
        RegisterInstance();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static InventoryTooltipUI GetOrCreateActiveInstance(Transform context)
    {
        var activeInstance = GetActiveInstance();
        if (activeInstance != null)
            return activeInstance;

        var canvas = ResolveTargetCanvas(context);
        if (canvas == null)
            return null;

        var template = FindTemplateInstance();
        var tooltipHost = new GameObject("InventoryTooltipUI_Runtime", typeof(RectTransform));
        tooltipHost.transform.SetParent(canvas.transform, false);
        var hostRect = tooltipHost.transform as RectTransform;
        if (hostRect != null)
        {
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;
        }

        var tooltip = tooltipHost.AddComponent<InventoryTooltipUI>();
        if (template != null && template != tooltip)
            tooltip.CopySettingsFrom(template);

        instance = tooltip;
        return tooltip;
    }

    private static InventoryTooltipUI GetActiveInstance()
    {
        if (IsUsable(instance))
            return instance;

        var tooltips = FindObjectsByType<InventoryTooltipUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tooltips.Length; i++)
        {
            if (!IsUsable(tooltips[i]))
                continue;

            instance = tooltips[i];
            return instance;
        }

        return null;
    }

    private static InventoryTooltipUI FindTemplateInstance()
    {
        if (instance != null)
            return instance;

        var tooltips = FindObjectsByType<InventoryTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tooltips.Length; i++)
        {
            if (tooltips[i] != null)
                return tooltips[i];
        }

        return null;
    }

    private static Canvas ResolveTargetCanvas(Transform context)
    {
        if (context != null)
        {
            var contextCanvas = context.GetComponentInParent<Canvas>();
            if (contextCanvas != null)
                return contextCanvas.rootCanvas != null ? contextCanvas.rootCanvas : contextCanvas;
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas cameraCanvas = null;
        Canvas fallbackCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null || !canvas.gameObject.activeInHierarchy)
                continue;

            if (fallbackCanvas == null)
                fallbackCanvas = canvas;

            if (!canvas.isRootCanvas)
                continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;

            if (cameraCanvas == null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
                cameraCanvas = canvas;
        }

        return cameraCanvas != null ? cameraCanvas : fallbackCanvas;
    }

    private static bool IsUsable(InventoryTooltipUI tooltip)
    {
        return tooltip != null && tooltip.isActiveAndEnabled && tooltip.gameObject.activeInHierarchy;
    }

    private void RegisterInstance()
    {
        if (instance == null || !IsUsable(instance) || IsUsable(this))
            instance = this;
    }

    private void CopySettingsFrom(InventoryTooltipUI source)
    {
        if (source == null || source == this)
            return;

        delay = source.delay;
        backgroundSprite = source.backgroundSprite;
        preserveSpriteColors = source.preserveSpriteColors;
        backgroundSpriteAlpha = source.backgroundSpriteAlpha;
        backgroundColor = source.backgroundColor;
        textColor = source.textColor;
        tooltipFont = source.tooltipFont;
        fontSize = source.fontSize;
        padding = source.padding;
        offset = source.offset;

        ApplyVisualSettings();
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
        backgroundImage = tooltipGO.AddComponent<Image>();
        ApplyBackgroundVisual();

        // Layout: padding automatico attorno al testo
        layoutGroup = tooltipGO.AddComponent<HorizontalLayoutGroup>();
        ApplyLayoutVisual();

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
        ApplyLabelVisual();
    }

    private void ApplyVisualSettings()
    {
        ApplyBackgroundVisual();
        ApplyLayoutVisual();
        ApplyLabelVisual();
    }

    private void ApplyLayoutVisual()
    {
        if (layoutGroup == null) return;

        layoutGroup.padding = new RectOffset(
            Mathf.RoundToInt(padding.x), Mathf.RoundToInt(padding.x),
            Mathf.RoundToInt(padding.y), Mathf.RoundToInt(padding.y));
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private void ApplyBackgroundVisual()
    {
        if (backgroundImage == null) return;

        backgroundImage.raycastTarget = false;

        if (backgroundSprite != null)
        {
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;

            if (preserveSpriteColors)
                backgroundImage.color = new Color(1f, 1f, 1f, backgroundSpriteAlpha);
            else
                backgroundImage.color = backgroundColor;
        }
        else
        {
            backgroundImage.sprite = null;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.color = backgroundColor;
        }
    }

    private void ApplyLabelVisual()
    {
        if (label == null) return;

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
