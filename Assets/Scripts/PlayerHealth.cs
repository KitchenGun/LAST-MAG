using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float m_maxHealth = 100f;
    [SerializeField] private float m_regenerationDelay = 5f;
    [SerializeField] private float m_regenerationPerSecond = 20f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => m_maxHealth;

    private float m_regenerationStartTime;
    private bool m_isDead;

    private void Awake()
    {
        CurrentHealth = m_maxHealth;
    }

    private void Update()
    {
        if (m_isDead || CurrentHealth >= m_maxHealth || Time.time < m_regenerationStartTime)
        {
            return;
        }

        CurrentHealth = Mathf.Min(m_maxHealth, CurrentHealth + m_regenerationPerSecond * Time.deltaTime);
    }

    public void ApplyDamage(float damage)
    {
        if (m_isDead || damage <= 0f)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        m_regenerationStartTime = Time.time + m_regenerationDelay;
        if (CurrentHealth > 0f)
        {
            return;
        }

        m_isDead = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
