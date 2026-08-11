using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class RangedProjectileGasEmitter : MonoBehaviour
{
    private const int k_ImpactParticleCount = 6;
    private const float k_ImpactRadius = 0.55f;
    private const float k_MaxSizeMultiplier = 1.15f;
    private static readonly Color k_LightTrailColor = new Color32(200, 182, 74, 34);
    private static readonly Color k_DarkTrailColor = new Color32(120, 109, 36, 22);
    private static readonly Color k_LightImpactColor = new Color32(200, 182, 74, 56);
    private static readonly Color k_DarkImpactColor = new Color32(120, 109, 36, 40);

    private ParticleSystem m_particles;

    private void Awake()
    {
        TryCacheParticles();
    }

    public void EmitChargeAt(Vector3 position)
    {
        EmitPuff(position, 0.14f, 0.22f, 0.20f, 0.28f, k_DarkTrailColor, k_LightTrailColor);
    }

    public void EmitTrailAt(Vector3 position)
    {
        EmitPuff(position, 0.18f, 0.30f, 0.25f, 0.30f, k_DarkTrailColor, k_LightTrailColor);
    }

    public void EmitImpactAt(Vector3 position)
    {
        if (!TryCacheParticles())
        {
            return;
        }

        for (int index = 0; index < k_ImpactParticleCount; index++)
        {
            float lifetime = Random.Range(0.45f, 0.65f);
            float size = Random.Range(0.25f, 0.45f);
            float speed = Random.Range(0.12f, 0.25f);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new(Mathf.Cos(angle), Random.Range(0.08f, 0.22f), Mathf.Sin(angle));
            direction.Normalize();
            float edgeAllowance = speed * lifetime + size * k_MaxSizeMultiplier * 0.5f;
            float maxStartRadius = Mathf.Max(0f, k_ImpactRadius - edgeAllowance);

            ParticleSystem.EmitParams emitParams = new()
            {
                position = position + direction * Random.Range(0f, maxStartRadius),
                velocity = direction * speed,
                startLifetime = lifetime,
                startSize = size,
                startColor = Color.Lerp(k_DarkImpactColor, k_LightImpactColor, Random.value),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    private void EmitPuff(Vector3 position, float minSize, float maxSize, float minLifetime,
        float maxLifetime, Color darkColor, Color lightColor)
    {
        if (!TryCacheParticles())
        {
            return;
        }

        ParticleSystem.EmitParams emitParams = new()
        {
            position = position,
            velocity = Random.insideUnitSphere * 0.08f,
            startLifetime = Random.Range(minLifetime, maxLifetime),
            startSize = Random.Range(minSize, maxSize),
            startColor = Color.Lerp(darkColor, lightColor, Random.value),
            rotation = Random.Range(0f, 360f)
        };
        m_particles.Emit(emitParams, 1);
    }

    private bool TryCacheParticles()
    {
        if (m_particles == null)
        {
            m_particles = GetComponent<ParticleSystem>();
        }

        return m_particles != null;
    }

    [ContextMenu("Run Ranged Projectile Gas Self Check")]
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

        if (!Application.isPlaying)
        {
            return;
        }

        m_particles.Clear(true);
        EmitChargeAt(transform.position);
        EmitTrailAt(transform.position);
        EmitImpactAt(transform.position);
        Debug.Assert(m_particles.particleCount == k_ImpactParticleCount + 2);
        m_particles.Clear(true);
    }
}
