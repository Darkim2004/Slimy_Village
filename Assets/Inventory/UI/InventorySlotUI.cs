using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Rappresentazione UI di un singolo slot inventario.
/// Gestisce click mouse e refresh visuale (icona/quantità/highlight).
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text amountText;
    [SerializeField] private GameObject highlightObject;

    private InventoryModel inventory;
    private InventoryInteractionController controller;
    private InventorySection section;
    private int index;

    public string SectionName => section != null ? section.sectionName : string.Empty;
    public int Index => index;

    public void Bind(InventoryModel inventory, InventoryInteractionController controller, InventorySection section, int index)
    {
        this.inventory = inventory;
        this.controller = controller;
        this.section = section;
        this.index = index;

        NormalizeHighlightToSlot();
        Refresh();
    }

    public void Refresh()
    {
        if (section == null)
        {
            SetEmptyVisual();
            return;
        }

        var stack = section.GetSlot(index);
        if (stack == null || stack.IsEmpty)
        {
            SetEmptyVisual();
            return;
        }

        if (iconImage != null)
        {
            iconImage.enabled = stack.def != null && stack.def.icon != null;
            iconImage.sprite = stack.def != null ? stack.def.icon : null;
        }

        if (amountText != null)
        {
            amountText.text = stack.amount > 1 ? stack.amount.ToString() : string.Empty;
        }
    }

    public void SetHighlight(bool enabled)
    {
        NormalizeHighlightToSlot();

        if (highlightObject != null)
            highlightObject.SetActive(enabled);
    }

    private void NormalizeHighlightToSlot()
    {
        if (highlightObject == null) return;

        var highlightRect = highlightObject.transform as RectTransform;
        if (highlightRect == null) return;

        // Mantiene sempre l'overlay highlight aderente allo slot,
        // evitando differenze di size tra stato attivo/non attivo.
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.pivot = new Vector2(0.5f, 0.5f);
        highlightRect.anchoredPosition = Vector2.zero;
        highlightRect.sizeDelta = Vector2.zero;
        highlightRect.localScale = Vector3.one;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null || section == null) return;

        var slot = section.RefAt(index);
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            controller.OnLeftClick(slot);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            controller.OnRightClick(slot);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlight(true);
        if (controller != null && section != null)
            controller.NotifyPointerEnter(section.RefAt(index));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlight(false);
        if (controller != null && section != null)
            controller.NotifyPointerExit(section.RefAt(index));
    }

    private void SetEmptyVisual()
    {
        if (iconImage != null)
        {
            iconImage.enabled = false;
            iconImage.sprite = null;
        }

        if (amountText != null)
            amountText.text = string.Empty;
    }
}
