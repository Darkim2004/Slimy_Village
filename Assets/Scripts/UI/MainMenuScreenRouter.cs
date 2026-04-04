using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Routes the main menu between different button screens without changing scene.
/// Background and title are left untouched.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class MainMenuScreenRouter : MonoBehaviour
{
    private static MainMenuScreenRouter instance;

    [System.Serializable]
    private struct AspectRatioPreset
    {
        public string label;
        public int width;
        public int height;
    }

    private enum MenuScreen
    {
        Main,
        LoadGame,
        Options,
        Credits,
        WorldCreation
    }

    [Serializable]
    private sealed class SavedWorldEntry
    {
        public string worldId;
        public string displayName;
        public string lastPlayedAtUtc;
        public long sortTicks;
    }

    [Serializable]
    private sealed class SavedWorldMetadata
    {
        public string worldId;
        public string displayName;
        public string lastPlayedAtUtc;
    }

    private const string PrefSfxVolume = "MainMenu.SfxVolume";
    private const string PrefMusicVolume = "MainMenu.MusicVolume";
    private const string PrefFullscreen = "MainMenu.IsFullscreen";
    private const string PrefAspectRatioIndex = "MainMenu.AspectRatioIndex";
    private const string PrefPendingWorldName = "MainMenu.PendingWorldName";
    private const string PrefPendingWorldSeed = "MainMenu.PendingWorldSeed";
    private const string PrefPendingWorldId = "MainMenu.PendingWorldId";
    private const string PrefHasPendingWorldCreation = "MainMenu.HasPendingWorldCreation";
    private const string PrefCurrentWorldId = "MainMenu.CurrentWorldId";
    private const string PrefCurrentWorldName = "MainMenu.CurrentWorldName";

    [Header("Auto Find")]
    [SerializeField] private string canvasName = "Canvas";
    [SerializeField] private string loadGameGroupName = "LoadGameGroup";
    [SerializeField] private string optionsGroupName = "OptionsButtonsGroup";
    [SerializeField] private string creditsGroupName = "CreditsGroup";
    [SerializeField] private string worldCreationGroupName = "WorldCreationGroup";

    [Header("Main Buttons Names")]
    [SerializeField] private string newGameButtonName = "NewGame";
    [SerializeField] private string loadGameButtonName = "LoadGame";
    [SerializeField] private string optionsButtonName = "Options";
    [SerializeField] private string creditsButtonName = "Credits";
    [SerializeField] private string quitButtonName = "Quit";

    [Header("Options Rows")]
    [SerializeField] private string sfxLabel = "SFX";
    [SerializeField] private string musicLabel = "Music";
    [SerializeField] private string displayModeLabelPrefix = "Display";
    [SerializeField] private string aspectRatioLabelPrefix = "Aspect Ratio";
    [SerializeField] private string backLabel = "Back";
    [SerializeField] private Vector2 optionsStartPosition = new Vector2(0f, 120f);
    [SerializeField] private float optionsVerticalSpacing = 100f;
    [SerializeField] private AspectRatioPreset[] aspectRatioPresets =
    {
        new AspectRatioPreset { label = "16:9", width = 16, height = 9 },
        new AspectRatioPreset { label = "16:10", width = 16, height = 10 },
        new AspectRatioPreset { label = "4:3", width = 4, height = 3 }
    };

    [Header("Options Binding")]
    [SerializeField] private bool rebuildOptionsUiWhenMissing;
    [SerializeField] private string sfxControlObjectName = "SfxRow";
    [SerializeField] private string musicControlObjectName = "MusicRow";
    [SerializeField] private string displayModeControlObjectName = "DisplayModeButton";
    [SerializeField] private string aspectRatioControlObjectName = "AspectRatioButton";
    [SerializeField] private string backControlObjectName = "BackButton";

    [Header("Credits")]
    [SerializeField] private bool rebuildCreditsUiWhenMissing;
    [SerializeField] private string creditsBackButtonObjectName = "CreditsBackButton";
    [SerializeField] private Vector2 creditsPanelSize = new Vector2(1050f, 620f);
    [SerializeField] private string[] creditsArtists =
    {
        "Aggiungi qui i nomi degli artisti.",
        "Esempio: Jane Doe - https://example.com",
        "Esempio: Pixel Studio - https://example.com"
    };

    [Header("World Creation")]
    [SerializeField] private bool rebuildWorldCreationUiWhenMissing;
    [SerializeField] private string worldNameRowObjectName = "WorldNameInputRow";
    [SerializeField] private string worldSeedRowObjectName = "WorldSeedInputRow";
    [SerializeField] private string worldCreationBackButtonObjectName = "WorldCreationBackButton";
    [SerializeField] private string worldCreationCreateButtonObjectName = "WorldCreationCreateButton";
    [SerializeField] private string worldCreationTitle = "Create World";
    [SerializeField] private string worldNameLabel = "World Name";
    [SerializeField] private string worldSeedLabel = "Seed";
    [SerializeField] private string worldNameDefaultValue = "New World";
    [SerializeField] private string worldNamePlaceholder = "Inserisci nome mondo";
    [SerializeField] private string worldSeedPlaceholder = "Vuoto = seed casuale";
    [SerializeField] private string createButtonLabel = "Create";
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private Vector2 worldCreationPanelSize = new Vector2(980f, 520f);
    [SerializeField] private Vector2 worldCreationInputStartPosition = new Vector2(0f, 70f);
    [SerializeField] private float worldCreationInputSpacing = 110f;

    [Header("Load Game")]
    [SerializeField] private bool rebuildLoadGameUiWhenMissing;
    [SerializeField] private string loadGameBackButtonObjectName = "LoadGameBackButton";
    [SerializeField] private string loadGamePlayButtonObjectName = "LoadGamePlayButton";
    [SerializeField] private string loadGameDeleteButtonObjectName = "LoadGameDeleteButton";
    [SerializeField] private string loadGameListObjectName = "LoadGameList";
    [SerializeField] private string loadGameTitle = "Load Game";
    [SerializeField] private string loadGameEmptyLabel = "Nessun mondo salvato trovato.";
    [SerializeField] private string loadGamePlayLabel = "Play";
    [SerializeField] private string loadGameDeleteLabel = "Delete";
    [SerializeField] private Vector2 loadGamePanelSize = new Vector2(980f, 560f);
    [SerializeField] private float loadGameRowHeight = 64f;
    [SerializeField] private float loadGameListSpacing = 8f;

    private readonly List<GameObject> mainButtons = new List<GameObject>();
    private readonly List<Selectable> loadGameSelectables = new List<Selectable>();
    private readonly List<Selectable> optionsSelectables = new List<Selectable>();
    private readonly List<Selectable> worldCreationSelectables = new List<Selectable>();
    private readonly List<SavedWorldEntry> savedWorlds = new List<SavedWorldEntry>();
    private readonly List<Button> loadGameWorldButtons = new List<Button>();

    private Canvas cachedCanvas;
    private GameObject loadGameGroup;
    private GameObject optionsGroup;
    private GameObject creditsGroup;
    private GameObject worldCreationGroup;
    private Button newGameOpenButton;
    private Button loadGameOpenButton;
    private Button optionsOpenButton;
    private Button creditsOpenButton;
    private Button quitMainButton;
    private Button firstMainButton;
    private Button backButton;
    private Button creditsBackButton;
    private Button displayModeButton;
    private Button aspectRatioButton;
    private Button loadGameBackButton;
    private Button loadGamePlayButton;
    private Button loadGameDeleteButton;
    private Button worldCreationBackButton;
    private Button worldCreationCreateButton;
    private Slider sfxSlider;
    private Slider musicSlider;
    private InputField worldNameInputField;
    private InputField worldSeedInputField;
    private RectTransform loadGameListRoot;
    private Selectable firstLoadGameSelectable;
    private Selectable firstOptionsSelectable;
    private Selectable firstWorldCreationSelectable;

    private float sfxVolume = 1f;
    private float musicVolume = 1f;
    private bool isFullscreen = true;
    private int aspectRatioIndex;
    private int selectedSavedWorldIndex = -1;
    private Vector3 lastMousePosition;
    private bool hasMousePositionSnapshot;

    private MenuScreen currentScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainMenu")
            return;

        var existingRouters = UnityEngine.Object.FindObjectsByType<MainMenuScreenRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existingRouters != null && existingRouters.Length > 0)
            return;

        var routerGo = new GameObject("MainMenuScreenRouter");
        routerGo.AddComponent<MainMenuScreenRouter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Time.timeScale = 1f;
        ForceHideOverlayGroupsInScene();

        if (!TryFindCanvas(out cachedCanvas))
        {
            Debug.LogWarning("[MainMenuScreenRouter] Canvas not found, router disabled.", this);
            enabled = false;
            return;
        }

        ForceHideOverlayGroups(cachedCanvas.transform);

        CacheMainButtons(cachedCanvas.transform);
        if (mainButtons.Count == 0 || optionsOpenButton == null)
        {
            Debug.LogWarning("[MainMenuScreenRouter] Missing main buttons or Options button, router disabled.", this);
            enabled = false;
            return;
        }

        if (newGameOpenButton != null)
        {
            newGameOpenButton.onClick.RemoveListener(ShowWorldCreation);
            newGameOpenButton.onClick.AddListener(ShowWorldCreation);
        }

        if (loadGameOpenButton != null)
        {
            loadGameOpenButton.onClick.RemoveListener(ShowLoadGame);
            loadGameOpenButton.onClick.AddListener(ShowLoadGame);
        }

        optionsOpenButton.onClick.RemoveListener(ShowOptions);
        optionsOpenButton.onClick.AddListener(ShowOptions);

        if (creditsOpenButton != null)
        {
            creditsOpenButton.onClick.RemoveListener(ShowCredits);
            creditsOpenButton.onClick.AddListener(ShowCredits);
        }

        if (quitMainButton != null)
        {
            quitMainButton.onClick.RemoveListener(OnQuitPressed);
            quitMainButton.onClick.AddListener(OnQuitPressed);
        }

        LoadSavedOptions();
        BuildOrFindLoadGameGroup(cachedCanvas.transform);
        BuildOrFindOptionsGroup(cachedCanvas.transform);
        BuildOrFindCreditsGroup(cachedCanvas.transform);
        BuildOrFindWorldCreationGroup(cachedCanvas.transform);
        ApplyOptionsToUi();
        ApplyRuntimeAudioVolumes();
        ApplyDisplaySettings();
        lastMousePosition = Input.mousePosition;
        hasMousePositionSnapshot = true;

        ShowMain();
    }

    private void Start()
    {
        // Safety pass after all Awake calls: guarantees a clean main-menu state on scene re-entry.
        ForceHideOverlayGroupsInScene();
        ShowMain();
    }

    private void Update()
    {
        HandleMixedNavigationSelection();

        if ((currentScreen == MenuScreen.LoadGame || currentScreen == MenuScreen.Options || currentScreen == MenuScreen.Credits || currentScreen == MenuScreen.WorldCreation) && Input.GetKeyDown(KeyCode.Escape))
            ShowMain();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (newGameOpenButton != null)
            newGameOpenButton.onClick.RemoveListener(ShowWorldCreation);

        if (loadGameOpenButton != null)
            loadGameOpenButton.onClick.RemoveListener(ShowLoadGame);

        if (optionsOpenButton != null)
            optionsOpenButton.onClick.RemoveListener(ShowOptions);

        if (creditsOpenButton != null)
            creditsOpenButton.onClick.RemoveListener(ShowCredits);

        if (quitMainButton != null)
            quitMainButton.onClick.RemoveListener(OnQuitPressed);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (displayModeButton != null)
            displayModeButton.onClick.RemoveListener(ToggleDisplayMode);

        if (aspectRatioButton != null)
            aspectRatioButton.onClick.RemoveListener(CycleAspectRatio);

        if (backButton != null)
            backButton.onClick.RemoveListener(ShowMain);

        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(ShowMain);

        if (loadGameBackButton != null)
            loadGameBackButton.onClick.RemoveListener(ShowMain);

        if (loadGamePlayButton != null)
            loadGamePlayButton.onClick.RemoveListener(OnLoadGamePlayPressed);

        if (loadGameDeleteButton != null)
            loadGameDeleteButton.onClick.RemoveListener(OnLoadGameDeletePressed);

        if (worldCreationBackButton != null)
            worldCreationBackButton.onClick.RemoveListener(ShowMain);

        if (worldCreationCreateButton != null)
            worldCreationCreateButton.onClick.RemoveListener(OnCreateWorldPressed);
    }

    private void ForceHideOverlayGroups(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        SetGroupActiveByName(canvasRoot, loadGameGroupName, false);
        SetGroupActiveByName(canvasRoot, optionsGroupName, false);
        SetGroupActiveByName(canvasRoot, creditsGroupName, false);
        SetGroupActiveByName(canvasRoot, worldCreationGroupName, false);
    }

    private void ForceHideOverlayGroupsInScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            ForceHideOverlayGroupsRecursive(roots[i].transform);
    }

    private void ForceHideOverlayGroupsRecursive(Transform node)
    {
        if (node == null)
            return;

        if (node.name == loadGameGroupName ||
            node.name == optionsGroupName ||
            node.name == creditsGroupName ||
            node.name == worldCreationGroupName)
        {
            node.gameObject.SetActive(false);
        }

        for (int i = 0; i < node.childCount; i++)
            ForceHideOverlayGroupsRecursive(node.GetChild(i));
    }

    private void SetGroupActiveByName(Transform root, string objectName, bool active)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        var group = FindChildByName(root, objectName);
        if (group != null)
            group.gameObject.SetActive(active);
    }

    public void ShowMain()
    {
        SetMainButtonsVisible(true);

        if (loadGameGroup != null)
            loadGameGroup.SetActive(false);

        if (optionsGroup != null)
            optionsGroup.SetActive(false);

        if (creditsGroup != null)
            creditsGroup.SetActive(false);

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(false);

        currentScreen = MenuScreen.Main;
        SelectElement(firstMainButton);
    }

    public void ShowOptions()
    {
        SetMainButtonsVisible(false);

        if (loadGameGroup != null)
            loadGameGroup.SetActive(false);

        if (optionsGroup != null)
            optionsGroup.SetActive(true);

        if (creditsGroup != null)
            creditsGroup.SetActive(false);

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(false);

        currentScreen = MenuScreen.Options;
        SelectElement(GetFirstOptionsSelectable());
    }

    public void ShowCredits()
    {
        SetMainButtonsVisible(false);

        if (loadGameGroup != null)
            loadGameGroup.SetActive(false);

        if (optionsGroup != null)
            optionsGroup.SetActive(false);

        if (creditsGroup != null)
            creditsGroup.SetActive(true);

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(false);

        currentScreen = MenuScreen.Credits;
        SelectElement(creditsBackButton);
    }

    public void ShowWorldCreation()
    {
        SetMainButtonsVisible(false);

        if (loadGameGroup != null)
            loadGameGroup.SetActive(false);

        if (optionsGroup != null)
            optionsGroup.SetActive(false);

        if (creditsGroup != null)
            creditsGroup.SetActive(false);

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(true);

        currentScreen = MenuScreen.WorldCreation;
        SelectElement(GetFirstWorldCreationSelectable());
    }

    public void ShowLoadGame()
    {
        SetMainButtonsVisible(false);

        if (loadGameGroup != null)
            loadGameGroup.SetActive(true);

        if (optionsGroup != null)
            optionsGroup.SetActive(false);

        if (creditsGroup != null)
            creditsGroup.SetActive(false);

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(false);

        RefreshLoadGameWorlds();
        currentScreen = MenuScreen.LoadGame;
        SelectElement(GetFirstLoadGameSelectable());
    }

    public string GetCurrentScreenName()
    {
        return currentScreen.ToString();
    }

    private bool TryFindCanvas(out Canvas canvas)
    {
        canvas = null;

        var namedCanvas = GameObject.Find(canvasName);
        if (namedCanvas != null)
            canvas = namedCanvas.GetComponent<Canvas>();

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        return canvas != null;
    }

    private void CacheMainButtons(Transform canvasRoot)
    {
        mainButtons.Clear();

        newGameOpenButton = FindButton(canvasRoot, newGameButtonName);
        loadGameOpenButton = FindButton(canvasRoot, loadGameButtonName);
        firstMainButton = newGameOpenButton;
        optionsOpenButton = FindButton(canvasRoot, optionsButtonName);
        creditsOpenButton = FindButton(canvasRoot, creditsButtonName);
        quitMainButton = FindButton(canvasRoot, quitButtonName);

        AddButtonObject(FindButton(canvasRoot, newGameButtonName));
        AddButtonObject(FindButton(canvasRoot, loadGameButtonName));
        AddButtonObject(FindButton(canvasRoot, optionsButtonName));
        AddButtonObject(FindButton(canvasRoot, creditsButtonName));
        AddButtonObject(FindButton(canvasRoot, quitButtonName));
    }

    private void AddButtonObject(Button button)
    {
        if (button == null)
            return;

        if (!mainButtons.Contains(button.gameObject))
            mainButtons.Add(button.gameObject);
    }

    private void SetMainButtonsVisible(bool visible)
    {
        for (int i = 0; i < mainButtons.Count; i++)
        {
            if (mainButtons[i] != null)
                mainButtons[i].SetActive(visible);
        }
    }

    private void BuildOrFindLoadGameGroup(Transform canvasRoot)
    {
        var existing = FindChildByName(canvasRoot, loadGameGroupName);
        if (existing != null)
            loadGameGroup = existing.gameObject;

        if (loadGameGroup == null)
        {
            loadGameGroup = new GameObject(loadGameGroupName, typeof(RectTransform));
            var groupRect = loadGameGroup.GetComponent<RectTransform>();
            groupRect.SetParent(canvasRoot, false);
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            BuildLoadGameControls(clearExisting: true);
        }
        else
        {
            bool hasLoadGameControls = TryBindLoadGameControlsFromScene();
            if (!hasLoadGameControls && rebuildLoadGameUiWhenMissing)
            {
                BuildLoadGameControls(clearExisting: true);
            }
            else if (!hasLoadGameControls)
            {
                Debug.LogWarning("[MainMenuScreenRouter] Load game group found, but one or more controls are missing. " +
                                 "Scene objects are preserved (no runtime overwrite).", this);
            }
        }

        if (loadGameGroup != null)
            loadGameGroup.SetActive(false);
    }

    private bool TryBindLoadGameControlsFromScene()
    {
        loadGameSelectables.Clear();
        loadGameWorldButtons.Clear();
        loadGameListRoot = null;
        loadGameBackButton = null;
        loadGamePlayButton = null;
        loadGameDeleteButton = null;
        firstLoadGameSelectable = null;

        if (loadGameGroup == null)
            return false;

        var listChild = FindChildByName(loadGameGroup.transform, loadGameListObjectName);
        if (listChild != null)
            loadGameListRoot = listChild as RectTransform;

        var backChild = FindChildByName(loadGameGroup.transform, loadGameBackButtonObjectName);
        if (backChild != null)
            loadGameBackButton = backChild.GetComponent<Button>();

        var playChild = FindChildByName(loadGameGroup.transform, loadGamePlayButtonObjectName);
        if (playChild != null)
            loadGamePlayButton = playChild.GetComponent<Button>();

        var deleteChild = FindChildByName(loadGameGroup.transform, loadGameDeleteButtonObjectName);
        if (deleteChild != null)
            loadGameDeleteButton = deleteChild.GetComponent<Button>();

        if (loadGameDeleteButton == null && loadGameListRoot != null)
        {
            var panelRect = loadGameListRoot.parent as RectTransform;
            if (panelRect != null)
                loadGameDeleteButton = CreateWorldCreationActionButton(panelRect, loadGameDeleteButtonObjectName, new Vector2(0f, 26f), loadGameDeleteLabel, OnLoadGameDeletePressed);
        }

        WireLoadGameControlListeners();
        RefreshLoadGameWorlds();

        return loadGameListRoot != null
            && loadGameBackButton != null
            && loadGamePlayButton != null;
    }

    private void BuildLoadGameControls(bool clearExisting)
    {
        if (loadGameGroup == null)
            return;

        if (clearExisting)
            ClearChildren(loadGameGroup.transform);

        loadGameSelectables.Clear();
        loadGameWorldButtons.Clear();

        var panelGo = new GameObject("LoadGamePanel", typeof(RectTransform), typeof(Image));
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.SetParent(loadGameGroup.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = loadGamePanelSize;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        var titleGo = new GameObject("LoadGameTitle", typeof(RectTransform), typeof(Text));
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -58f);
        titleRect.offsetMax = new Vector2(-24f, -8f);

        var titleText = titleGo.GetComponent<Text>();
        titleText.text = loadGameTitle;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        titleText.fontSize = 34;
        titleText.color = Color.white;

        var listGo = new GameObject(loadGameListObjectName, typeof(RectTransform));
        loadGameListRoot = listGo.GetComponent<RectTransform>();
        loadGameListRoot.SetParent(panelRect, false);
        loadGameListRoot.anchorMin = new Vector2(0f, 0f);
        loadGameListRoot.anchorMax = new Vector2(1f, 1f);
        loadGameListRoot.offsetMin = new Vector2(28f, 108f);
        loadGameListRoot.offsetMax = new Vector2(-28f, -96f);

        var listLayout = listGo.AddComponent<VerticalLayoutGroup>();
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.spacing = Mathf.Max(0f, loadGameListSpacing);
        listLayout.padding = new RectOffset(4, 4, 4, 4);

        var listFitter = listGo.AddComponent<ContentSizeFitter>();
        listFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        listFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        loadGameBackButton = CreateWorldCreationActionButton(panelRect, loadGameBackButtonObjectName, new Vector2(-180f, 26f), backLabel, ShowMain);
        loadGameDeleteButton = CreateWorldCreationActionButton(panelRect, loadGameDeleteButtonObjectName, new Vector2(0f, 26f), loadGameDeleteLabel, OnLoadGameDeletePressed);
        loadGamePlayButton = CreateWorldCreationActionButton(panelRect, loadGamePlayButtonObjectName, new Vector2(300f, 26f), loadGamePlayLabel, OnLoadGamePlayPressed);

        if (loadGameBackButton != null)
        {
            var backRect = loadGameBackButton.GetComponent<RectTransform>();
            if (backRect != null)
                backRect.anchoredPosition = new Vector2(-300f, 26f);
        }

        WireLoadGameControlListeners();
        RefreshLoadGameWorlds();
    }

    private void WireLoadGameControlListeners()
    {
        if (loadGameBackButton != null)
        {
            loadGameBackButton.onClick.RemoveListener(ShowMain);
            loadGameBackButton.onClick.AddListener(ShowMain);
        }

        if (loadGamePlayButton != null)
        {
            loadGamePlayButton.onClick.RemoveListener(OnLoadGamePlayPressed);
            loadGamePlayButton.onClick.AddListener(OnLoadGamePlayPressed);
        }

        if (loadGameDeleteButton != null)
        {
            loadGameDeleteButton.onClick.RemoveListener(OnLoadGameDeletePressed);
            loadGameDeleteButton.onClick.AddListener(OnLoadGameDeletePressed);
        }
    }

    private void RefreshLoadGameWorlds()
    {
        if (loadGameListRoot == null)
            return;

        ClearChildren(loadGameListRoot);
        loadGameSelectables.Clear();
        loadGameWorldButtons.Clear();
        savedWorlds.Clear();

        CollectSavedWorldEntries(savedWorlds);
        savedWorlds.Sort((a, b) => b.sortTicks.CompareTo(a.sortTicks));

        if (savedWorlds.Count == 0)
        {
            selectedSavedWorldIndex = -1;
            CreateLoadGameEmptyRow();
        }
        else
        {
            if (selectedSavedWorldIndex < 0 || selectedSavedWorldIndex >= savedWorlds.Count)
                selectedSavedWorldIndex = 0;

            for (int i = 0; i < savedWorlds.Count; i++)
                CreateLoadGameWorldRow(i, savedWorlds[i]);
        }

        if (loadGameBackButton != null)
            loadGameSelectables.Add(loadGameBackButton);

        if (loadGamePlayButton != null)
            loadGameSelectables.Add(loadGamePlayButton);

        if (loadGameDeleteButton != null)
            loadGameSelectables.Add(loadGameDeleteButton);

        UpdateLoadGameRowLabels();

        if (savedWorlds.Count > 0 && selectedSavedWorldIndex >= 0 && selectedSavedWorldIndex < loadGameWorldButtons.Count)
            firstLoadGameSelectable = loadGameWorldButtons[selectedSavedWorldIndex];
        else
            firstLoadGameSelectable = loadGameBackButton != null ? (Selectable)loadGameBackButton : loadGamePlayButton;
    }

    private void CreateLoadGameEmptyRow()
    {
        var emptyGo = new GameObject("LoadGameEmpty", typeof(RectTransform), typeof(Text));
        var emptyRect = emptyGo.GetComponent<RectTransform>();
        emptyRect.SetParent(loadGameListRoot, false);
        emptyRect.sizeDelta = new Vector2(0f, loadGameRowHeight);

        var text = emptyGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 24;
        text.color = new Color(1f, 1f, 1f, 0.85f);
        text.text = loadGameEmptyLabel;
    }

    private void CreateLoadGameWorldRow(int index, SavedWorldEntry entry)
    {
        GameObject rowGo;
        if (optionsOpenButton != null)
            rowGo = Instantiate(optionsOpenButton.gameObject, loadGameListRoot);
        else
            rowGo = DefaultControls.CreateButton(new DefaultControls.Resources());

        rowGo.name = "LoadGameRow_" + index;
        rowGo.transform.SetParent(loadGameListRoot, false);

        var rowRect = rowGo.GetComponent<RectTransform>();
        if (rowRect != null)
            rowRect.sizeDelta = new Vector2(0f, loadGameRowHeight);

        var button = rowGo.GetComponent<Button>();
        if (button != null)
        {
            int capturedIndex = index;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnLoadGameRowClicked(capturedIndex));
            loadGameWorldButtons.Add(button);
            loadGameSelectables.Add(button);
        }

        var text = rowGo.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = FormatLoadGameRowLabel(entry, index == selectedSavedWorldIndex);
    }

    private void OnLoadGameRowClicked(int index)
    {
        if (index < 0 || index >= savedWorlds.Count)
            return;

        selectedSavedWorldIndex = index;
        UpdateLoadGameRowLabels();
    }

    private void UpdateLoadGameRowLabels()
    {
        bool hasSelection = selectedSavedWorldIndex >= 0 && selectedSavedWorldIndex < savedWorlds.Count;

        if (loadGamePlayButton != null)
            loadGamePlayButton.interactable = hasSelection;

        if (loadGameDeleteButton != null)
            loadGameDeleteButton.interactable = hasSelection;

        int count = Mathf.Min(loadGameWorldButtons.Count, savedWorlds.Count);
        for (int i = 0; i < count; i++)
        {
            var text = loadGameWorldButtons[i] != null ? loadGameWorldButtons[i].GetComponentInChildren<Text>(true) : null;
            if (text == null)
                continue;

            text.text = FormatLoadGameRowLabel(savedWorlds[i], i == selectedSavedWorldIndex);
        }
    }

    private string FormatLoadGameRowLabel(SavedWorldEntry entry, bool selected)
    {
        string marker = selected ? "> " : string.Empty;
        string name = string.IsNullOrWhiteSpace(entry.displayName) ? "Unnamed World" : entry.displayName;

        if (string.IsNullOrWhiteSpace(entry.lastPlayedAtUtc))
            return marker + name;

        DateTime parsed;
        if (!DateTime.TryParse(entry.lastPlayedAtUtc, out parsed))
            return marker + name;

        return marker + name + "  (" + parsed.ToLocalTime().ToString("g") + ")";
    }

    private void CollectSavedWorldEntries(List<SavedWorldEntry> target)
    {
        if (target == null)
            return;

        string worldsRoot = Path.Combine(Application.persistentDataPath, "worlds");
        if (!Directory.Exists(worldsRoot))
            return;

        string[] dirs = Directory.GetDirectories(worldsRoot);
        for (int i = 0; i < dirs.Length; i++)
        {
            string worldDir = dirs[i];
            string fallbackWorldId = Path.GetFileName(worldDir);
            string metadataPath = Path.Combine(worldDir, "metadata.json");

            var entry = new SavedWorldEntry();
            entry.worldId = fallbackWorldId;
            entry.displayName = "World " + fallbackWorldId;
            entry.lastPlayedAtUtc = string.Empty;
            entry.sortTicks = Directory.GetLastWriteTimeUtc(worldDir).Ticks;

            if (File.Exists(metadataPath))
            {
                try
                {
                    var metadata = JsonUtility.FromJson<SavedWorldMetadata>(File.ReadAllText(metadataPath));
                    if (metadata != null)
                    {
                        if (!string.IsNullOrWhiteSpace(metadata.worldId))
                            entry.worldId = metadata.worldId.Trim();

                        if (!string.IsNullOrWhiteSpace(metadata.displayName))
                            entry.displayName = metadata.displayName.Trim();

                        if (!string.IsNullOrWhiteSpace(metadata.lastPlayedAtUtc))
                        {
                            entry.lastPlayedAtUtc = metadata.lastPlayedAtUtc;
                            DateTime parsed;
                            if (DateTime.TryParse(metadata.lastPlayedAtUtc, out parsed))
                                entry.sortTicks = parsed.ToUniversalTime().Ticks;
                        }
                    }
                }
                catch
                {
                    entry.worldId = fallbackWorldId;
                }
            }

            if (string.IsNullOrWhiteSpace(entry.worldId))
                entry.worldId = fallbackWorldId;

            if (string.IsNullOrWhiteSpace(entry.displayName))
                entry.displayName = "World " + entry.worldId;

            target.Add(entry);
        }
    }

    private void OnLoadGamePlayPressed()
    {
        if (selectedSavedWorldIndex < 0 || selectedSavedWorldIndex >= savedWorlds.Count)
            return;

        var selectedWorld = savedWorlds[selectedSavedWorldIndex];

        PlayerPrefs.SetString(PrefCurrentWorldId, selectedWorld.worldId ?? string.Empty);
        PlayerPrefs.SetString(PrefCurrentWorldName, selectedWorld.displayName ?? string.Empty);
        PlayerPrefs.DeleteKey(PrefPendingWorldName);
        PlayerPrefs.DeleteKey(PrefPendingWorldSeed);
        PlayerPrefs.DeleteKey(PrefPendingWorldId);
        PlayerPrefs.DeleteKey(PrefHasPendingWorldCreation);
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(gameSceneName) && Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        Debug.LogWarning("[MainMenuScreenRouter] Game scene '" + gameSceneName + "' is not loadable.", this);
    }

    private void OnLoadGameDeletePressed()
    {
        if (selectedSavedWorldIndex < 0 || selectedSavedWorldIndex >= savedWorlds.Count)
            return;

        var selectedWorld = savedWorlds[selectedSavedWorldIndex];
        if (selectedWorld == null || string.IsNullOrWhiteSpace(selectedWorld.worldId))
            return;

        string worldDir = Path.Combine(Application.persistentDataPath, "worlds", selectedWorld.worldId.Trim());

        try
        {
            if (Directory.Exists(worldDir))
                Directory.Delete(worldDir, true);

            if (PlayerPrefs.GetString(PrefCurrentWorldId, string.Empty) == selectedWorld.worldId)
            {
                PlayerPrefs.DeleteKey(PrefCurrentWorldId);
                PlayerPrefs.DeleteKey(PrefCurrentWorldName);
            }

            if (PlayerPrefs.GetString(PrefPendingWorldId, string.Empty) == selectedWorld.worldId)
            {
                PlayerPrefs.DeleteKey(PrefPendingWorldId);
                PlayerPrefs.DeleteKey(PrefPendingWorldName);
                PlayerPrefs.DeleteKey(PrefPendingWorldSeed);
                PlayerPrefs.DeleteKey(PrefHasPendingWorldCreation);
            }

            PlayerPrefs.Save();

            selectedSavedWorldIndex = -1;
            RefreshLoadGameWorlds();
            SelectElement(GetFirstLoadGameSelectable());

            Debug.Log("[MainMenuScreenRouter] Deleted world '" + selectedWorld.displayName + "' (" + selectedWorld.worldId + ").", this);
        }
        catch (Exception ex)
        {
            Debug.LogError("[MainMenuScreenRouter] Failed to delete world '" + selectedWorld.worldId + "': " + ex.Message, this);
        }
    }

    private Selectable GetFirstLoadGameSelectable()
    {
        for (int i = 0; i < loadGameSelectables.Count; i++)
        {
            if (loadGameSelectables[i] != null && loadGameSelectables[i].isActiveAndEnabled)
                return loadGameSelectables[i];
        }

        return firstLoadGameSelectable;
    }

    private void BuildOrFindOptionsGroup(Transform canvasRoot)
    {
        var existing = FindChildByName(canvasRoot, optionsGroupName);
        if (existing != null)
            optionsGroup = existing.gameObject;

        if (optionsGroup == null)
        {
            optionsGroup = new GameObject(optionsGroupName, typeof(RectTransform));
            var groupRect = optionsGroup.GetComponent<RectTransform>();
            groupRect.SetParent(canvasRoot, false);
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            BuildOptionsControls(clearExisting: true);
        }
        else
        {
            bool hasAllControls = TryBindOptionsControlsFromScene();
            if (!hasAllControls && rebuildOptionsUiWhenMissing)
            {
                BuildOptionsControls(clearExisting: true);
            }
            else if (!hasAllControls)
            {
                Debug.LogWarning("[MainMenuScreenRouter] Options group found, but one or more controls are missing. " +
                                 "Scene objects are preserved (no runtime overwrite).", this);
            }
        }

        optionsGroup.SetActive(false);
    }

    private void BuildOrFindCreditsGroup(Transform canvasRoot)
    {
        var existing = FindChildByName(canvasRoot, creditsGroupName);
        if (existing != null)
            creditsGroup = existing.gameObject;

        if (creditsGroup == null)
        {
            creditsGroup = new GameObject(creditsGroupName, typeof(RectTransform));
            var groupRect = creditsGroup.GetComponent<RectTransform>();
            groupRect.SetParent(canvasRoot, false);
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            BuildCreditsControls(clearExisting: true);
        }
        else
        {
            bool hasCreditsControls = TryBindCreditsControlsFromScene();
            if (!hasCreditsControls && rebuildCreditsUiWhenMissing)
            {
                BuildCreditsControls(clearExisting: true);
            }
            else if (!hasCreditsControls)
            {
                Debug.LogWarning("[MainMenuScreenRouter] Credits group found, but one or more controls are missing. " +
                                 "Scene objects are preserved (no runtime overwrite).", this);
            }
        }

        if (creditsGroup != null)
            creditsGroup.SetActive(false);
    }

    private void BuildOrFindWorldCreationGroup(Transform canvasRoot)
    {
        var existing = FindChildByName(canvasRoot, worldCreationGroupName);
        if (existing != null)
            worldCreationGroup = existing.gameObject;

        if (worldCreationGroup == null)
        {
            worldCreationGroup = new GameObject(worldCreationGroupName, typeof(RectTransform));
            var groupRect = worldCreationGroup.GetComponent<RectTransform>();
            groupRect.SetParent(canvasRoot, false);
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = Vector2.zero;
            groupRect.sizeDelta = Vector2.zero;

            BuildWorldCreationControls(clearExisting: true);
        }
        else
        {
            bool hasWorldCreationControls = TryBindWorldCreationControlsFromScene();
            if (!hasWorldCreationControls && rebuildWorldCreationUiWhenMissing)
            {
                BuildWorldCreationControls(clearExisting: true);
            }
            else if (!hasWorldCreationControls)
            {
                Debug.LogWarning("[MainMenuScreenRouter] World creation group found, but one or more controls are missing. " +
                                 "Scene objects are preserved (no runtime overwrite).", this);
            }
        }

        if (worldCreationGroup != null)
            worldCreationGroup.SetActive(false);
    }

    private bool TryBindCreditsControlsFromScene()
    {
        creditsBackButton = null;

        if (creditsGroup == null)
            return false;

        var backChild = FindChildByName(creditsGroup.transform, creditsBackButtonObjectName);
        if (backChild != null)
            creditsBackButton = backChild.GetComponent<Button>();

        if (creditsBackButton == null)
        {
            var buttons = creditsGroup.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.name.ToLowerInvariant().Contains("back"))
                {
                    creditsBackButton = buttons[i];
                    break;
                }
            }
        }

        if (creditsBackButton != null)
        {
            creditsBackButton.onClick.RemoveListener(ShowMain);
            creditsBackButton.onClick.AddListener(ShowMain);
        }

        return creditsBackButton != null;
    }

    private void BuildCreditsControls(bool clearExisting)
    {
        if (creditsGroup == null)
            return;

        if (clearExisting)
            ClearChildren(creditsGroup.transform);

        var panelGo = new GameObject("CreditsPanel", typeof(RectTransform), typeof(Image));
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.SetParent(creditsGroup.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = creditsPanelSize;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        var titleGo = new GameObject("CreditsTitle", typeof(RectTransform), typeof(Text));
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -58f);
        titleRect.offsetMax = new Vector2(-24f, -8f);

        var titleText = titleGo.GetComponent<Text>();
        titleText.text = "Credits";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        titleText.fontSize = 34;
        titleText.color = Color.white;

        var contentGo = new GameObject("CreditsContent", typeof(RectTransform));
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.SetParent(panelRect, false);
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(32f, 82f);
        contentRect.offsetMax = new Vector2(-32f, -76f);

        PopulateCreditsContent(contentRect);

        var backTemplate = optionsOpenButton;
        GameObject backGo;
        if (backTemplate != null)
        {
            backGo = Instantiate(backTemplate.gameObject, panelRect);
        }
        else
        {
            backGo = DefaultControls.CreateButton(new DefaultControls.Resources());
            backGo.transform.SetParent(panelRect, false);
        }

        backGo.name = creditsBackButtonObjectName;
        var backRect = backGo.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, 16f);
        backRect.sizeDelta = new Vector2(320f, 56f);

        creditsBackButton = backGo.GetComponent<Button>();
        if (creditsBackButton != null)
        {
            creditsBackButton.onClick.RemoveAllListeners();
            creditsBackButton.onClick.AddListener(ShowMain);
        }

        var backText = backGo.GetComponentInChildren<Text>(true);
        if (backText != null)
            backText.text = backLabel;
    }

    private bool TryBindWorldCreationControlsFromScene()
    {
        worldCreationSelectables.Clear();
        worldNameInputField = null;
        worldSeedInputField = null;
        worldCreationBackButton = null;
        worldCreationCreateButton = null;

        if (worldCreationGroup == null)
            return false;

        var worldNameRow = FindChildByName(worldCreationGroup.transform, worldNameRowObjectName);
        if (worldNameRow != null)
            worldNameInputField = worldNameRow.GetComponentInChildren<InputField>(true);

        var worldSeedRow = FindChildByName(worldCreationGroup.transform, worldSeedRowObjectName);
        if (worldSeedRow != null)
            worldSeedInputField = worldSeedRow.GetComponentInChildren<InputField>(true);

        var backChild = FindChildByName(worldCreationGroup.transform, worldCreationBackButtonObjectName);
        if (backChild != null)
            worldCreationBackButton = backChild.GetComponent<Button>();

        var createChild = FindChildByName(worldCreationGroup.transform, worldCreationCreateButtonObjectName);
        if (createChild != null)
            worldCreationCreateButton = createChild.GetComponent<Button>();

        if (worldNameInputField != null) worldCreationSelectables.Add(worldNameInputField);
        if (worldSeedInputField != null) worldCreationSelectables.Add(worldSeedInputField);
        if (worldCreationBackButton != null) worldCreationSelectables.Add(worldCreationBackButton);
        if (worldCreationCreateButton != null) worldCreationSelectables.Add(worldCreationCreateButton);

        firstWorldCreationSelectable = worldNameInputField != null
            ? worldNameInputField
            : (worldCreationBackButton != null ? worldCreationBackButton : worldCreationCreateButton);

        WireWorldCreationControlListeners();

        return worldNameInputField != null
            && worldSeedInputField != null
            && worldCreationBackButton != null
            && worldCreationCreateButton != null;
    }

    private void BuildWorldCreationControls(bool clearExisting)
    {
        if (worldCreationGroup == null)
            return;

        if (clearExisting)
            ClearChildren(worldCreationGroup.transform);

        worldCreationSelectables.Clear();

        var panelGo = new GameObject("WorldCreationPanel", typeof(RectTransform), typeof(Image));
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.SetParent(worldCreationGroup.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = worldCreationPanelSize;
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        var titleGo = new GameObject("WorldCreationTitle", typeof(RectTransform), typeof(Text));
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.SetParent(panelRect, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -58f);
        titleRect.offsetMax = new Vector2(-24f, -8f);

        var titleText = titleGo.GetComponent<Text>();
        titleText.text = worldCreationTitle;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontStyle = FontStyle.Bold;
        titleText.fontSize = 34;
        titleText.color = Color.white;

        worldNameInputField = CreateWorldCreationInputRow(panelRect, worldNameRowObjectName, 0, worldNameLabel, worldNamePlaceholder, false);
        worldSeedInputField = CreateWorldCreationInputRow(panelRect, worldSeedRowObjectName, 1, worldSeedLabel, worldSeedPlaceholder, true);

        worldCreationBackButton = CreateWorldCreationActionButton(panelRect, worldCreationBackButtonObjectName, new Vector2(-180f, 26f), backLabel, ShowMain);
        worldCreationCreateButton = CreateWorldCreationActionButton(panelRect, worldCreationCreateButtonObjectName, new Vector2(180f, 26f), createButtonLabel, OnCreateWorldPressed);

        if (worldNameInputField != null) worldCreationSelectables.Add(worldNameInputField);
        if (worldSeedInputField != null) worldCreationSelectables.Add(worldSeedInputField);
        if (worldCreationBackButton != null) worldCreationSelectables.Add(worldCreationBackButton);
        if (worldCreationCreateButton != null) worldCreationSelectables.Add(worldCreationCreateButton);

        firstWorldCreationSelectable = worldNameInputField != null
            ? worldNameInputField
            : (worldCreationBackButton != null ? worldCreationBackButton : worldCreationCreateButton);

        WireWorldCreationControlListeners();
    }

    private InputField CreateWorldCreationInputRow(RectTransform parent, string rowName, int rowIndex, string label, string placeholderText, bool integerOnly)
    {
        if (optionsOpenButton == null)
            return null;

        var row = Instantiate(optionsOpenButton.gameObject, parent);
        row.name = rowName;

        var rowRect = row.GetComponent<RectTransform>();
        if (rowRect != null)
        {
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = worldCreationInputStartPosition + (Vector2.down * worldCreationInputSpacing * rowIndex);
            rowRect.sizeDelta = new Vector2(760f, 72f);
        }

        var rowButton = row.GetComponent<Button>();
        if (rowButton != null)
            Destroy(rowButton);

        var rowHover = row.GetComponent<UIButtonHoverSprite>();
        if (rowHover != null)
            Destroy(rowHover);

        var rowImage = row.GetComponent<Image>();
        if (rowImage != null)
            rowImage.raycastTarget = false;

        var labelText = row.GetComponentInChildren<Text>(true);
        if (labelText != null)
        {
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleLeft;

            var textRect = labelText.rectTransform;
            textRect.anchorMin = new Vector2(0.05f, 0.18f);
            textRect.anchorMax = new Vector2(0.38f, 0.82f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        var inputGo = DefaultControls.CreateInputField(new DefaultControls.Resources());
        inputGo.name = "InputField";
        inputGo.transform.SetParent(row.transform, false);

        var inputRect = inputGo.GetComponent<RectTransform>();
        if (inputRect != null)
        {
            inputRect.anchorMin = new Vector2(0.40f, 0.18f);
            inputRect.anchorMax = new Vector2(0.95f, 0.82f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
        }

        var inputField = inputGo.GetComponent<InputField>();
        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.contentType = integerOnly ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;

            var placeholder = inputField.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.text = placeholderText;
                placeholder.fontSize = 20;
            }

            var text = inputField.textComponent;
            if (text != null)
                text.fontSize = 22;
        }

        return inputField;
    }

    private Button CreateWorldCreationActionButton(RectTransform parent, string buttonName, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonGo;
        if (optionsOpenButton != null)
            buttonGo = Instantiate(optionsOpenButton.gameObject, parent);
        else
            buttonGo = DefaultControls.CreateButton(new DefaultControls.Resources());

        buttonGo.name = buttonName;
        buttonGo.transform.SetParent(parent, false);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(300f, 58f);
        }

        var button = buttonGo.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (action != null)
                button.onClick.AddListener(action);
        }

        var buttonText = buttonGo.GetComponentInChildren<Text>(true);
        if (buttonText != null)
            buttonText.text = label;

        return button;
    }

    private void WireWorldCreationControlListeners()
    {
        if (worldCreationBackButton != null)
        {
            worldCreationBackButton.onClick.RemoveListener(ShowMain);
            worldCreationBackButton.onClick.AddListener(ShowMain);
        }

        if (worldCreationCreateButton != null)
        {
            worldCreationCreateButton.onClick.RemoveListener(OnCreateWorldPressed);
            worldCreationCreateButton.onClick.AddListener(OnCreateWorldPressed);
        }
    }

    private Selectable GetFirstWorldCreationSelectable()
    {
        for (int i = 0; i < worldCreationSelectables.Count; i++)
        {
            if (worldCreationSelectables[i] != null && worldCreationSelectables[i].isActiveAndEnabled)
                return worldCreationSelectables[i];
        }

        return firstWorldCreationSelectable;
    }

    private void OnCreateWorldPressed()
    {
        string worldName = GetResolvedWorldName();
        int seed = GetResolvedWorldSeed();
        string worldId = Guid.NewGuid().ToString("N");

        PlayerPrefs.SetString(PrefPendingWorldName, worldName);
        PlayerPrefs.SetInt(PrefPendingWorldSeed, seed);
        PlayerPrefs.SetString(PrefPendingWorldId, worldId);
        PlayerPrefs.SetInt(PrefHasPendingWorldCreation, 1);
        PlayerPrefs.SetString(PrefCurrentWorldId, worldId);
        PlayerPrefs.SetString(PrefCurrentWorldName, worldName);
        PlayerPrefs.Save();

        Debug.Log("[MainMenuScreenRouter] Create world requested. Name: '" + worldName + "' Seed: " + seed + " WorldId: " + worldId, this);

        if (!string.IsNullOrEmpty(gameSceneName) && Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        Debug.LogWarning("[MainMenuScreenRouter] Game scene '" + gameSceneName + "' is not loadable. " +
                         "Pending world settings were saved and will be used when the game scene starts.", this);
    }

    private string GetResolvedWorldName()
    {
        string rawName = worldNameInputField != null ? worldNameInputField.text : string.Empty;
        string trimmedName = string.IsNullOrWhiteSpace(rawName) ? string.Empty : rawName.Trim();
        return string.IsNullOrEmpty(trimmedName) ? worldNameDefaultValue : trimmedName;
    }

    private int GetResolvedWorldSeed()
    {
        string rawSeed = worldSeedInputField != null ? worldSeedInputField.text : string.Empty;
        string trimmedSeed = string.IsNullOrWhiteSpace(rawSeed) ? string.Empty : rawSeed.Trim();

        if (string.IsNullOrEmpty(trimmedSeed))
            return Guid.NewGuid().GetHashCode();

        int parsedSeed;
        if (int.TryParse(trimmedSeed, out parsedSeed))
            return parsedSeed;

        Debug.LogWarning("[MainMenuScreenRouter] Invalid world seed input. Falling back to random seed.", this);
        return Guid.NewGuid().GetHashCode();
    }

    private void PopulateCreditsContent(RectTransform contentRect)
    {
        if (contentRect == null)
            return;

        ClearChildren(contentRect);

        var vlg = contentRect.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(8, 32, 8, 8);

        var fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        for (int i = 0; i < creditsArtists.Length; i++)
        {
            var rowGo = new GameObject("CreditRow_" + i, typeof(RectTransform), typeof(Text));
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.SetParent(contentRect, false);
            rowRect.sizeDelta = new Vector2(0f, 38f);

            var rowText = rowGo.GetComponent<Text>();
            rowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rowText.fontSize = 24;
            rowText.alignment = TextAnchor.MiddleLeft;
            rowText.color = Color.white;
            rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rowText.verticalOverflow = VerticalWrapMode.Overflow;
            rowText.text = creditsArtists[i];
        }
    }

    private void BuildOptionsControls(bool clearExisting)
    {
        if (optionsOpenButton == null || optionsGroup == null)
            return;

        if (clearExisting)
            ClearChildren(optionsGroup.transform);

        optionsSelectables.Clear();

        sfxSlider = CreateSliderRow(sfxControlObjectName, 0, sfxLabel);
        musicSlider = CreateSliderRow(musicControlObjectName, 1, musicLabel);
        displayModeButton = CreateActionButtonRow(displayModeControlObjectName, 2, string.Empty, ToggleDisplayMode);
        aspectRatioButton = CreateActionButtonRow(aspectRatioControlObjectName, 3, string.Empty, CycleAspectRatio);
        backButton = CreateActionButtonRow(backControlObjectName, 4, backLabel, ShowMain);

        WireOptionsControlListeners();
        firstOptionsSelectable = sfxSlider != null ? sfxSlider : displayModeButton;
    }

    private bool TryBindOptionsControlsFromScene()
    {
        optionsSelectables.Clear();

        sfxSlider = FindSliderControl(sfxControlObjectName, 0);
        musicSlider = FindSliderControl(musicControlObjectName, 1);
        displayModeButton = FindButtonControl(displayModeControlObjectName);
        aspectRatioButton = FindButtonControl(aspectRatioControlObjectName);
        backButton = FindButtonControl(backControlObjectName);

        if (sfxSlider != null) optionsSelectables.Add(sfxSlider);
        if (musicSlider != null) optionsSelectables.Add(musicSlider);
        if (displayModeButton != null) optionsSelectables.Add(displayModeButton);
        if (aspectRatioButton != null) optionsSelectables.Add(aspectRatioButton);
        if (backButton != null) optionsSelectables.Add(backButton);

        firstOptionsSelectable = sfxSlider != null
            ? sfxSlider
            : (musicSlider != null ? musicSlider : (Selectable)displayModeButton);

        WireOptionsControlListeners();

        return sfxSlider != null
            && musicSlider != null
            && displayModeButton != null
            && aspectRatioButton != null
            && backButton != null;
    }

    private Slider FindSliderControl(string objectName, int fallbackIndex)
    {
        if (optionsGroup == null)
            return null;

        var child = FindChildByName(optionsGroup.transform, objectName);
        if (child != null)
        {
            var slider = child.GetComponentInChildren<Slider>(true);
            if (slider != null)
                return slider;
        }

        var sliders = optionsGroup.GetComponentsInChildren<Slider>(true);
        if (fallbackIndex >= 0 && fallbackIndex < sliders.Length)
            return sliders[fallbackIndex];

        return null;
    }

    private Button FindButtonControl(string objectName)
    {
        if (optionsGroup == null)
            return null;

        var child = FindChildByName(optionsGroup.transform, objectName);
        if (child != null)
            return child.GetComponent<Button>();

        return null;
    }

    private Slider CreateSliderRow(string rowName, int rowIndex, string label)
    {
        var row = Instantiate(optionsOpenButton.gameObject, optionsGroup.transform);
        row.name = rowName;
        PlaceRow(row.GetComponent<RectTransform>(), rowIndex);

        var rowButton = row.GetComponent<Button>();
        if (rowButton != null)
            Destroy(rowButton);

        var rowHover = row.GetComponent<UIButtonHoverSprite>();
        if (rowHover != null)
            Destroy(rowHover);

        var rowImage = row.GetComponent<Image>();
        if (rowImage != null)
            rowImage.raycastTarget = false;

        var rowText = row.GetComponentInChildren<Text>(true);
        if (rowText != null)
        {
            rowText.text = label;
            rowText.alignment = TextAnchor.MiddleLeft;

            var textRect = rowText.rectTransform;
            textRect.anchorMin = new Vector2(0.06f, 0.2f);
            textRect.anchorMax = new Vector2(0.42f, 0.8f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderGo.name = "Slider";
        sliderGo.transform.SetParent(row.transform, false);

        var sliderRect = sliderGo.GetComponent<RectTransform>();
        if (sliderRect != null)
        {
            sliderRect.anchorMin = new Vector2(0.42f, 0.25f);
            sliderRect.anchorMax = new Vector2(0.95f, 0.75f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
        }

        var slider = sliderGo.GetComponent<Slider>();
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            optionsSelectables.Add(slider);
        }

        return slider;
    }

    private Button CreateActionButtonRow(string rowName, int rowIndex, string label, UnityEngine.Events.UnityAction action)
    {
        var row = Instantiate(optionsOpenButton.gameObject, optionsGroup.transform);
        row.name = rowName;
        PlaceRow(row.GetComponent<RectTransform>(), rowIndex);

        var button = row.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (action != null)
                button.onClick.AddListener(action);

            optionsSelectables.Add(button);
        }

        var text = row.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = label;

        return button;
    }

    private void PlaceRow(RectTransform rectTransform, int rowIndex)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = optionsStartPosition + (Vector2.down * optionsVerticalSpacing * rowIndex);
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void WireOptionsControlListeners()
    {
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (displayModeButton != null)
        {
            displayModeButton.onClick.RemoveListener(ToggleDisplayMode);
            displayModeButton.onClick.AddListener(ToggleDisplayMode);
        }

        if (aspectRatioButton != null)
        {
            aspectRatioButton.onClick.RemoveListener(CycleAspectRatio);
            aspectRatioButton.onClick.AddListener(CycleAspectRatio);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ShowMain);
            backButton.onClick.AddListener(ShowMain);
        }
    }

    private void ApplyOptionsToUi()
    {
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVolume);

        UpdateDisplayModeButtonLabel();
        UpdateAspectRatioButtonLabel();
    }

    private void OnSfxSliderChanged(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefSfxVolume, sfxVolume);
        PlayerPrefs.Save();
        ApplyRuntimeAudioVolumes();
    }

    private void OnMusicSliderChanged(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefMusicVolume, musicVolume);
        PlayerPrefs.Save();
        ApplyRuntimeAudioVolumes();
    }

    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ApplyRuntimeAudioVolumes()
    {
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source == null)
                continue;

            string sourceName = source.gameObject.name.ToLowerInvariant();
            bool isMusicSource = source.loop || sourceName.Contains("music") || sourceName.Contains("bgm") || sourceName.Contains("theme");
            source.volume = isMusicSource ? musicVolume : sfxVolume;
        }
    }

    private void ToggleDisplayMode()
    {
        isFullscreen = !isFullscreen;
        ApplyDisplaySettings();
        SaveDisplaySettings();
        UpdateDisplayModeButtonLabel();
    }

    private void CycleAspectRatio()
    {
        if (aspectRatioPresets == null || aspectRatioPresets.Length == 0)
            return;

        aspectRatioIndex = (aspectRatioIndex + 1) % aspectRatioPresets.Length;
        ApplyDisplaySettings();
        SaveDisplaySettings();
        UpdateAspectRatioButtonLabel();
    }

    private void LoadSavedOptions()
    {
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, 1f));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusicVolume, 1f));
        isFullscreen = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;

        int maxIndex = aspectRatioPresets != null ? aspectRatioPresets.Length - 1 : 0;
        aspectRatioIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefAspectRatioIndex, 0), 0, Mathf.Max(0, maxIndex));
    }

    private void SaveDisplaySettings()
    {
        PlayerPrefs.SetInt(PrefFullscreen, isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(PrefAspectRatioIndex, aspectRatioIndex);
        PlayerPrefs.Save();
    }

    private void ApplyDisplaySettings()
    {
        if (aspectRatioPresets == null || aspectRatioPresets.Length == 0)
            return;

        var preset = aspectRatioPresets[Mathf.Clamp(aspectRatioIndex, 0, aspectRatioPresets.Length - 1)];
        if (preset.width <= 0 || preset.height <= 0)
            return;

        float ratio = (float)preset.width / preset.height;
        int displayWidth = Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Screen.width;
        int displayHeight = Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Screen.height;

        int targetWidth;
        int targetHeight;

        if (isFullscreen)
        {
            targetHeight = displayHeight;
            targetWidth = Mathf.RoundToInt(targetHeight * ratio);

            if (targetWidth > displayWidth)
            {
                targetWidth = displayWidth;
                targetHeight = Mathf.RoundToInt(targetWidth / ratio);
            }

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.FullScreenWindow);
        }
        else
        {
            int maxWidth = Mathf.Max(640, Mathf.RoundToInt(displayWidth * 0.9f));
            int maxHeight = Mathf.Max(360, Mathf.RoundToInt(displayHeight * 0.9f));

            targetHeight = maxHeight;
            targetWidth = Mathf.RoundToInt(targetHeight * ratio);

            if (targetWidth > maxWidth)
            {
                targetWidth = maxWidth;
                targetHeight = Mathf.RoundToInt(targetWidth / ratio);
            }

            targetWidth = Mathf.Max(640, targetWidth);
            targetHeight = Mathf.Max(360, targetHeight);

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
        }
    }

    private void UpdateDisplayModeButtonLabel()
    {
        if (displayModeButton == null)
            return;

        var text = displayModeButton.GetComponentInChildren<Text>();
        if (text == null)
            return;

        text.text = displayModeLabelPrefix + ": " + (isFullscreen ? "Fullscreen" : "Windowed");
    }

    private void UpdateAspectRatioButtonLabel()
    {
        if (aspectRatioButton == null)
            return;

        var text = aspectRatioButton.GetComponentInChildren<Text>();
        if (text == null)
            return;

        string ratioLabel = "N/A";
        if (aspectRatioPresets != null && aspectRatioPresets.Length > 0)
            ratioLabel = aspectRatioPresets[Mathf.Clamp(aspectRatioIndex, 0, aspectRatioPresets.Length - 1)].label;

        text.text = aspectRatioLabelPrefix + ": " + ratioLabel;
    }

    private Selectable GetFirstOptionsSelectable()
    {
        for (int i = 0; i < optionsSelectables.Count; i++)
        {
            if (optionsSelectables[i] != null && optionsSelectables[i].isActiveAndEnabled)
                return optionsSelectables[i];
        }

        return firstOptionsSelectable;
    }

    private void HandleMixedNavigationSelection()
    {
        if (EventSystem.current == null)
            return;

        if (WasPointerUsedThisFrame() && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (IsKeyboardNavigationInputThisFrame() && EventSystem.current.currentSelectedGameObject == null)
            SelectCurrentScreenDefault();
    }

    private bool WasPointerUsedThisFrame()
    {
        var mousePosition = Input.mousePosition;
        bool moved = hasMousePositionSnapshot && (mousePosition - lastMousePosition).sqrMagnitude > 0.01f;
        lastMousePosition = mousePosition;
        hasMousePositionSnapshot = true;

        return moved
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonDown(1)
            || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
    }

    private bool IsKeyboardNavigationInputThisFrame()
    {
        return Input.GetKeyDown(KeyCode.Tab)
            || Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow)
            || Input.GetKeyDown(KeyCode.W)
            || Input.GetKeyDown(KeyCode.A)
            || Input.GetKeyDown(KeyCode.S)
            || Input.GetKeyDown(KeyCode.D)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
    }

    private void SelectCurrentScreenDefault()
    {
        switch (currentScreen)
        {
            case MenuScreen.Main:
                SelectElement(firstMainButton);
                break;
            case MenuScreen.LoadGame:
                SelectElement(GetFirstLoadGameSelectable());
                break;
            case MenuScreen.Options:
                SelectElement(GetFirstOptionsSelectable());
                break;
            case MenuScreen.Credits:
                SelectElement(creditsBackButton);
                break;
            case MenuScreen.WorldCreation:
                SelectElement(GetFirstWorldCreationSelectable());
                break;
        }
    }

    private void SelectElement(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private Button FindButton(Transform root, string objectName)
    {
        var child = FindChildByName(root, objectName);
        if (child == null)
            return null;

        return child.GetComponent<Button>();
    }

    private Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return null;

        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var found = FindChildByName(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
