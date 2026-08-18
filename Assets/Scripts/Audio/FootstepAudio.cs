using UnityEngine;
using UnityEngine.AI;

public sealed class FootstepAudio : MonoBehaviour
{
    [SerializeField] private AudioSource m_audioSource;
    [SerializeField] private AudioClip[] m_clips;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.5f;
    [SerializeField] private NavMeshAgent m_movementSource;
    [SerializeField] private bool m_useMovementDrivenPlayback;
    [SerializeField, Min(0.1f)] private float m_stepInterval = 0.4f;

    private int m_lastClipIndex = -1;
    private float m_stepTimer;

    private void Awake()
    {
        if (m_audioSource == null)
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }

    public void PlayFootstep()
    {
        if (m_useMovementDrivenPlayback)
        {
            return;
        }

        PlaySelectedFootstep(false);
    }

    private void Update()
    {
        if (!m_useMovementDrivenPlayback || m_movementSource == null)
        {
            return;
        }

        if (!m_movementSource.isActiveAndEnabled || !m_movementSource.isOnNavMesh
            || m_movementSource.velocity.sqrMagnitude <= 0.01f)
        {
            m_stepTimer = 0f;
            return;
        }

        m_stepTimer -= Time.deltaTime;
        if (m_stepTimer > 0f)
        {
            return;
        }

        PlaySelectedFootstep(true);
        m_stepTimer = m_stepInterval;
    }

    private void PlaySelectedFootstep(bool useAttachedSource)
    {
        if (m_audioSource == null)
        {
            return;
        }

        int selectedIndex = SelectClipIndex(Random.Range(0, m_clips != null ? m_clips.Length : 0));
        if (selectedIndex < 0)
        {
            return;
        }

        m_lastClipIndex = selectedIndex;
        if (useAttachedSource || m_audioSource.spatialBlend <= 0f)
        {
            m_audioSource.PlayOneShot(m_clips[selectedIndex], m_volume);
            return;
        }

        float volume = m_volume * m_audioSource.volume;
        SpatialAudio.PlayOneShot(m_clips[selectedIndex], transform.position,
            m_audioSource.maxDistance, volume);
    }

    [ContextMenu("Run Footstep Audio Self Check")]
    private void RunSelfCheck()
    {
        if (m_clips == null || m_clips.Length < 2)
        {
            return;
        }

        int previousIndex = m_lastClipIndex;
        m_lastClipIndex = 0;
        Debug.Assert(SelectClipIndex(0) != 0, "Footsteps must not immediately repeat a clip.");
        m_lastClipIndex = previousIndex;
    }

    private int SelectClipIndex(int startIndex)
    {
        if (m_clips == null || m_clips.Length == 0)
        {
            return -1;
        }

        int playableCount = 0;
        for (int index = 0; index < m_clips.Length; index++)
        {
            playableCount += m_clips[index] != null ? 1 : 0;
        }

        for (int offset = 0; offset < m_clips.Length; offset++)
        {
            int candidateIndex = (startIndex + offset) % m_clips.Length;
            if (m_clips[candidateIndex] != null && (playableCount == 1 || candidateIndex != m_lastClipIndex))
            {
                return candidateIndex;
            }
        }

        return -1;
    }
}
