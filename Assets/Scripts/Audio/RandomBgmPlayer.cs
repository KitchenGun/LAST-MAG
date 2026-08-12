using System.Collections;
using UnityEngine;

public sealed class RandomBgmPlayer : MonoBehaviour
{
    private const float k_MinPlaybackPitch = 0.01f;

    [SerializeField] private AudioClip[] m_clips;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.45f;

    private AudioSource m_audioSource;
    private int m_lastClipIndex = -1;

    private void Awake()
    {
        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.playOnAwake = false;
        m_audioSource.loop = false;
        m_audioSource.spatialBlend = 0f;
        m_audioSource.volume = m_volume;
    }

    private void Start()
    {
        StartCoroutine(PlayRandomLoop());
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
}
