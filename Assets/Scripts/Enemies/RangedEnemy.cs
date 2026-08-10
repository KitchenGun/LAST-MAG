using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent))]
public sealed class RangedEnemy : MonoBehaviour
{
    private static readonly int s_IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int s_Attack = Animator.StringToHash("Attack");
    private static readonly Vector3[] s_SearchDirections =
    {
        Vector3.forward, Vector3.right, Vector3.back, Vector3.left,
        new(0.707f, 0f, 0.707f), new(0.707f, 0f, -0.707f),
        new(-0.707f, 0f, -0.707f), new(-0.707f, 0f, 0.707f)
    };

    [SerializeField] private float m_moveSpeed = 2.5f;
    [SerializeField] private float m_attackDamage = 15f;
    [SerializeField] private float m_attackWarning = 0.7f;
    [SerializeField] private float m_attackInterval = 2.2f;
    [SerializeField] private float m_minimumRange = 10f;
    [SerializeField] private float m_maximumRange = 18f;
    [SerializeField] private float m_projectileSpeed = 12f;
    [SerializeField] private float m_projectileRadius = 0.25f;
    [SerializeField] private float m_projectileLifetime = 3f;
    [SerializeField] private Animator m_animator;
    [SerializeField] private Transform m_projectileOrigin;

    private EnemyHealth m_health;
    private NavMeshAgent m_agent;
    private PlayerHealth m_target;
    private GameObject m_chargeVisual;
    private float m_fireTime;
    private float m_nextAttackTime;
    private float m_nextRepositionTime;
    private int m_searchDirectionIndex;
    private bool m_isAiming;
    private bool m_isMoving;

    private void Awake()
    {
        m_health = GetComponent<EnemyHealth>();
        m_health.ZeroHealthReached += DisableEnemy;
        m_agent = GetComponent<NavMeshAgent>();
        m_agent.speed = m_moveSpeed;
        if (m_animator == null)
        {
            m_animator = GetComponentInChildren<Animator>();
        }
        if (m_projectileOrigin == null)
        {
            m_projectileOrigin = FindTransform("RightHand");
        }
        if (m_projectileOrigin == null)
        {
            m_projectileOrigin = transform;
        }
    }

    private void Start()
    {
        if (!m_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            m_agent.Warp(hit.position);
        }
    }

    private void OnDestroy()
    {
        if (m_health != null)
        {
            m_health.ZeroHealthReached -= DisableEnemy;
        }
    }

    private void Update()
    {
        if (m_health.IsDisabled)
        {
            return;
        }

        if (m_target == null)
        {
            m_target = FindFirstObjectByType<PlayerHealth>();
        }
        if (m_target == null || !m_agent.isOnNavMesh)
        {
            SetMoving(false);
            return;
        }

        if (m_isAiming)
        {
            UpdateAim();
            return;
        }

        if (CanFireFrom(transform.position))
        {
            m_agent.isStopped = true;
            SetMoving(false);
            FaceTarget();
            if (Time.time >= m_nextAttackTime)
            {
                BeginAim();
            }
            return;
        }

        Reposition();
    }

