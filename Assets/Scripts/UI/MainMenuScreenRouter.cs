using System.Collections.Generic;
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
        Options
    }

    private const string PrefSfxVolume = "MainMenu.SfxVolume";
    private const string PrefMusicVolume = "MainMenu.MusicVolume";
    private const string PrefFullscreen = "MainMenu.IsFullscreen";
    private const string PrefAspectRatioIndex = "MainMenu.AspectRatioIndex";

    [Header("Auto Find")]
    [SerializeField] private string canvasName = "Canvas";
    [SerializeField] private string optionsGroupName = "OptionsButtonsGroup";

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

    private readonly List<GameObject> mainButtons = new List<GameObject>();
    private readonly List<Selectable> optionsSelectables = new List<Selectable>();

    private Canvas cachedCanvas;
    private GameObject optionsGroup;
    private Button optionsOpenButton;
    private Button firstMainButton;
    private Button backButton;
    private Button displayModeButton;
    private Button aspectRatioButton;
    private Slider sfxSlider;
    private Slider musicSlider;
    private Selectable firstOptionsSelectable;

    private float sfxVolume = 1f;
    private float musicVolume = 1f;
    private bool isFullscreen = true;
    private int aspectRatioIndex;

    private MenuScreen currentScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainMenu")
            return;

        if (Object.FindFirstObjectByType<MainMenuScreenRouter>() != null)
            return;

        var routerGo = new GameObject("MainMenuScreenRouter");
        routerGo.AddComponent<MainMenuScreenRouter>();
    }

    private void Awake()
    {
        if (!TryFindCanvas(out cachedCanvas))
        {
            Debug.LogWarning("[MainMenuScreenRouter] Canvas not found, router disabled.", this);
            enabled = false;
            return;
        }

        CacheMainButtons(cachedCanvas.transform);
        if (mainButtons.Count == 0 || optionsOpenButton == null)
        {
            Debug.LogWarning("[MainMenuScreenRouter] Missing main buttons or Options button, router disabled.", this);
            enabled = false;
            return;
        }

        optionsOpenButton.onClick.RemoveListener(ShowOptions);
        optionsOpenButton.onClick.AddListener(ShowOptions);

        LoadSavedOptions();
        BuildOrFindOptionsGroup(cachedCanvas.transform);
        ApplyOptionsToUi();
        ApplyRuntimeAudioVolumes();
        ApplyDisplaySettings();

        ShowMain();
    }

    private void Update()
    {
        if (currentScreen == MenuScreen.Options && Input.GetKeyDown(KeyCode.Escape))
            ShowMain();
    }

    private void OnDestroy()
    {
        if (optionsOpenButton != null)
            optionsOpenButton.onClick.RemoveListener(ShowOptions);

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
    }

    public void ShowMain()
    {
        SetMainButtonsVisible(true);

        if (optionsGroup != null)
            optionsGroup.SetActive(false);

        currentScreen = MenuScreen.Main;
        SelectElement(firstMainButton);
    }

    public void ShowOptions()
    {
        SetMainButtonsVisible(false);

        if (optionsGroup != null)
            optionsGroup.SetActive(true);

        currentScreen = MenuScreen.Options;
        SelectElement(GetFirstOptionsSelectable());
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

        firstMainButton = FindButton(canvasRoot, newGameButtonName);
        optionsOpenButton = FindButton(canvasRoot, optionsButtonName);

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
