using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyLedgeTraversal : MonoBehaviour
{
    private const float k_ClimbDuration = 0.5f;
    private const float k_CrestDuration = 0.2f;
    private const float k_DescentDuration = 1f;
    private const float k_TraversalAnimationSpeed = 0.65f;
    private const float k_CrestClearance = 0.05f;

    private NavMeshAgent m_agent;
    private Animator m_animator;
    private Vector3 m_start;
    private Vector3 m_crest;
    private Vector3 m_end;
    private float m_elapsed;
    private float m_previousAnimatorSpeed;
    private bool m_previousUpdatePosition;
    private bool m_previousUpdateRotation;

    public bool IsTraversing { get; private set; }

    private void Awake()
    {
        m_agent = GetComponent<NavMeshAgent>();
        m_animator = GetComponentInChildren<Animator>();
        m_agent.autoTraverseOffMeshLink = false;
    }

    private void OnEnable()
    {
        IsTraversing = false;
        m_elapsed = 0f;
        if (m_agent != null)
        {
            m_agent.autoTraverseOffMeshLink = false;
        }
    }

    private void OnDisable()
    {
        RestoreState();
    }

    private void Update()
    {
        if (!IsTraversing)
        {
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh && m_agent.isOnOffMeshLink)
            {
                BeginTraversal();
            }
            return;
        }

        m_elapsed += Time.deltaTime;
        if (m_elapsed < k_ClimbDuration)
        {
            Move(Vector3.Lerp(m_start, m_crest,
                Mathf.SmoothStep(0f, 1f, m_elapsed / k_ClimbDuration)));
            return;
        }

        if (m_elapsed < k_ClimbDuration + k_CrestDuration)
        {
            Move(m_crest);
            return;
        }

        float descentTime = m_elapsed - k_ClimbDuration - k_CrestDuration;
        if (descentTime < k_DescentDuration)
        {
            Move(Vector3.Lerp(m_crest, m_end,
                Mathf.SmoothStep(0f, 1f, descentTime / k_DescentDuration)));
            return;
        }

        CompleteTraversal();
    }

    private void BeginTraversal()
    {
        OffMeshLinkData data = m_agent.currentOffMeshLinkData;
        if (!data.valid || data.owner == null || data.owner.name != "EnemyDropLink")
        {
            return;
        }

        m_start = transform.position;
        m_end = data.endPos + Vector3.up * m_agent.baseOffset;
        m_crest = Vector3.Lerp(data.startPos, data.endPos, 0.5f);

        NavMeshLink link = data.owner as NavMeshLink;
        Renderer railing = link != null && link.transform.parent != null
            ? link.transform.parent.GetComponentInChildren<Renderer>()
            : null;
        m_crest.y = railing != null
            ? railing.bounds.max.y + k_CrestClearance
            : Mathf.Max(data.startPos.y, data.endPos.y) + 1f;

        Vector3 facing = m_end - m_start;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(facing);
        }

        m_previousUpdatePosition = m_agent.updatePosition;
        m_previousUpdateRotation = m_agent.updateRotation;
        m_agent.updatePosition = false;
        m_agent.updateRotation = false;
        m_agent.velocity = Vector3.zero;

        if (m_animator != null)
        {
            m_previousAnimatorSpeed = m_animator.speed;
            m_animator.speed = m_previousAnimatorSpeed * k_TraversalAnimationSpeed;
        }

        m_elapsed = 0f;
        IsTraversing = true;
    }

    private void Move(Vector3 position)
    {
        transform.position = position;
        m_agent.nextPosition = position;
    }

    private void CompleteTraversal()
    {
        Move(m_end);
        m_agent.CompleteOffMeshLink();
        RestoreState();
    }

    private void RestoreState()
    {
        if (!IsTraversing)
        {
            return;
        }

        if (m_agent != null)
        {
            m_agent.updatePosition = m_previousUpdatePosition;
            m_agent.updateRotation = m_previousUpdateRotation;
        }
        if (m_animator != null && IsTraversing)
        {
            m_animator.speed = m_previousAnimatorSpeed;
        }

        IsTraversing = false;
        m_elapsed = 0f;
    }

    [ContextMenu("Run Ledge Traversal Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Mathf.Approximately(k_ClimbDuration + k_CrestDuration + k_DescentDuration, 1.7f));
        Debug.Assert(k_TraversalAnimationSpeed > 0f && k_TraversalAnimationSpeed < 1f);
    }
}
