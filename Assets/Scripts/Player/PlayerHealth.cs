using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScoreSystem))]
public sealed class PlayerHealth : MonoBehaviour
{
    private enum SystemVoice
    {
        None,
        AbilityReady,
        AmmunitionZero,
        CriticalTrauma,
        Death
    }

    private const float k_CriticalTraumaThreshold = 0.5f;
    private const float k_CriticalTraumaRearmThreshold = 0.6f;
    private const float k_DeathPresentationMinimumDuration = 3.3f;

    [SerializeField] private float m_maxHealth = 100f;
    [SerializeField] private float m_regenerationDelay = 5f;
    [SerializeField] private float m_regenerationPerSecond = 20f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => m_maxHealth;
    public event Action<float> HealthNormalizedChanged;

    private float m_regenerationStartTime;
    private bool m_isDead;
    private ScoreSystem m_scoreSystem;
    private FirstPersonController m_firstPersonController;
    private AudioSource m_systemVoiceSource;
    private AudioClip m_criticalTraumaClip;
    private AudioClip m_abilityReadyClip;
    private AudioClip m_ammunitionZeroClip;
    private AudioClip m_deathClip;
    private bool m_criticalTraumaAnnounced;
    private bool m_abilityReadyQueued;
    private bool m_ammunitionZeroQueued;
    private SystemVoice m_activeSystemVoice;
    private Coroutine m_systemVoiceCoroutine;
    private Coroutine m_deathPresentationCoroutine;

    private void Awake()
    {
        m_scoreSystem = GetComponent<ScoreSystem>();
        m_firstPersonController = GetComponent<FirstPersonController>();
        Debug.Assert(m_firstPersonController != null, "PF_Player is missing FirstPersonController.");
        CurrentHealth = m_maxHealth;
        InitializeSystemVoice();
    }

