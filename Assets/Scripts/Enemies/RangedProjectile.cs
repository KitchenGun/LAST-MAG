using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class RangedProjectile : MonoBehaviour
{
    private const float k_TrailSpacing = 0.8f;

    private GameplayObjectPool m_pool;
    private SphereCollider m_collider;
    private Rigidbody m_body;
    private Vector3 m_lastTrailPosition;
    private float m_damage;
    private float m_releaseTime;

    public bool IsPooled { get; private set; }

    private void Awake()
    {
        m_collider = GetComponent<SphereCollider>();
        m_collider.isTrigger = true;
        m_body = GetComponent<Rigidbody>();
        m_body.useGravity = false;
        m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void Launch(GameplayObjectPool pool, Vector3 position, Vector3 direction, float speed,
        float damage, float radius, float lifetime)
    {
        m_pool = pool;
        IsPooled = false;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        transform.localScale = Vector3.one * radius * 2f;
        m_damage = damage;
        m_releaseTime = Time.time + lifetime;
        m_lastTrailPosition = position;
        m_collider.enabled = true;
        m_body.linearVelocity = direction.normalized * speed;
    }

    private void Update()
    {
        if (Time.time >= m_releaseTime)
        {
            ReturnToPool(false);
            return;
        }

        if ((transform.position - m_lastTrailPosition).sqrMagnitude >= k_TrailSpacing * k_TrailSpacing)
        {
            m_pool?.EmitProjectileTrail(transform.position);
            m_lastTrailPosition = transform.position;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.ApplyDamage(m_damage, PlayerDeathCause.RangedHumanoid);
        }
        ReturnToPool(true);
    }

    private void OnDisable()
    {
        if (m_body != null)
        {
            m_body.linearVelocity = Vector3.zero;
            m_body.angularVelocity = Vector3.zero;
        }
        if (m_collider != null)
        {
            m_collider.enabled = false;
        }
    }

    private void ReturnToPool(bool emitImpact)
    {
        if (IsPooled)
        {
            return;
        }

        if (m_pool == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (emitImpact)
        {
            m_pool.EmitProjectileImpact(transform.position);
        }
        m_pool.ReleaseProjectile(this);
    }

    internal void MarkPooled()
    {
        IsPooled = true;
    }

}
