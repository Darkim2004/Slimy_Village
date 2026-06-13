using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu per leggere un libro selezionato nella hotbar.
/// Gestisce solo apertura/chiusura; la parte visuale e gestita direttamente dal prefab.
/// </summary>
public class BookReadingMenuUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root UI del menu. Se nullo viene usato il GameObject corrente.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("Runtime Presentation")]
    [SerializeField] private bool keepAboveOtherCanvases = true;
    [SerializeField] private int sortingOrderOffset = 1000;
    [SerializeField] private bool keepReadableSize = true;
    [SerializeField] private Vector2 minimumPanelSize = new Vector2(560f, 420f);
    [SerializeField] private Vector2 panelPadding = new Vector2(48f, 48f);

    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;

    private bool closeListenerRegistered;
    private Canvas runtimeCanvas;

    private void Awake()
    {
        EnsureReferences();
        RegisterCloseButton();

        if (!isOpen)
            Hide();
    }

    private void OnDestroy()
    {
        UnregisterCloseButton();
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public void Show(ItemDefinition book)
    {
        if (book == null || !book.IsBook)
        {
            Hide();
            return;
        }

        EnsureReferences();
        isOpen = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        RegisterCloseButton();
        EnsureRuntimePresentation();
    }

    public void SetBook(ItemDefinition book)
    {
        if (!isOpen) return;

        if (book == null || !book.IsBook)
        {
            Hide();
            return;
        }
    }

    public void Hide()
    {
        isOpen = false;
        EnsureReferences();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void EnsureReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }

    private void RegisterCloseButton()
    {
        if (closeButton == null || closeListenerRegistered)
            return;

        closeButton.onClick.AddListener(Hide);
        closeListenerRegistered = true;
    }

    private void UnregisterCloseButton()
    {
        if (closeButton == null || !closeListenerRegistered)
            return;

        closeButton.onClick.RemoveListener(Hide);
        closeListenerRegistered = false;
    }

    private void EnsureRuntimePresentation()
    {
        if (panelRoot == null)
            return;

        panelRoot.transform.SetAsLastSibling();

        if (keepReadableSize)
            EnsureReadableRect(panelRoot.transform as RectTransform);

        if (keepAboveOtherCanvases)
            EnsureTopMostCanvas();
    }

    private void EnsureTopMostCanvas()
    {
        GameObject target = panelRoot != null ? panelRoot : gameObject;

        if (runtimeCanvas == null)
            runtimeCanvas = target.GetComponent<Canvas>();

        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        int baseSortingOrder = parentCanvas != null && parentCanvas.rootCanvas != null
            ? parentCanvas.rootCanvas.sortingOrder
            : 0;

        if (runtimeCanvas == null)
            runtimeCanvas = target.AddComponent<Canvas>();

        runtimeCanvas.overrideSorting = true;
        runtimeCanvas.sortingOrder = Mathf.Max(runtimeCanvas.sortingOrder, baseSortingOrder + sortingOrderOffset);

        if (target.GetComponent<GraphicRaycaster>() == null)
            target.AddComponent<GraphicRaycaster>();
    }

    private void EnsureReadableRect(RectTransform rect)
    {
        if (rect == null)
            return;

        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        Vector2 parentSize = parentRect.rect.size;
        if (parentSize.x <= 0f || parentSize.y <= 0f)
            return;

        float maxWidth = Mathf.Max(1f, parentSize.x - panelPadding.x * 2f);
        float maxHeight = Mathf.Max(1f, parentSize.y - panelPadding.y * 2f);
        float minWidth = Mathf.Min(Mathf.Max(1f, minimumPanelSize.x), maxWidth);
        float minHeight = Mathf.Min(Mathf.Max(1f, minimumPanelSize.y), maxHeight);

        Vector2 sizeDelta = rect.sizeDelta;
        bool changed = false;

        float currentWidth = GetCurrentAxisSize(parentSize.x, rect.anchorMin.x, rect.anchorMax.x, sizeDelta.x);
        if (currentWidth < minWidth)
        {
            sizeDelta.x += minWidth - currentWidth;
            changed = true;
        }

        float currentHeight = GetCurrentAxisSize(parentSize.y, rect.anchorMin.y, rect.anchorMax.y, sizeDelta.y);
        if (currentHeight < minHeight)
        {
            sizeDelta.y += minHeight - currentHeight;
            changed = true;
        }

        if (changed)
            rect.sizeDelta = sizeDelta;
    }

    private static float GetCurrentAxisSize(float parentSize, float anchorMin, float anchorMax, float sizeDelta)
    {
        return parentSize * Mathf.Abs(anchorMax - anchorMin) + sizeDelta;
    }
}
