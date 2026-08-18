using System.Collections;
using TMPro;
using UnityEngine;

public sealed class FacilityLockdownAnnouncement : MonoBehaviour
{
    private const string k_AudioPath = "Audio/SystemVoice/SFX_FacilityLockdown";
    private const string k_SubtitleObjectName = "FacilityLockdownSubtitle";

    [SerializeField] private GameplayHUD m_gameplayHud;
    [Header("Subtitle Layout")]
    [SerializeField] private Vector2 m_subtitlePosition = new(0f, 110f);
    [SerializeField] private Vector2 m_subtitleSize = new(1200f, 96f);
    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.85f;
    [SerializeField] private float m_audioStartDelay = 3f;
    [Header("Subtitle Timing")]
    [SerializeField] private float m_subtitleStartTime = 0.303f;
    [SerializeField] private float m_subtitleDuration = 3f;

    private AudioSource m_audioSource;
    private TextMeshProUGUI m_subtitleText;
    private float m_startedAt;
    private bool m_hasStarted;

    internal float SubtitleEndDelay => m_audioStartDelay + m_subtitleStartTime + m_subtitleDuration;

    private IEnumerator Start()
    {
        AudioClip clip = Resources.Load<AudioClip>(k_AudioPath);
        if (clip == null)
        {
            Debug.LogWarning("Facility lockdown announcement audio is missing.", this);
            yield break;
        }

        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.playOnAwake = false;
        m_audioSource.loop = false;
        m_audioSource.spatialBlend = 0f;
        m_audioSource.volume = m_volume;
        m_audioSource.clip = clip;

        CreateSubtitle();
        yield return new WaitForSecondsRealtime(m_audioStartDelay);
        m_startedAt = Time.realtimeSinceStartup;
        m_hasStarted = true;
        m_audioSource.Play();
    }

    private void Update()
    {
        if (m_subtitleText == null || !m_hasStarted)
        {
            return;
        }

        float elapsed = Time.realtimeSinceStartup - m_startedAt;
        m_subtitleText.text = GetSubtitle(elapsed);
        m_subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(m_subtitleText.text));
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

    private void OnValidate()
    {
        m_volume = Mathf.Clamp01(m_volume);
        m_audioStartDelay = Mathf.Max(0f, m_audioStartDelay);
        m_subtitleDuration = Mathf.Max(0f, m_subtitleDuration);
    }
}
