using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftingItemIconAmountUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;
    [SerializeField] private bool forceReadOnly = true;

    private bool amountNormalized;
    private ItemDefinition currentItem;
    private Button cachedButton;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
        ApplyReadOnlyState();
        NormalizeAmountToSlot();
        Refresh(null, 0);
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void OnRectTransformDimensionsChange()
    {
        AdaptFontSizeToSlot();
    }

    public void Refresh(ItemDefinition item, int amount)
    {
        if (!amountNormalized)
            NormalizeAmountToSlot();

        currentItem = item;

        if (iconImage != null)
        {
            iconImage.enabled = item != null && item.icon != null;
            iconImage.sprite = item != null ? item.icon : null;
        }

        if (amountText != null)
            amountText.text = amount > 1 ? amount.ToString() : string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (InventoryTooltipUI.Instance == null) return;

        if (currentItem != null)
            InventoryTooltipUI.Instance.RequestShow(currentItem.displayName);
        else
            InventoryTooltipUI.Instance.Hide();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void NormalizeAmountToSlot()
    {
        if (amountText == null) return;

        var amountRect = amountText.rectTransform;
        if (amountRect == null) return;

        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.pivot = new Vector2(1f, 0f);
        amountRect.anchoredPosition = Vector2.zero;
        amountRect.sizeDelta = new Vector2(-4f, -4f);
        amountRect.localScale = Vector3.one;

        amountText.alignment = TextAnchor.LowerRight;
        AdaptFontSizeToSlot();
        amountNormalized = true;
    }

    private void AdaptFontSizeToSlot()
    {
        if (amountText == null) return;

        var slotRect = transform as RectTransform;
        if (slotRect == null || slotRect.rect.height < 1f) return;

        const float ratio = 0.42f;
        amountText.fontSize = Mathf.Clamp(Mathf.RoundToInt(slotRect.rect.height * ratio), 10, 60);
    }

    private void ApplyReadOnlyState()
    {
        if (!forceReadOnly) return;
        if (cachedButton == null) return;

        cachedButton.interactable = false;
    }

    private static void HideTooltip()
    {
        if (InventoryTooltipUI.Instance != null)
            InventoryTooltipUI.Instance.Hide();
    }
}
