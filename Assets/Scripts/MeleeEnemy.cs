using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Collider), typeof(NavMeshAgent))]
public sealed class MeleeEnemy : MonoBehaviour
{
    private static readonly int s_IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int[] s_AmmoAmounts = { 12, 6, 30 };

    [SerializeField] private float m_moveSpeed = 2f;
    [SerializeField] private float m_maxHealth = 60f;
    [SerializeField] private float m_explosionDamage = 60f;
    [SerializeField] private float m_explosionRadius = 4f;
    [SerializeField] private float m_warningDuration = 0.8f;
    [SerializeField] private float m_deathExplosionDelay = 0.2f;
    [SerializeField] private Animator m_animator;
    [SerializeField] private Renderer m_warningRenderer;
    [SerializeField, Min(0)] private int m_warningMaterialIndex = 1;

    private readonly HashSet<PlayerHealth> m_damagedPlayers = new();
    private readonly HashSet<MeleeEnemy> m_damagedEnemies = new();
    private PlayerHealth m_target;
    private FirstPersonController m_targetController;
    private NavMeshAgent m_agent;
    private Collider m_headHitbox;
    private MaterialPropertyBlock m_warningProperties;
    private Color m_normalHeadColor;
    private Color m_normalEmissionColor;
    private float m_currentHealth;
    private float m_explosionTime;
    private int m_sourceWeaponSlot;
    private bool m_isWarning;
    private bool m_isDying;
    private bool m_hasExploded;
    private bool m_isMoving;

