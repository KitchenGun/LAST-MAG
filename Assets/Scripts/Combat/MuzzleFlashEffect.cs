using UnityEngine;

[RequireComponent(typeof(ParticleSystem), typeof(Light))]
public sealed class MuzzleFlashEffect : MonoBehaviour
{
    [SerializeField] private float m_lightDuration = 0.04f;
    [SerializeField] private Material[] m_variations;

    private ParticleSystem m_particles;
    private ParticleSystemRenderer m_particleRenderer;
    private Light m_light;
    private float m_lightOffTime;
    private int m_lastVariationIndex = -1;

    private void Awake()
    {
        CacheComponents();
        StopEffect();
    }

    private void Update()
    {
        if (m_light != null && m_light.enabled && Time.time >= m_lightOffTime)
        {
            m_light.enabled = false;
        }
    }

    private void OnDisable()
    {
        StopEffect();
    }

    public void Play()
    {
        CacheComponents();
        m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ApplyRandomVariation();
        m_particles.Play(true);
        m_light.enabled = true;
        m_lightOffTime = Time.time + m_lightDuration;
    }

    public void StopEffect()
    {
        CacheComponents();
        m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        m_light.enabled = false;
    }

    private void CacheComponents()
    {
        if (m_particles == null)
        {
            m_particles = GetComponent<ParticleSystem>();
        }

        if (m_particleRenderer == null)
        {
            m_particleRenderer = GetComponent<ParticleSystemRenderer>();
        }

        if (m_light == null)
        {
            m_light = GetComponent<Light>();
        }
    }

    private void ApplyRandomVariation()
    {
        if (m_variations == null || m_variations.Length == 0)
        {
            return;
        }

        int variationIndex = Random.Range(0, m_variations.Length);
        if (m_variations.Length > 1 && variationIndex == m_lastVariationIndex)
        {
            variationIndex = (variationIndex + Random.Range(1, m_variations.Length)) % m_variations.Length;
        }

        m_lastVariationIndex = variationIndex;
        m_particleRenderer.sharedMaterial = m_variations[variationIndex];
    }

    private void OnValidate()
    {
        m_lightDuration = Mathf.Max(0.01f, m_lightDuration);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.46f, 0.09f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.025f);
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.1f);
    }
}
