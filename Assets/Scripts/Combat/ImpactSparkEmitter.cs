using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ImpactSparkEmitter : MonoBehaviour
{
    private const int k_MinParticlesPerImpact = 4;
    private const int k_MaxParticlesPerImpact = 7;

    private ParticleSystem m_particles;

    private void Awake()
    {
        TryCacheParticles();
    }

    public void EmitAt(Vector3 position, Vector3 normal)
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

        if (!Application.isPlaying)
        {
            Debug.Log("Impact spark configuration self check passed. Run it in Play Mode to verify emission count.", this);
            return;
        }

        m_particles.Clear(true);
        EmitAt(transform.position, Vector3.up);
        Debug.Assert(m_particles.particleCount >= k_MinParticlesPerImpact
            && m_particles.particleCount <= k_MaxParticlesPerImpact);
        m_particles.Clear(true);
    }
}
