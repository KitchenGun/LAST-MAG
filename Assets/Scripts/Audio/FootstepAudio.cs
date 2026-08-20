using UnityEngine;
using UnityEngine.AI;

public sealed class FootstepAudio : MonoBehaviour
{
    private const float k_EnemyMaxDistance = 10f;
    private const int k_MaxRegularFootstepsPerSecond = 8;
    private const int k_MaxPriorityFootstepsPerSecond = 4;

    [SerializeField] private AudioSource m_audioSource;
    [SerializeField] private AudioClip[] m_clips;
    [SerializeField, Range(0f, 1f)] private float m_volume = 0.5f;
    [SerializeField] private NavMeshAgent m_movementSource;
    [SerializeField] private bool m_useMovementDrivenPlayback;
    [SerializeField] private bool m_isPriorityFootstep;
    [SerializeField, Min(0.1f)] private float m_stepInterval = 0.4f;

    private static float s_BudgetWindowStartedAt = float.NegativeInfinity;
    private static int s_RegularFootstepsInWindow;
    private static int s_PriorityFootstepsInWindow;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static int DiagnosticAcceptedCount { get; private set; }
    internal static int DiagnosticDistanceRejectedCount { get; private set; }
    internal static int DiagnosticBudgetRejectedCount { get; private set; }
#endif

    private int m_lastClipIndex = -1;
    private float m_stepTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_BudgetWindowStartedAt = float.NegativeInfinity;
        s_RegularFootstepsInWindow = 0;
        s_PriorityFootstepsInWindow = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ResetDiagnostics();
#endif
    }

    private void Awake()
    {
        if (m_audioSource == null)
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (m_useMovementDrivenPlayback)
        {
            m_stepTimer = Random.Range(0f, m_stepInterval);
        }
    }

    public void PlayFootstep()
    {
        if (m_useMovementDrivenPlayback)
        {
            return;
        }

        PlaySelectedFootstep();
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
            return;
        }

        m_stepTimer -= Time.deltaTime;
        if (m_stepTimer > 0f)
        {
            return;
        }

        PlaySelectedFootstep();
        m_stepTimer = m_stepInterval;
    }

    private void PlaySelectedFootstep()
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

        if (m_movementSource != null)
        {
            float maxDistance = Mathf.Min(k_EnemyMaxDistance, m_audioSource.maxDistance);
            if (!SpatialAudio.IsAudible(transform.position, maxDistance))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DiagnosticDistanceRejectedCount++;
#endif
                return;
            }
            if (!TryReserveFootstep(m_isPriorityFootstep, Time.time))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DiagnosticBudgetRejectedCount++;
#endif
                return;
            }

            m_lastClipIndex = selectedIndex;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticAcceptedCount++;
#endif
            float enemyVolume = m_volume * m_audioSource.volume;
            SpatialAudio.PlayOneShot(m_clips[selectedIndex], transform.position, maxDistance, enemyVolume,
                m_isPriorityFootstep ? SpatialAudio.CuePriority.Important : SpatialAudio.CuePriority.Ambient);
            return;
        }

        m_lastClipIndex = selectedIndex;
        if (m_audioSource.spatialBlend <= 0f)
        {
            m_audioSource.PlayOneShot(m_clips[selectedIndex], m_volume);
            return;
        }

        float spatialVolume = m_volume * m_audioSource.volume;
        SpatialAudio.PlayOneShot(m_clips[selectedIndex], transform.position,
            m_audioSource.maxDistance, spatialVolume);
    }

    private static bool TryReserveFootstep(bool isPriority, float now)
    {
        if (now < s_BudgetWindowStartedAt || now >= s_BudgetWindowStartedAt + 1f)
        {
            s_BudgetWindowStartedAt = now;
            s_RegularFootstepsInWindow = 0;
            s_PriorityFootstepsInWindow = 0;
        }

        if (isPriority)
        {
            if (s_PriorityFootstepsInWindow >= k_MaxPriorityFootstepsPerSecond)
            {
                return false;
            }
            s_PriorityFootstepsInWindow++;
            return true;
        }

        if (s_RegularFootstepsInWindow >= k_MaxRegularFootstepsPerSecond)
        {
            return false;
        }
        s_RegularFootstepsInWindow++;
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void ResetDiagnostics()
    {
        DiagnosticAcceptedCount = 0;
        DiagnosticDistanceRejectedCount = 0;
        DiagnosticBudgetRejectedCount = 0;
    }
#endif

    [ContextMenu("Run Footstep Audio Self Check")]
    private void RunSelfCheck()
    {
        if (m_clips != null && m_clips.Length >= 2)
        {
            int previousIndex = m_lastClipIndex;
            m_lastClipIndex = 0;
            Debug.Assert(SelectClipIndex(0) != 0, "Footsteps must not immediately repeat a clip.");
            m_lastClipIndex = previousIndex;
        }

        float previousWindow = s_BudgetWindowStartedAt;
        int previousRegularCount = s_RegularFootstepsInWindow;
        int previousPriorityCount = s_PriorityFootstepsInWindow;
        s_BudgetWindowStartedAt = float.NegativeInfinity;
        for (int index = 0; index < k_MaxRegularFootstepsPerSecond; index++)
        {
            Debug.Assert(TryReserveFootstep(false, 0f));
        }
        Debug.Assert(!TryReserveFootstep(false, 0f));
        for (int index = 0; index < k_MaxPriorityFootstepsPerSecond; index++)
        {
            Debug.Assert(TryReserveFootstep(true, 0f));
        }
        Debug.Assert(!TryReserveFootstep(true, 0f));
        Debug.Assert(TryReserveFootstep(false, 1f), "Footstep budget must reset after one second.");
        s_BudgetWindowStartedAt = previousWindow;
        s_RegularFootstepsInWindow = previousRegularCount;
        s_PriorityFootstepsInWindow = previousPriorityCount;

        Debug.Assert(SpatialAudio.IsWithinDistance(Vector3.zero, new Vector3(10f, 0f, 0f), 10f));
        Debug.Assert(!SpatialAudio.IsWithinDistance(Vector3.zero, new Vector3(10.01f, 0f, 0f), 10f));
        Debug.Assert(!SpatialAudio.CanReplace(SpatialAudio.CuePriority.Gameplay, SpatialAudio.CuePriority.Ambient));
        Debug.Assert(!SpatialAudio.CanReplace(SpatialAudio.CuePriority.Important, SpatialAudio.CuePriority.Important));
        Debug.Assert(SpatialAudio.CanReplace(SpatialAudio.CuePriority.Ambient, SpatialAudio.CuePriority.Important));
        Debug.Assert(SpatialAudio.CanReplace(SpatialAudio.CuePriority.Important, SpatialAudio.CuePriority.Gameplay));
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
