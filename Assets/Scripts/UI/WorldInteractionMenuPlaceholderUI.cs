using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu placeholder aperto quando il player interagisce con un PlacedObject.
/// Viene costruito a runtime per evitare dipendenze da prefab.
/// </summary>
public class WorldInteractionMenuPlaceholderUI : PlaceableInteractionMenuBase
{
    [Header("Visual")]
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 220f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color bodyColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Font menuFont;

    [Header("Text")]
    [SerializeField] private string titlePrefix = "Interazione";
    [SerializeField] private string bodyTemplate = "Placeholder menu per: {0}\n\nQui potrai agganciare la UI reale dell'oggetto.";

    private static WorldInteractionMenuPlaceholderUI instance;
    public static WorldInteractionMenuPlaceholderUI Instance
    {
        get
        {
            if (instance == null)
                EnsureInstance();
            return instance;
        }
    }

    private Canvas parentCanvas;
    private GameObject panelGO;
    private Text titleLabel;
    private Text bodyLabel;
    private Button closeButton;

    private static void EnsureInstance()
    {
        if (instance != null) return;

        instance = FindFirstObjectByType<WorldInteractionMenuPlaceholderUI>();
        if (instance != null) return;

        var parent = FindFirstObjectByType<Canvas>();
        var go = new GameObject("WorldInteractionMenuPlaceholderUI_Auto");
        if (parent != null)
            go.transform.SetParent(parent.transform, false);

        instance = go.AddComponent<WorldInteractionMenuPlaceholderUI>();
        Debug.LogWarning("[WorldInteractionMenuPlaceholderUI] Nessuna istanza trovata in scena: creata automaticamente a runtime.");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            parentCanvas = FindFirstObjectByType<Canvas>();

        BuildUI();
        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public override void Show(PlacedObject placedObject)
    {
        if (placedObject == null || placedObject.definition == null)
        {
            Hide();
            return;
        }

        string interactionName = placedObject.definition.interactionText;
        if (string.IsNullOrWhiteSpace(interactionName))
            interactionName = placedObject.definition.name;

        if (titleLabel != null)
            titleLabel.text = titlePrefix + ": " + interactionName;

        if (bodyLabel != null)
            bodyLabel.text = string.Format(bodyTemplate, interactionName);

        if (panelGO != null)
            panelGO.SetActive(true);

        base.Show(placedObject);
    }

    public override void Hide()
    {
        base.Hide();

        if (panelGO != null)
            panelGO.SetActive(false);
    }

    private void BuildUI()
    {
        if (panelGO != null) return;

        panelGO = new GameObject("WorldInteractionMenuPanel");
        if (parentCanvas != null)
            panelGO.transform.SetParent(parentCanvas.transform, false);
        else
            panelGO.transform.SetParent(transform, false);

        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = panelColor;

        var verticalLayout = panelGO.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(16, 16, 16, 16);
        verticalLayout.spacing = 8f;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        titleLabel = CreateText("Title", 24, titleColor, TextAnchor.UpperLeft);
        titleLabel.transform.SetParent(panelGO.transform, false);

        bodyLabel = CreateText("Body", 18, bodyColor, TextAnchor.UpperLeft);
        bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyLabel.verticalOverflow = VerticalWrapMode.Truncate;
        var bodyLayout = bodyLabel.gameObject.AddComponent<LayoutElement>();
        bodyLayout.preferredHeight = 110f;
        bodyLabel.transform.SetParent(panelGO.transform, false);

        var buttonGO = new GameObject("CloseButton");
        buttonGO.transform.SetParent(panelGO.transform, false);
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(120f, 36f);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        closeButton = buttonGO.AddComponent<Button>();
        closeButton.targetGraphic = buttonImage;
        closeButton.onClick.AddListener(Hide);

        var closeText = CreateText("CloseLabel", 16, Color.white, TextAnchor.MiddleCenter);
        closeText.text = "Chiudi";
        closeText.transform.SetParent(buttonGO.transform, false);

        var closeTextRect = closeText.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;
    }

    private Text CreateText(string name, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name);
        var text = go.AddComponent<Text>();
        text.font = menuFont != null
            ? menuFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
