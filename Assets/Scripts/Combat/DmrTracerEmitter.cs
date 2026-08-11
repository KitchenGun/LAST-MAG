using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class DmrTracerEmitter : MonoBehaviour
{
    private const float k_MinTravelTime = 0.06f;
    private const float k_MaxTravelTime = 0.1f;
    private const float k_VisualSpeed = 1000f;
    private const float k_TracerWidth = 0.026f;
    private const float k_LaunchStreakSpeed = 25f;
    private const float k_LaunchStreakLifetime = 0.08f;
    private const float k_LaunchStreakWidth = 0.038f;
    private const int k_SmokeParticleCount = 4;

    [SerializeField] private ParticleSystem m_muzzleSmoke;

    private ParticleSystem m_particles;

    private void Awake()
    {
        CacheParticles();
    }

    private void OnDisable()
    {
        StopEffect();
    }

    public void EmitTo(Vector3 endPoint)
    {
        if (!CacheParticles())
        {
            return;
        }

        Vector3 displacement = endPoint - transform.position;
        float distance = displacement.magnitude;
        if (distance <= 0.05f)
        {
            return;
        }

        float travelTime = Mathf.Clamp(distance / k_VisualSpeed, k_MinTravelTime, k_MaxTravelTime);
        if (!m_particles.isPlaying)
        {
            m_particles.Play(true);
        }

        Vector3 direction = displacement / distance;
        EmitMuzzleSmoke(direction);

        ParticleSystem.EmitParams launchStreak = new()
        {
            position = transform.position,
            velocity = direction * k_LaunchStreakSpeed,
            startLifetime = k_LaunchStreakLifetime,
            startSize = k_LaunchStreakWidth,
            startColor = new Color(1f, 0.96f, 0.86f, 0.8f)
        };
        m_particles.Emit(launchStreak, 1);

        ParticleSystem.EmitParams emitParams = new()
        {
            position = transform.position,
            velocity = displacement / travelTime,
            startLifetime = travelTime,
            startSize = k_TracerWidth,
            startColor = new Color(1f, 0.96f, 0.86f, 0.72f)
        };
        m_particles.Emit(emitParams, 1);
    }

    private void EmitMuzzleSmoke(Vector3 direction)
    {
        if (m_muzzleSmoke == null)
        {
            return;
        }

        if (!m_muzzleSmoke.isPlaying)
        {
            m_muzzleSmoke.Play(true);
        }

        for (int index = 0; index < k_SmokeParticleCount; index++)
        {
            Vector3 spreadDirection = (direction + UnityEngine.Random.insideUnitSphere * 0.28f).normalized;
            ParticleSystem.EmitParams smoke = new()
            {
                position = transform.position + direction * 0.035f,
                velocity = spreadDirection * UnityEngine.Random.Range(0.8f, 1.5f) + Vector3.up * 0.18f,
                startLifetime = UnityEngine.Random.Range(0.22f, 0.38f),
                startSize = UnityEngine.Random.Range(0.12f, 0.2f),
                rotation = UnityEngine.Random.Range(0f, 360f),
                startColor = new Color(0.62f, 0.64f, 0.66f, UnityEngine.Random.Range(0.18f, 0.28f))
            };
            m_muzzleSmoke.Emit(smoke, 1);
        }
    }

    public void StopEffect()
    {
        if (CacheParticles())
        {
            m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (m_muzzleSmoke != null)
        {
            m_muzzleSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private bool CacheParticles()
    {
        if (m_particles == null)
        {
            m_particles = GetComponent<ParticleSystem>();
        }

        return m_particles != null;
    }

    [ContextMenu("Run DMR Tracer Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(CacheParticles());
        ParticleSystem.MainModule main = m_particles.main;
        Debug.Assert(main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(main.maxParticles == 8 && main.useUnscaledTime);
        Debug.Assert(!m_particles.emission.enabled && !m_particles.shape.enabled
            && !m_particles.collision.enabled && !m_particles.trails.enabled && !m_particles.lights.enabled);
        Debug.Assert(m_muzzleSmoke != null && m_muzzleSmoke.main.simulationSpace == ParticleSystemSimulationSpace.World);
    }
}
