using UnityEngine;
using UnityEngine.UI;

public class CraftingItemIconAmountUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;

    private bool amountNormalized;

    private void Awake()
    {
        NormalizeAmountToSlot();
        Refresh(null, 0);
    }

    private void OnRectTransformDimensionsChange()
    {
        AdaptFontSizeToSlot();
    }

    public void Refresh(ItemDefinition item, int amount)
    {
        if (!amountNormalized)
            NormalizeAmountToSlot();

        if (iconImage != null)
        {
            iconImage.enabled = item != null && item.icon != null;
            iconImage.sprite = item != null ? item.icon : null;
        }

        if (amountText != null)
            amountText.text = amount > 1 ? amount.ToString() : string.Empty;
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
}
