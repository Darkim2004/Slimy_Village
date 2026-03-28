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
    private bool warnedMissingIconImage;

    private void Awake()
    {
        ResolveReferencesIfNeeded();
        cachedButton = GetComponent<Button>();
        ApplyReadOnlyState();
        NormalizeAmountToSlot();
        Refresh(null, 0);
    }

    private void OnValidate()
    {
        ResolveReferencesIfNeeded();
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
        ResolveReferencesIfNeeded();

        if (!amountNormalized)
            NormalizeAmountToSlot();

        currentItem = item;

        if (iconImage != null)
        {
            var sprite = item != null ? item.icon : null;
            iconImage.enabled = sprite != null;
            iconImage.sprite = sprite;

            if (sprite != null)
            {
                var c = iconImage.color;
                if (c.a <= 0.01f)
                    iconImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }
        else if (!warnedMissingIconImage)
        {
            warnedMissingIconImage = true;
            Debug.LogWarning("[CraftingItemIconAmountUI] iconImage non assegnata nel prefab slot.", this);
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

    private void ResolveReferencesIfNeeded()
    {
        if (iconImage == null)
            iconImage = FindBestImageReference();

        if (amountText == null)
            amountText = FindBestTextReference();
    }

    private Image FindBestImageReference()
    {
        var images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var img = images[i];
            if (img == null) continue;
            if (img.gameObject == gameObject) continue;

            string n = img.gameObject.name;
            if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("icon"))
                return img;
        }

        if (images.Length > 0)
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != gameObject)
                    return images[i];
            }

            return images[0];
        }

        return GetComponent<Image>();
    }

    private Text FindBestTextReference()
    {
        var texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var txt = texts[i];
            if (txt == null) continue;
            string n = txt.gameObject.name;
            if (string.IsNullOrEmpty(n)) continue;

            string lower = n.ToLowerInvariant();
            if (lower.Contains("amount") || lower.Contains("count") || lower.Contains("qty"))
                return txt;
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static void HideTooltip()
    {
        if (InventoryTooltipUI.Instance != null)
            InventoryTooltipUI.Instance.Hide();
    }
}
