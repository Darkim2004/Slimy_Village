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

    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;

    private bool closeListenerRegistered;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        RegisterCloseButton();
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

        if (panelRoot != null)
            panelRoot.SetActive(true);

        isOpen = true;
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

        if (panelRoot != null)
            panelRoot.SetActive(false);
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
}
