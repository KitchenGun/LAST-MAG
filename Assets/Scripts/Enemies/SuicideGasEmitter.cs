using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class SuicideGasEmitter : MonoBehaviour
{
    private const int k_ParticlesPerExplosion = 48;
    private const int k_ParticlesPerWarningPulse = 4;
    private const float k_EffectDuration = 1.1f;
    private const float k_GroundOffset = 0.25f;
    private const float k_MaxSizeMultiplier = 1.25f;
    private const float k_MinWarningAlphaMultiplier = 0.25f;
    private const float k_MaxWarningAlphaMultiplier = 0.6f;
    private static readonly Color k_LightGasColor = new Color32(200, 182, 74, 84);
    private static readonly Color k_DarkGasColor = new Color32(120, 109, 36, 108);

    private ParticleSystem m_particles;

    private void Awake()
    {
        TryCacheParticles();
    }

    public void EmitAt(Vector3 position, float radius)
    {
        Emit(position, radius, k_ParticlesPerExplosion, 1f);
    }

    public void EmitWarningAt(Vector3 position, float radius, float progress)
    {
        Emit(position, radius, k_ParticlesPerWarningPulse,
            Mathf.Lerp(k_MinWarningAlphaMultiplier, k_MaxWarningAlphaMultiplier, Mathf.Clamp01(progress)));
    }

    private void Emit(Vector3 position, float radius, int particleCount, float alphaMultiplier)
    {
        if (radius <= 0f || !TryCacheParticles())
        {
            return;
        }

        Vector3 origin = position + Vector3.up * k_GroundOffset;
        for (int index = 0; index < particleCount; index++)
        {
            float lifetime = Random.Range(0.85f, k_EffectDuration);
            float size = Mathf.Min(Random.Range(0.8f, 1.35f), radius);
            float speed = Random.Range(0.1f, 0.35f);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float edgeAllowance = size * k_MaxSizeMultiplier * 0.5f + speed * lifetime;
            float maxCenterDistance = Mathf.Max(0f, radius - edgeAllowance);
            float centerDistance = Mathf.Sqrt(Random.value) * maxCenterDistance;

            Color color = Color.Lerp(k_DarkGasColor, k_LightGasColor, Random.value);
            color.a *= alphaMultiplier;
            ParticleSystem.EmitParams emitParams = new()
            {
                position = origin
                    + direction * centerDistance
                    + Vector3.up * Random.Range(0f, radius * 0.16f),
                velocity = direction * speed + Vector3.up * Random.Range(0.08f, 0.25f),
                startLifetime = lifetime,
                startSize = size,
                startColor = color,
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

    [ContextMenu("Run Suicide Gas Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(TryCacheParticles());
        ParticleSystem.MainModule main = m_particles.main;
        Debug.Assert(main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(main.maxParticles == 256);
        Debug.Assert(!main.loop && !main.playOnAwake);
        Debug.Assert(!m_particles.emission.enabled);
        Debug.Assert(!m_particles.shape.enabled);
        Debug.Assert(!m_particles.collision.enabled);
        Debug.Assert(!m_particles.trails.enabled);
        Debug.Assert(!m_particles.lights.enabled);
        Debug.Assert(k_ParticlesPerExplosion == 48);
        Debug.Assert(k_ParticlesPerWarningPulse == 4);
        Debug.Assert(Mathf.Approximately(k_MinWarningAlphaMultiplier, 0.25f));
        Debug.Assert(Mathf.Approximately(k_MaxWarningAlphaMultiplier, 0.6f));

        if (!Application.isPlaying)
        {
            return;
        }

        m_particles.Clear(true);
        EmitAt(transform.position, 4f);
        Debug.Assert(m_particles.particleCount == k_ParticlesPerExplosion);
        ParticleSystem.Particle[] emitted = new ParticleSystem.Particle[k_ParticlesPerExplosion];
        int emittedCount = m_particles.GetParticles(emitted);
        for (int index = 0; index < emittedCount; index++)
        {
            ParticleSystem.Particle particle = emitted[index];
            Vector3 offset = particle.position - (transform.position + Vector3.up * k_GroundOffset);
            float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
            float horizontalSpeed = new Vector2(particle.velocity.x, particle.velocity.z).magnitude;
            float maximumEdge = horizontalDistance
                + horizontalSpeed * particle.startLifetime
                + particle.startSize * k_MaxSizeMultiplier * 0.5f;
            Debug.Assert(maximumEdge <= 4.001f);
        }

        for (int index = 1; index < 11; index++)
        {
            EmitAt(transform.position, 4f);
        }
        Debug.Assert(m_particles.particleCount == 256);
        m_particles.Clear(true);
        EmitWarningAt(transform.position, 4f, 0f);
        Debug.Assert(m_particles.particleCount == k_ParticlesPerWarningPulse);
        m_particles.Clear(true);
    }
}
