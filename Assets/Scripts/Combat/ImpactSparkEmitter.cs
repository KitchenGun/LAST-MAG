using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public sealed class ImpactSparkEmitter : MonoBehaviour
{
    private const int k_MinParticlesPerImpact = 4;
    private const int k_MaxParticlesPerImpact = 7;
    private const int k_MinSuicideBiologicalParticles = 4;
    private const int k_MaxSuicideBiologicalParticles = 6;
    private const int k_MinSuicideHeadshotParticles = 7;
    private const int k_MaxSuicideHeadshotParticles = 9;
    private const int k_MinHumanoidBiologicalParticles = 14;
    private const int k_MaxHumanoidBiologicalParticles = 18;
    private const int k_MinHumanoidHeadshotParticles = 24;
    private const int k_MaxHumanoidHeadshotParticles = 30;
    private const int k_MaxSplats = 20;
    private const float k_SplatLifetime = 4f;
    private const float k_SplatSurfaceOffset = 0.008f;
    private const float k_SplatRayLength = 2f;
    private const float k_ParticleSizeMultiplier = 12f;
    private const int k_RocketTrailParticles = 2;
    private const int k_GrenadeExplosionParticles = 32;
    private const int k_RocketExplosionParticles = 12;

    private struct Splat
    {
        public Matrix4x4 Matrix;
        public float ExpireAt;
    }

    private readonly RaycastHit[] m_splatHits = new RaycastHit[16];
    private readonly Splat[] m_splats = new Splat[k_MaxSplats];
    private ParticleSystem m_particles;
    [SerializeField] private ParticleSystem m_biologicalParticles;
    [SerializeField] private Mesh m_splatMesh;
    [SerializeField] private Material m_splatMaterial;
    private int m_nextSplatIndex;

    private void Awake()
    {
        TryCacheParticles();
    }

    private void LateUpdate()
    {
        if (m_splatMesh == null || m_splatMaterial == null)
        {
            return;
        }

        float now = Time.time;
        for (int index = 0; index < k_MaxSplats; index++)
        {
            if (m_splats[index].ExpireAt > now)
            {
                Graphics.DrawMesh(m_splatMesh, m_splats[index].Matrix, m_splatMaterial, 0);
            }
        }
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
                startSize = Random.Range(0.008f, 0.018f) * k_ParticleSizeMultiplier,
                startColor = new Color(1f, Random.Range(0.78f, 0.95f), Random.Range(0.35f, 0.65f), 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    public void EmitBiologicalAt(Vector3 position, Vector3 normal, bool isHeadshot, EnemyType enemyType,
        Vector3 shotDirection)
    {
        if (m_biologicalParticles == null)
        {
            return;
        }

        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        bool humanoid = enemyType != EnemyType.Suicide;
        int minCount = humanoid
            ? (isHeadshot ? k_MinHumanoidHeadshotParticles : k_MinHumanoidBiologicalParticles)
            : (isHeadshot ? k_MinSuicideHeadshotParticles : k_MinSuicideBiologicalParticles);
        int maxCount = humanoid
            ? (isHeadshot ? k_MaxHumanoidHeadshotParticles : k_MaxHumanoidBiologicalParticles)
            : (isHeadshot ? k_MaxSuicideHeadshotParticles : k_MaxSuicideBiologicalParticles);
        int particleCount = Random.Range(minCount, maxCount + 1);
        Vector3 emissionPosition = position + normal * 0.012f;

        for (int index = 0; index < particleCount; index++)
        {
            Vector3 direction = (normal + Random.insideUnitSphere * (humanoid ? 1.15f : 0.8f)).normalized;
            Color color = humanoid
                ? (Random.value < 0.65f ? new Color(0.34f, 0.78f, 0.12f, 0.92f) : new Color(0.28f, 0.38f, 0.08f, 0.9f))
                : (Random.value < 0.65f ? new Color(0.44f, 0.5f, 0.27f, 0.92f) : new Color(0.48f, 0.25f, 0.2f, 0.9f));
            ParticleSystem.EmitParams emitParams = new()
            {
                position = emissionPosition,
                velocity = direction * Random.Range(humanoid ? 2f : 0.6f, humanoid ? 5.5f : 1.8f),
                startLifetime = Random.Range(humanoid ? 0.25f : 0.08f, humanoid ? 0.55f : 0.16f),
                startSize = Random.Range(humanoid ? 0.02f : 0.015f, humanoid ? 0.055f : 0.035f)
                    * k_ParticleSizeMultiplier,
                startColor = color,
                rotation = Random.Range(0f, 360f)
            };
            m_biologicalParticles.Emit(emitParams, 1);
        }

        if (humanoid)
        {
            TryEmitSplat(position, shotDirection, isHeadshot);
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
            float lifetime = isRocket ? Random.Range(0.16f, 0.32f) : Random.Range(0.35f, 0.68f);
            float size = Random.Range(0.012f, isRocket ? 0.024f : 0.028f) * k_ParticleSizeMultiplier;
            float gravityTravel = 0.5f * gravity * lifetime * lifetime;
            float safeTravel = Mathf.Max(0f, radius * (isRocket ? 0.55f : 0.9f) - gravityTravel - size * 2f);
            Vector3 direction = isRocket ? Random.onUnitSphere : GetGrenadeFragmentDirection(index, particleCount);
            direction.y = isRocket ? Mathf.Abs(direction.y) + 0.08f : direction.y;
            direction.Normalize();
            ParticleSystem.EmitParams emitParams = new()
            {
                position = position,
                velocity = direction * (safeTravel * Random.Range(0.55f, 1f) / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = Random.value < 0.55f ? new Color(1f, Random.Range(0.72f, 0.95f), Random.Range(0.24f, 0.5f), 1f) : new Color(1f, 0.97f, Random.Range(0.72f, 0.92f), 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    internal void EmitRocketTrailAt(Vector3 position, Vector3 forward)
    {
        if (!TryCacheParticles())
        {
            return;
        }

        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        for (int index = 0; index < k_RocketTrailParticles; index++)
        {
            Vector3 direction = (-forward + Random.insideUnitSphere * 0.18f).normalized;
            ParticleSystem.EmitParams emitParams = new()
            {
                position = position,
                velocity = direction * Random.Range(4.5f, 7.5f),
                startLifetime = Random.Range(0.05f, 0.11f),
                startSize = Random.Range(0.008f, 0.018f) * k_ParticleSizeMultiplier,
                startColor = index == 0 ? new Color(1f, 0.96f, 0.7f, 1f) : new Color(1f, 0.48f, 0.08f, 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_particles.Emit(emitParams, 1);
        }
    }

    internal int ActiveParticleCount => m_particles != null ? m_particles.particleCount : 0;

    internal void ClearParticles()
    {
        if (TryCacheParticles())
        {
            m_particles.Clear(true);
        }
    }

    private void TryEmitSplat(Vector3 position, Vector3 shotDirection, bool isHeadshot)
    {
        if (m_splatMesh == null || m_splatMaterial == null || shotDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 direction = shotDirection.normalized;
        int hitCount = Physics.RaycastNonAlloc(position + direction * 0.02f, direction, m_splatHits, k_SplatRayLength,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;
        for (int index = 0; index < hitCount; index++)
        {
            Collider collider = m_splatHits[index].collider;
            if (collider != null && !IsIgnoredSplatCollider(collider) && m_splatHits[index].distance < nearestDistance)
            {
                nearestIndex = index;
                nearestDistance = m_splatHits[index].distance;
            }
        }

        if (nearestIndex < 0)
        {
            return;
        }

        RaycastHit hit = m_splatHits[nearestIndex];
        AddSplat(hit.point, hit.normal, Random.Range(0.18f, 0.34f));
        if (isHeadshot)
        {
            AddSplat(hit.point + hit.normal * 0.002f, hit.normal, Random.Range(0.12f, 0.19f));
        }
    }

    private static bool IsIgnoredSplatCollider(Collider collider)
    {
        return collider.GetComponentInParent<EnemyHealth>() != null
            || collider.GetComponentInParent<FirstPersonController>() != null
            || collider.GetComponentInParent<AmmoPickup>() != null
            || collider.GetComponentInParent<RangedProjectile>() != null
            || collider.GetComponentInParent<PlayerSkillProjectile>() != null;
    }

    private void AddSplat(Vector3 position, Vector3 normal, float size)
    {
        normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal) * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        m_splats[m_nextSplatIndex] = new Splat
        {
            Matrix = Matrix4x4.TRS(position + normal * k_SplatSurfaceOffset, rotation, new Vector3(size, size, 1f)),
            ExpireAt = Time.time + k_SplatLifetime
        };
        m_nextSplatIndex = (m_nextSplatIndex + 1) % k_MaxSplats;
    }

    private static Vector3 GetGrenadeFragmentDirection(int index, int count)
    {
        float angle = Mathf.PI * 2f * index / count + Random.Range(-0.1f, 0.1f);
        float upward = Random.Range(0.55f, 1.15f);
        return new Vector3(Mathf.Cos(angle) * Random.Range(0.25f, 0.65f), upward, Mathf.Sin(angle) * Random.Range(0.25f, 0.65f));
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
        if (m_biologicalParticles != null)
        {
            ParticleSystem.MainModule biologicalMain = m_biologicalParticles.main;
            Debug.Assert(biologicalMain.simulationSpace == ParticleSystemSimulationSpace.World);
            Debug.Assert(biologicalMain.maxParticles == 256);
        }
        Debug.Assert(k_MaxSplats == 20 && Mathf.Approximately(k_SplatLifetime, 4f));

        if (!Application.isPlaying)
        {
            Debug.Log("Impact spark configuration self check passed. Run it in Play Mode to verify emission count.", this);
            return;
        }

        m_particles.Clear(true);
        EmitSurfaceAt(transform.position, Vector3.up);
        Debug.Assert(m_particles.particleCount >= k_MinParticlesPerImpact && m_particles.particleCount <= k_MaxParticlesPerImpact);
        m_particles.Clear(true);
        EmitRocketTrailAt(transform.position, Vector3.forward);
        Debug.Assert(m_particles.particleCount == k_RocketTrailParticles);
        m_particles.Clear(true);
        EmitExplosionAt(transform.position, 4f, true);
        Debug.Assert(m_particles.particleCount == k_RocketExplosionParticles);
        AssertExplosionRadius(m_particles, 4f);
        m_particles.Clear(true);
        EmitExplosionAt(transform.position, 5f, false);
        Debug.Assert(m_particles.particleCount == k_GrenadeExplosionParticles);
        AssertExplosionRadius(m_particles, 5f);
        m_particles.Clear(true);
        m_biologicalParticles.Clear(true);
        EmitBiologicalAt(transform.position, Vector3.up, true, EnemyType.Melee, Vector3.forward);
        Debug.Assert(m_biologicalParticles.particleCount >= k_MinHumanoidHeadshotParticles && m_biologicalParticles.particleCount <= k_MaxHumanoidHeadshotParticles);
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
            float reach = particle.velocity.magnitude * particle.startLifetime + 0.5f * gravity * particle.startLifetime * particle.startLifetime + particle.startSize * 2f;
            Debug.Assert(reach <= radius + 0.02f);
        }
    }
}
