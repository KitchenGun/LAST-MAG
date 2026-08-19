using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(GameplayObjectPool))]
public sealed class EnemySpawner : MonoBehaviour
{
    private const float k_RetryInterval = 0.25f;
    private const float k_MaxRetryInterval = 1f;
    private const float k_MinPlayerDistance = 15f;

    [SerializeField] private Transform m_player;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private EnemySpawnPoint[] m_spawnPoints;
    [SerializeField] private float m_initialDelay = 3f;

    private readonly int[] m_enemyCycle = { 0, 1, 2 };
    private readonly List<int> m_candidateIndices = new();
    private readonly List<Vector3> m_candidatePositions = new();
    private readonly RaycastHit[] m_visibilityHits = new RaycastHit[32];
    private readonly Vector3[] m_pathCorners = new Vector3[64];
    private Vector3[] m_spawnNavPositions;
    private bool[] m_spawnNavPositionValid;
    private NavMeshPath m_spawnPath;
    private GameplayObjectPool m_objectPool;
    private int m_cycleIndex;
    private int m_lastSpawnPoint = -1;
    private float m_startTime;
    private float m_nextAttemptTime;
    private float m_retryInterval = k_RetryInterval;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int k_StressSampleCapacity = 12000;
    private int m_stressEnemyTarget;
    private readonly float[] m_stressFrameSamples = new float[k_StressSampleCapacity];
    private readonly float[] m_stressCpuSamples = new float[k_StressSampleCapacity];
    private readonly float[] m_stressGpuSamples = new float[k_StressSampleCapacity];
    private readonly FrameTiming[] m_frameTiming = new FrameTiming[1];
    private int m_stressFrameSampleCount;
    private int m_stressCpuSampleCount;
    private int m_stressGpuSampleCount;
    private float m_stressWarmupEndsAt;
    private float m_stressSampleStartedAt;
    private bool m_stressMeasurementStarted;
    private bool m_stressMeasurementComplete;
    internal static bool IsStressTestActive { get; private set; }
#endif

    private void Awake()
    {
        m_spawnPath = new NavMeshPath();
        if (m_player == null)
        {
            FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
            m_player = controller != null ? controller.transform : null;
        }

        if (m_playerCamera == null)
        {
            m_playerCamera = Camera.main;
        }

        m_objectPool = GetComponent<GameplayObjectPool>();
        CacheSpawnPositions();
        ShuffleCycle();
    }

