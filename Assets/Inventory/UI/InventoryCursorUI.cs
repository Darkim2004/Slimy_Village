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

    private Canvas parentCanvas;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
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

        if (iconImage != null)
        {
            iconImage.enabled = cursor.def != null && cursor.def.icon != null;
            iconImage.sprite = cursor.def != null ? cursor.def.icon : null;
        }

        if (amountText != null)
            amountText.text = cursor.amount > 1 ? cursor.amount.ToString() : string.Empty;
    }
}
