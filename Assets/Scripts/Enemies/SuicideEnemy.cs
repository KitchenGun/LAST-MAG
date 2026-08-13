using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent))]
public sealed class SuicideEnemy : MonoBehaviour
{
    private const float k_PathUpdateInterval = 0.25f;
    private const float k_PathDestinationThresholdSqr = 1f;
    private const float k_MinWarningEmission = 0.25f;
    private const float k_MaxWarningEmission = 4f;
    private static readonly int s_IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int s_Warning = Animator.StringToHash("Warning");
    private static readonly int s_WarningSpeed = Animator.StringToHash("WarningSpeed");
    private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Collider[] s_ExplosionHits = new Collider[256];

    [SerializeField] private float m_moveSpeed = 2f;
    [SerializeField] private float m_explosionDamage = 60f;
    [SerializeField] private float m_explosionRadius = 4f;
    [SerializeField] private float m_warningDuration = 0.8f;
    [SerializeField] private float m_deathExplosionDelay = 0.2f;
    [SerializeField] private Animator m_animator;
    [SerializeField] private Renderer m_warningRenderer;
    [SerializeField, Min(0)] private int m_warningMaterialIndex = 1;
    [Header("Explosion Audio")]
    [SerializeField] private AudioClip m_explosionClip;
    [SerializeField] private float m_explosionMaxDistance = 35f;
    [SerializeField, Range(0f, 1f)] private float m_explosionVolume = 1f;

    private readonly HashSet<PlayerHealth> m_damagedPlayers = new();
    private readonly HashSet<EnemyHealth> m_damagedEnemies = new();
    private readonly List<EnemyType> m_chainKills = new(16);
    private PlayerHealth m_target;
    private FirstPersonController m_targetController;
    private EnemyHealth m_health;
    private NavMeshAgent m_agent;
    private MaterialPropertyBlock m_warningProperties;
    private ScoreSystem m_scoreSystem;
    private SuicideGasEmitter m_gasEmitter;
    private Color m_normalHeadColor;
    private float m_explosionTime;
    private float m_nextPathUpdateTime;
    private WeaponId m_sourceWeapon;
    private bool m_hasPlayerAttribution;
    private bool m_hasClassSkillAttribution;
    private bool m_isWarning;
    private bool m_isDying;
    private bool m_hasExploded;
    private bool m_isMoving;
    private bool m_hasPathDestination;
    private Vector3 m_lastPathDestination;

