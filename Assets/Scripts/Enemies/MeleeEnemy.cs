using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(EnemyLedgeTraversal))]
public sealed class MeleeEnemy : MonoBehaviour
{
    private const float k_PathUpdateInterval = 0.25f;
    private const float k_PathDestinationThresholdSqr = 1f;
    private static readonly Collider[] s_AttackHits = new Collider[128];
    private static readonly int s_IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int s_Attack = Animator.StringToHash("Attack");

    [SerializeField] private float m_moveSpeed = 4f;
    [SerializeField] private float m_attackDamage = 15f;
    [SerializeField] private float m_attackRange = 1.8f;
    [SerializeField] private float m_attackWarning = 0.45f;
    [SerializeField] private float m_attackInterval = 1.2f;
    [SerializeField] private float m_hitRadius = 1f;
    [SerializeField] private Animator m_animator;
    [Header("Attack Voice")]
    [SerializeField] private AudioClip[] m_attackClips;
    [SerializeField] private float m_attackVoiceMaxDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float m_attackVoiceVolume = 1f;

    private EnemyHealth m_health;
    private NavMeshAgent m_agent;
    private EnemyLedgeTraversal m_ledgeTraversal;
    private PlayerHealth m_target;
    private Vector3 m_lockedAttackDirection;
    private float m_hitTime;
    private float m_nextAttackTime;
    private float m_nextPathUpdateTime;
    private bool m_isAttacking;
    private bool m_isMoving;
    private bool m_hasPathDestination;
    private Vector3 m_lastPathDestination;

    private void Awake()
    {
        m_health = GetComponent<EnemyHealth>();
        m_health.ZeroHealthReached += DisableEnemy;
        m_agent = GetComponent<NavMeshAgent>();
        m_ledgeTraversal = GetComponent<EnemyLedgeTraversal>();
        m_agent.speed = m_moveSpeed;
        m_agent.stoppingDistance = m_attackRange * 0.8f;
        if (m_animator == null)
        {
            m_animator = GetComponentInChildren<Animator>();
        }
        if (m_animator != null)
        {
            m_animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        Debug.Assert(m_attackClips != null && m_attackClips.Length == 6);
    }

    private void OnEnable()
    {
        m_isAttacking = false;
        m_isMoving = false;
        m_hasPathDestination = false;
        m_hitTime = 0f;
        m_nextAttackTime = 0f;
        m_nextPathUpdateTime = Time.time
            + Mathf.Abs(GetInstanceID() % 1000) / 1000f * k_PathUpdateInterval;
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

        if (m_isAttacking)
        {
            if (Time.time >= m_hitTime)
            {
                ResolveAttack();
            }
            return;
        }

        Vector3 toTarget = m_target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude <= m_attackRange && Time.time >= m_nextAttackTime)
        {
            BeginAttack(toTarget);
            return;
        }

        Vector3 targetPosition = m_target.transform.position;
        bool destinationChanged = !m_hasPathDestination
            || (targetPosition - m_lastPathDestination).sqrMagnitude >= k_PathDestinationThresholdSqr;
        if (Time.time >= m_nextPathUpdateTime && destinationChanged)
        {
            m_nextPathUpdateTime = Time.time + k_PathUpdateInterval;
            m_lastPathDestination = targetPosition;
            m_hasPathDestination = m_agent.SetDestination(targetPosition);
        }
        m_agent.isStopped = false;
        SetMoving(m_hasPathDestination && m_agent.hasPath && !m_agent.pathPending
            && m_agent.pathStatus == NavMeshPathStatus.PathComplete && m_agent.velocity.sqrMagnitude > 0.01f);
    }

    private void BeginAttack(Vector3 toTarget)
    {
        m_isAttacking = true;
        m_agent.isStopped = true;
        m_lockedAttackDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
        transform.forward = m_lockedAttackDirection;
        m_hitTime = Time.time + m_attackWarning;
        m_nextAttackTime = Time.time + m_attackInterval;
        SetMoving(false);
        SpatialAudio.PlayRandomOneShot(m_attackClips, transform.position, m_attackVoiceMaxDistance, m_attackVoiceVolume);
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            m_animator.SetTrigger(s_Attack);
        }
    }

    private void ResolveAttack()
    {
        m_isAttacking = false;
        Vector3 hitCenter = transform.position + Vector3.up + m_lockedAttackDirection * Mathf.Max(0.8f, m_attackRange - m_hitRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(hitCenter, m_hitRadius, s_AttackHits,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hitCount; index++)
        {
            Collider hit = s_AttackHits[index];
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.ApplyDamage(m_attackDamage, PlayerDeathCause.MeleeHumanoid);
                break;
            }
        }
    }

    private void DisableEnemy(KillContext context)
    {
        m_isAttacking = false;
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

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_attackDamage = Mathf.Max(0f, m_attackDamage);
        m_attackRange = Mathf.Max(0.1f, m_attackRange);
        m_attackWarning = Mathf.Max(0.1f, m_attackWarning);
        m_attackInterval = Mathf.Max(m_attackWarning, m_attackInterval);
        m_hitRadius = Mathf.Max(0.1f, m_hitRadius);
        m_attackVoiceMaxDistance = Mathf.Max(0.1f, m_attackVoiceMaxDistance);
        m_attackVoiceVolume = Mathf.Clamp01(m_attackVoiceVolume);
    }
}
