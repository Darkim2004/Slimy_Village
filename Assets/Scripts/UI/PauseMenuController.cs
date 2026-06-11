using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gestisce il menu pausa attivato con ESC.
/// Blocca il player, ferma il tempo, e previene sovrapposizioni con altri menu.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button volumeButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio Options")]
    [SerializeField] private string volumeButtonObjectName = "VolumeButton";
    [SerializeField] private string sfxRowObjectName = "SfxRow";
    [SerializeField] private string musicRowObjectName = "MusicRow";
    [SerializeField] private string sfxLabel = "SFX";
    [SerializeField] private string musicLabel = "Music";
    [SerializeField] private Vector2 audioRowSize = new Vector2(400f, 100f);
    [SerializeField] private Vector2 musicRowPosition = new Vector2(0f, -300f);
    [SerializeField] private Vector2 sfxRowPosition = new Vector2(0f, -420f);
    [SerializeField] private Vector2 quitButtonPositionWithAudioRows = new Vector2(0f, -540f);
    [SerializeField] private Vector2 generatedSliderPosition = new Vector2(-20f, 0f);
    [SerializeField] private Vector2 generatedSliderSize = new Vector2(210f, 50f);

    private PlayerTopDown playerTopDown;
    private InventoryToggleController inventoryToggleController;
    private Slider sfxSlider;
    private Slider musicSlider;
    private bool updatingAudioSliders;
    private bool isPaused;
    private float timeScaleBeforePause = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        playerTopDown = FindFirstObjectByType<PlayerTopDown>();
        inventoryToggleController = FindFirstObjectByType<InventoryToggleController>();

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ClosePause);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        EnsureAudioOptionsUi();
        BindVolumeButton();
        RefreshAudioSlidersFromSavedValues();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        isPaused = false;
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ClosePause);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);

        if (volumeButton != null)
            volumeButton.onClick.RemoveListener(FocusFirstAudioSlider);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (isPaused)
            Time.timeScale = timeScaleBeforePause;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscapeKey();
    }

    private void HandleEscapeKey()
    {
        if (playerTopDown != null && playerTopDown.IsAnyInteractionMenuOpenPublic())
        {
            playerTopDown.CloseAllInteractionMenusPublic();
            return;
        }

        if (inventoryToggleController != null && inventoryToggleController.IsOpen)
        {
            inventoryToggleController.SetOpen(false);
            return;
        }

        TogglePause();
    }

    public void TogglePause()
    {
        if (isPaused)
            ClosePause();
        else
            TryOpenPause();
    }

    public void TryOpenPause()
    {
        if (playerTopDown != null && playerTopDown.IsInputLocked)
            return;

        if (playerTopDown != null && playerTopDown.IsAnyInteractionMenuOpenPublic())
            return;

        if (inventoryToggleController != null && inventoryToggleController.IsOpen)
            return;

        OpenPause();
    }

    private void OpenPause()
    {
        if (isPaused)
            return;

        isPaused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(true);

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            RefreshAudioSlidersFromSavedValues();

            if (EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    public void ClosePause()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = timeScaleBeforePause;

        if (playerTopDown != null)
            playerTopDown.SetInputLocked(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void OnQuitClicked()
    {
        WorldSaveSystem.Instance?.SaveNow("save-and-quit");
        ClosePause();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnSaveClicked()
    {
        bool success = WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.SaveNow("manual");
        Debug.Log(success ? "[PauseMenu] Manual save completed." : "[PauseMenu] Manual save skipped or failed.");
    }

    private void EnsureAudioOptionsUi()
    {
        if (pausePanel == null)
            return;

        EnsureAudioOptionDefaults();

        if (volumeButton == null)
            volumeButton = FindButtonByName(pausePanel.transform, volumeButtonObjectName);

        bool generatedAudioRows = EnsureAudioRows();
        BindAudioSliders();

        if (generatedAudioRows)
            ApplyGeneratedPauseLayout();
    }

    private void EnsureAudioOptionDefaults()
    {
        if (string.IsNullOrWhiteSpace(volumeButtonObjectName))
            volumeButtonObjectName = "VolumeButton";

        if (string.IsNullOrWhiteSpace(sfxRowObjectName))
            sfxRowObjectName = "SfxRow";

        if (string.IsNullOrWhiteSpace(musicRowObjectName))
            musicRowObjectName = "MusicRow";

        if (string.IsNullOrWhiteSpace(sfxLabel))
            sfxLabel = "SFX";

        if (string.IsNullOrWhiteSpace(musicLabel))
            musicLabel = "Music";

        if (audioRowSize == Vector2.zero)
            audioRowSize = new Vector2(400f, 100f);

        if (musicRowPosition == Vector2.zero)
            musicRowPosition = new Vector2(0f, -300f);

        if (sfxRowPosition == Vector2.zero)
            sfxRowPosition = new Vector2(0f, -420f);

        if (quitButtonPositionWithAudioRows == Vector2.zero)
            quitButtonPositionWithAudioRows = new Vector2(0f, -540f);

        if (generatedSliderPosition == Vector2.zero)
            generatedSliderPosition = new Vector2(-20f, 0f);

        if (generatedSliderSize == Vector2.zero)
            generatedSliderSize = new Vector2(210f, 50f);
    }

    private bool EnsureAudioRows()
    {
        musicSlider = FindOrCreateAudioSlider(musicRowObjectName, musicLabel, musicRowPosition, out bool generatedMusicRow);
        sfxSlider = FindOrCreateAudioSlider(sfxRowObjectName, sfxLabel, sfxRowPosition, out bool generatedSfxRow);

        ConfigureAudioSlider(musicSlider);
        ConfigureAudioSlider(sfxSlider);

        return generatedMusicRow || generatedSfxRow;
    }

    private Slider FindOrCreateAudioSlider(string rowObjectName, string label, Vector2 rowPosition, out bool generatedControl)
    {
        generatedControl = false;

        Transform row = FindChildByName(pausePanel.transform, rowObjectName);
        if (row == null)
        {
            generatedControl = true;
            return CreateAudioSliderRow(rowObjectName, label, rowPosition);
        }

        Slider slider = row.GetComponentInChildren<Slider>(true);
        if (slider != null)
            return slider;

        generatedControl = true;
        return CreateSliderForRow(row, label);
    }

    private void BindAudioSliders()
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
    }

    private void BindVolumeButton()
    {
        if (volumeButton == null)
            return;

        volumeButton.onClick.RemoveListener(FocusFirstAudioSlider);
        volumeButton.onClick.AddListener(FocusFirstAudioSlider);
    }

    private Slider CreateAudioSliderRow(string rowObjectName, string label, Vector2 rowPosition)
    {
        GameObject rowGo = new GameObject(rowObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rowGo.layer = pausePanel.layer;
        rowGo.transform.SetParent(pausePanel.transform, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = rowPosition;
        rowRect.sizeDelta = audioRowSize;

        Image rowImage = rowGo.GetComponent<Image>();
        CopyPauseButtonImageStyle(rowImage);
        rowImage.raycastTarget = false;

        CreateAudioLabel(rowGo.transform, label);
        return CreateSliderForRow(rowGo.transform, label);
    }

    private Slider CreateSliderForRow(Transform row, string label)
    {
        GameObject sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderGo.name = "Slider";
        sliderGo.transform.SetParent(row, false);
        SetLayerRecursively(sliderGo, row.gameObject.layer);

        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        if (sliderRect != null)
        {
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(1f, 0.5f);
            sliderRect.anchoredPosition = generatedSliderPosition;
            sliderRect.sizeDelta = generatedSliderSize;
        }

        Slider slider = sliderGo.GetComponent<Slider>();
        ConfigureAudioSlider(slider);

        if (!HasTextChild(row))
            CreateAudioLabel(row, label);

        return slider;
    }

    private void CreateAudioLabel(Transform row, string label)
    {
        GameObject labelGo = new GameObject("Text (Legacy)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.layer = row.gameObject.layer;
        labelGo.transform.SetParent(row, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.06f, 0.2f);
        labelRect.anchorMax = new Vector2(0.42f, 0.8f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text labelText = labelGo.GetComponent<Text>();
        CopyPauseButtonTextStyle(labelText);
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.raycastTarget = false;
    }

    private void ConfigureAudioSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void ApplyGeneratedPauseLayout()
    {
        ConfigurePauseButtonRect(resumeButton, new Vector2(0f, -180f));
        ConfigurePauseButtonRect(quitButton, quitButtonPositionWithAudioRows);
    }

    private void ConfigurePauseButtonRect(Button button, Vector2 position)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = audioRowSize;
    }

    private void FocusFirstAudioSlider()
    {
        RefreshAudioSlidersFromSavedValues();

        if (EventSystem.current == null)
            return;

        if (musicSlider != null)
            EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
        else if (sfxSlider != null)
            EventSystem.current.SetSelectedGameObject(sfxSlider.gameObject);
    }

    private void RefreshAudioSlidersFromSavedValues()
    {
        updatingAudioSliders = true;
        GlobalAudioVolume.GetSavedVolumes(out float sfxVolume, out float musicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVolume);

        updatingAudioSliders = false;
    }

    private void OnSfxSliderChanged(float value)
    {
        if (updatingAudioSliders)
            return;

        PlayerPrefs.SetFloat(GlobalAudioVolume.PrefSfxVolume, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        GlobalAudioVolume.ApplyToSceneAudioSources();
    }

    private void OnMusicSliderChanged(float value)
    {
        if (updatingAudioSliders)
            return;

        PlayerPrefs.SetFloat(GlobalAudioVolume.PrefMusicVolume, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        GlobalAudioVolume.ApplyToSceneAudioSources();
    }

    private void CopyPauseButtonImageStyle(Image target)
    {
        if (target == null)
            return;

        Image source = null;
        if (resumeButton != null)
            source = resumeButton.targetGraphic as Image ?? resumeButton.GetComponent<Image>();

        if (source == null && quitButton != null)
            source = quitButton.targetGraphic as Image ?? quitButton.GetComponent<Image>();

        if (source == null)
        {
            target.color = Color.white;
            return;
        }

        target.sprite = source.sprite;
        target.overrideSprite = source.overrideSprite;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.material = source.material;
        target.color = source.color;
    }

    private void CopyPauseButtonTextStyle(Text target)
    {
        if (target == null)
            return;

        Text source = null;
        if (resumeButton != null)
            source = resumeButton.GetComponentInChildren<Text>(true);

        if (source == null && quitButton != null)
            source = quitButton.GetComponentInChildren<Text>(true);

        if (source == null)
        {
            target.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            target.fontSize = 14;
            target.fontStyle = FontStyle.Bold;
            target.resizeTextForBestFit = true;
            target.resizeTextMinSize = 10;
            target.resizeTextMaxSize = 40;
            target.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
            return;
        }

        target.font = source.font;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.resizeTextForBestFit = source.resizeTextForBestFit;
        target.resizeTextMinSize = source.resizeTextMinSize;
        target.resizeTextMaxSize = source.resizeTextMaxSize;
        target.horizontalOverflow = source.horizontalOverflow;
        target.verticalOverflow = source.verticalOverflow;
        target.supportRichText = source.supportRichText;
        target.lineSpacing = source.lineSpacing;
        target.color = source.color;
    }

    private static bool HasTextChild(Transform row)
    {
        return row != null && row.GetComponentInChildren<Text>(true) != null;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
    }

    private static Button FindButtonByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform child = FindChildByName(root, objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
