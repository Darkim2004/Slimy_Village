using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI del cursore inventario: mostra l'item in mano e segue il mouse.
/// </summary>
public class InventoryCursorUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private InventoryInteractionController controller;

    [Header("UI")]
    [SerializeField] private RectTransform cursorRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;
    [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);
    [Tooltip("Nasconde l'Image eventualmente presente su CursorRoot per evitare lo sfondo bianco.")]
    [SerializeField] private bool hideCursorRootBackground = true;

    [Header("Draw Order")]
    [Tooltip("Se true, aggiunge/usa un Canvas dedicato sul cursorRoot con sorting alto.")]
    [SerializeField] private bool useDedicatedCursorCanvas = true;
    [Tooltip("Ordine di sorting del canvas del cursore (più alto = più sopra).")]
    [SerializeField] private int cursorSortingOrder = 500;

    private Canvas parentCanvas;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        EnsureCursorOnTopSetup();
    }

    private void OnEnable()
    {
        if (controller != null)
            controller.OnCursorChanged += HandleCursorChanged;

        HandleCursorChanged(controller != null ? controller.CursorStack : null);
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.OnCursorChanged -= HandleCursorChanged;
    }

    private void LateUpdate()
    {
        if (cursorRoot == null) return;

        // Mantiene il cursore in cima tra i fratelli della UI.
        cursorRoot.SetAsLastSibling();

        Vector2 screenPos = (Vector2)Input.mousePosition + screenOffset;

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                screenPos,
                parentCanvas.worldCamera,
                out var localPoint
            );
            cursorRoot.anchoredPosition = localPoint;
            return;
        }

        cursorRoot.position = screenPos;
    }

    private void EnsureCursorOnTopSetup()
    {
        if (cursorRoot == null) return;

        if (hideCursorRootBackground)
        {
            var rootImage = cursorRoot.GetComponent<Image>();
            if (rootImage != null)
                rootImage.enabled = false;
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }
        if (amountText != null)
            amountText.raycastTarget = false;

        var canvasGroup = cursorRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = cursorRoot.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (!useDedicatedCursorCanvas) return;

        var cursorCanvas = cursorRoot.GetComponent<Canvas>();
        if (cursorCanvas == null)
            cursorCanvas = cursorRoot.gameObject.AddComponent<Canvas>();

        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = cursorSortingOrder;
    }

    private bool amountNormalized;

    private void HandleCursorChanged(ItemStack cursor)
    {
        bool hasItem = cursor != null && !cursor.IsEmpty;

        if (cursorRoot != null)
            cursorRoot.gameObject.SetActive(hasItem);

        if (!hasItem)
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }

            if (amountText != null)
                amountText.text = string.Empty;

            return;
        }

        if (!amountNormalized)
            NormalizeAmountToCursor();

        if (iconImage != null)
        {
            iconImage.enabled = cursor.def != null && cursor.def.icon != null;
            iconImage.sprite = cursor.def != null ? cursor.def.icon : null;
        }

        if (amountText != null)
            amountText.text = cursor.amount > 1 ? cursor.amount.ToString() : string.Empty;
    }

    private void NormalizeAmountToCursor()
    {
        if (amountText == null || cursorRoot == null) return;

        var amountRect = amountText.rectTransform;

        // Stretch su tutto il cursorRoot con piccolo margine
        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.anchoredPosition = Vector2.zero;
        amountRect.sizeDelta = new Vector2(-4f, -4f);
        amountRect.localScale = Vector3.one;

        amountText.alignment = TextAnchor.LowerRight;

        // Font proporzionale all'altezza del cursore (stessa logica degli slot: 42%)
        const float ratio = 0.42f;
        float height = cursorRoot.rect.height;
        if (height >= 1f)
            amountText.fontSize = Mathf.Clamp(Mathf.RoundToInt(height * ratio), 10, 60);

        amountNormalized = true;
    }
}
