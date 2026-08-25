using TMPro;
using UnityEngine;

public sealed class FacilityLockdownAnnouncement : MonoBehaviour
{
    private const string k_AudioPath = "Audio/SystemVoice/SFX_FacilityLockdown";
    private const string k_SubtitleObjectName = "FacilityLockdownSubtitle";

    [SerializeField] private GameplayHUD m_gameplayHud;
    [SerializeField] private FirstPersonController m_player;
    [SerializeField] private TextMeshProUGUI m_countdownText;
    [Header("Subtitle Layout")]
    [SerializeField] private Vector2 m_subtitlePosition = new(0f, 110f);
    [SerializeField] private Vector2 m_subtitleSize = new(1200f, 96f);
    [Header("Audio")]
    [SerializeField] private AudioClip m_countdownTickClip;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.85f;
    [SerializeField] private float m_audioStartDelay = 3f;
    [Header("Subtitle Timing")]
    [SerializeField] private float m_subtitleStartTime = 0.303f;
    [SerializeField] private float m_subtitleDuration = 3f;

    private AudioSource m_audioSource;
    private AudioClip m_announcementClip;
    private TextMeshProUGUI m_subtitleText;
    private float m_countdownElapsed;
    private float m_startedAt;
    private int m_lastCountdownNumber;
    private bool m_countdownActive;
    private bool m_hasStarted;

    internal bool IsComplete => m_hasStarted
        && GameplayClock.Now - m_startedAt >= m_subtitleStartTime + m_subtitleDuration;

    private void Awake()
    {
        m_player?.SetStartupLocked(true);
        ShowCountdown(3);
    }

    private void Start()
    {
        m_announcementClip = Resources.Load<AudioClip>(k_AudioPath);
        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.playOnAwake = false;
        m_audioSource.loop = false;
        m_audioSource.spatialBlend = 0f;
        m_audioSource.volume = m_volume;

        CreateSubtitle();
        m_countdownActive = true;
        m_lastCountdownNumber = 3;
        PlayTick();
    }

    private void Update()
    {
        if (m_countdownActive)
        {
            UpdateCountdown();
        }

        if (m_subtitleText == null || !m_hasStarted)
        {
            return;
        }

        float elapsed = GameplayClock.Now - m_startedAt;
        m_subtitleText.text = GetSubtitle(elapsed);
        m_subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(m_subtitleText.text));
    }

    private void UpdateCountdown()
    {
        m_countdownElapsed += GameplayClock.DeltaTime;
        if (m_countdownElapsed >= m_audioStartDelay)
        {
            m_countdownActive = false;
            if (m_countdownText != null)
            {
                m_countdownText.gameObject.SetActive(false);
            }
            m_player?.SetStartupLocked(false);
            PlayAnnouncement();
            return;
        }

        int countdownNumber = GetCountdownNumber(m_countdownElapsed, m_audioStartDelay);
        if (countdownNumber != m_lastCountdownNumber)
        {
            m_lastCountdownNumber = countdownNumber;
            ShowCountdown(countdownNumber);
            PlayTick();
        }
    }

    private void ShowCountdown(int number)
    {
        if (m_countdownText == null)
        {
            return;
        }

        m_countdownText.text = number.ToString();
        m_countdownText.gameObject.SetActive(true);
    }

    private void PlayTick()
    {
        if (m_countdownTickClip != null && m_audioSource != null)
        {
            m_audioSource.PlayOneShot(m_countdownTickClip);
        }
    }

    private void PlayAnnouncement()
    {
        m_startedAt = GameplayClock.Now;
        m_hasStarted = true;
        if (m_announcementClip != null)
        {
            m_audioSource.PlayOneShot(m_announcementClip);
        }
        else
        {
            Debug.LogWarning("Facility lockdown announcement audio is missing.", this);
        }
    }

    private void CreateSubtitle()
    {
        if (m_gameplayHud == null)
        {
            Debug.LogWarning("Facility lockdown announcement is missing GameplayHUD.", this);
            return;
        }

        Transform existing = m_gameplayHud.transform.Find(k_SubtitleObjectName);
        m_subtitleText = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (m_subtitleText == null)
        {
            GameObject subtitleObject = new(k_SubtitleObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subtitleObject.transform.SetParent(m_gameplayHud.transform, false);
            m_subtitleText = subtitleObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rectTransform = m_subtitleText.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.anchoredPosition = m_subtitlePosition;
        rectTransform.sizeDelta = m_subtitleSize;
        m_subtitleText.font = TMP_Settings.defaultFontAsset;
        m_subtitleText.fontSize = 26f;
        m_subtitleText.alignment = TextAlignmentOptions.Center;
        m_subtitleText.enableAutoSizing = false;
        m_subtitleText.textWrappingMode = TextWrappingModes.Normal;
        m_subtitleText.overflowMode = TextOverflowModes.Overflow;
        m_subtitleText.color = Color.white;
        m_subtitleText.raycastTarget = false;
        m_subtitleText.gameObject.SetActive(false);
    }

    private string GetSubtitle(float elapsed)
    {
        return IsSubtitleActive(elapsed, m_subtitleStartTime, m_subtitleDuration)
            ? "CLEAR THE AREA."
            : string.Empty;
    }

    private static bool IsSubtitleActive(float elapsed, float startTime, float duration)
    {
        return elapsed >= startTime && elapsed < startTime + Mathf.Max(0f, duration);
    }

    private static int GetCountdownNumber(float elapsed, float duration)
    {
        return Mathf.Clamp(Mathf.CeilToInt(duration - elapsed), 1, 3);
    }

    private void OnDisable()
    {
        if (m_countdownActive)
        {
            m_player?.SetStartupLocked(false);
        }
        if (m_countdownText != null)
        {
            m_countdownText.gameObject.SetActive(false);
        }
    }

    [ContextMenu("Run Facility Lockdown Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(GetCountdownNumber(0f, 3f) == 3);
        Debug.Assert(GetCountdownNumber(1f, 3f) == 2);
        Debug.Assert(GetCountdownNumber(2f, 3f) == 1);
    }

    private void OnValidate()
    {
        m_volume = Mathf.Clamp01(m_volume);
        m_audioStartDelay = Mathf.Max(0f, m_audioStartDelay);
        m_subtitleDuration = Mathf.Max(0f, m_subtitleDuration);
    }
}