    private void Awake()
    {
        m_health = GetComponent<EnemyHealth>();
        m_health.ZeroHealthReached += StartDeathExplosion;
        m_agent = GetComponent<NavMeshAgent>();
        m_agent.speed = m_moveSpeed;
        m_agent.stoppingDistance = Mathf.Max(0f, m_explosionRadius - 0.25f);
        m_agent.updateRotation = true;

        if (m_warningRenderer == null)
        {
            m_warningRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        InitializeWarningMaterial();
        m_scoreSystem = FindFirstObjectByType<ScoreSystem>();
        m_gasEmitter = FindFirstObjectByType<SuicideGasEmitter>();
    }

    private void OnEnable()
    {
        m_damagedPlayers.Clear();
        m_damagedEnemies.Clear();
        m_explosionTime = 0f;
        m_nextPathUpdateTime = Time.time
            + Mathf.Abs(GetInstanceID() % 1000) / 1000f * k_PathUpdateInterval;
        m_sourceWeapon = WeaponId.Unknown;
        m_hasPlayerAttribution = false;
        m_hasClassSkillAttribution = false;
        m_isWarning = false;
        m_isDying = false;
        m_hasExploded = false;
        m_isMoving = false;
        m_hasPathDestination = false;
        ResetWarningMaterial();
        if (m_animator != null)
        {
            m_animator.SetBool(s_IsMoving, false);
            m_animator.ResetTrigger(s_Warning);
            m_animator.SetFloat(s_WarningSpeed, 1f);
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
            m_health.ZeroHealthReached -= StartDeathExplosion;
        }
    }

    private void Update()
    {
        if (m_hasExploded)
        {
            return;
        }

        if (m_isDying || m_isWarning)
        {
            UpdateWarningMaterial();
            if (Time.time >= m_explosionTime)
            {
                Explode();
            }
            return;
        }

        FindTarget();
        if (m_target == null || !m_agent.isOnNavMesh)
        {
            SetMoving(false);
            return;
        }

        m_agent.speed = m_moveSpeed;
        Vector3 targetPosition = m_target.transform.position;
        bool destinationChanged = !m_hasPathDestination
            || (targetPosition - m_lastPathDestination).sqrMagnitude >= k_PathDestinationThresholdSqr;
        if (Time.time >= m_nextPathUpdateTime && destinationChanged)
        {
            m_nextPathUpdateTime = Time.time + k_PathUpdateInterval;
            m_lastPathDestination = targetPosition;
            m_hasPathDestination = m_agent.SetDestination(targetPosition);
        }
        bool hasCompletePath = m_hasPathDestination && m_agent.hasPath && !m_agent.pathPending
            && m_agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (hasCompletePath && Vector3.Distance(transform.position, m_target.transform.position) <= m_explosionRadius)
        {
            StartWarning();
            return;
        }

        SetMoving(hasCompletePath && m_agent.velocity.sqrMagnitude > 0.01f);
    }

    private void FindTarget()
    {
        if (m_target != null)
        {
            return;
        }

        m_target = FindFirstObjectByType<PlayerHealth>();
        m_targetController = m_target != null ? m_target.GetComponent<FirstPersonController>() : null;
    }

    private void StartWarning()
    {
        m_isWarning = true;
        m_agent.isStopped = true;
        m_explosionTime = Time.time + m_warningDuration;
        SetMoving(false);
        PlayWarningAnimation(1f);
        UpdateWarningMaterial();
    }

    private void StartDeathExplosion(KillContext context)
    {
        m_isDying = true;
        if (m_scoreSystem == null)
        {
            m_scoreSystem = FindFirstObjectByType<ScoreSystem>();
        }
        m_sourceWeapon = context.Weapon;
        m_hasPlayerAttribution = context.IsPlayerAttributed;
        m_hasClassSkillAttribution = ResolveClassSkillAttribution(
            context, m_scoreSystem != null && m_scoreSystem.IsBulletTimeActive);
        if (m_agent.isOnNavMesh)
        {
            m_agent.isStopped = true;
        }
        m_explosionTime = Time.time + m_deathExplosionDelay;
        SetMoving(false);
        PlayWarningAnimation(4f);
        UpdateWarningMaterial();
    }

    private void Explode()
    {
        if (m_hasExploded)
        {
            return;
        }

        m_hasExploded = true;
        m_health.DisableColliders();
        SetMoving(false);
        SpatialAudio.PlayOneShot(m_explosionClip, transform.position, m_explosionMaxDistance, m_explosionVolume);
        if (m_gasEmitter == null)
        {
            m_gasEmitter = FindFirstObjectByType<SuicideGasEmitter>();
        }
        m_gasEmitter?.EmitAt(transform.position, m_explosionRadius);

        WeaponId sourceWeapon = ResolveSourceWeapon();
        if (m_scoreSystem == null)
        {
            m_scoreSystem = FindFirstObjectByType<ScoreSystem>();
        }
        m_chainKills.Clear();
        KillContext explosionContext = KillContext.Chain(
            sourceWeapon, m_hasPlayerAttribution, m_hasClassSkillAttribution);
        m_damagedPlayers.Clear();
        m_damagedEnemies.Clear();
        PlayerHealth playerToDamage = null;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, m_explosionRadius, s_ExplosionHits,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hitCount; index++)
        {
            Collider hit = s_ExplosionHits[index];
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && m_damagedPlayers.Add(player))
            {
                playerToDamage ??= player;
            }

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null && enemy != m_health && m_damagedEnemies.Add(enemy))
            {
                if (enemy.ApplyExplosionDamage(m_explosionDamage, explosionContext) && m_hasPlayerAttribution)
                {
                    m_chainKills.Add(enemy.Type);
                }
            }
        }

        if (m_hasPlayerAttribution)
        {
            m_scoreSystem?.RegisterChainBatch(
                m_chainKills, sourceWeapon, m_hasClassSkillAttribution);
        }

        m_health.DropAmmo(transform.position);
        m_health.ReturnToPool();
        playerToDamage?.ApplyDamage(m_explosionDamage, PlayerDeathCause.SuicideBacteriophage);
    }

    private static bool ResolveClassSkillAttribution(KillContext context, bool bulletTimeActive)
    {
        return context.IsClassSkillAttributed
            || (context.Source == DamageSource.DirectWeapon && bulletTimeActive);
    }

    private WeaponId ResolveSourceWeapon()
    {
        if (m_sourceWeapon is >= WeaponId.Pistol and <= WeaponId.DMR)
        {
            return m_sourceWeapon;
        }

        FindTarget();
        return m_targetController != null ? m_targetController.CurrentWeapon : WeaponId.Pistol;
    }

    private void InitializeWarningMaterial()
    {
        if (m_warningRenderer == null || m_warningMaterialIndex >= m_warningRenderer.sharedMaterials.Length)
        {
            return;
        }

        Material material = m_warningRenderer.sharedMaterials[m_warningMaterialIndex];
        m_normalHeadColor = material.HasProperty(s_BaseColor) ? material.GetColor(s_BaseColor) : Color.white;
        material.EnableKeyword("_EMISSION");
        m_warningProperties = new MaterialPropertyBlock();
    }

    private void UpdateWarningMaterial()
    {
        if (m_warningRenderer == null || m_warningProperties == null)
        {
            return;
        }

        float duration = m_isDying ? m_deathExplosionDelay : m_warningDuration;
        float progress = duration > 0f
            ? 1f - Mathf.Clamp01((m_explosionTime - Time.time) / duration)
            : 1f;
        float pulse = m_isDying ? Mathf.SmoothStep(0f, 1f, progress) : EvaluateWarningPulse(progress);
        float emission = Mathf.Lerp(k_MinWarningEmission, k_MaxWarningEmission, pulse);

        m_warningRenderer.GetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
        m_warningProperties.SetColor(s_BaseColor, m_normalHeadColor);
        m_warningProperties.SetColor(s_EmissionColor, m_normalHeadColor * emission);
        m_warningRenderer.SetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
    }

    private void ResetWarningMaterial()
    {
        if (m_warningRenderer == null || m_warningProperties == null)
        {
            return;
        }

        m_warningRenderer.GetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
        m_warningProperties.SetColor(s_BaseColor, m_normalHeadColor);
        m_warningProperties.SetColor(s_EmissionColor, Color.black);
        m_warningRenderer.SetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
    }

    private static float EvaluateWarningPulse(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return 0.5f - 0.5f * Mathf.Cos(5f * Mathf.PI * progress * progress);
    }

    private void PlayWarningAnimation(float speed)
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null)
        {
            return;
        }

        m_animator.SetFloat(s_WarningSpeed, speed);
        m_animator.SetTrigger(s_Warning);
    }

    private void SetMoving(bool isMoving)
    {
        if (m_isMoving == isMoving)
        {
            return;
        }

        m_isMoving = isMoving;
        if (m_animator != null)
        {
            m_animator.SetBool(s_IsMoving, isMoving);
        }
    }

    [ContextMenu("Run Suicide Enemy Self Check")]
    private void RunSuicideEnemySelfCheck()
    {
        Debug.Assert(Application.isPlaying, "Run this check in Play Mode.");
        Debug.Assert(m_health != null);
        Debug.Assert(!m_hasExploded || m_isDying || m_isWarning);
        Debug.Assert(Mathf.Approximately(EvaluateWarningPulse(0f), 0f));
        Debug.Assert(Mathf.Approximately(EvaluateWarningPulse(1f), 1f));
        Debug.Assert(!ResolveClassSkillAttribution(KillContext.Direct(WeaponId.DMR, false), false));
        Debug.Assert(ResolveClassSkillAttribution(KillContext.Direct(WeaponId.DMR, false), true));
        Debug.Assert(!ResolveClassSkillAttribution(KillContext.Chain(WeaponId.DMR, true), true));
        Debug.Assert(ResolveClassSkillAttribution(KillContext.Skill(WeaponId.Rifle), false));
        Debug.Assert(ResolveClassSkillAttribution(KillContext.Chain(WeaponId.Rifle, true, true), false));
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_explosionDamage = Mathf.Max(0f, m_explosionDamage);
        m_explosionRadius = Mathf.Max(0.1f, m_explosionRadius);
        m_warningDuration = Mathf.Max(0.1f, m_warningDuration);
        m_deathExplosionDelay = Mathf.Max(0f, m_deathExplosionDelay);
        m_warningMaterialIndex = Mathf.Max(0, m_warningMaterialIndex);
        m_explosionMaxDistance = Mathf.Max(0.1f, m_explosionMaxDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.78f, 0.71f, 0.29f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, m_explosionRadius);
    }
}
