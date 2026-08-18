using System.Collections;
using TMPro;
using UnityEngine;

public sealed class RandomBgmPlayer : MonoBehaviour
{
    private const float k_MinPlaybackPitch = 0.01f;
    private const float k_IconEnterDuration = 0.2f;
    private const float k_TitleAnimationDuration = 0.35f;
    private const float k_TitleHoldDuration = 2f;
    private const float k_IconExitDelay = 0.5f;
    private const float k_IconExitDuration = 0.2f;

    [Header("Playlist")]
    [SerializeField] private AudioClip[] m_clips;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.45f;
    [SerializeField] private FacilityLockdownAnnouncement m_facilityLockdownAnnouncement;
    [Header("Now Playing UI")]
    [SerializeField] private GameObject m_nowPlayingRoot;
    [SerializeField] private RectTransform m_nowPlayingIcon;
    [SerializeField] private TextMeshProUGUI m_nowPlayingTitle;

    private AudioSource m_audioSource;
    private int m_lastClipIndex = -1;
    private Coroutine m_nowPlayingCoroutine;
    private Vector3 m_nowPlayingIconRestScale = Vector3.one;

    private void Awake()
    {
        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.playOnAwake = false;
        m_audioSource.loop = false;
        m_audioSource.spatialBlend = 0f;
        m_audioSource.volume = m_volume;

        if (m_nowPlayingRoot != null)
        {
            if (m_nowPlayingIcon != null)
            {
                m_nowPlayingIconRestScale = m_nowPlayingIcon.localScale;
            }

            m_nowPlayingRoot.SetActive(false);
        }
    }

    private IEnumerator Start()
    {
        if (m_facilityLockdownAnnouncement != null)
        {
            yield return new WaitForSecondsRealtime(m_facilityLockdownAnnouncement.SubtitleEndDelay);
        }

        yield return PlayRandomLoop();
    }

    private void Update()
    {
        m_audioSource.pitch = Mathf.Max(k_MinPlaybackPitch, Time.timeScale);
    }

    private IEnumerator PlayRandomLoop()
    {
        while (TrySelectClip(out AudioClip clip, out int clipIndex))
        {
            m_lastClipIndex = clipIndex;
            m_audioSource.clip = clip;
            m_audioSource.Play();
            ShowNowPlaying(clip.name);

            while (m_audioSource.isPlaying)
            {
                yield return null;
            }
        }
    }

    private bool TrySelectClip(out AudioClip clip, out int clipIndex)
    {
        clip = null;
        clipIndex = -1;
        if (m_clips == null || m_clips.Length == 0)
        {
            return false;
        }

        int playableCount = 0;
        for (int index = 0; index < m_clips.Length; index++)
        {
            playableCount += m_clips[index] != null ? 1 : 0;
        }

        if (playableCount == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, m_clips.Length);
        for (int offset = 0; offset < m_clips.Length; offset++)
        {
            int candidateIndex = (startIndex + offset) % m_clips.Length;
            if (m_clips[candidateIndex] != null && (playableCount == 1 || candidateIndex != m_lastClipIndex))
            {
                clip = m_clips[candidateIndex];
                clipIndex = candidateIndex;
                return true;
            }
        }

        return false;
    }

    private void ShowNowPlaying(string trackTitle)
    {
        if (m_nowPlayingRoot == null || m_nowPlayingIcon == null || m_nowPlayingTitle == null)
        {
            Debug.LogWarning("RandomBgmPlayer now-playing UI references are not assigned.", this);
            return;
        }

        if (m_nowPlayingCoroutine != null)
        {
            StopCoroutine(m_nowPlayingCoroutine);
        }

        m_nowPlayingRoot.SetActive(true);
        m_nowPlayingTitle.text = trackTitle;
        m_nowPlayingTitle.ForceMeshUpdate();
        m_nowPlayingTitle.maxVisibleCharacters = 0;
        m_nowPlayingIcon.localScale = Vector3.zero;
        m_nowPlayingCoroutine = StartCoroutine(AnimateNowPlaying(m_nowPlayingTitle.textInfo.characterCount));
    }

    private IEnumerator AnimateNowPlaying(int characterCount)
    {
        float elapsed = 0f;
        while (elapsed < k_IconEnterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_nowPlayingIcon.localScale = m_nowPlayingIconRestScale * Mathf.Clamp01(elapsed / k_IconEnterDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < k_TitleAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_nowPlayingTitle.maxVisibleCharacters = Mathf.CeilToInt(characterCount * Mathf.Clamp01(elapsed / k_TitleAnimationDuration));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(k_TitleHoldDuration);

        elapsed = 0f;
        while (elapsed < k_TitleAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_nowPlayingTitle.maxVisibleCharacters = Mathf.FloorToInt(characterCount * (1f - Mathf.Clamp01(elapsed / k_TitleAnimationDuration)));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(k_IconExitDelay);

        elapsed = 0f;
        while (elapsed < k_IconExitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_nowPlayingIcon.localScale = m_nowPlayingIconRestScale * (1f - Mathf.Clamp01(elapsed / k_IconExitDuration));
            yield return null;
        }

        m_nowPlayingRoot.SetActive(false);
        m_nowPlayingCoroutine = null;
    }

    private void OnValidate()
    {
        m_volume = Mathf.Clamp01(m_volume);
    }
}
