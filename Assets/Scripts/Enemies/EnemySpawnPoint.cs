using UnityEngine;
using UnityEngine.AI;

public sealed class EnemySpawnPoint : MonoBehaviour
{
    private const float k_SampleRadius = 2f;

    public bool TryGetNavMeshPosition(out Vector3 position)
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, k_SampleRadius, NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        position = default;
        return false;
    }

    private void OnDrawGizmos()
    {
        bool isValid = TryGetNavMeshPosition(out Vector3 navMeshPosition);
        Gizmos.color = isValid ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
        Gizmos.DrawLine(transform.position, isValid ? navMeshPosition : transform.position + Vector3.down * k_SampleRadius);
    }
}
