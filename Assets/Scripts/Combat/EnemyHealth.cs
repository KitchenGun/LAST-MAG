using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EnemyHealth : MonoBehaviour
{
    private const float k_AmmoDropChance = 0.5f;
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
    private Collider[] m_colliders;
    private bool[] m_colliderDefaults;
    private bool m_hasDroppedAmmo;
    private GameplayObjectPool m_pool;

    public event Action<KillContext> ZeroHealthReached;
    public float CurrentHealth { get; private set; }
    public bool IsDisabled { get; private set; }
    public bool IsPooled { get; private set; }
    public EnemyType Type { get; private set; }
    public GameplayObjectPool Pool => m_pool;

    private void Awake()
    {
        CurrentHealth = m_maxHealth;
        Type = ResolveEnemyType();
        if (m_characterRenderer == null)
        {
            m_characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
        CreateHeadHitbox();
        CacheColliders();
        if (m_animator == null)
        {
            m_animator = GetComponentInChildren<Animator>();
        }
        if (m_animator != null && Type != EnemyType.Suicide)
        {
            m_animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
    }

    public void PrepareForSpawn(Vector3 position, Quaternion rotation, GameplayObjectPool pool)
    {
        StopAllCoroutines();
        transform.SetPositionAndRotation(position, rotation);
        m_pool = pool;
        CurrentHealth = m_maxHealth;
        IsDisabled = false;
        IsPooled = false;
        m_hasDroppedAmmo = false;
        RestoreColliders();
        if (m_animator != null && m_animator.runtimeAnimatorController != null)
        {
            m_animator.Rebind();
        }
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
            DropAmmo(transform.position);
        }
        if (Type != EnemyType.Suicide)
        {
            StartCoroutine(ReturnAfterDelay(m_playHitAndDeathAnimations ? m_deathRemovalDelay : 0f));
        }
        return true;
    }

    public void DropAmmo(Vector3 deathPosition)
    {
        if (m_hasDroppedAmmo)
        {
            return;
        }

        m_hasDroppedAmmo = true;
        if (UnityEngine.Random.value >= k_AmmoDropChance)
        {
            return;
        }

        if (m_pool != null)
        {
            m_pool.SpawnAmmoDrop(deathPosition);
        }
        else
        {
            AmmoPickup.CreateDrop(m_ammoPickupPrefab, deathPosition);
        }
    }

    public bool ApplyExplosionDamage(float damage, KillContext context)
    {
        return ApplyDamage(damage, context);
    }

    public bool IsHeadHit(Ray shotRay, float maxDistance)
    {
        return m_headHitbox != null
            && m_headHitbox.enabled
            && maxDistance > 0f
            && m_headHitbox.Raycast(shotRay, out _, maxDistance);
    }

    public void DisableColliders()
    {
        foreach (Collider ownCollider in m_colliders)
        {
            ownCollider.enabled = false;
        }
    }

    public void ReturnToPool()
    {
        if (IsPooled)
        {
            return;
        }

        if (m_pool != null)
        {
            m_pool.ReleaseEnemy(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    internal void MarkPooled()
    {
        IsPooled = true;
    }

    private void CreateHeadHitbox()
    {
        GameObject head = new("HeadHitbox");
        head.transform.SetParent(transform, false);
        if (Type == EnemyType.Suicide)
        {
            head.transform.localPosition = new Vector3(-0.0004f, 0.945f, -0.0004f);
        }
        else
        {
            head.transform.localPosition = new Vector3(-0.006f, 1.8619f, -0.013f);
        }

        SphereCollider collider = head.AddComponent<SphereCollider>();
        collider.radius = Type == EnemyType.Suicide ? 0.291f : 0.282f;
        collider.isTrigger = true;
        m_headHitbox = collider;
    }

    private void CacheColliders()
    {
        m_colliders = GetComponentsInChildren<Collider>(true);
        m_colliderDefaults = new bool[m_colliders.Length];
        for (int index = 0; index < m_colliders.Length; index++)
        {
            m_colliderDefaults[index] = m_colliders[index].enabled;
        }
    }

    private void RestoreColliders()
    {
        for (int index = 0; index < m_colliders.Length; index++)
        {
            m_colliders[index].enabled = m_colliderDefaults[index];
        }
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        ReturnToPool();
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
        Debug.Assert(m_headHitbox == null || m_headHitbox.isTrigger);
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
