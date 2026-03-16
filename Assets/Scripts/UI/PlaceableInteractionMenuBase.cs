using UnityEngine;

/// <summary>
/// Base class per menu di interazione legati a un oggetto piazzabile.
/// Ogni menu specifico (chest, falo, ecc.) puo derivare da qui.
/// </summary>
public abstract class PlaceableInteractionMenuBase : MonoBehaviour
{
    protected PlacedObject currentPlacedObject;

    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;

    public virtual void Show(PlacedObject placedObject)
    {
        currentPlacedObject = placedObject;
        SetOpenState(true);
    }

    public virtual void Hide()
    {
        SetOpenState(false);
        currentPlacedObject = null;
    }

    protected void SetOpenState(bool open)
    {
        isOpen = open;
        gameObject.SetActive(open);
    }

    protected virtual void OnDisable()
    {
        // Se il GO viene disattivato da sistemi esterni, evita stato "open ma invisibile".
        if (isOpen)
            isOpen = false;
    }
}
