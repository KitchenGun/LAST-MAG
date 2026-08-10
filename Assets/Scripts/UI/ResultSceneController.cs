using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultSceneController : MonoBehaviour
{
    private const string DefaultNickname = "Player";
    private const string OfflineSubmissionText = "ONLINE SUBMISSION\nNOT ENABLED";
    private const string OfflineRankingText = "ONLINE RANKING\nNOT ENABLED";

    [Header("Result")]
    [SerializeField] private TMP_Text m_finalScoreText;
    [SerializeField] private TMP_Text m_personalBestText;
    [SerializeField] private TMP_Text[] m_runValues;
    [SerializeField] private TMP_Text[] m_weaponKillValues;
    [SerializeField] private TMP_Text m_deathCauseText;
    [SerializeField] private Button m_retryButton;

    [Header("Online ranking")]
    [SerializeField] private string m_leaderboardUrl;
    [SerializeField] private TMP_InputField m_nicknameInput;
    [SerializeField] private Button m_submitButton;
    [SerializeField] private TMP_Text m_submitButtonText;
    [SerializeField] private TMP_Text m_submissionStatusText;
    [SerializeField] private TMP_Text m_rankingText;

    private bool m_isLoading;
    private bool m_submissionStarted;
    private string m_runId;
    private RunResultSnapshot m_result;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        m_result = RunResultStore.Current;
        Render(m_result);
        InitializeSubmission();

        if (m_retryButton != null)
        {
            m_retryButton.onClick.AddListener(Retry);
        }

        EventSystem.current?.SetSelectedGameObject(
            m_nicknameInput != null && m_nicknameInput.gameObject.activeSelf
                ? m_nicknameInput.gameObject
                : m_retryButton?.gameObject);
    }

    private void OnDestroy()
    {
        m_retryButton?.onClick.RemoveListener(Retry);
        m_submitButton?.onClick.RemoveListener(SubmitScore);
    }

    private void InitializeSubmission()
    {
        if (m_nicknameInput != null)
        {
            m_nicknameInput.characterLimit = 12;
            m_nicknameInput.onValidateInput = ValidateNicknameCharacter;
            m_nicknameInput.SetTextWithoutNotify(DefaultNickname);
        }

        m_submitButton?.onClick.AddListener(SubmitScore);
        m_runId = Guid.NewGuid().ToString("D");

        if (m_result == null || string.IsNullOrWhiteSpace(m_leaderboardUrl))
        {
            ShowOfflineFallback();
            return;
        }

        SetInputVisible(true);
        SetText(m_submissionStatusText, string.Empty);
        m_submissionStatusText?.gameObject.SetActive(false);
        SetText(m_submitButtonText, "SUBMIT SCORE");
        SetText(m_rankingText, OfflineRankingText);
        if (m_submitButton != null)
        {
            m_submitButton.interactable = true;
        }
    }

    private void SubmitScore()
    {
        if (m_submissionStarted || m_result == null || m_nicknameInput == null)
        {
            return;
        }

        string nickname = m_nicknameInput.text;
        if (!IsValidNickname(nickname))
        {
            SetText(m_submitButtonText, "2-12 LETTERS OR NUMBERS");
            return;
        }

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

        using UnityWebRequest get = UnityWebRequest.Get($"{m_leaderboardUrl.TrimEnd('/')}/v1/leaderboard");
        get.timeout = 8;
        yield return get.SendWebRequest();

        if (!Succeeded(get))
        {
            Debug.LogWarning($"[Leaderboard] GET failed ({get.responseCode}): {get.error}");
            SetText(m_rankingText, OfflineRankingText);
            yield break;
        }

        LeaderboardResponse leaderboard = JsonUtility.FromJson<LeaderboardResponse>(get.downloadHandler.text);
        if (leaderboard?.top10 == null)
        {
            Debug.LogWarning("[Leaderboard] GET returned an invalid response.");
            SetText(m_rankingText, OfflineRankingText);
            yield break;
        }

        SetText(m_rankingText, FormatRanking(leaderboard.top10));
        Debug.Log($"[Leaderboard] Submitted {nickname}/{payload.score}; refreshed {leaderboard.top10.Length} ranks once.");
    }

    private void ShowOfflineFallback()
    {
        SetInputVisible(false);
        ShowSubmissionStatus("LOCAL RESULT SAVED");
        SetText(m_submitButtonText, OfflineSubmissionText);
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

    private static string FormatRanking(ScoreEntry[] entries)
    {
        var builder = new StringBuilder();
        int count = Mathf.Min(10, entries.Length);
        for (int i = 0; i < count; i++)
        {
            ScoreEntry entry = entries[i];
            if (i > 0)
            {
                builder.AppendLine();
            }
            builder.Append('#').Append(entry.rank.ToString("00"))
                .Append("  ").Append(entry.nickname)
                .Append("  ").Append(Mathf.Max(0, entry.score).ToString("000,000"));
        }
        return builder.ToString();
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
        SceneManager.LoadScene("GameplayScene");
    }

    private void Render(RunResultSnapshot result)
    {
        int personalBest = result?.PersonalBest ?? RunResultStore.PersonalBest;
        SetText(m_finalScoreText, FormatScore(result?.FinalScore ?? 0));
        SetText(
            m_personalBestText,
            result != null && result.IsNewPersonalBest
                ? $"NEW PERSONAL BEST  {FormatScore(personalBest)}"
                : $"PERSONAL BEST  {FormatScore(personalBest)}");

        if (m_runValues != null && m_runValues.Length >= 6)
        {
            SetText(m_runValues[0], FormatTime(result?.SurvivalTime ?? 0f));
            SetText(m_runValues[1], (result?.TotalKills ?? 0).ToString());
            SetText(m_runValues[2], $"{result?.SuicideKills ?? 0} / {result?.MeleeKills ?? 0} / {result?.RangedKills ?? 0}");
            SetText(m_runValues[3], (result?.HeadshotKills ?? 0).ToString());
            SetText(m_runValues[4], (result?.ChainKills ?? 0).ToString());
            int combo = result?.MaxComboLevel ?? 0;
            SetText(m_runValues[5], $"x{combo} / x{ScoreSystem.GetComboMultiplier(combo):0.0}");
        }

        if (m_weaponKillValues != null && m_weaponKillValues.Length >= 3)
        {
            SetText(m_weaponKillValues[0], (result?.PistolKills ?? 0).ToString());
            SetText(m_weaponKillValues[1], (result?.ShotgunKills ?? 0).ToString());
            SetText(m_weaponKillValues[2], (result?.RifleKills ?? 0).ToString());
        }

        SetText(m_deathCauseText, GetDeathCauseLabel(result?.DeathCause ?? PlayerDeathCause.Unknown));
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
            _ => "UNKNOWN HOSTILE"
        };
    }

    [Serializable]
    private sealed class ScoreSubmission
    {
        public string runId;
        public string nickname;
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
        public int score;
    }

    [ContextMenu("Run Result Formatting Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(FormatScore(48750) == "048,750");
        Debug.Assert(FormatTime(462f) == "07:42");
        Debug.Assert(GetDeathCauseLabel(PlayerDeathCause.RangedHumanoid) == "RANGED HUMANOID");
        Debug.Assert(IsValidNickname("Player"));
        Debug.Assert(!IsValidNickname("P"));
        Debug.Assert(!IsValidNickname("Player!"));
    }
}
