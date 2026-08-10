using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EnemyHealth : MonoBehaviour
{
    private static readonly int s_Hit = Animator.StringToHash("Hit");
    private static readonly int s_Die = Animator.StringToHash("Die");
    private static readonly int s_Attack = Animator.StringToHash("Attack");
    private static readonly int s_DeathState = Animator.StringToHash("Base Layer.Death");

    [SerializeField] private float m_maxHealth = 60f;
    [SerializeField] private Renderer m_characterRenderer;
    [SerializeField] private bool m_playHitAndDeathAnimations;
    [SerializeField] private Animator m_animator;
    [SerializeField] private float m_deathRemovalDelay = 1.2f;
    [SerializeField] private AmmoPickup m_ammoPickupPrefab;
    [Header("Humanoid Voice")]
    [SerializeField] private AudioClip[] m_hurtClips;
    [SerializeField] private AudioClip[] m_deathClips;
    [SerializeField] private float m_voiceMaxDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float m_voiceVolume = 1f;

    private Collider m_headHitbox;
    private bool m_hasDroppedAmmo;

    public event Action<KillContext> ZeroHealthReached;
    public float CurrentHealth { get; private set; }
    public bool IsDisabled { get; private set; }
    public EnemyType Type { get; private set; }

    private void Awake()
    {
        CurrentHealth = m_maxHealth;
        Type = ResolveEnemyType();
        if (m_characterRenderer == null)
        {
            m_characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
        CreateHeadHitbox();
    }

    public bool ApplyDamage(float damage, KillContext context)
    {
        if (damage <= 0f || IsDisabled)
        {
            return false;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (CurrentHealth > 0f)
        {
            PlayAnimation(s_Hit);
            PlayHumanoidVoice(m_hurtClips);
            return false;
        }

        IsDisabled = true;
        DisableColliders();
        PlayAnimation(s_Die);
        PlayHumanoidVoice(m_deathClips);
        ZeroHealthReached?.Invoke(context);
        if (Type != EnemyType.Suicide)
        {
            DropAmmo(transform.position, context.WeaponSlot);
        }
        if (m_playHitAndDeathAnimations)
        {
            Destroy(gameObject, m_deathRemovalDelay);
        }
        return true;
    }

    public void DropAmmo(Vector3 deathPosition, int sourceWeaponSlot)
    {
        if (m_hasDroppedAmmo)
        {
            return;
        }

        m_hasDroppedAmmo = true;
        AmmoPickup.CreateDrop(m_ammoPickupPrefab, deathPosition, sourceWeaponSlot);
    }

    public bool ApplyExplosionDamage(float damage, KillContext context)
    {
        return ApplyDamage(damage, context);
    }

    public bool IsHeadHit(Collider hitCollider)
    {
        return hitCollider != null && hitCollider == m_headHitbox;
    }

    public void DisableColliders()
    {
        foreach (Collider ownCollider in GetComponentsInChildren<Collider>())
        {
            ownCollider.enabled = false;
        }
    }

    private void CreateHeadHitbox()
    {
        if (m_characterRenderer == null)
        {
            return;
        }

        Bounds bounds = m_characterRenderer.bounds;
        GameObject head = new("HeadHitbox");
        head.transform.SetParent(transform, true);
        head.transform.position = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.14f, bounds.center.z);
        SphereCollider collider = head.AddComponent<SphereCollider>();
        collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.32f;
        m_headHitbox = collider;
    }

    private EnemyType ResolveEnemyType()
    {
        if (GetComponent<SuicideEnemy>() != null)
        {
            return EnemyType.Suicide;
        }
        if (GetComponent<RangedEnemy>() != null)
        {
            return EnemyType.Ranged;
        }
        return EnemyType.Melee;
    }

    private void PlayAnimation(int trigger)
    {
        if (!m_playHitAndDeathAnimations)
        {
            return;
        }
        if (m_animator == null)
        {
            m_animator = GetComponentInChildren<Animator>();
        }
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            if (trigger == s_Die)
            {
                m_animator.ResetTrigger(s_Attack);
                m_animator.ResetTrigger(s_Hit);
                m_animator.ResetTrigger(s_Die);
                m_animator.CrossFadeInFixedTime(s_DeathState, 0.03f, 0, 0f);
                return;
            }

            m_animator.SetTrigger(trigger);
        }
    }

    private void PlayHumanoidVoice(AudioClip[] clips)
    {
        if (Type != EnemyType.Suicide)
        {
            SpatialAudio.PlayRandomOneShot(clips, transform.position, m_voiceMaxDistance, m_voiceVolume);
        }
    }

    [ContextMenu("Run Enemy Health Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(m_maxHealth > 0f);
        Debug.Assert(CurrentHealth >= 0f && CurrentHealth <= m_maxHealth);
        Debug.Assert(!IsDisabled || CurrentHealth == 0f);
        Debug.Assert(!m_playHitAndDeathAnimations || m_animator != null);
        Debug.Assert(m_deathRemovalDelay >= 0f);
        Debug.Assert(m_ammoPickupPrefab != null);
        Debug.Assert(!m_playHitAndDeathAnimations || (m_hurtClips != null && m_hurtClips.Length == 4));
        Debug.Assert(!m_playHitAndDeathAnimations || (m_deathClips != null && m_deathClips.Length == 2));
    }

    private void OnValidate()
    {
        m_maxHealth = Mathf.Max(1f, m_maxHealth);
        m_deathRemovalDelay = Mathf.Max(0f, m_deathRemovalDelay);
        m_voiceMaxDistance = Mathf.Max(0.1f, m_voiceMaxDistance);
        m_voiceVolume = Mathf.Clamp01(m_voiceVolume);
    }
}
