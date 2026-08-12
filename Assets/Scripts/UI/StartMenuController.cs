using TMPro;
using UnityEngine;
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
    private bool m_showingClassSelection;

    private void Awake()
    {
        RunResultStore.Clear();
        InitializeBuildVersion();
        m_playButton = FindPlayButton();
        if (m_playButton == null)
        {
            Debug.LogError("[StartMenu] PlayButton was not found.");
            return;
        }

        m_classSelectionPanel = FindChildObject("ClassSelectionPanel");
        m_title = FindChildObject("Title");
        m_controls = FindChildObject("Controls");
        m_showingClassSelection = false;
        if (m_classSelectionPanel != null) m_classSelectionPanel.SetActive(false);
        if (m_title != null) m_title.SetActive(true);
        if (m_controls != null) m_controls.SetActive(true);
        m_playButton.interactable = true;
        SetPlayButtonLabel("PLAY");
        ConfigurePlayButton(false);
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

    private void InitializeBuildVersion()
    {
        GameObject versionObject = new(
            "BuildVersionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        versionObject.transform.SetParent(transform, false);

        TextMeshProUGUI versionText = versionObject.GetComponent<TextMeshProUGUI>();
        TMP_Text fontSource = FindFirstFontSource();
        if (fontSource != null)
        {
            versionText.font = fontSource.font;
        }
        versionText.text = FormatBuildVersion(Application.version, Application.buildGUID, Application.isEditor);
        versionText.fontSize = 14f;
        versionText.color = new Color(0.68f, 0.78f, 0.8f, 0.65f);
        versionText.alignment = TextAlignmentOptions.BottomLeft;
        versionText.raycastTarget = false;
        versionText.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = versionText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(20f, 14f);
        rect.sizeDelta = new Vector2(520f, 28f);
    }

    private TMP_Text FindFirstFontSource()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null)
            {
                return text;
            }
        }
        return null;
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

    private Button FindPlayButton()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == "PlayButton")
            {
                return button;
            }
        }
        return null;
    }

    private void SelectClass(PlayerClassId playerClass)
    {
        RunResultStore.SelectClass(playerClass);
        m_playButton.interactable = true;
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
        if (!m_showingClassSelection)
        {
            ShowClassSelection();
            return;
        }

        if (RunResultStore.SelectedClass == PlayerClassId.Unknown)
        {
            return;
        }

        RunResultStore.ClearResult();
        SceneManager.LoadScene("GameplayScene");
    }

    private void ShowClassSelection()
    {
        m_showingClassSelection = true;
        if (m_title != null) m_title.SetActive(false);
        if (m_controls != null) m_controls.SetActive(false);
        if (m_classSelectionPanel != null) m_classSelectionPanel.SetActive(true);
        m_playButton.interactable = false;
        SetPlayButtonLabel("CONFIRM");
        ConfigurePlayButton(true);
    }

    private void SetPlayButtonLabel(string value)
    {
        TMP_Text tmp = m_playButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = value;
        Text legacy = m_playButton.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = value;
    }

    private void ConfigurePlayButton(bool classSelectionLayout)
    {
        RectTransform rect = m_playButton.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = classSelectionLayout
            ? new Vector2(1f, 0f)
            : new Vector2(1f, 0.5f);
        rect.pivot = classSelectionLayout ? new Vector2(1f, 0f) : new Vector2(1f, 0.5f);
        rect.anchoredPosition = classSelectionLayout ? new Vector2(-64f, 36f) : new Vector2(-82f, 220f);
        rect.sizeDelta = classSelectionLayout ? new Vector2(350f, 90f) : new Vector2(350f, 100f);
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
    }
}
