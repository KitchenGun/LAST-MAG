using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ImpactSparkEmitter : MonoBehaviour
{
    private const int k_MinParticlesPerImpact = 4;
    private const int k_MaxParticlesPerImpact = 7;
    private const int k_MinBiologicalParticles = 4;
    private const int k_MaxBiologicalParticles = 6;
    private const int k_MinHeadshotParticles = 7;
    private const int k_MaxHeadshotParticles = 9;
    private const int k_GrenadeExplosionParticles = 22;
    private const int k_RocketExplosionParticles = 30;

    private ParticleSystem m_particles;
    [SerializeField] private ParticleSystem m_biologicalParticles;

    private void Awake()
    {
        TryCacheParticles();
    }

    public void EmitSurfaceAt(Vector3 position, Vector3 normal)
    {
        if (!TryCacheParticles())
        {
            return;
        }

        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        int particleCount = Random.Range(k_MinParticlesPerImpact, k_MaxParticlesPerImpact + 1);
        Vector3 emissionPosition = position + normal * 0.01f;

        for (int index = 0; index < particleCount; index++)
        {
            Vector3 direction = (normal + Random.insideUnitSphere * 0.85f).normalized;
            ParticleSystem.EmitParams emitParams = new()
            {
                position = emissionPosition,
                velocity = direction * Random.Range(1.5f, 3.5f),
                startLifetime = Random.Range(0.08f, 0.18f),
                startSize = Random.Range(0.008f, 0.018f),
                startColor = new Color(1f, Random.Range(0.78f, 0.95f), Random.Range(0.35f, 0.65f), 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    public void EmitBiologicalAt(Vector3 position, Vector3 normal, bool isHeadshot)
    {
        if (m_biologicalParticles == null)
        {
            return;
        }

        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        int minCount = isHeadshot ? k_MinHeadshotParticles : k_MinBiologicalParticles;
        int maxCount = isHeadshot ? k_MaxHeadshotParticles : k_MaxBiologicalParticles;
        int particleCount = Random.Range(minCount, maxCount + 1);
        Vector3 emissionPosition = position + normal * 0.012f;

        for (int index = 0; index < particleCount; index++)
        {
            Vector3 direction = (normal + Random.insideUnitSphere * 1.15f).normalized;
            Color color = Random.value < 0.65f
                ? new Color(0.44f, 0.5f, 0.27f, 0.92f)
                : new Color(0.48f, 0.25f, 0.2f, 0.9f);
            ParticleSystem.EmitParams emitParams = new()
            {
                position = emissionPosition,
                velocity = direction * Random.Range(0.6f, 1.8f),
                startLifetime = Random.Range(0.08f, 0.16f),
                startSize = Random.Range(0.015f, 0.035f),
                startColor = color,
                rotation = Random.Range(0f, 360f)
            };
            m_biologicalParticles.Emit(emitParams, 1);
        }
    }

    internal void EmitExplosionAt(Vector3 position, float radius, bool isRocket)
    {
        if (!TryCacheParticles() || radius <= 0f)
        {
            return;
        }

        int particleCount = isRocket ? k_RocketExplosionParticles : k_GrenadeExplosionParticles;
        float gravity = Physics.gravity.magnitude * Mathf.Max(0f, m_particles.main.gravityModifier.constantMax);
        for (int index = 0; index < particleCount; index++)
        {
            float lifetime = Random.Range(0.25f, 0.55f);
            float size = Random.Range(0.012f, 0.03f);
            float gravityTravel = 0.5f * gravity * lifetime * lifetime;
            float safeTravel = Mathf.Max(0f, radius * 0.85f - gravityTravel - size * 2f);
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) + 0.18f;
            direction.Normalize();
            ParticleSystem.EmitParams emitParams = new()
            {
                position = position,
                velocity = direction * (safeTravel * Random.Range(0.55f, 1f) / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = Random.value < 0.55f
                    ? new Color(1f, Random.Range(0.72f, 0.95f), Random.Range(0.24f, 0.5f), 1f)
                    : new Color(1f, 0.97f, Random.Range(0.72f, 0.92f), 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    private bool TryCacheParticles()
    {
        if (m_particles == null)
        {
            m_particles = GetComponent<ParticleSystem>();
        }

        return m_particles != null;
    }

    [ContextMenu("Run Impact Spark Self Check")]
    private void RunSelfCheck()
    {
        if (!TryCacheParticles())
        {
            Debug.LogError("Impact spark self check failed: ParticleSystem is missing.", this);
            return;
        }

        ParticleSystem.MainModule main = m_particles.main;
        Debug.Assert(main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(main.maxParticles == 128);
        Debug.Assert(m_biologicalParticles != null);

        if (!Application.isPlaying)
        {
            Debug.Log("Impact spark configuration self check passed. Run it in Play Mode to verify emission count.", this);
            return;
        }

        m_particles.Clear(true);
        EmitSurfaceAt(transform.position, Vector3.up);
        Debug.Assert(m_particles.particleCount >= k_MinParticlesPerImpact
            && m_particles.particleCount <= k_MaxParticlesPerImpact);
        m_particles.Clear(true);
        EmitExplosionAt(transform.position, 4f, true);
        Debug.Assert(m_particles.particleCount == k_RocketExplosionParticles);
        AssertExplosionRadius(m_particles, 4f);
        m_particles.Clear(true);
        m_biologicalParticles.Clear(true);
        EmitBiologicalAt(transform.position, Vector3.up, true);
        Debug.Assert(m_biologicalParticles.particleCount >= k_MinHeadshotParticles
            && m_biologicalParticles.particleCount <= k_MaxHeadshotParticles);
        m_biologicalParticles.Clear(true);
    }

    private static void AssertExplosionRadius(ParticleSystem particles, float radius)
    {
        ParticleSystem.Particle[] emitted = new ParticleSystem.Particle[particles.particleCount];
        int count = particles.GetParticles(emitted);
        float gravity = Physics.gravity.magnitude * Mathf.Max(0f, particles.main.gravityModifier.constantMax);
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = emitted[index];
            float reach = particle.velocity.magnitude * particle.startLifetime
                + 0.5f * gravity * particle.startLifetime * particle.startLifetime
                + particle.startSize * 2f;
            Debug.Assert(reach <= radius + 0.02f);
        }
    }
}