    private void BeginAim()
    {
        m_isAiming = true;
        m_fireTime = Time.time + m_attackWarning;
        m_nextAttackTime = Time.time + m_attackInterval;
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            m_animator.SetTrigger(s_Attack);
        }
        m_chargeVisual = RangedProjectile.CreateChargeVisual(m_projectileOrigin, m_projectileRadius);
    }

    private void UpdateAim()
    {
        if (!CanFireFrom(transform.position))
        {
            CancelAim();
            return;
        }

        FaceTarget();
        if (Time.time < m_fireTime)
        {
            return;
        }

        Vector3 origin = m_projectileOrigin.position;
        Vector3 targetPosition = m_target.transform.position + Vector3.up;
        RangedProjectile.Create(origin, targetPosition - origin, m_projectileSpeed, m_attackDamage, m_projectileRadius, m_projectileLifetime);
        Destroy(m_chargeVisual);
        m_chargeVisual = null;
        m_isAiming = false;
    }

    private void CancelAim()
    {
        m_isAiming = false;
        Destroy(m_chargeVisual);
        m_chargeVisual = null;
        m_nextRepositionTime = 0f;
    }

    private void Reposition()
    {
        if (Time.time < m_nextRepositionTime)
        {
            SetMoving(m_agent.velocity.sqrMagnitude > 0.01f);
            return;
        }
        m_nextRepositionTime = Time.time + 0.5f;

        Vector3 toTarget = m_target.transform.position - transform.position;
        toTarget.y = 0f;
        Vector3 destination;
        if (toTarget.magnitude > m_maximumRange)
        {
            destination = m_target.transform.position;
        }
        else if (toTarget.magnitude < m_minimumRange)
        {
            Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
            destination = transform.position + away * (m_minimumRange - toTarget.magnitude + 2f);
        }
        else if (!TryFindFiringPosition(out destination))
        {
            destination = m_target.transform.position;
        }

        m_agent.isStopped = false;
        bool accepted = m_agent.SetDestination(destination);
        SetMoving(accepted);
    }

    private bool TryFindFiringPosition(out Vector3 position)
    {
        for (int offset = 0; offset < s_SearchDirections.Length; offset++)
        {
            Vector3 direction = s_SearchDirections[(m_searchDirectionIndex + offset) % s_SearchDirections.Length];
            Vector3 candidate = m_target.transform.position + direction * ((m_minimumRange + m_maximumRange) * 0.5f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas) || !CanSeeTargetFrom(navHit.position))
            {
                continue;
            }

            NavMeshPath path = new();
            if (!NavMesh.CalculatePath(transform.position, navHit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            m_searchDirectionIndex = (m_searchDirectionIndex + offset + 1) % s_SearchDirections.Length;
            position = navHit.position;
            return true;
        }

        position = transform.position;
        return false;
    }

    private bool CanFireFrom(Vector3 position)
    {
        float distance = Vector3.Distance(position, m_target.transform.position);
        return distance <= m_maximumRange && CanSeeTargetFrom(position);
    }

    private bool CanSeeTargetFrom(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 1.4f;
        Vector3 targetPosition = m_target.transform.position + Vector3.up;
        if (!Physics.Raycast(origin, targetPosition - origin, out RaycastHit hit, Vector3.Distance(origin, targetPosition), Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }
        return hit.collider.GetComponentInParent<PlayerHealth>() == m_target;
    }

    private void FaceTarget()
    {
        Vector3 direction = m_target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.forward = direction.normalized;
        }
    }

    private Transform FindTransform(string transformName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == transformName)
            {
                return child;
            }
        }
        return null;
    }

    private void DisableEnemy(KillContext context)
    {
        CancelAim();
        if (m_agent.isOnNavMesh)
        {
            m_agent.isStopped = true;
        }
        SetMoving(false);
        enabled = false;
    }

    private void SetMoving(bool isMoving)
    {
        if (m_isMoving == isMoving)
        {
            return;
        }
        m_isMoving = isMoving;
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            m_animator.SetBool(s_IsMoving, isMoving);
        }
    }

    [ContextMenu("Run Three Enemy Combat Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Application.isPlaying, "Run this check in Play Mode.");
        Debug.Assert(m_minimumRange < m_maximumRange);
        Debug.Assert(m_attackWarning < m_attackInterval);
        Debug.Assert(m_projectileSpeed == 12f && m_projectileRadius == 0.25f && m_projectileLifetime == 3f);
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_attackDamage = Mathf.Max(0f, m_attackDamage);
        m_attackWarning = Mathf.Max(0.1f, m_attackWarning);
        m_attackInterval = Mathf.Max(m_attackWarning, m_attackInterval);
        m_minimumRange = Mathf.Max(0.1f, m_minimumRange);
        m_maximumRange = Mathf.Max(m_minimumRange + 0.1f, m_maximumRange);
        m_projectileSpeed = Mathf.Max(0.1f, m_projectileSpeed);
        m_projectileRadius = Mathf.Max(0.05f, m_projectileRadius);
        m_projectileLifetime = Mathf.Max(0.1f, m_projectileLifetime);
    }
}
