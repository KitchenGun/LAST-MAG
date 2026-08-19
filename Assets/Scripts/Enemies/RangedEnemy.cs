using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(EnemyLedgeTraversal))]
public sealed class RangedEnemy : MonoBehaviour
{
    private const float k_ChargePuffInterval = 0.12f;
    private const float k_LineOfSightInterval = 0.1f;
    private const float k_RepositionInterval = 0.75f;
    private const int k_SearchCandidatesPerTick = 2;
    private static readonly RaycastHit[] s_LineOfSightHits = new RaycastHit[32];
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
    [SerializeField] private GameObject m_chargeVisual;
    [Header("Attack Voice")]
    [SerializeField] private AudioClip[] m_attackClips;
    [SerializeField] private float m_attackVoiceMaxDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float m_attackVoiceVolume = 1f;

    private NavMeshPath m_searchPath;
    private EnemyHealth m_health;
    private NavMeshAgent m_agent;
    private EnemyLedgeTraversal m_ledgeTraversal;
    private PlayerHealth m_target;
    private float m_fireTime;
    private float m_nextChargePuffTime;
    private float m_nextAttackTime;
    private float m_nextLineOfSightCheckTime;
    private float m_nextRepositionTime;
    private int m_searchDirectionIndex;
    private bool m_isAiming;
    private bool m_cachedCanFire;
    private bool m_isMoving;

    private void Awake()
    {
        m_searchPath = new NavMeshPath();
        m_health = GetComponent<EnemyHealth>();
        m_health.ZeroHealthReached += DisableEnemy;
        m_agent = GetComponent<NavMeshAgent>();
        m_ledgeTraversal = GetComponent<EnemyLedgeTraversal>();
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
        if (m_animator != null)
        {
            m_animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        if (m_chargeVisual != null)
        {
            m_chargeVisual.SetActive(false);
        }
    }

    private void OnEnable()
    {
        m_isAiming = false;
        m_isMoving = false;
        m_fireTime = 0f;
        m_nextChargePuffTime = 0f;
        m_nextAttackTime = 0f;
        float stagger = Mathf.Abs(GetInstanceID() % 1000) / 1000f;
        m_nextLineOfSightCheckTime = Time.time + stagger * k_LineOfSightInterval;
        m_nextRepositionTime = Time.time + stagger * k_RepositionInterval;
        m_cachedCanFire = false;
        if (m_chargeVisual != null)
        {
            m_chargeVisual.SetActive(false);
        }
        if (m_animator != null)
        {
            m_animator.SetBool(s_IsMoving, false);
            m_animator.ResetTrigger(s_Attack);
        }
        if (m_agent != null && m_agent.isOnNavMesh)
        {
            m_agent.ResetPath();
            m_agent.isStopped = false;
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

        if (m_ledgeTraversal.IsTraversing)
        {
            SetMoving(true);
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

        if (CanFireFromCached())
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
        m_nextChargePuffTime = Time.time;
        m_nextAttackTime = Time.time + m_attackInterval;
        SpatialAudio.PlayRandomOneShot(m_attackClips, transform.position,
            m_attackVoiceMaxDistance, m_attackVoiceVolume);
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            m_animator.SetTrigger(s_Attack);
        }
        if (m_chargeVisual != null)
        {
            m_chargeVisual.transform.localScale = Vector3.one * m_projectileRadius * 2f;
            m_chargeVisual.SetActive(true);
        }
    }

    private void UpdateAim()
    {
        if (!CanFireFromCached())
        {
            CancelAim();
            return;
        }

        FaceTarget();
        if (Time.time >= m_nextChargePuffTime)
        {
            m_health.Pool?.EmitProjectileCharge(m_projectileOrigin.position);
            m_nextChargePuffTime = Time.time + k_ChargePuffInterval;
        }
        if (Time.time < m_fireTime)
        {
            return;
        }

        Vector3 origin = m_projectileOrigin.position;
        Vector3 targetPosition = m_target.transform.position + Vector3.up;
        m_health.Pool?.SpawnProjectile(origin, targetPosition - origin, m_projectileSpeed,
            m_attackDamage, m_projectileRadius, m_projectileLifetime);
        if (m_chargeVisual != null)
        {
            m_chargeVisual.SetActive(false);
        }
        m_isAiming = false;
    }

    private void CancelAim()
    {
        m_isAiming = false;
        if (m_chargeVisual != null)
        {
            m_chargeVisual.SetActive(false);
        }
        m_nextRepositionTime = 0f;
    }

    private void Reposition()
    {
        if (Time.time < m_nextRepositionTime)
        {
            SetMoving(m_agent.velocity.sqrMagnitude > 0.01f);
            return;
        }
        m_nextRepositionTime = Time.time + k_RepositionInterval;

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
        for (int offset = 0; offset < k_SearchCandidatesPerTick; offset++)
        {
            Vector3 direction = s_SearchDirections[(m_searchDirectionIndex + offset) % s_SearchDirections.Length];
            Vector3 candidate = m_target.transform.position + direction * ((m_minimumRange + m_maximumRange) * 0.5f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas) || !CanSeeTargetFrom(navHit.position))
            {
                continue;
            }

            if (!NavMesh.CalculatePath(transform.position, navHit.position, NavMesh.AllAreas, m_searchPath)
                || m_searchPath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            m_searchDirectionIndex = (m_searchDirectionIndex + offset + 1) % s_SearchDirections.Length;
            position = navHit.position;
            return true;
        }

        m_searchDirectionIndex = (m_searchDirectionIndex + k_SearchCandidatesPerTick)
            % s_SearchDirections.Length;
        position = transform.position;
        return false;
    }

    private bool CanFireFromCached()
    {
        if (Time.time >= m_nextLineOfSightCheckTime)
        {
            m_nextLineOfSightCheckTime = Time.time + k_LineOfSightInterval;
            m_cachedCanFire = CanFireFrom(transform.position);
        }
        return m_cachedCanFire;
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
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(origin, direction / distance, s_LineOfSightHits, distance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        RaycastHit nearestNonEnemy = default;
        bool foundNonEnemy = false;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = s_LineOfSightHits[index];
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null
                || foundNonEnemy && hit.distance >= nearestNonEnemy.distance)
            {
                continue;
            }

            nearestNonEnemy = hit;
            foundNonEnemy = true;
        }

        return foundNonEnemy && nearestNonEnemy.collider.GetComponentInParent<PlayerHealth>() == m_target;
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
        m_attackVoiceMaxDistance = Mathf.Max(0.1f, m_attackVoiceMaxDistance);
        m_attackVoiceVolume = Mathf.Clamp01(m_attackVoiceVolume);
    }
}
