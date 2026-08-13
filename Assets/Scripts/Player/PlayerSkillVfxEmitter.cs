using UnityEngine;

public sealed class PlayerSkillVfxEmitter : MonoBehaviour
{
    private const float k_GrenadeTrailSpacing = 0.24f;
    private const float k_RocketTrailSpacing = 0.18f;

    [SerializeField] private Material m_fireMaterial;
    [SerializeField] private Material m_smokeMaterial;

    private ParticleSystem m_fireParticles;
    private ParticleSystem m_smokeParticles;
    private ImpactSparkEmitter m_impactSparkEmitter;
    private Light m_rocketLight;

    public float GrenadeTrailSpacing => k_GrenadeTrailSpacing;
    public float RocketTrailSpacing => k_RocketTrailSpacing;

    private void Awake()
    {
        EnsureParticleSystems();
    }

    public void EmitRocketLaunch(Vector3 position, Vector3 direction)
    {
        if (!EnsureParticleSystems())
        {
            return;
        }

        direction = NormalizeOrForward(direction);
        EmitFire(position, direction, 12, 4.5f, 8f, 0.08f, 0.16f, 0.12f, 0.28f, 0.28f);
        EmitSmoke(position, -direction, 16, 1.2f, 3.2f, 0.35f, 0.7f, 0.18f, 0.48f, 0.55f);
        UpdateRocketFlight(position, direction);
    }

    public void EmitRocketTrail(Vector3 position, Vector3 direction)
    {
        if (!EnsureParticleSystems())
        {
            return;
        }

        direction = NormalizeOrForward(direction);
        Vector3 tailPosition = position - direction * 0.16f;
        EmitFire(tailPosition, -direction, 2, 1.2f, 2.8f, 0.1f, 0.2f, 0.09f, 0.18f, 0.28f);
        EmitSmoke(tailPosition, -direction, 1, 0.2f, 0.65f, 0.24f, 0.44f, 0.12f, 0.24f, 0.6f);
    }

    public void UpdateRocketFlight(Vector3 position, Vector3 direction)
    {
        if (!EnsureParticleSystems())
        {
            return;
        }

        direction = NormalizeOrForward(direction);
        m_rocketLight.transform.position = position - direction * 0.12f;
        m_rocketLight.enabled = true;
    }

    public void EndRocketFlight()
    {
        if (m_rocketLight != null)
        {
            m_rocketLight.enabled = false;
        }
    }

    public void EmitGrenadeTrail(Vector3 position)
    {
        if (EnsureParticleSystems())
        {
            EmitFire(position, Vector3.up, 1, 0.05f, 0.18f, 0.12f, 0.2f, 0.035f, 0.06f, 1f);
            EmitSmoke(position, Vector3.up, 1, 0.05f, 0.25f, 0.28f, 0.48f, 0.1f, 0.2f, 1f);
        }
    }

    public void EmitExplosion(Vector3 position, float radius, bool isRocket)
    {
        if (!EnsureParticleSystems())
        {
            return;
        }

        float scale = Mathf.Clamp(radius / 4f, 0.75f, 1.4f);
        EmitExplosionCore(position, isRocket ? 8 : 6, scale);
        EmitExplosionFire(position, radius, isRocket ? 28 : 20, scale);
        EmitExplosionSmoke(position, radius, isRocket ? 22 : 18, scale);
        m_impactSparkEmitter?.EmitExplosionAt(position, radius, isRocket);
    }

    private void EmitExplosionCore(Vector3 position, int count, float scale)
    {
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = Random.Range(0.06f, 0.14f),
                startSize = Random.Range(0.65f, 1.25f) * scale,
                startColor = index % 3 == 0
                    ? new Color(1f, 0.96f, 0.72f, 1f)
                    : new Color(1f, Random.Range(0.42f, 0.65f), 0.04f, 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_fireParticles.Emit(emit, 1);
        }
    }

