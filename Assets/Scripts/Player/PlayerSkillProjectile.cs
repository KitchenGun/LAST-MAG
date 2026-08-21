using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class PlayerSkillProjectile : MonoBehaviour
{
    private const int k_GrenadeExplosionCount = 3;
    private const float k_GrenadePulseInterval = 1f;
    private const float k_AirLinearDamping = 0f;
    private const float k_AirAngularDamping = 0.05f;
    private const float k_GroundLinearDamping = 2.5f;
    private const float k_GroundAngularDamping = 8f;
    private const float k_GroundNormalMinY = 0.55f;
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
    private PlayerSkillVfxEmitter m_vfxEmitter;
    private Rigidbody m_body;
    private SphereCollider m_collider;
    private Transform m_homeParent;
    private float m_enemyDamage;
    private float m_selfDamage;
    private float m_radius;
    private float m_explodeAt;
    private WeaponId m_sourceWeapon;
    private PlayerDeathCause m_selfDeathCause;
    private Vector3 m_lastTrailPosition;
    private int m_explosionsRemaining;
    private bool m_usesGravity;
    private bool m_isGrenadePulsing;
    private bool m_isLaunched;

    public void Initialize(PlayerSkillController owner, PlayerHealth player, ScoreSystem scoreSystem)
    {
        m_owner = owner;
        m_player = player;
        m_scoreSystem = scoreSystem;
        m_vfxEmitter = owner != null ? owner.GetComponent<PlayerSkillVfxEmitter>() : null;
        m_body ??= GetComponent<Rigidbody>();
        m_collider ??= GetComponent<SphereCollider>();
        m_homeParent ??= transform.parent;
        m_body.interpolation = RigidbodyInterpolation.None;
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
        m_isGrenadePulsing = false;
        m_explosionsRemaining = 0;
        m_body.interpolation = RigidbodyInterpolation.None;
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
        m_usesGravity = useGravity;
        m_isGrenadePulsing = false;
        m_explosionsRemaining = useGravity ? k_GrenadeExplosionCount : 1;
        m_lastTrailPosition = position;
        m_explodeAt = Time.fixedTime + lifetime;
        m_isLaunched = true;
        m_collider.enabled = true;
        m_body.interpolation = RigidbodyInterpolation.Interpolate;
        m_body.isKinematic = false;
        m_body.useGravity = useGravity;
        ResetAirDamping();
        m_body.linearVelocity = direction.normalized * speed;
        m_body.angularVelocity = useGravity ? new Vector3(4f, 2f, 3f) : Vector3.zero;
        if (useGravity)
        {
            SpatialAudio.PlayRandomOneShot(m_throwClips, position, m_throwMaxDistance, m_throwVolume,
                SpatialAudio.CuePriority.Gameplay);
            m_vfxEmitter?.EmitGrenadeTrail(position);
        }
        else
        {
            SpatialAudio.PlayRandomOneShot(m_launchClips, position, m_launchMaxDistance, m_launchVolume,
                SpatialAudio.CuePriority.Gameplay);
            m_vfxEmitter?.EmitRocketLaunch(position, direction);
        }
    }

    private void Update()
    {
        if (m_isLaunched && !m_isGrenadePulsing && m_vfxEmitter != null)
        {
            float spacing = m_usesGravity ? m_vfxEmitter.GrenadeTrailSpacing : m_vfxEmitter.RocketTrailSpacing;
            Vector3 trailSegment = transform.position - m_lastTrailPosition;
            float trailDistance = trailSegment.magnitude;
            if (!m_usesGravity)
            {
                m_vfxEmitter.UpdateRocketFlight(transform.position, m_body.linearVelocity);
            }

            if (trailDistance >= spacing)
            {
                if (m_usesGravity)
                {
                    m_vfxEmitter.EmitGrenadeTrail(transform.position);
                    m_lastTrailPosition = transform.position;
                }
                else
                {
                    Vector3 trailDirection = trailSegment / trailDistance;
                    int sampleCount = Mathf.Min(Mathf.FloorToInt(trailDistance / spacing), 8);
                    for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
                    {
                        m_vfxEmitter.EmitRocketTrail(
                            m_lastTrailPosition + trailDirection * spacing * sampleIndex,
                            m_body.linearVelocity);
                    }
                    m_lastTrailPosition += trailDirection * spacing * sampleCount;
                }
            }
        }

    }

    private void FixedUpdate()
    {
        if (m_isLaunched && Time.fixedTime >= m_explodeAt)
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
            bool hitEnemy = collision.collider.GetComponentInParent<EnemyHealth>() != null;
            if (!hitEnemy && HasGroundContact(collision))
            {
                m_body.linearDamping = k_GroundLinearDamping;
                m_body.angularDamping = k_GroundAngularDamping;
            }
            if (!hitEnemy)
            {
                SpatialAudio.PlayRandomOneShot(m_collisionClips, collision.GetContact(0).point,
                    m_collisionMaxDistance, m_collisionVolume, SpatialAudio.CuePriority.Gameplay);
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

        if (m_usesGravity && !m_isGrenadePulsing)
        {
            m_isGrenadePulsing = true;
            m_body.linearVelocity = Vector3.zero;
            m_body.angularVelocity = Vector3.zero;
            m_body.isKinematic = true;
            m_collider.enabled = false;
        }
        if (!m_usesGravity)
        {
            m_vfxEmitter?.EndRocketFlight();
        }
        m_vfxEmitter?.EmitExplosion(transform.position, m_radius, !m_usesGravity);
        FirstPersonController.CurrentInstance?.ApplyExplosionShake(
            transform.position, m_radius, !m_usesGravity);
        SpatialAudio.PlayRandomOneShot(m_explosionClips, transform.position,
            m_explosionMaxDistance, m_explosionVolume, SpatialAudio.CuePriority.Gameplay);
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

        PlayerHealth playerInBlast = m_player != null
            && Vector3.Distance(transform.position, m_player.transform.position) <= m_radius
                ? m_player : null;
        m_scoreSystem?.RegisterSkillBatch(m_killedEnemies);
        m_explosionsRemaining--;
        if (m_explosionsRemaining > 0)
        {
            m_explodeAt += k_GrenadePulseInterval;
        }
        else
        {
            Hide();
            m_owner?.NotifyProjectileExploded();
        }
        playerInBlast?.ApplyDamage(m_selfDamage, m_selfDeathCause);
    }

    public void Hide()
    {
        m_isLaunched = false;
        if (!m_usesGravity)
        {
            m_vfxEmitter?.EndRocketFlight();
        }
        if (m_body == null || m_collider == null)
        {
            return;
        }
        if (!m_body.isKinematic)
        {
            m_body.linearVelocity = Vector3.zero;
            m_body.angularVelocity = Vector3.zero;
        }
        m_body.isKinematic = true;
        m_body.interpolation = RigidbodyInterpolation.None;
        ResetAirDamping();
        m_collider.enabled = false;
        m_explodeAt = 0f;
        m_explosionsRemaining = 0;
        m_isGrenadePulsing = false;
        if (m_homeParent != null)
        {
            transform.SetParent(m_homeParent, false);
        }
        gameObject.SetActive(false);
    }

    internal bool DropAsInertDeathProp(Vector3 inheritedVelocity, Vector3 worldForward)
    {
        CharacterController controller = m_player != null
            ? m_player.GetComponent<CharacterController>()
            : null;
        bool created = WeaponViewmodelController.CreateDeathDropClone(
            transform, inheritedVelocity, worldForward, controller);
        Hide();
        return created;
    }

    private static bool HasGroundContact(Collision collision)
    {
        for (int index = 0; index < collision.contactCount; index++)
        {
            if (collision.GetContact(index).normal.y >= k_GroundNormalMinY)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetAirDamping()
    {
        m_body.linearDamping = k_AirLinearDamping;
        m_body.angularDamping = k_AirAngularDamping;
    }

    [ContextMenu("Run Skill Projectile Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(k_GrenadeExplosionCount == 3);
        Debug.Assert(Mathf.Approximately(k_GrenadePulseInterval, 1f));
        Debug.Assert(Mathf.Approximately(k_AirLinearDamping, 0f));
        Debug.Assert(Mathf.Approximately(k_AirAngularDamping, 0.05f));
        Debug.Assert(Mathf.Approximately(k_GroundLinearDamping, 2.5f));
        Debug.Assert(Mathf.Approximately(k_GroundAngularDamping, 8f));
        Debug.Assert(Mathf.Approximately(k_GroundNormalMinY, 0.55f));
    }
}
