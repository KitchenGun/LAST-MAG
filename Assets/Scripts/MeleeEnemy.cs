using UnityEngine;

public sealed class MeleeEnemy : MonoBehaviour
{
    [SerializeField] private float m_moveSpeed = 2f;
    [SerializeField] private float m_attackRange = 1.5f;
    [SerializeField] private float m_attackDamage = 10f;
    [SerializeField] private float m_attackInterval = 1f;

    private PlayerHealth m_target;
    private float m_nextAttackTime;

    private void Update()
    {
        if (m_target == null)
        {
            m_target = FindFirstObjectByType<PlayerHealth>();
            if (m_target == null)
            {
                return;
            }
        }

        Vector3 toTarget = m_target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance > m_attackRange)
        {
            transform.position += toTarget.normalized * Mathf.Min(m_moveSpeed * Time.deltaTime, distance - m_attackRange);
            transform.forward = toTarget.normalized;
            return;
        }

        if (Time.time >= m_nextAttackTime)
        {
            m_target.ApplyDamage(m_attackDamage);
            m_nextAttackTime = Time.time + m_attackInterval;
        }
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_attackRange = Mathf.Max(0f, m_attackRange);
        m_attackDamage = Mathf.Max(0f, m_attackDamage);
        m_attackInterval = Mathf.Max(0.1f, m_attackInterval);
    }
}
