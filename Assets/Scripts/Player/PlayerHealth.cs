using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScoreSystem))]
public sealed class PlayerHealth : MonoBehaviour
{
    private const float k_CriticalTraumaThreshold = 0.3f;
    private const float k_CriticalTraumaRearmThreshold = 0.55f;

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
    private bool m_criticalTraumaAnnounced;
    private bool m_criticalTraumaPlaying;
    private bool m_abilityReadyQueued;

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
        SceneManager.LoadScene("ResultScene");
    }

    private void NotifyHealthChanged()
    {
        HealthNormalizedChanged?.Invoke(CurrentHealth / m_maxHealth);
    }

    private void InitializeSystemVoice()
    {
        m_criticalTraumaClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_CriticalTraumaDetected");
        m_abilityReadyClip = Resources.Load<AudioClip>("Audio/SystemVoice/SFX_AbilityReady");
        if (m_criticalTraumaClip == null)
        {
            Debug.LogWarning("Critical trauma warning audio is missing.");
        }
        if (m_abilityReadyClip == null)
        {
            Debug.LogWarning("Ability ready system voice is missing.");
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
        m_criticalTraumaPlaying = true;
        m_abilityReadyQueued = false;
        m_systemVoiceSource.Stop();
        StartCoroutine(PlayCriticalTraumaSequence());
    }

    private IEnumerator PlayCriticalTraumaSequence()
    {
        m_systemVoiceSource.PlayOneShot(m_criticalTraumaClip);
        yield return new WaitForSecondsRealtime(m_criticalTraumaClip.length);
        m_criticalTraumaPlaying = false;
        if (m_abilityReadyQueued)
        {
            m_abilityReadyQueued = false;
            m_systemVoiceSource.PlayOneShot(m_abilityReadyClip);
        }
    }

    internal void PlayAbilityReadyAnnouncement()
    {
        if (m_systemVoiceSource == null || m_abilityReadyClip == null)
        {
            return;
        }
        if (m_criticalTraumaPlaying)
        {
            m_abilityReadyQueued = true;
            return;
        }
        m_systemVoiceSource.PlayOneShot(m_abilityReadyClip);
    }

    [ContextMenu("Run Health Self Check")]
    private void RunHealthSelfCheck()
    {
        Debug.Assert(Mathf.Min(100f, 80f + 20f) == 100f);
        Debug.Assert(Mathf.Max(0f, 20f - 30f) == 0f);
    }

    private void OnValidate()
    {
        m_maxHealth = Mathf.Max(1f, m_maxHealth);
        m_regenerationDelay = Mathf.Max(0f, m_regenerationDelay);
        m_regenerationPerSecond = Mathf.Max(0f, m_regenerationPerSecond);
    }
}