    private void EmitExplosionFire(Vector3 position, float radius, int count, float scale)
    {
        for (int index = 0; index < count; index++)
        {
            float lifetime = Random.Range(0.14f, 0.32f);
            float size = Random.Range(0.2f, 0.62f) * scale;
            float maxCenterDistance = Mathf.Max(0.1f, radius - size * 0.5f);
            float targetDistance = maxCenterDistance * (index == 0 ? 1f : Random.Range(0.45f, 1f));
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = GetExplosionDirection(index, count) * (targetDistance / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = Random.value < 0.55f
                    ? new Color(1f, 0.48f, 0.06f, 1f)
                    : new Color(1f, 0.88f, 0.42f, 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_fireParticles.Emit(emit, 1);
        }
    }

    private void EmitExplosionSmoke(Vector3 position, float radius, int count, float scale)
    {
        for (int index = 0; index < count; index++)
        {
            float lifetime = Random.Range(0.9f, 1.6f);
            float size = Random.Range(0.45f, 1.2f) * scale;
            float maxCenterDistance = Mathf.Max(0.1f, radius - size * 0.725f);
            float targetDistance = maxCenterDistance * (index == 0 ? 1f : Random.Range(0.5f, 1f));
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = GetExplosionDirection(index, count) * (targetDistance / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = new Color(
                    Random.Range(0.015f, 0.06f),
                    Random.Range(0.015f, 0.055f),
                    Random.Range(0.012f, 0.045f),
                    Random.Range(0.5f, 0.72f)),
                rotation = Random.Range(0f, 360f)
            };
            m_smokeParticles.Emit(emit, 1);
        }
    }

    private static Vector3 GetExplosionDirection(int index, int count)
    {
        float angle = Mathf.PI * 2f * index / count + Random.Range(-0.12f, 0.12f);
        return new Vector3(Mathf.Cos(angle), Random.Range(0.08f, 0.35f), Mathf.Sin(angle)).normalized;
    }

    private void EmitFire(Vector3 position, Vector3 direction, int count, float minSpeed, float maxSpeed,
        float minLifetime, float maxLifetime, float minSize, float maxSize, float spread)
    {
        for (int index = 0; index < count; index++)
        {
            Vector3 velocityDirection = (direction + Random.insideUnitSphere * spread).normalized;
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = velocityDirection * Random.Range(minSpeed, maxSpeed),
                startLifetime = Random.Range(minLifetime, maxLifetime),
                startSize = Random.Range(minSize, maxSize),
                startColor = Random.value < 0.55f
                    ? new Color(1f, 0.48f, 0.06f, 1f)
                    : new Color(1f, 0.88f, 0.42f, 1f),
                rotation = Random.Range(0f, 360f)
            };
            m_fireParticles.Emit(emit, 1);
        }
    }

    private void EmitSmoke(Vector3 position, Vector3 direction, int count, float minSpeed, float maxSpeed,
        float minLifetime, float maxLifetime, float minSize, float maxSize, float spread)
    {
        for (int index = 0; index < count; index++)
        {
            Vector3 velocityDirection = (direction + Random.insideUnitSphere * spread + Vector3.up * 0.2f).normalized;
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = velocityDirection * Random.Range(minSpeed, maxSpeed),
                startLifetime = Random.Range(minLifetime, maxLifetime),
                startSize = Random.Range(minSize, maxSize),
                startColor = new Color(
                    Random.Range(0.14f, 0.23f),
                    Random.Range(0.12f, 0.19f),
                    Random.Range(0.09f, 0.14f),
                    Random.Range(0.38f, 0.58f)),
                rotation = Random.Range(0f, 360f)
            };
            m_smokeParticles.Emit(emit, 1);
        }
    }

    private bool EnsureParticleSystems()
    {
        if (m_fireMaterial == null || m_smokeMaterial == null)
        {
            return false;
        }

        m_fireParticles ??= CreateParticleSystem("SkillFireParticles", m_fireMaterial, false);
        m_smokeParticles ??= CreateParticleSystem("SkillSmokeParticles", m_smokeMaterial, true);
        m_impactSparkEmitter ??= GetComponentInChildren<ImpactSparkEmitter>(true);
        m_rocketLight ??= CreateRocketLight();
        return true;
    }

    private Light CreateRocketLight()
    {
        GameObject lightObject = new("SkillRocketLight", typeof(Light));
        lightObject.transform.SetParent(transform, false);
        Light rocketLight = lightObject.GetComponent<Light>();
        rocketLight.type = LightType.Point;
        rocketLight.color = new Color32(255, 117, 24, 255);
        rocketLight.intensity = 3.5f;
        rocketLight.range = 2.6f;
        rocketLight.shadows = LightShadows.None;
        rocketLight.enabled = false;
        return rocketLight;
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material, bool isSmoke)
    {
        GameObject particleObject = new(objectName, typeof(ParticleSystem));
        particleObject.transform.SetParent(transform, false);
        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.startSize = 1f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.CollisionModule collision = particles.collision;
        collision.enabled = false;
        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            isSmoke ? AnimationCurve.Linear(0f, 0.55f, 1f, 1.45f)
                : AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        if (isSmoke)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
        }

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private static Vector3 NormalizeOrForward(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    private void OnDisable()
    {
        EndRocketFlight();
    }

    [ContextMenu("Run Player Skill VFX Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(m_fireMaterial != null && m_smokeMaterial != null);
        Debug.Assert(EnsureParticleSystems());
        Debug.Assert(m_fireParticles.main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_smokeParticles.main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_fireParticles.main.maxParticles == 256 && m_smokeParticles.main.maxParticles == 256);
        Debug.Assert(m_rocketLight != null && m_rocketLight.type == LightType.Point);
        if (!Application.isPlaying)
        {
            return;
        }

        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        EmitRocketLaunch(transform.position, transform.forward);
        Debug.Assert(m_fireParticles.particleCount == 12);
        Debug.Assert(m_smokeParticles.particleCount == 16);
        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        EmitExplosion(transform.position, 4f, true);
        Debug.Assert(m_fireParticles.particleCount == 36);
        Debug.Assert(m_smokeParticles.particleCount == 22);
        AssertExplosionRadius(m_fireParticles, 4f, 0.5f);
        AssertExplosionRadius(m_smokeParticles, 4f, 0.725f);
        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
    }

    private static void AssertExplosionRadius(ParticleSystem particles, float radius, float halfSizeMultiplier)
    {
        ParticleSystem.Particle[] emitted = new ParticleSystem.Particle[particles.particleCount];
        int count = particles.GetParticles(emitted);
        float maximumReach = 0f;
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.Particle particle = emitted[index];
            float reach = particle.velocity.magnitude * particle.startLifetime
                + particle.startSize * halfSizeMultiplier;
            maximumReach = Mathf.Max(maximumReach, reach);
            Debug.Assert(reach <= radius + 0.02f);
        }
        Debug.Assert(maximumReach >= radius - 0.02f);
    }
}
