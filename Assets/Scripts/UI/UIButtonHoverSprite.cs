using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cambia lo sprite del bottone quando il cursore passa sopra,
/// e ripristina lo sprite base quando il cursore esce.
/// </summary>
public class UIButtonHoverSprite : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;
    [SerializeField] private bool includeKeyboardSelection = true;

    private void Awake()
    {
        if (targetImage == null)
        {
            var button = GetComponent<Button>();
            if (button != null && button.targetGraphic is Image buttonImage)
                targetImage = buttonImage;
            else
                targetImage = GetComponent<Image>();
        }

        if (targetImage != null && normalSprite == null)
            normalSprite = targetImage.sprite;
    }

    private void OnEnable()
    {
        ApplyNormal();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHighlighted();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (includeKeyboardSelection && IsCurrentlySelected())
        {
            ApplyHighlighted();
            return;
        }

        ApplyNormal();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!includeKeyboardSelection)
            return;

        ApplyHighlighted();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!includeKeyboardSelection)
            return;

        ApplyNormal();
    }

    private bool IsCurrentlySelected()
    {
        return EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
    }

    private void ApplyHighlighted()
    {
        if (targetImage == null || highlightedSprite == null)
            return;

        targetImage.sprite = highlightedSprite;
    }

    private void ApplyNormal()
    {
        if (targetImage == null || normalSprite == null)
            return;

        targetImage.sprite = normalSprite;
    }
}