    private void Start()
    {
        m_startTime = Time.time;
        m_nextAttemptTime = m_startTime + m_initialDelay;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        m_stressEnemyTarget = ParseStressTarget(Application.absoluteURL);
        IsStressTestActive = m_stressEnemyTarget > 0;
        if (m_stressEnemyTarget > 0)
        {
            m_nextAttemptTime = Time.time;
        }
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UpdateStressMeasurement();
#endif
        if (Time.time < m_nextAttemptTime || !HasRequiredReferences())
        {
            return;
        }

        float elapsedMinutes = (Time.time - m_startTime) / 60f;
        int activeTarget = EvaluateActiveTarget(elapsedMinutes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool isStressFill = m_stressEnemyTarget > 0 && m_objectPool.ActiveEnemyCount < m_stressEnemyTarget;
        if (isStressFill)
        {
            activeTarget = m_stressEnemyTarget;
        }
#else
        const bool isStressFill = false;
#endif
        if (m_objectPool.ActiveEnemyCount >= activeTarget)
        {
            m_nextAttemptTime = Time.time + k_RetryInterval;
            return;
        }

        int enemyIndex = m_enemyCycle[m_cycleIndex];
        if (!TryChooseSpawnPosition(out int pointIndex, out Vector3 spawnPosition))
        {
            ScheduleRetry();
            return;
        }

        Vector3 forward = m_player.position - spawnPosition;
        forward.y = 0f;
        Quaternion rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        m_objectPool.SpawnEnemy(enemyIndex, spawnPosition, rotation);

        m_lastSpawnPoint = pointIndex;
        AdvanceCycle();
        m_retryInterval = k_RetryInterval;
        m_nextAttemptTime = isStressFill ? Time.time : Time.time + EvaluateSpawnInterval(elapsedMinutes);
    }

    private bool TryChooseSpawnPosition(out int pointIndex, out Vector3 spawnPosition)
    {
        pointIndex = -1;
        spawnPosition = default;
        m_candidateIndices.Clear();
        m_candidatePositions.Clear();

        if (!NavMesh.SamplePosition(m_player.position, out NavMeshHit playerHit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        for (int index = 0; index < m_spawnPoints.Length; index++)
        {
            EnemySpawnPoint point = m_spawnPoints[index];
            if (index == m_lastSpawnPoint || point == null || !m_spawnNavPositionValid[index])
            {
                continue;
            }
            Vector3 candidate = m_spawnNavPositions[index];

            if (Vector3.Distance(candidate, playerHit.position) < k_MinPlayerDistance
                || !HasLongCompletePath(candidate, playerHit.position)
                || !IsHiddenFromCamera(candidate))
            {
                continue;
            }

            m_candidateIndices.Add(index);
            m_candidatePositions.Add(candidate);
        }

        if (m_candidateIndices.Count == 0)
        {
            return false;
        }

        int selected = UnityEngine.Random.Range(0, m_candidateIndices.Count);
        pointIndex = m_candidateIndices[selected];
        spawnPosition = m_candidatePositions[selected];
        return true;
    }

    private bool HasLongCompletePath(Vector3 start, Vector3 destination)
    {
        if (!NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, m_spawnPath)
            || m_spawnPath.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        float pathLength = 0f;
        int cornerCount = m_spawnPath.GetCornersNonAlloc(m_pathCorners);
        for (int index = 1; index < cornerCount; index++)
        {
            pathLength += Vector3.Distance(m_pathCorners[index - 1], m_pathCorners[index]);
        }
        return pathLength >= k_MinPlayerDistance;
    }

    private bool IsHiddenFromCamera(Vector3 position)
    {
        Vector3 right = m_playerCamera.transform.right * 0.55f;
        Vector3 middle = position + Vector3.up * 1.1f;
        return IsProbeHidden(position + Vector3.up * 0.25f)
            && IsProbeHidden(middle)
            && IsProbeHidden(position + Vector3.up * 2.2f)
            && IsProbeHidden(middle + right)
            && IsProbeHidden(middle - right);
    }

    private bool IsProbeHidden(Vector3 probe)
    {
        Vector3 viewport = m_playerCamera.WorldToViewportPoint(probe);
        bool isOnScreen = viewport.z > 0f
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
        return !isOnScreen || HasWorldOccluder(probe);
    }

    private void CacheSpawnPositions()
    {
        int count = m_spawnPoints != null ? m_spawnPoints.Length : 0;
        m_spawnNavPositions = new Vector3[count];
        m_spawnNavPositionValid = new bool[count];
        for (int index = 0; index < count; index++)
        {
            EnemySpawnPoint point = m_spawnPoints[index];
            m_spawnNavPositionValid[index] = point != null
                && point.TryGetNavMeshPosition(out m_spawnNavPositions[index]);
        }
    }

    private void ScheduleRetry()
    {
        m_nextAttemptTime = Time.time + m_retryInterval;
        m_retryInterval = Mathf.Min(k_MaxRetryInterval, m_retryInterval * 2f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void UpdateStressMeasurement()
    {
        if (!IsStressTestActive || m_stressMeasurementComplete
            || m_objectPool == null || m_objectPool.ActiveEnemyCount < m_stressEnemyTarget)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (!m_stressMeasurementStarted)
        {
            m_stressMeasurementStarted = true;
            m_stressWarmupEndsAt = now + 10f;
            return;
        }
        if (now < m_stressWarmupEndsAt)
        {
            return;
        }
        if (m_stressSampleStartedAt <= 0f)
        {
            m_stressSampleStartedAt = now;
        }

        // ponytail: 12000 samples cover 60 seconds at up to 200 FPS.
        if (m_stressFrameSampleCount < k_StressSampleCapacity)
        {
            m_stressFrameSamples[m_stressFrameSampleCount++] = Time.unscaledDeltaTime * 1000f;
        }
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, m_frameTiming) > 0)
        {
            FrameTiming timing = m_frameTiming[0];
            if (timing.cpuFrameTime > 0d && m_stressCpuSampleCount < k_StressSampleCapacity)
            {
                m_stressCpuSamples[m_stressCpuSampleCount++] = (float)timing.cpuFrameTime;
            }
            if (timing.gpuFrameTime > 0d && m_stressGpuSampleCount < k_StressSampleCapacity)
            {
                m_stressGpuSamples[m_stressGpuSampleCount++] = (float)timing.gpuFrameTime;
            }
        }

        float duration = now - m_stressSampleStartedAt;
        if (duration < 60f)
        {
            return;
        }

        m_stressMeasurementComplete = true;
        int over33Ms = 0;
        for (int index = 0; index < m_stressFrameSampleCount; index++)
        {
            over33Ms += m_stressFrameSamples[index] >= 33f ? 1 : 0;
        }
        float averageFps = m_stressFrameSampleCount / duration;
        Debug.Log($"[WebGL Stress] target={m_stressEnemyTarget} duration={duration:F1}s "
            + $"averageFps={averageFps:F1} frameP95Ms={CalculateP95(m_stressFrameSamples, m_stressFrameSampleCount):F2} "
            + $"cpuP95Ms={CalculateP95(m_stressCpuSamples, m_stressCpuSampleCount):F2} "
            + $"gpuP95Ms={CalculateP95(m_stressGpuSamples, m_stressGpuSampleCount):F2} over33Ms={over33Ms}");
    }

    private static float CalculateP95(float[] samples, int count)
    {
        if (count <= 0)
        {
            return -1f;
        }
        Array.Sort(samples, 0, count);
        return samples[Mathf.Clamp(Mathf.CeilToInt(count * 0.95f) - 1, 0, count - 1)];
    }

    private static int ParseStressTarget(string absoluteUrl)
    {
        if (string.IsNullOrEmpty(absoluteUrl) || !Uri.TryCreate(absoluteUrl, UriKind.Absolute, out Uri uri))
        {
            return 0;
        }

        string[] parameters = uri.Query.TrimStart('?').Split('&');
        for (int index = 0; index < parameters.Length; index++)
        {
            string[] pair = parameters[index].Split('=');
            if (pair.Length == 2 && pair[0] == "stressEnemies"
                && int.TryParse(pair[1], out int target) && (target == 48 || target == 108))
            {
                return target;
            }
        }
        return 0;
    }
#endif

    private bool HasWorldOccluder(Vector3 probe)
    {
        Vector3 origin = m_playerCamera.transform.position;
        Vector3 direction = probe - origin;
        int hitCount = Physics.RaycastNonAlloc(origin, direction.normalized, m_visibilityHits, direction.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = m_visibilityHits[index];
            if (hit.collider.GetComponentInParent<PlayerHealth>() == null
                && hit.collider.GetComponentInParent<EnemyHealth>() == null)
            {
                return true;
            }
        }
        return false;
    }

    private void AdvanceCycle()
    {
        m_cycleIndex++;
        if (m_cycleIndex < m_enemyCycle.Length)
        {
            return;
        }

        m_cycleIndex = 0;
        ShuffleCycle();
    }

    private void ShuffleCycle()
    {
        for (int index = m_enemyCycle.Length - 1; index > 0; index--)
        {
            int swapIndex = UnityEngine.Random.Range(0, index + 1);
            (m_enemyCycle[index], m_enemyCycle[swapIndex]) = (m_enemyCycle[swapIndex], m_enemyCycle[index]);
        }
    }

    private bool HasRequiredReferences()
    {
        return m_player != null && m_playerCamera != null
            && m_objectPool != null && m_objectPool.IsConfigured
            && m_spawnPoints != null && m_spawnPoints.Length > 1;
    }

    private static int EvaluateActiveTarget(float elapsedMinutes)
    {
        float value = 3f * Mathf.Pow(Mathf.Max(0f, elapsedMinutes) + 1f, 2f);
        return Mathf.FloorToInt(value);
    }

    private static float EvaluateSpawnInterval(float elapsedMinutes)
    {
        elapsedMinutes = Mathf.Max(0f, elapsedMinutes);
        if (elapsedMinutes <= 1f) return Mathf.Lerp(3f, 2.4f, elapsedMinutes);
        if (elapsedMinutes <= 3f) return Mathf.Lerp(2.4f, 1.6f, (elapsedMinutes - 1f) / 2f);
        if (elapsedMinutes <= 5f) return Mathf.Lerp(1.6f, 1.1f, (elapsedMinutes - 3f) / 2f);
        if (elapsedMinutes <= 8f) return Mathf.Lerp(0.7f, 0.5f, (elapsedMinutes - 5f) / 3f);
        // 8분 이후에도 생성 압박이 계속 증가하도록 점진적으로 간격을 줄인다.
        return 0.5f * Mathf.Exp(-(elapsedMinutes - 8f) * 0.18f);
    }

    [ContextMenu("Run Enemy Spawner Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(EvaluateActiveTarget(0f) == 3);
        Debug.Assert(EvaluateActiveTarget(1f) == 12);
        Debug.Assert(EvaluateActiveTarget(2f) == 27);
        Debug.Assert(EvaluateActiveTarget(5f) == 108);
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(0f), 3f));
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(1f), 2.4f));
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(3f), 1.6f));
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(5f), 0.7f));
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(8f), 0.5f));
        Debug.Assert(EvaluateSpawnInterval(10f) < EvaluateSpawnInterval(8f)
            && EvaluateSpawnInterval(20f) < EvaluateSpawnInterval(10f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Assert(ParseStressTarget("https://localhost/?stressEnemies=48") == 48);
        Debug.Assert(ParseStressTarget("https://localhost/?stressEnemies=108") == 108);
        Debug.Assert(ParseStressTarget("https://localhost/?stressEnemies=99") == 0);
#endif
        Debug.Assert(HasRequiredReferences());
    }

    private void OnValidate()
    {
        m_initialDelay = Mathf.Max(0f, m_initialDelay);
    }
}
