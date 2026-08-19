using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultSceneController : MonoBehaviour
{
    private const int MaxRankingEntries = 10;
    private const string DefaultNickname = "Player";
    private const string OfflineSubmissionText = "ONLINE SUBMISSION\nNOT ENABLED";
    private const string OfflineRankingText = "ONLINE RANKING\nNOT ENABLED";

    [Header("Result")]
    [SerializeField] private TMP_Text m_finalScoreText;
    [SerializeField] private TMP_Text m_personalBestText;
    [SerializeField] private TMP_Text[] m_runValues;
    [SerializeField] private TMP_Text[] m_weaponKillValues;
    [SerializeField] private Sprite m_dmrSilhouette;
    [SerializeField] private Sprite m_grenadeSkillSilhouette;
    [SerializeField] private Sprite m_rocketSkillSilhouette;
    [SerializeField] private Sprite m_bulletTimeSkillSilhouette;
    [SerializeField] private TMP_Text m_deathCauseText;
    [SerializeField] private Button m_retryButton;

    [Header("UI Skin")]
    [SerializeField] private Sprite m_rankingRowNormalSprite;
    [SerializeField] private Sprite m_rankingRowPlayerSprite;

    [Header("Online ranking")]
    [SerializeField] private string m_leaderboardUrl;
    [SerializeField] private TMP_InputField m_nicknameInput;
    [SerializeField] private Button m_submitButton;
    [SerializeField] private TMP_Text m_submitButtonText;
    [SerializeField] private TMP_Text m_submissionStatusText;
    [SerializeField] private TMP_Text m_submissionHelpText;
    [SerializeField] private TMP_Text m_rankingText;
    [SerializeField] private TMP_Text[] m_rankingRankTexts;
    [SerializeField] private TMP_Text[] m_rankingNicknameTexts;
    [SerializeField] private TMP_Text[] m_rankingScoreTexts;

    private TMP_Text[] m_rankingClassTexts;
    private readonly Color[] m_rankColors = new Color[MaxRankingEntries];
    private readonly Color[] m_nicknameColors = new Color[MaxRankingEntries];
    private readonly Color[] m_classColors = new Color[MaxRankingEntries];
    private readonly Color[] m_scoreColors = new Color[MaxRankingEntries];
    private readonly Image[] m_rankingRowBackgrounds = new Image[MaxRankingEntries];
    private bool m_isLoading;
    private bool m_submissionArmed;
    private bool m_submissionStarted;
    private int m_rankingRequestId;
    private string m_runId;
    private RunResultSnapshot m_result;
    private TMP_Text m_loadoutHeader;
    private readonly TMP_Text[] m_loadoutSlotTexts = new TMP_Text[3];
    private readonly TMP_Text[] m_loadoutNameTexts = new TMP_Text[3];
    private readonly Image[] m_loadoutIcons = new Image[3];
    private Sprite m_pistolSilhouette;
    private Sprite m_shotgunSilhouette;
    private Sprite m_rifleSilhouette;
    private TMP_Text m_totalKillsLabel;
    private TMP_Text m_headshotKillsLabel;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PrepareResultUi();
        m_result = RunResultStore.Current;
        Render(m_result);
        InitializeSubmission();

        if (m_retryButton != null)
        {
            m_retryButton.onClick.AddListener(Retry);
        }

        bool inputIsAvailable = m_nicknameInput != null && m_nicknameInput.gameObject.activeSelf;
        EventSystem.current?.SetSelectedGameObject(
            inputIsAvailable ? m_nicknameInput.gameObject : m_retryButton?.gameObject);
        if (inputIsAvailable)
        {
            m_nicknameInput.Select();
            m_nicknameInput.ActivateInputField();
        }
    }

    private void OnDestroy()
    {
        m_retryButton?.onClick.RemoveListener(Retry);
        m_submitButton?.onClick.RemoveListener(SubmitScore);
    }

    private void InitializeSubmission()
    {
        m_submissionArmed = false;
        if (m_nicknameInput != null)
        {
            m_nicknameInput.characterLimit = 12;
            m_nicknameInput.onValidateInput = ValidateNicknameCharacter;
            m_nicknameInput.SetTextWithoutNotify(DefaultNickname);
        }

        m_submitButton?.onClick.AddListener(SubmitScore);
        m_runId = Guid.NewGuid().ToString("D");

        if (string.IsNullOrWhiteSpace(m_leaderboardUrl))
        {
            ShowOfflineFallback();
            return;
        }

        if (m_result == null)
        {
            ShowOfflineFallback();
            StartCoroutine(RefreshLeaderboard());
            return;
        }

        SetInputVisible(true);
        m_submissionHelpText?.gameObject.SetActive(true);
        SetText(m_submissionStatusText, string.Empty);
        m_submissionStatusText?.gameObject.SetActive(false);
        SetText(m_submitButtonText, "SUBMIT SCORE");
        ClearRankingRows();
        SetText(m_rankingText, "LOADING RANKING");
        if (m_submitButton != null)
        {
            m_submitButton.interactable = false;
        }

        StartCoroutine(ArmSubmissionAfterPointerRelease());
        StartCoroutine(RefreshLeaderboard());
    }

    private IEnumerator ArmSubmissionAfterPointerRelease()
    {
        yield return null;
        while (Pointer.current != null && Pointer.current.press.isPressed)
        {
            yield return null;
        }
        yield return null;

        if (m_submissionStarted || m_result == null || string.IsNullOrWhiteSpace(m_leaderboardUrl))
        {
            yield break;
        }

        m_submissionArmed = true;
        if (m_submitButton != null)
        {
            m_submitButton.interactable = true;
        }
    }

    private void SubmitScore()
    {
        if (!m_submissionArmed || m_submissionStarted || m_result == null || m_nicknameInput == null)
        {
            return;
        }

        string nickname = m_nicknameInput.text;
        if (!IsValidNickname(nickname))
        {
            SetText(m_submitButtonText, "2-12 LETTERS OR NUMBERS");
            return;
        }

        m_submissionArmed = false;
        m_submissionStarted = true;
        SetInputVisible(false);
        ShowSubmissionStatus("SUBMITTING");
        SetText(m_submitButtonText, "PLEASE WAIT");
        if (m_submitButton != null)
        {
            m_submitButton.interactable = false;
        }

        StartCoroutine(SubmitAndRefresh(nickname));
    }

    private IEnumerator SubmitAndRefresh(string nickname)
    {
        var payload = new ScoreSubmission
        {
            runId = m_runId,
            nickname = nickname,
            playerClass = GetSubmissionClass(m_result.PlayerClass),
            score = Mathf.Max(0, m_result.FinalScore)
        };

        using UnityWebRequest post = CreateJsonRequest(
            $"{m_leaderboardUrl.TrimEnd('/')}/v1/scores",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(payload));
        yield return post.SendWebRequest();

        if (!Succeeded(post))
        {
            Debug.LogWarning($"[Leaderboard] POST failed ({post.responseCode}): {post.error}");
            ShowOfflineFallback();
            yield break;
        }

        SubmissionResponse submission = JsonUtility.FromJson<SubmissionResponse>(post.downloadHandler.text);
        if (submission == null || !submission.accepted)
        {
            Debug.LogWarning("[Leaderboard] POST returned an invalid response.");
            ShowOfflineFallback();
            yield break;
        }

        ShowSubmissionStatus($"TOP {submission.percentile}%");
        SetText(m_submitButtonText, "SCORE SUBMITTED");

        yield return RefreshLeaderboard(m_runId);
        Debug.Log($"[Leaderboard] Submitted {nickname}/{payload.score}; refreshed ranking once.");
    }

    private IEnumerator RefreshLeaderboard(string currentRunId = null)
    {
        int requestId = ++m_rankingRequestId;
        string url = $"{m_leaderboardUrl.TrimEnd('/')}/v1/leaderboard";
        if (!string.IsNullOrEmpty(currentRunId))
        {
            url += $"?currentRunId={UnityWebRequest.EscapeURL(currentRunId)}";
        }

        using UnityWebRequest get = UnityWebRequest.Get(url);
        get.timeout = 8;
        yield return get.SendWebRequest();

        if (requestId != m_rankingRequestId)
        {
            yield break;
        }

        if (!Succeeded(get))
        {
            Debug.LogWarning($"[Leaderboard] GET failed ({get.responseCode}): {get.error}");
            ClearRankingRows();
            SetText(m_rankingText, OfflineRankingText);
            yield break;
        }

        LeaderboardResponse leaderboard = JsonUtility.FromJson<LeaderboardResponse>(get.downloadHandler.text);
        if (leaderboard?.top10 == null)
        {
            Debug.LogWarning("[Leaderboard] GET returned an invalid response.");
            ClearRankingRows();
            SetText(m_rankingText, OfflineRankingText);
            yield break;
        }

        RenderRanking(leaderboard.top10);
        SetText(m_rankingText, string.Empty);
        Debug.Log($"[Leaderboard] Received {leaderboard.top10.Length} ranks.");
    }

    private void ShowOfflineFallback()
    {
        m_submissionArmed = false;
        m_rankingRequestId++;
        SetInputVisible(false);
        m_submissionHelpText?.gameObject.SetActive(false);
        ShowSubmissionStatus("LOCAL RESULT SAVED");
        SetText(m_submitButtonText, OfflineSubmissionText);
        ClearRankingRows();
        SetText(m_rankingText, OfflineRankingText);
        if (m_submitButton != null)
        {
            m_submitButton.interactable = false;
        }
    }

    private void ShowSubmissionStatus(string value)
    {
        if (m_submissionStatusText != null)
        {
            m_submissionStatusText.gameObject.SetActive(true);
            m_submissionStatusText.text = value;
        }
    }

    private void SetInputVisible(bool visible)
    {
        if (m_nicknameInput != null)
        {
            m_nicknameInput.interactable = visible;
            m_nicknameInput.gameObject.SetActive(visible);
        }
    }

    private static UnityWebRequest CreateJsonRequest(string url, string method, string json)
    {
        var request = new UnityWebRequest(url, method)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 8
        };
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static bool Succeeded(UnityWebRequest request)
    {
        return request.result == UnityWebRequest.Result.Success
            && request.responseCode >= 200
            && request.responseCode < 300;
    }

    private static char ValidateNicknameCharacter(string _, int index, char character)
    {
        bool isAsciiLetter = character >= 'A' && character <= 'Z'
            || character >= 'a' && character <= 'z';
        bool isDigit = character >= '0' && character <= '9';
        return index < 12 && (isAsciiLetter || isDigit) ? character : '\0';
    }

    private static bool IsValidNickname(string nickname)
    {
        if (nickname == null || nickname.Length < 2 || nickname.Length > 12)
        {
            return false;
        }

        foreach (char character in nickname)
        {
            if (ValidateNicknameCharacter(string.Empty, 0, character) == '\0')
            {
                return false;
            }
        }

        return true;
    }

    private void RenderRanking(ScoreEntry[] entries)
    {
        ClearRankingRows();
        int count = entries == null ? 0 : Mathf.Min(MaxRankingEntries, entries.Length);
        for (int index = 0; index < count; index++)
        {
            ScoreEntry entry = entries[index];
            if (entry == null)
            {
                continue;
            }

            int rank = entry.rank > 0 ? entry.rank : index + 1;
            SetText(GetRankingText(m_rankingRankTexts, index), $"#{rank:00}");
            SetText(GetRankingText(m_rankingNicknameTexts, index), entry.nickname ?? string.Empty);
            SetText(GetRankingText(m_rankingClassTexts, index), entry.playerClass ?? "UNKNOWN");
            SetText(GetRankingText(m_rankingScoreTexts, index), Mathf.Max(0, entry.score).ToString("000,000"));
            SetRankingRowColor(index, entry.isCurrent);
        }
    }

    private void ClearRankingRows()
    {
        for (int index = 0; index < MaxRankingEntries; index++)
        {
            SetText(GetRankingText(m_rankingRankTexts, index), string.Empty);
            SetText(GetRankingText(m_rankingNicknameTexts, index), string.Empty);
            SetText(GetRankingText(m_rankingClassTexts, index), string.Empty);
            SetText(GetRankingText(m_rankingScoreTexts, index), string.Empty);
            SetRankingRowColor(index, false);
        }
    }

    private void PrepareResultUi()
    {
        m_totalKillsLabel = FindComponent<TMP_Text>("RunLabel_1");
        m_headshotKillsLabel = FindComponent<TMP_Text>("RunLabel_3");
        m_loadoutHeader = FindComponent<TMP_Text>("WeaponHeader");
        for (int index = 0; index < 3; index++)
        {
            m_loadoutSlotTexts[index] = FindComponent<TMP_Text>($"WeaponSlot_{index}");
            m_loadoutNameTexts[index] = FindComponent<TMP_Text>($"WeaponName_{index}");
            m_loadoutIcons[index] = FindComponent<Image>($"WeaponIcon_{index}");
        }
        m_pistolSilhouette = m_loadoutIcons[0]?.sprite;
        m_shotgunSilhouette = m_loadoutIcons[1]?.sprite;
        m_rifleSilhouette = m_loadoutIcons[2]?.sprite;

        m_rankingClassTexts = new TMP_Text[MaxRankingEntries];
        for (int index = 0; index < MaxRankingEntries; index++)
        {
            TMP_Text rankText = GetRankingText(m_rankingRankTexts, index);
            TMP_Text nicknameText = GetRankingText(m_rankingNicknameTexts, index);
            TMP_Text scoreText = GetRankingText(m_rankingScoreTexts, index);
            if (nicknameText == null)
            {
                continue;
            }

            RectTransform nicknameRect = nicknameText.rectTransform;
            m_rankingRowBackgrounds[index] = nicknameText.transform.parent?.GetComponent<Image>();
            if (m_rankingRowBackgrounds[index] != null)
            {
                m_rankingRowBackgrounds[index].raycastTarget = false;
                m_rankingRowBackgrounds[index].type = Image.Type.Sliced;
                m_rankingRowBackgrounds[index].color = Color.white;
            }
            nicknameRect.sizeDelta = new Vector2(150f, nicknameRect.sizeDelta.y);
            TMP_Text classText = Instantiate(nicknameText, nicknameText.transform.parent);
            classText.name = $"Class_{index + 1:00}";
            classText.rectTransform.anchoredPosition = new Vector2(210f, nicknameRect.anchoredPosition.y);
            classText.rectTransform.sizeDelta = new Vector2(135f, nicknameRect.sizeDelta.y);
            m_rankingClassTexts[index] = classText;

            m_rankColors[index] = rankText != null ? rankText.color : Color.white;
            m_nicknameColors[index] = nicknameText.color;
            m_classColors[index] = classText.color;
            m_scoreColors[index] = scoreText != null ? scoreText.color : Color.white;
        }
    }

    private static T FindComponent<T>(string objectName) where T : Component
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private void SetRankingRowColor(int index, bool highlighted)
    {
        Color accent = new Color32(0, 229, 255, 255);
        SetColor(GetRankingText(m_rankingRankTexts, index), highlighted ? accent : m_rankColors[index]);
        SetColor(GetRankingText(m_rankingNicknameTexts, index), highlighted ? accent : m_nicknameColors[index]);
        SetColor(GetRankingText(m_rankingClassTexts, index), highlighted ? accent : m_classColors[index]);
        SetColor(GetRankingText(m_rankingScoreTexts, index), highlighted ? accent : m_scoreColors[index]);
        Image rowBackground = index >= 0 && index < m_rankingRowBackgrounds.Length
            ? m_rankingRowBackgrounds[index]
            : null;
        if (rowBackground != null)
        {
            Sprite stateSprite = highlighted ? m_rankingRowPlayerSprite : m_rankingRowNormalSprite;
            if (stateSprite != null)
            {
                rowBackground.sprite = stateSprite;
                rowBackground.type = Image.Type.Sliced;
                rowBackground.color = Color.white;
            }
        }
    }

    private static void SetColor(TMP_Text target, Color color)
    {
        if (target != null)
        {
            target.color = color;
        }
    }

    private static TMP_Text GetRankingText(TMP_Text[] texts, int index)
    {
        return texts != null && index >= 0 && index < texts.Length ? texts[index] : null;
    }

    public void Retry()
    {
        if (m_isLoading)
        {
            return;
        }

        m_isLoading = true;
        if (m_retryButton != null)
        {
            m_retryButton.interactable = false;
        }

        RunResultStore.Clear();
        SceneManager.LoadScene("StartScene");
    }

    private void Render(RunResultSnapshot result)
    {
        int personalBest = result?.PersonalBest ?? RunResultStore.PersonalBest;
        SetText(m_finalScoreText, FormatScore(result?.FinalScore ?? 0));
        SetText(
            m_personalBestText,
            result != null && result.IsNewPersonalBest
                ? $"NEW PERSONAL BEST\n{FormatScore(personalBest)}"
                : $"PERSONAL BEST\n{FormatScore(personalBest)}");

        if (m_runValues != null && m_runValues.Length >= 6)
        {
            SetText(m_runValues[0], FormatTime(result?.SurvivalTime ?? 0f));
            SetText(m_totalKillsLabel, "TOTAL KILLS");
            SetText(m_runValues[1], $"{result?.TotalKills ?? 0}");
            SetText(m_headshotKillsLabel, "HEADSHOT / CHAIN");
            SetText(m_runValues[3], $"{result?.HeadshotKills ?? 0} / {result?.ChainKills ?? 0}");
            int combo = result?.MaxComboCount ?? 0;
            SetText(m_runValues[5], $"x{combo}");
        }

        RenderLoadoutKills(result);

        SetText(m_deathCauseText, GetDeathCauseLabel(result?.DeathCause ?? PlayerDeathCause.Unknown));
    }

    private void RenderLoadoutKills(RunResultSnapshot result)
    {
        PlayerClassId playerClass = result?.PlayerClass ?? PlayerClassId.Unknown;
        WeaponId primary = RunResultStore.GetPrimaryWeapon(playerClass);
        SetText(m_loadoutHeader, "LOADOUT KILLS");
        SetLoadoutRow(0, "1", GetWeaponName(primary), GetWeaponKills(result, primary),
            GetWeaponSilhouette(primary), GetWeaponColor(primary), GetWeaponIconSize(primary));
        SetLoadoutRow(1, "2", "PISTOL", result?.PistolKills ?? 0,
            m_pistolSilhouette, GetWeaponColor(WeaponId.Pistol), GetWeaponIconSize(WeaponId.Pistol));
        SetLoadoutRow(2, "F", GetSkillName(playerClass), result?.SkillKills ?? 0,
            GetSkillSilhouette(playerClass), GetClassColor(playerClass), new Vector2(56f, 48f));
    }

    private void SetLoadoutRow(
        int index, string slot, string label, int kills, Sprite sprite, Color color, Vector2 iconSize)
    {
        SetText(m_loadoutSlotTexts[index], slot);
        SetText(m_loadoutNameTexts[index], label);
        SetText(m_weaponKillValues[index], Mathf.Max(0, kills).ToString());
        SetColor(m_loadoutSlotTexts[index], color);
        SetColor(m_loadoutNameTexts[index], color);
        SetColor(m_weaponKillValues[index], color);
        if (m_loadoutIcons[index] != null)
        {
            m_loadoutIcons[index].sprite = sprite;
            m_loadoutIcons[index].preserveAspect = true;
            m_loadoutIcons[index].rectTransform.sizeDelta = iconSize;
            m_loadoutIcons[index].color = color;
            m_loadoutIcons[index].enabled = sprite != null;
        }
    }

    private static string GetWeaponName(WeaponId weapon)
    {
        return weapon == WeaponId.Unknown ? "PRIMARY" : weapon.ToString().ToUpperInvariant();
    }

    private static string GetSkillName(PlayerClassId playerClass)
    {
        return playerClass switch
        {
            PlayerClassId.Grenadier => "GRENADE",
            PlayerClassId.Engineer => "ROCKET",
            PlayerClassId.Sniper => "BULLET TIME",
            _ => "SKILL"
        };
    }

    private static int GetWeaponKills(RunResultSnapshot result, WeaponId weapon)
    {
        if (result == null)
        {
            return 0;
        }
        return weapon switch
        {
            WeaponId.Pistol => result.PistolKills,
            WeaponId.Shotgun => result.ShotgunKills,
            WeaponId.Rifle => result.RifleKills,
            WeaponId.DMR => result.DmrKills,
            _ => 0
        };
    }

    private Sprite GetWeaponSilhouette(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => m_pistolSilhouette,
            WeaponId.Shotgun => m_shotgunSilhouette,
            WeaponId.Rifle => m_rifleSilhouette,
            WeaponId.DMR => m_dmrSilhouette,
            _ => null
        };
    }

    private Sprite GetSkillSilhouette(PlayerClassId playerClass)
    {
        return playerClass switch
        {
            PlayerClassId.Grenadier => m_grenadeSkillSilhouette,
            PlayerClassId.Engineer => m_rocketSkillSilhouette,
            PlayerClassId.Sniper => m_bulletTimeSkillSilhouette,
            _ => null
        };
    }

    private static Vector2 GetWeaponIconSize(WeaponId weapon)
    {
        return weapon is WeaponId.Rifle or WeaponId.Shotgun
            ? new Vector2(135f, 48f)
            : new Vector2(56f, 48f);
    }

    private static Color GetClassColor(PlayerClassId playerClass)
    {
        return playerClass switch
        {
            PlayerClassId.Grenadier => new Color32(234, 64, 71, 255),
            PlayerClassId.Engineer => new Color32(53, 199, 89, 255),
            PlayerClassId.Sniper => new Color32(44, 135, 232, 255),
            _ => Color.white
        };
    }

    private static Color GetWeaponColor(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => new Color32(234, 64, 71, 255),
            WeaponId.Shotgun => new Color32(53, 199, 89, 255),
            WeaponId.Rifle or WeaponId.DMR => new Color32(44, 135, 232, 255),
            _ => Color.white
        };
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string FormatScore(int score)
    {
        return Mathf.Max(0, score).ToString("000,000");
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private static string GetDeathCauseLabel(PlayerDeathCause cause)
    {
        return cause switch
        {
            PlayerDeathCause.SuicideBacteriophage => "SUICIDE BACTERIOPHAGE",
            PlayerDeathCause.MeleeHumanoid => "MELEE HUMANOID",
            PlayerDeathCause.RangedHumanoid => "RANGED HUMANOID",
            PlayerDeathCause.GrenadeSelfDamage => "GRENADE SELF-DAMAGE",
            PlayerDeathCause.RocketSelfDamage => "ROCKET SELF-DAMAGE",
            _ => "UNKNOWN HOSTILE"
        };
    }

    private static string GetSubmissionClass(PlayerClassId playerClass)
    {
        return playerClass == PlayerClassId.Unknown
            ? null
            : RunResultStore.GetPlayerClassName(playerClass);
    }

    [Serializable]
    private sealed class ScoreSubmission
    {
        public string runId;
        public string nickname;
        public string playerClass;
        public int score;
    }

    [Serializable]
    private sealed class SubmissionResponse
    {
        public bool accepted;
        public int percentile;
    }

    [Serializable]
    private sealed class LeaderboardResponse
    {
        public ScoreEntry[] top10;
    }

    [Serializable]
    private sealed class ScoreEntry
    {
        public int rank;
        public string nickname;
        public string playerClass;
        public int score;
        public bool isCurrent;
    }

    [ContextMenu("Run Result Formatting Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(FormatScore(48750) == "048,750");
        Debug.Assert(FormatTime(462f) == "07:42");
        Debug.Assert(GetDeathCauseLabel(PlayerDeathCause.RangedHumanoid) == "RANGED HUMANOID");
        Debug.Assert(GetDeathCauseLabel(PlayerDeathCause.GrenadeSelfDamage) == "GRENADE SELF-DAMAGE");
        Debug.Assert(GetDeathCauseLabel(PlayerDeathCause.RocketSelfDamage) == "ROCKET SELF-DAMAGE");
        Debug.Assert(IsValidNickname("Player"));
        Debug.Assert(!IsValidNickname("P"));
        Debug.Assert(!IsValidNickname("Player!"));
        Debug.Assert(GetSubmissionClass(PlayerClassId.Engineer) == "ENGINEER");
        Debug.Assert(GetSubmissionClass(PlayerClassId.Unknown) == null);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Grenadier) == WeaponId.Rifle);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Engineer) == WeaponId.Shotgun);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Sniper) == WeaponId.DMR);
        Debug.Assert(MaxRankingEntries == 10);
    }
}