    private void Awake()
    {
        m_currentHealth = m_maxHealth;
        m_agent = GetComponent<NavMeshAgent>();
        if (m_agent == null)
        {
            m_agent = gameObject.AddComponent<NavMeshAgent>();
        }
        m_agent.speed = m_moveSpeed;
        m_agent.stoppingDistance = Mathf.Max(0f, m_explosionRadius - 0.25f);
        m_agent.updateRotation = true;

        if (m_warningRenderer == null)
        {
            m_warningRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        InitializeWarningMaterial();
        CreateHeadHitbox();
    }

    private void Start()
    {
        if (!m_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            m_agent.Warp(hit.position);
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
        bool destinationAccepted = m_agent.SetDestination(m_target.transform.position);
        bool hasCompletePath = destinationAccepted && m_agent.hasPath && !m_agent.pathPending && m_agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (hasCompletePath && Vector3.Distance(transform.position, m_target.transform.position) <= m_explosionRadius)
        {
            StartWarning();
            return;
        }

        SetMoving(hasCompletePath && m_agent.velocity.sqrMagnitude > 0.01f);
    }

    public void ApplyDamage(float damage, int weaponSlot, bool isHeadshot)
    {
        if (damage <= 0f || m_isDying || m_hasExploded)
        {
            return;
        }

        m_sourceWeaponSlot = Mathf.Clamp(weaponSlot, 1, 3);
        m_currentHealth = Mathf.Max(0f, m_currentHealth - damage);
        if (m_currentHealth <= 0f)
        {
            StartDeathExplosion(m_sourceWeaponSlot);
        }
    }

    public void ApplyExplosionDamage(float damage, int sourceWeaponSlot)
    {
        ApplyDamage(damage, sourceWeaponSlot, false);
    }

    public bool IsHeadHit(Collider hitCollider)
    {
        return hitCollider != null && hitCollider == m_headHitbox;
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
        SetWarningMaterial(true);
    }

    private void StartDeathExplosion(int sourceWeaponSlot)
    {
        m_isDying = true;
        m_sourceWeaponSlot = sourceWeaponSlot;
        if (m_agent.isOnNavMesh)
        {
            m_agent.isStopped = true;
        }
        m_explosionTime = Time.time + m_deathExplosionDelay;
        SetMoving(false);
        SetWarningMaterial(true);
    }

    private void Explode()
    {
        if (m_hasExploded)
        {
            return;
        }

        m_hasExploded = true;
        SetMoving(false);
        foreach (Collider ownCollider in GetComponentsInChildren<Collider>())
        {
            ownCollider.enabled = false;
        }

        int sourceWeaponSlot = ResolveSourceWeaponSlot();
        m_damagedPlayers.Clear();
        m_damagedEnemies.Clear();
        foreach (Collider hit in Physics.OverlapSphere(transform.position, m_explosionRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && m_damagedPlayers.Add(player))
            {
                player.ApplyDamage(m_explosionDamage);
            }

            MeleeEnemy enemy = hit.GetComponentInParent<MeleeEnemy>();
            if (enemy != null && enemy != this && m_damagedEnemies.Add(enemy))
            {
                enemy.ApplyExplosionDamage(m_explosionDamage, sourceWeaponSlot);
            }
        }

        int ammoSlot = ChooseAmmoSlot(sourceWeaponSlot, Random.value);
        AmmoPickup.Create(transform.position, ammoSlot, s_AmmoAmounts[ammoSlot - 1]);
        Destroy(gameObject);
    }

    private int ResolveSourceWeaponSlot()
    {
        if (m_sourceWeaponSlot >= 1 && m_sourceWeaponSlot <= 3)
        {
            return m_sourceWeaponSlot;
        }

        FindTarget();
        return m_targetController != null ? m_targetController.ActiveWeaponSlot : 1;
    }

    private static int ChooseAmmoSlot(int sourceWeaponSlot, float roll)
    {
        sourceWeaponSlot = Mathf.Clamp(sourceWeaponSlot, 1, 3);
        if (roll < 0.2f)
        {
            return sourceWeaponSlot;
        }

        int firstOtherSlot = sourceWeaponSlot == 1 ? 2 : 1;
        int secondOtherSlot = 6 - sourceWeaponSlot - firstOtherSlot;
        return roll < 0.6f ? firstOtherSlot : secondOtherSlot;
    }

    private void InitializeWarningMaterial()
    {
        if (m_warningRenderer == null || m_warningMaterialIndex >= m_warningRenderer.sharedMaterials.Length)
        {
            return;
        }

        Material material = m_warningRenderer.sharedMaterials[m_warningMaterialIndex];
        m_normalHeadColor = material.HasProperty(s_BaseColor) ? material.GetColor(s_BaseColor) : Color.white;
        m_normalEmissionColor = material.HasProperty(s_EmissionColor) ? material.GetColor(s_EmissionColor) : Color.black;
        material.EnableKeyword("_EMISSION");
        m_warningProperties = new MaterialPropertyBlock();
    }

    private void SetWarningMaterial(bool enabled)
    {
        if (m_warningRenderer == null || m_warningProperties == null)
        {
            return;
        }

        m_warningRenderer.GetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
        m_warningProperties.SetColor(s_BaseColor, enabled ? Color.red : m_normalHeadColor);
        m_warningProperties.SetColor(s_EmissionColor, enabled ? Color.red * 3f : m_normalEmissionColor);
        m_warningRenderer.SetPropertyBlock(m_warningProperties, m_warningMaterialIndex);
    }

    private void CreateHeadHitbox()
    {
        if (m_warningRenderer == null)
        {
            return;
        }

        Bounds bounds = m_warningRenderer.bounds;
        GameObject head = new("HeadHitbox");
        head.transform.SetParent(transform, true);
        head.transform.position = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.18f, bounds.center.z);
        SphereCollider collider = head.AddComponent<SphereCollider>();
        collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f;
        m_headHitbox = collider;
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
        Debug.Assert(ChooseAmmoSlot(1, 0.1f) == 1);
        Debug.Assert(ChooseAmmoSlot(1, 0.2f) == 2);
        Debug.Assert(ChooseAmmoSlot(1, 0.6f) == 3);
        Debug.Assert(m_currentHealth >= 0f && m_currentHealth <= m_maxHealth);
        Debug.Assert(!m_hasExploded || m_isDying || m_isWarning);
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_maxHealth = Mathf.Max(1f, m_maxHealth);
        m_explosionDamage = Mathf.Max(0f, m_explosionDamage);
        m_explosionRadius = Mathf.Max(0.1f, m_explosionRadius);
        m_warningDuration = Mathf.Max(0.1f, m_warningDuration);
        m_deathExplosionDelay = Mathf.Max(0f, m_deathExplosionDelay);
        m_warningMaterialIndex = Mathf.Max(0, m_warningMaterialIndex);
    }
}
