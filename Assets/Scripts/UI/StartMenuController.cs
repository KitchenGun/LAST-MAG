using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartMenuController : MonoBehaviour
{
    private const int k_BuildIdLength = 8;

    private static readonly PlayerClassId[] Classes =
    {
        PlayerClassId.Grenadier,
        PlayerClassId.Engineer,
        PlayerClassId.Sniper
    };

    private Button m_playButton;
    private Button m_confirmButton;
    private Button m_backButton;
    private Button m_settingsButton;
    private Button[] m_classButtons;
    private TMP_Text m_selectedClassName;
    private TMP_Text m_selectedClassLoadout;
    private TMP_Text m_selectedClassSkill;
    private TMP_Text m_selectHint;
    private Image m_detailAccent;
    private GameObject[] m_classLoadoutVisuals;
    private GameObject m_classSelectionPanel;
    private GameObject m_title;
    private GameObject m_controls;
    private SettingsPanelController m_settingsPanel;
    private bool m_showingClassSelection;
    private bool m_showingSettings;

    private void Awake()
    {
        RunResultStore.Clear();
        InitializeBuildVersion();
        m_playButton = FindButton("PlayButton");
        m_confirmButton = FindButton("ConfirmButton");
        m_backButton = FindButton("BackButton");
        if (m_playButton == null || m_confirmButton == null || m_backButton == null)
        {
            Debug.LogError("[StartMenu] PlayButton, ConfirmButton, or BackButton was not found.");
            return;
        }

        m_playButton.onClick.RemoveAllListeners();
        m_playButton.onClick.AddListener(Play);
        m_confirmButton.onClick.RemoveAllListeners();
        m_confirmButton.onClick.AddListener(ConfirmClassSelection);
        m_backButton.onClick.RemoveAllListeners();
        m_backButton.onClick.AddListener(BackToMainMenu);

        m_settingsButton = FindButton("SettingsButton");
        m_settingsPanel = GetComponentInChildren<SettingsPanelController>(true);
        if (m_settingsButton == null || m_settingsPanel == null)
        {
            Debug.LogError("[StartMenu] Serialized settings UI is incomplete in StartScene.");
            return;
        }

        m_settingsButton.onClick.RemoveAllListeners();
        m_settingsButton.onClick.AddListener(OpenSettings);

        m_classSelectionPanel = FindChildObject("ClassSelectionPanel");
        m_title = FindChildObject("Title");
        m_controls = FindChildObject("Controls");
        InitializeFlashlightControlHint();
        m_showingClassSelection = false;
        m_showingSettings = false;
        m_settingsPanel.Hide();
        if (m_classSelectionPanel != null) m_classSelectionPanel.SetActive(false);
        if (m_title != null) m_title.SetActive(true);
        if (m_controls != null) m_controls.SetActive(true);
        m_playButton.interactable = true;
        m_playButton.gameObject.SetActive(true);
        m_confirmButton.gameObject.SetActive(false);
        m_confirmButton.interactable = false;
        m_backButton.gameObject.SetActive(true);
        m_backButton.interactable = true;
        m_selectedClassName = FindText("SelectedClassName");
        m_selectedClassLoadout = FindText("SelectedClassLoadout");
        m_selectedClassSkill = FindText("SelectedClassSkill");
        m_selectHint = FindText("SelectHint");
        m_detailAccent = FindImage("DetailAccent");
        m_classLoadoutVisuals = new[]
        {
            FindChildObject("GrenadierLoadoutVisuals"),
            FindChildObject("EngineerLoadoutVisuals"),
            FindChildObject("SniperLoadoutVisuals")
        };
        m_classButtons = new Button[Classes.Length];
        for (int index = 0; index < Classes.Length; index++)
        {
            PlayerClassId playerClass = Classes[index];
            Button button = FindButton($"{playerClass}Button");
            if (button == null)
            {
                Debug.LogError($"[StartMenu] {playerClass}Button was not found.");
                continue;
            }

            ColorBlock colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectClass(playerClass));
            m_classButtons[index] = button;
        }

        UpdateSelectionVisuals(PlayerClassId.Unknown);
    }

    private void Update()
    {
        if (m_showingSettings && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    private void InitializeBuildVersion()
    {
        TMP_Text versionText = FindText("BuildVersionText");
        if (versionText == null)
        {
            Debug.LogError("[StartMenu] BuildVersionText was not found.");
            return;
        }

        versionText.text = FormatBuildVersion(Application.version, Application.buildGUID, Application.isEditor);
        versionText.raycastTarget = false;
        versionText.overflowMode = TextOverflowModes.Overflow;
    }

    private void InitializeFlashlightControlHint()
    {
        if (m_controls == null
            || m_controls.transform.Find("FlashlightIcon") != null
            || m_controls.transform.Find("FlashlightLabel") != null)
        {
            return;
        }

        TMP_Text template = null;
        foreach (TMP_Text candidate in m_controls.GetComponentsInChildren<TMP_Text>(true))
        {
            template = candidate;
            break;
        }

        if (template == null)
        {
            Debug.LogWarning("[StartMenu] Controls text template was not found.");
            return;
        }

        GameObject hintObject = new("FlashlightControlHint");
        hintObject.transform.SetParent(m_controls.transform, false);

        TMP_Text keyHint = CreateHintText(hintObject.transform, template, "V", new Vector2(0f, -396f), new Vector2(64f, 64f));
        keyHint.alignment = TextAlignmentOptions.Center;
        TMP_Text descriptionHint = CreateHintText(hintObject.transform, template, "FLASHLIGHT    TOGGLE", new Vector2(104f, -396f), new Vector2(500f, 64f));
        descriptionHint.alignment = template.alignment;
    }

    private TMP_Text CreateHintText(Transform parent, TMP_Text template, string text, Vector2 position, Vector2 size)
    {
        GameObject textObject = new(text);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text hint = textObject.AddComponent<TextMeshProUGUI>();
        hint.font = template.font;
        hint.fontSize = template.fontSize;
        hint.color = template.color;
        hint.raycastTarget = false;
        hint.text = text;
        return hint;
    }

    private void OpenSettings()
    {
        if (m_showingClassSelection || m_settingsPanel == null)
        {
            return;
        }
        m_showingSettings = true;
        if (m_controls != null) m_controls.SetActive(false);
        m_settingsPanel.Show(CloseSettings);
    }

    private void CloseSettings()
    {
        m_showingSettings = false;
        m_settingsPanel?.Hide();
        if (!m_showingClassSelection && m_controls != null) m_controls.SetActive(true);
    }

    internal static string FormatBuildVersion(string version, string buildGuid, bool isEditor)
    {
        string normalizedVersion = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
        string compactGuid = string.IsNullOrWhiteSpace(buildGuid)
            ? string.Empty
            : buildGuid.Replace("-", string.Empty).ToUpperInvariant();
        string buildId = isEditor
            ? "EDITOR"
            : compactGuid.Length >= k_BuildIdLength
                ? compactGuid[..k_BuildIdLength]
                : "UNKNOWN";
        return $"v{normalizedVersion} · WEB-{buildId}";
    }

    private Button FindButton(string objectName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName) return button;
        }
        return null;
    }

    private TMP_Text FindText(string objectName)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == objectName) return text;
        }
        return null;
    }

    private Image FindImage(string objectName)
    {
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.name == objectName) return image;
        }
        return null;
    }

    private GameObject FindChildObject(string objectName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName) return child.gameObject;
        }
        return null;
    }

    private void SelectClass(PlayerClassId playerClass)
    {
        RunResultStore.SelectClass(playerClass);
        m_confirmButton.gameObject.SetActive(true);
        m_confirmButton.interactable = true;
        UpdateSelectionVisuals(playerClass);
    }

    private void UpdateSelectionVisuals(PlayerClassId playerClass)
    {
        for (int index = 0; index < m_classButtons.Length; index++)
        {
            if (m_classButtons[index] == null) continue;
            Transform outline = m_classButtons[index].transform.Find("SelectionOutline");
            if (outline == null) continue;
            Image fill = outline.GetComponent<Image>();
            if (fill != null) fill.enabled = false;
            outline.gameObject.SetActive(Classes[index] == playerClass);
        }

        if (playerClass == PlayerClassId.Unknown)
        {
            SetLoadoutVisuals(PlayerClassId.Unknown);
            SetDetailText("SELECT A CLASS", "CHOOSE AN OPERATOR", "LOADOUT DETAILS WILL APPEAR HERE",
                "Click a portrait to select a class");
            return;
        }

        Color accent;
        string className = RunResultStore.GetPlayerClassName(playerClass);
        if (playerClass == PlayerClassId.Grenadier)
        {
            accent = new Color32(234, 64, 71, 255);
            SetDetailText(className, "RIFLE  /  PISTOL", "SKILL: FRAGMENTATION GRENADE",
                "Sustained fire and grenades");
        }
        else if (playerClass == PlayerClassId.Engineer)
        {
            accent = new Color32(53, 199, 89, 255);
            SetDetailText(className, "SHOTGUN  /  PISTOL", "SKILL: ROCKET LAUNCHER",
                "Close-range firepower and powerful rockets");
        }
        else
        {
            accent = new Color32(44, 135, 232, 255);
            SetDetailText(className, "DMR  /  PISTOL", "SKILL: BULLET TIME",
                "Penetration and bullet time");
        }

        if (m_detailAccent != null) m_detailAccent.color = accent;
        SetLoadoutVisuals(playerClass);
    }

    private void SetLoadoutVisuals(PlayerClassId playerClass)
    {
        bool hasSelection = playerClass != PlayerClassId.Unknown;
        for (int index = 0; index < m_classLoadoutVisuals.Length; index++)
        {
            if (m_classLoadoutVisuals[index] != null)
            {
                m_classLoadoutVisuals[index].SetActive(hasSelection && Classes[index] == playerClass);
            }
        }

        if (m_selectedClassLoadout != null) m_selectedClassLoadout.gameObject.SetActive(!hasSelection);
        if (m_selectedClassSkill != null) m_selectedClassSkill.gameObject.SetActive(!hasSelection);
    }

    private void SetDetailText(string className, string loadout, string skill, string hint)
    {
        if (m_selectedClassName != null) m_selectedClassName.text = className;
        if (m_selectedClassLoadout != null) m_selectedClassLoadout.text = loadout;
        if (m_selectedClassSkill != null) m_selectedClassSkill.text = skill;
        if (m_selectHint != null) m_selectHint.text = hint;
    }

    public void Play()
    {
        if (m_showingSettings)
        {
            return;
        }
        if (m_showingClassSelection)
        {
            return;
        }

        ShowClassSelection();
    }

    public void ConfirmClassSelection()
    {
        if (!m_showingClassSelection || RunResultStore.SelectedClass == PlayerClassId.Unknown)
        {
            return;
        }

        RunResultStore.ClearResult();
        SceneManager.LoadScene("GameplayScene");
    }

    public void BackToMainMenu()
    {
        if (!m_showingClassSelection)
        {
            return;
        }

        RunResultStore.SelectClass(PlayerClassId.Unknown);
        m_showingClassSelection = false;
        if (m_classSelectionPanel != null) m_classSelectionPanel.SetActive(false);
        if (m_title != null) m_title.SetActive(true);
        if (m_controls != null) m_controls.SetActive(true);
        if (m_settingsButton != null) m_settingsButton.gameObject.SetActive(true);
        if (m_playButton != null)
        {
            m_playButton.gameObject.SetActive(true);
            m_playButton.interactable = true;
        }
        if (m_confirmButton != null)
        {
            m_confirmButton.gameObject.SetActive(false);
            m_confirmButton.interactable = false;
        }
        UpdateSelectionVisuals(PlayerClassId.Unknown);
    }

    private void ShowClassSelection()
    {
        CloseSettings();
        m_showingClassSelection = true;
        if (m_title != null) m_title.SetActive(false);
        if (m_controls != null) m_controls.SetActive(false);
        if (m_settingsButton != null) m_settingsButton.gameObject.SetActive(false);
        if (m_classSelectionPanel != null) m_classSelectionPanel.SetActive(true);
        m_playButton.gameObject.SetActive(false);
        m_confirmButton.gameObject.SetActive(false);
        m_confirmButton.interactable = false;
        m_backButton.gameObject.SetActive(true);
        m_backButton.interactable = true;
    }

    [ContextMenu("Run Class Selection Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(RunResultStore.GetPlayerClassName(PlayerClassId.Grenadier) == "GRENADIER");
        Debug.Assert(RunResultStore.GetPlayerClassName(PlayerClassId.Engineer) == "ENGINEER");
        Debug.Assert(RunResultStore.GetPlayerClassName(PlayerClassId.Sniper) == "SNIPER");
        Debug.Assert(RunResultStore.GetPlayerClassName(PlayerClassId.Unknown) == "UNKNOWN");
        Debug.Assert(FormatBuildVersion("0.1.0", "01234567-89ab-cdef", false)
            == "v0.1.0 · WEB-01234567");
        Debug.Assert(FormatBuildVersion("0.1.0", string.Empty, true) == "v0.1.0 · WEB-EDITOR");
        Debug.Assert(SettingsPanelController.FormatSensitivity(0f) == "0.00"
            && SettingsPanelController.FormatSensitivity(0.5f) == "0.50"
            && SettingsPanelController.FormatSensitivity(1f) == "1.00");
        Debug.Assert(SettingsPanelController.FormatVolume(0f) == "0%"
            && SettingsPanelController.FormatVolume(0.5f) == "50%"
            && SettingsPanelController.FormatVolume(1f) == "100%");
        Debug.Assert(GameSettings.IsValidZoomInputMode(ZoomInputMode.Toggle)
            && GameSettings.IsValidZoomInputMode(ZoomInputMode.Hold)
            && !GameSettings.IsValidZoomInputMode((ZoomInputMode)2));
    }
}
