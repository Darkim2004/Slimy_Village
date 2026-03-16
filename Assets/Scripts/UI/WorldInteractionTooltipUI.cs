using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tooltip che mostra il nome dell'oggetto interattivo corrente.
/// Creato a runtime come InventoryTooltipUI, si posiziona sopra l'oggetto nel mondo rispetto alla telecamera.
/// </summary>
public class WorldInteractionTooltipUI : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private bool preserveSpriteColors = true;
    [Range(0f, 1f)]
    [SerializeField] private float backgroundSpriteAlpha = 0.9f;
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color textColor       = Color.white;
    [SerializeField] private Font tooltipFont;
    [SerializeField] private int   fontSize         = 16;
    [SerializeField] private Vector2 padding        = new Vector2(12f, 6f);
    
    [Tooltip("Offset verticale in coordinate mondo dal trasform target (es. +1 unità y = sopra l'oggetto).")]
    [SerializeField] private float worldOffsetY = 1f;

    // ── Singleton ──────────────────────────────────────────
    private static WorldInteractionTooltipUI instance;
    public static WorldInteractionTooltipUI Instance
    {
        get
        {
            if (instance == null)
                EnsureInstance();
            return instance;
        }
    }

    // ── Componenti runtime ─────────────────────────────────
    private GameObject tooltipGO;
    private RectTransform tooltipRect;
    private Text label;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    // ── Stato ──────────────────────────────────────────────
    private bool isShowing;
    private Transform targetTransform;
    private Camera mainCamera;

    private static void EnsureInstance()
    {
        if (instance != null) return;

        instance = FindFirstObjectByType<WorldInteractionTooltipUI>();
        if (instance != null) return;

        var parentCanvas = FindFirstObjectByType<Canvas>();
        var go = new GameObject("WorldInteractionTooltipUI_Auto");
        if (parentCanvas != null)
            go.transform.SetParent(parentCanvas.transform, false);

        instance = go.AddComponent<WorldInteractionTooltipUI>();
        Debug.LogWarning("[WorldInteractionTooltipUI] Nessuna istanza trovata in scena: creata automaticamente a runtime.");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (mainCamera == null)
            mainCamera = Camera.main;

        parentCanvas = GetComponentInParent<Canvas>();
        EnsureCanvas();

        BuildTooltip();
        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Mostra il testo sopra il target.
    /// </summary>
    public void Show(string text, Transform target)
    {
        if (string.IsNullOrEmpty(text) || target == null)
        {
            Hide();
            return;
        }

        targetTransform = target;
        
        if (label != null)
            label.text = text;
            
        isShowing = true;
        if (tooltipGO != null)
            tooltipGO.SetActive(true);
            
        UpdatePosition();
    }

    public void Hide()
    {
        isShowing = false;
        targetTransform = null;
        if (tooltipGO != null)
            tooltipGO.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!isShowing || targetTransform == null)
        {
            if (isShowing) Hide();
            return;
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (tooltipRect == null) return;
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        EnsureCanvas();
        if (canvasRect == null || parentCanvas == null)
            return;

        // Trova la posizione sopra il target nel mondo
        Vector3 worldPos = targetTransform.position + new Vector3(0f, worldOffsetY, 0f);

        // Converti su schermo
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPos);
        if (screenPoint.z < 0f)
        {
            if (tooltipGO != null)
                tooltipGO.SetActive(false);
            return;
        }

        if (tooltipGO != null && isShowing)
            tooltipGO.SetActive(true);

        Vector2 screenPos = screenPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
            out var localPoint);

        tooltipRect.localPosition = localPoint;
    }

    private void EnsureCanvas()
    {
        if (parentCanvas == null)
        {
            // Cerca un canvas generico se non ne abbiamo uno nel parent
            parentCanvas = FindFirstObjectByType<Canvas>();
        }

        if (parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;
    }

    private void BuildTooltip()
    {
        tooltipGO = new GameObject("WorldInteractionTooltip");
        // Lo attacchiamo a un canvas esistente o a noi stessi se siamo sotto un canvas
        if (parentCanvas != null)
        {
            tooltipGO.transform.SetParent(parentCanvas.transform, false);
        }
        else
        {
            tooltipGO.transform.SetParent(transform, false);
        }

        tooltipRect = tooltipGO.AddComponent<RectTransform>();
        tooltipRect.pivot = new Vector2(0.5f, 0f); // Ancora in basso al centro

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

        // Layout
        var hlg = tooltipGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(
            Mathf.RoundToInt(padding.x), Mathf.RoundToInt(padding.x),
            Mathf.RoundToInt(padding.y), Mathf.RoundToInt(padding.y));
        hlg.childAlignment    = TextAnchor.MiddleCenter;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Content size fitter
        var csf = tooltipGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Canvas proprio per stare sempre in cima, se necessario
        var tipCanvas = tooltipGO.AddComponent<Canvas>();
        tipCanvas.overrideSorting = true;
        tipCanvas.sortingOrder    = 500; // Sopra al mondo, ma sotto InventoryTooltip (600)

        tooltipGO.AddComponent<GraphicRaycaster>();

        // Testo
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
        
        // Shadow/Outline opzionale per leggibilità
        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(1, -1);
    }
}
