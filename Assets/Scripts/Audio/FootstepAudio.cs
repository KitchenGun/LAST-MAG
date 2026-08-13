using UnityEngine;
using UnityEngine.AI;

public sealed class FootstepAudio : MonoBehaviour
{
    [SerializeField] private AudioSource m_audioSource;
    [SerializeField] private AudioClip[] m_clips;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.5f;
    [SerializeField] private NavMeshAgent m_movementSource;

    private int m_lastClipIndex = -1;

    private void Awake()
    {
        if (m_audioSource == null)
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }

    public void PlayFootstep()
    {
        if (m_audioSource == null || !CanPlayWhileMoving())
        {
            return;
        }

        int selectedIndex = SelectClipIndex(Random.Range(0, m_clips != null ? m_clips.Length : 0));
        if (selectedIndex < 0)
        {
            return;
        }

        m_lastClipIndex = selectedIndex;
        SpatialAudio.PlayOneShot(m_clips[selectedIndex], transform.position,
            m_audioSource.maxDistance, m_volume * m_audioSource.volume);
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

    private bool CanPlayWhileMoving()
    {
        if (m_movementSource == null)
        {
            return true;
        }

        return m_movementSource.isActiveAndEnabled
            && m_movementSource.isOnNavMesh
            && !m_movementSource.isStopped
            && m_movementSource.hasPath
            && m_movementSource.remainingDistance > m_movementSource.stoppingDistance;
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
