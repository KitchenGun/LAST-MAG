using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class PlayerSkillProjectile : MonoBehaviour
{
    private static readonly Collider[] s_ExplosionHits = new Collider[128];

    [Header("Explosion Audio")]
    [SerializeField] private AudioClip[] m_explosionClips;
    [SerializeField] private float m_explosionMaxDistance = 35f;
    [SerializeField, Range(0f, 1f)] private float m_explosionVolume = 1f;

    [Header("Grenade Collision Audio")]
    [SerializeField] private AudioClip[] m_collisionClips;
    [SerializeField] private float m_collisionMaxDistance = 15f;
    [SerializeField, Range(0f, 1f)] private float m_collisionVolume = 0.65f;

    [Header("Grenade Throw Audio")]
    [SerializeField] private AudioClip[] m_throwClips;
    [SerializeField] private float m_throwMaxDistance = 15f;
    [SerializeField, Range(0f, 1f)] private float m_throwVolume = 0.75f;

    [Header("Rocket Launch Audio")]
    [SerializeField] private AudioClip[] m_launchClips;
    [SerializeField] private float m_launchMaxDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float m_launchVolume = 1f;

    private readonly HashSet<EnemyHealth> m_damagedEnemies = new();
    private readonly List<EnemyType> m_killedEnemies = new();
    private PlayerSkillController m_owner;
    private PlayerHealth m_player;
    private ScoreSystem m_scoreSystem;
    private Rigidbody m_body;
    private SphereCollider m_collider;
    private Transform m_homeParent;
    private float m_enemyDamage;
    private float m_selfDamage;
    private float m_radius;
    private float m_explodeAt;
    private WeaponId m_sourceWeapon;
    private PlayerDeathCause m_selfDeathCause;
    private bool m_isLaunched;

    public void Initialize(PlayerSkillController owner, PlayerHealth player, ScoreSystem scoreSystem)
    {
        m_owner = owner;
        m_player = player;
        m_scoreSystem = scoreSystem;
        m_body ??= GetComponent<Rigidbody>();
        m_collider ??= GetComponent<SphereCollider>();
        m_homeParent ??= transform.parent;
        m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (m_player != null && m_player.TryGetComponent(out CharacterController controller))
        {
            Physics.IgnoreCollision(m_collider, controller, true);
        }
        Hide();
    }

    public void ShowArmed(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        m_isLaunched = false;
        m_body.isKinematic = true;
        m_collider.enabled = false;
        transform.SetParent(parent, false);
        transform.SetLocalPositionAndRotation(localPosition, localRotation);
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
    }

    public void Launch(Vector3 position, Vector3 direction, float speed, float enemyDamage, float selfDamage,
        float radius, float lifetime, bool useGravity, WeaponId sourceWeapon, PlayerDeathCause selfDeathCause)
    {
        gameObject.SetActive(true);
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        m_enemyDamage = enemyDamage;
        m_selfDamage = selfDamage;
        m_radius = radius;
        m_sourceWeapon = sourceWeapon;
        m_selfDeathCause = selfDeathCause;
        m_explodeAt = Time.time + lifetime;
        m_isLaunched = true;
        m_collider.enabled = true;
        m_body.isKinematic = false;
        m_body.useGravity = useGravity;
        m_body.linearVelocity = direction.normalized * speed;
        m_body.angularVelocity = useGravity ? new Vector3(4f, 2f, 3f) : Vector3.zero;
        if (useGravity)
        {
            SpatialAudio.PlayRandomOneShot(m_throwClips, position, m_throwMaxDistance, m_throwVolume);
        }
        else
        {
            SpatialAudio.PlayRandomOneShot(m_launchClips, position, m_launchMaxDistance, m_launchVolume);
        }
    }

    private void Update()
    {
        if (m_isLaunched && Time.time >= m_explodeAt)
        {
            Explode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!m_isLaunched)
        {
            return;
        }

        if (m_body.useGravity)
        {
            if (collision.collider.GetComponentInParent<EnemyHealth>() == null)
            {
                SpatialAudio.PlayRandomOneShot(m_collisionClips, collision.GetContact(0).point,
                    m_collisionMaxDistance, m_collisionVolume);
            }
            return;
        }

        Explode();
    }

    private void Explode()
    {
        if (!m_isLaunched)
        {
            return;
        }

        m_isLaunched = false;
        SpatialAudio.PlayRandomOneShot(m_explosionClips, transform.position,
            m_explosionMaxDistance, m_explosionVolume);
        int comboSnapshot = m_scoreSystem != null ? m_scoreSystem.ComboLevel : 0;
        m_damagedEnemies.Clear();
        m_killedEnemies.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, m_radius, s_ExplosionHits,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hitCount; index++)
        {
            EnemyHealth enemy = s_ExplosionHits[index].GetComponentInParent<EnemyHealth>();
            if (enemy != null && m_damagedEnemies.Add(enemy)
                && enemy.ApplyExplosionDamage(m_enemyDamage, KillContext.Skill(m_sourceWeapon)))
            {
                m_killedEnemies.Add(enemy.Type);
            }
        }

        if (m_player != null && Vector3.Distance(transform.position, m_player.transform.position) <= m_radius)
        {
            m_player.ApplyDamage(m_selfDamage, m_selfDeathCause);
        }
        m_scoreSystem?.RegisterSkillBatch(m_killedEnemies, comboSnapshot);
        Hide();
        m_owner?.NotifyProjectileExploded();
    }

    public void Hide()
    {
        m_isLaunched = false;
        if (m_body == null || m_collider == null)
        {
            return;
        }
        m_body.linearVelocity = Vector3.zero;
        m_body.angularVelocity = Vector3.zero;
        m_body.isKinematic = true;
        m_collider.enabled = false;
        if (m_homeParent != null)
        {
            transform.SetParent(m_homeParent, false);
        }
        gameObject.SetActive(false);
    }
}
