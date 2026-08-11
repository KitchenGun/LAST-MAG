using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(GameplayObjectPool))]
public sealed class EnemySpawner : MonoBehaviour
{
    private const float k_RetryInterval = 0.25f;
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
    private NavMeshPath m_spawnPath;
    private GameplayObjectPool m_objectPool;
    private int m_cycleIndex;
    private int m_lastSpawnPoint = -1;
    private float m_startTime;
    private float m_nextAttemptTime;

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
        ShuffleCycle();
    }

    private void Start()
    {
        m_startTime = Time.time;
        m_nextAttemptTime = m_startTime + m_initialDelay;
    }

    private void Update()
    {
        if (Time.time < m_nextAttemptTime || !HasRequiredReferences())
        {
            return;
        }

        float elapsedMinutes = (Time.time - m_startTime) / 60f;
        if (m_objectPool.ActiveEnemyCount >= EvaluateActiveTarget(elapsedMinutes))
        {
            m_nextAttemptTime = Time.time + k_RetryInterval;
            return;
        }

        int enemyIndex = m_enemyCycle[m_cycleIndex];
        if (!TryChooseSpawnPosition(out int pointIndex, out Vector3 spawnPosition))
        {
            m_nextAttemptTime = Time.time + k_RetryInterval;
            return;
        }

        Vector3 forward = m_player.position - spawnPosition;
        forward.y = 0f;
        Quaternion rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        m_objectPool.SpawnEnemy(enemyIndex, spawnPosition, rotation);

        m_lastSpawnPoint = pointIndex;
        AdvanceCycle();
        m_nextAttemptTime = Time.time + EvaluateSpawnInterval(elapsedMinutes);
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
            if (index == m_lastSpawnPoint || point == null || !point.TryGetNavMeshPosition(out Vector3 candidate))
            {
                continue;
            }

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
        Vector3[] probes =
        {
            position + Vector3.up * 0.25f,
            position + Vector3.up * 1.1f,
            position + Vector3.up * 2.2f,
            position + Vector3.up * 1.1f + right,
            position + Vector3.up * 1.1f - right
        };

        foreach (Vector3 probe in probes)
        {
            Vector3 viewport = m_playerCamera.WorldToViewportPoint(probe);
            bool isOnScreen = viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
            if (isOnScreen && !HasWorldOccluder(probe))
            {
                return false;
            }
        }
        return true;
    }

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
        if (elapsedMinutes <= 8f) return Mathf.Lerp(1.1f, 0.7f, (elapsedMinutes - 5f) / 3f);
        return 0.7f;
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
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(5f), 1.1f));
        Debug.Assert(Mathf.Approximately(EvaluateSpawnInterval(8f), 0.7f));
        Debug.Assert(HasRequiredReferences());
    }

    private void OnValidate()
    {
        m_initialDelay = Mathf.Max(0f, m_initialDelay);
    }
}
