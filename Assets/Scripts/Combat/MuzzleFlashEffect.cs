using UnityEngine;

[RequireComponent(typeof(ParticleSystem), typeof(Light))]
public sealed class MuzzleFlashEffect : MonoBehaviour
{
    [SerializeField] private float m_lightDuration = 0.04f;
    [SerializeField] private Material[] m_variations;
    [SerializeField] private ParticleSystem m_smokeParticles;
    [SerializeField] private int m_smokeParticleCount;
    [SerializeField] private int m_smokeEveryShots = 1;

    private ParticleSystem m_particles;
    private ParticleSystemRenderer m_particleRenderer;
    private Light m_light;
    private float m_lightOffTime;
    private int m_lastVariationIndex = -1;
    private int m_smokeShotCounter;

    private void Awake()
    {
        CacheComponents();
        UseScaledTime(m_particles);
        UseScaledTime(m_smokeParticles);
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
        EmitMuzzleSmoke();
        m_light.enabled = true;
        m_lightOffTime = Time.time + m_lightDuration;
    }

    public void StopEffect()
    {
        CacheComponents();
        m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        m_light.enabled = false;
    }

    private void EmitMuzzleSmoke()
    {
        if (m_smokeParticles == null || m_smokeParticleCount <= 0)
        {
            return;
        }

        m_smokeShotCounter++;
        if ((m_smokeShotCounter - 1) % Mathf.Max(1, m_smokeEveryShots) != 0)
        {
            return;
        }

        if (!m_smokeParticles.isPlaying)
        {
            m_smokeParticles.Play(true);
        }

        Vector3 direction = transform.right;
        bool heavySmoke = m_smokeParticleCount >= 6;
        GetSmokeRanges(out Vector2 lifetime, out Vector2 size, out Vector2 speed);
        for (int index = 0; index < m_smokeParticleCount; index++)
        {
            float layer = m_smokeParticleCount > 1 ? index / (m_smokeParticleCount - 1f) : 0f;
            float spread = heavySmoke ? Mathf.Lerp(0.16f, 0.42f, layer) : 0.3f;
            Vector3 spreadDirection = (direction + UnityEngine.Random.insideUnitSphere * spread).normalized;
            ParticleSystem.EmitParams smoke = new()
            {
                position = transform.position + direction * 0.035f
                    + (heavySmoke ? UnityEngine.Random.insideUnitSphere * 0.012f : Vector3.zero),
                velocity = spreadDirection * (heavySmoke
                    ? Mathf.Lerp(speed.y, speed.x, layer)
                    : UnityEngine.Random.Range(speed.x, speed.y))
                    + Vector3.up * (heavySmoke ? Mathf.Lerp(0.12f, 0.36f, layer) : 0.18f),
                startLifetime = UnityEngine.Random.Range(lifetime.x, lifetime.y),
                startSize = UnityEngine.Random.Range(size.x, size.y),
                rotation = UnityEngine.Random.Range(0f, 360f),
                startColor = new Color(0.58f, 0.6f, 0.62f,
                    UnityEngine.Random.Range(heavySmoke ? 0.22f : 0.18f, heavySmoke ? 0.34f : 0.28f))
            };
            m_smokeParticles.Emit(smoke, 1);
        }
    }

    private void GetSmokeRanges(out Vector2 lifetime, out Vector2 size, out Vector2 speed)
    {
        if (m_smokeParticleCount >= 6)
        {
            lifetime = new Vector2(0.45f, 0.8f);
            size = new Vector2(0.18f, 0.34f);
            speed = new Vector2(0.42f, 1.2f);
            return;
        }
        if (m_smokeParticleCount >= 4)
        {
            lifetime = new Vector2(0.22f, 0.38f);
            size = new Vector2(0.12f, 0.2f);
            speed = new Vector2(0.8f, 1.5f);
            return;
        }
        if (m_smokeEveryShots >= 3)
        {
            lifetime = new Vector2(0.12f, 0.22f);
            size = new Vector2(0.05f, 0.09f);
            speed = new Vector2(0.8f, 1.4f);
            return;
        }

        lifetime = new Vector2(0.18f, 0.3f);
        size = new Vector2(0.06f, 0.11f);
        speed = new Vector2(0.8f, 1.4f);
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

    private static void UseScaledTime(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.useUnscaledTime = false;
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
        m_smokeParticleCount = Mathf.Max(0, m_smokeParticleCount);
        m_smokeEveryShots = Mathf.Max(1, m_smokeEveryShots);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.46f, 0.09f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.025f);
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.1f);
    }
}
