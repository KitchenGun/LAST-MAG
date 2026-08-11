using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScoreSystem))]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float m_maxHealth = 100f;
    [SerializeField] private float m_regenerationDelay = 5f;
    [SerializeField] private float m_regenerationPerSecond = 20f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => m_maxHealth;
    public event Action<float> HealthNormalizedChanged;

    private float m_regenerationStartTime;
    private bool m_isDead;
    private ScoreSystem m_scoreSystem;

    private void Awake()
    {
        m_scoreSystem = GetComponent<ScoreSystem>();
        CurrentHealth = m_maxHealth;
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
    }

    public void ApplyDamage(float damage, PlayerDeathCause deathCause)
    {
        if (m_isDead || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        NotifyHealthChanged();
        m_regenerationStartTime = Time.unscaledTime + m_regenerationDelay;
        if (CurrentHealth > 0f)
        {
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