    private void Update()
    {
        if (m_isDead || CurrentHealth >= m_maxHealth || Time.unscaledTime < m_regenerationStartTime)
        {
            return;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(m_maxHealth, CurrentHealth + m_regenerationPerSecond * Time.unscaledDeltaTime);
        if (!Mathf.Approximately(previousHealth, CurrentHealth))
        {
            NotifyHealthChanged();
        }
        if (CurrentHealth / m_maxHealth >= k_CriticalTraumaRearmThreshold)
        {
            m_criticalTraumaAnnounced = false;
        }
    }

    public void ApplyDamage(float damage, PlayerDeathCause deathCause)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (EnemySpawner.IsStressTestActive)
        {
            return;
        }
#endif
        if (m_isDead || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        NotifyHealthChanged();
        m_firstPersonController?.ApplyDamageAimPunch(deathCause);
        m_regenerationStartTime = Time.unscaledTime + m_regenerationDelay;
        if (CurrentHealth > 0f)
        {
            PlayCriticalTraumaWarning();
            return;
        }

        m_isDead = true;
        m_scoreSystem.CompleteRun(deathCause);
        PlayDeathAnnouncement();
        float duration = Mathf.Max(k_DeathPresentationMinimumDuration,
            m_deathClip != null ? m_deathClip.length : 0f);
        m_firstPersonController?.BeginDeathPresentation(duration);
        m_deathPresentationCoroutine = StartCoroutine(LoadResultAfterDeathPresentation(duration));
    }

    private void NotifyHealthChanged()
    {
        HealthNormalizedChanged?.Invoke(CurrentHealth / m_maxHealth);
    }

    private void InitializeSystemVoice()
    {
        m_criticalTraumaClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_CriticalTraumaDetected");
        m_abilityReadyClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_AbilityReady");
        m_ammunitionZeroClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_AmmunitionZero");
        m_deathClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_Death_VitalSignsLow");
        if (m_criticalTraumaClip == null)
        {
            Debug.LogWarning("Critical trauma warning audio is missing.");
        }
        if (m_abilityReadyClip == null)
        {
            Debug.LogWarning("Ability ready system voice is missing.");
        }
        if (m_ammunitionZeroClip == null)
        {
            Debug.LogWarning("Ammunition zero system voice is missing.");
        }
        if (m_deathClip == null)
        {
            Debug.LogWarning("Death system voice is missing.");
        }

        m_systemVoiceSource = gameObject.AddComponent<AudioSource>();
        m_systemVoiceSource.playOnAwake = false;
        m_systemVoiceSource.loop = false;
        m_systemVoiceSource.spatialBlend = 0f;
        m_systemVoiceSource.volume = 0.85f;
    }

    private void PlayCriticalTraumaWarning()
    {
        if (m_criticalTraumaAnnounced || m_systemVoiceSource == null
            || m_criticalTraumaClip == null
            || CurrentHealth / m_maxHealth > k_CriticalTraumaThreshold)
        {
            return;
        }

        m_criticalTraumaAnnounced = true;
        StopActiveSystemVoice();
        PlaySystemVoice(SystemVoice.CriticalTrauma, m_criticalTraumaClip);
    }

    internal void PlayAmmunitionZeroAnnouncement()
    {
        if (m_systemVoiceSource == null || m_ammunitionZeroClip == null
            || m_activeSystemVoice == SystemVoice.AmmunitionZero)
        {
            return;
        }
        if (m_activeSystemVoice == SystemVoice.CriticalTrauma)
        {
            m_ammunitionZeroQueued = true;
            return;
        }
        if (m_activeSystemVoice == SystemVoice.AbilityReady)
        {
            m_abilityReadyQueued = true;
            StopActiveSystemVoice();
        }
        PlaySystemVoice(SystemVoice.AmmunitionZero, m_ammunitionZeroClip);
    }

    internal void PlayAbilityReadyAnnouncement()
    {
        if (m_systemVoiceSource == null || m_abilityReadyClip == null)
        {
            return;
        }
        if (m_activeSystemVoice is SystemVoice.CriticalTrauma or SystemVoice.AmmunitionZero)
        {
            m_abilityReadyQueued = true;
            return;
        }
        if (m_activeSystemVoice != SystemVoice.AbilityReady)
        {
            PlaySystemVoice(SystemVoice.AbilityReady, m_abilityReadyClip);
        }
    }

    private void PlaySystemVoice(SystemVoice voice, AudioClip clip)
    {
        m_activeSystemVoice = voice;
        m_systemVoiceCoroutine = StartCoroutine(PlaySystemVoiceSequence(clip));
    }

    private IEnumerator PlaySystemVoiceSequence(AudioClip clip)
    {
        m_systemVoiceSource.clip = clip;
        m_systemVoiceSource.Play();
        yield return new WaitForSecondsRealtime(clip.length);
        m_activeSystemVoice = SystemVoice.None;
        m_systemVoiceCoroutine = null;
        if (m_ammunitionZeroQueued)
        {
            m_ammunitionZeroQueued = false;
            PlaySystemVoice(SystemVoice.AmmunitionZero, m_ammunitionZeroClip);
        }
        else if (m_abilityReadyQueued)
        {
            m_abilityReadyQueued = false;
            PlaySystemVoice(SystemVoice.AbilityReady, m_abilityReadyClip);
        }
    }

    private void StopActiveSystemVoice()
    {
        if (m_systemVoiceCoroutine != null)
        {
            StopCoroutine(m_systemVoiceCoroutine);
            m_systemVoiceCoroutine = null;
        }
        m_systemVoiceSource.Stop();
        m_activeSystemVoice = SystemVoice.None;
    }

    private void PlayDeathAnnouncement()
    {
        StopActiveSystemVoice();
        m_abilityReadyQueued = false;
        m_ammunitionZeroQueued = false;
        if (m_systemVoiceSource == null || m_deathClip == null)
        {
            return;
        }

        m_activeSystemVoice = SystemVoice.Death;
        m_systemVoiceSource.clip = m_deathClip;
        m_systemVoiceSource.volume = 1f;
        m_systemVoiceSource.Play();
    }

    private IEnumerator LoadResultAfterDeathPresentation(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        SceneManager.LoadScene("ResultScene");
    }

    [ContextMenu("Run Health Self Check")]
    private void RunHealthSelfCheck()
    {
        Debug.Assert(Mathf.Min(100f, 80f + 20f) == 100f);
        Debug.Assert(Mathf.Max(0f, 20f - 30f) == 0f);
        Debug.Assert(Mathf.Approximately(k_CriticalTraumaThreshold, 0.5f));
        Debug.Assert(Mathf.Approximately(k_CriticalTraumaRearmThreshold, 0.6f));
        Debug.Assert(0.5f <= k_CriticalTraumaThreshold && 0.51f > k_CriticalTraumaThreshold);
        Debug.Assert(0.59f < k_CriticalTraumaRearmThreshold && 0.6f >= k_CriticalTraumaRearmThreshold);
        Debug.Assert(k_DeathPresentationMinimumDuration >= 3.3f);
    }

    private void OnValidate()
    {
        m_maxHealth = Mathf.Max(1f, m_maxHealth);
        m_regenerationDelay = Mathf.Max(0f, m_regenerationDelay);
        m_regenerationPerSecond = Mathf.Max(0f, m_regenerationPerSecond);
    }
}
