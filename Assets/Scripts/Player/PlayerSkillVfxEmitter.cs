using UnityEngine;

public sealed class PlayerSkillVfxEmitter : MonoBehaviour
{
    private const float k_GrenadeTrailSpacing = 0.24f;
    private const float k_RocketTrailSpacing = 0.18f;
    private const float k_RocketExhaustInterval = 1f / 30f;

    [SerializeField] private Material m_fireMaterial;
    [SerializeField] private Material m_smokeMaterial;
    [SerializeField] private Material m_rocketExhaustMaterial;
    [SerializeField] private Material m_explosionFireMaterial;

    private ParticleSystem m_fireParticles;
    private ParticleSystem m_smokeParticles;
    private ParticleSystem m_rocketExhaustParticles;
    private ParticleSystem m_explosionFireParticles;
    private ImpactSparkEmitter m_impactSparkEmitter;
    private Light m_rocketLight;
    private float m_nextRocketExhaustAt;

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
        m_nextRocketExhaustAt = 0f;
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
        m_impactSparkEmitter?.EmitRocketTrailAt(tailPosition, direction);
        EmitSmoke(tailPosition, -direction, 1, 0.2f, 0.65f, 0.24f, 0.44f, 0.12f, 0.24f, 0.6f);
    }

    public void UpdateRocketFlight(Vector3 position, Vector3 direction)
    {
        if (!EnsureParticleSystems())
        {
            return;
        }

        direction = NormalizeOrForward(direction);
        if (Time.time >= m_nextRocketExhaustAt)
        {
            EmitRocketExhaust(position - direction * 0.14f, direction);
            m_nextRocketExhaustAt = Time.time + k_RocketExhaustInterval;
        }
        m_rocketLight.transform.position = position - direction * 0.12f;
        m_rocketLight.enabled = true;
    }

    private void EmitRocketExhaust(Vector3 position, Vector3 direction)
    {
        ParticleSystem.EmitParams emit = new()
        {
            position = position,
            velocity = -direction * Random.Range(0.8f, 1.4f),
            startLifetime = Random.Range(0.07f, 0.12f),
            startSize = Random.Range(0.26f, 0.4f),
            startColor = Color.white,
            rotation = 0f
        };
        m_rocketExhaustParticles.Emit(emit, 1);
    }

    public void EndRocketFlight()
    {
        m_nextRocketExhaustAt = 0f;
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
        EmitExplosionCore(position, isRocket ? 10 : 6, scale, isRocket);
        EmitExplosionFire(position, radius, isRocket ? 28 : 20, scale, isRocket);
        if (isRocket)
        {
            EmitRocketExplosionSmoke(position, radius, 32, scale);
        }
        else
        {
            EmitGrenadeExplosionSmoke(position, radius, scale);
        }
        m_impactSparkEmitter?.EmitExplosionAt(position, radius, isRocket);
    }

    private void EmitExplosionCore(Vector3 position, int count, float scale, bool isRocket)
    {
        for (int index = 0; index < count; index++)
        {
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = isRocket
                    ? Random.Range(0.14f, 0.24f)
                    : Random.Range(0.08f, 0.16f),
                startSize = Random.Range(0.7f, 1.35f) * scale,
                startColor = Color.white,
                rotation = Random.Range(0f, 360f)
            };
            m_explosionFireParticles.Emit(emit, 1);
        }
    }

    private void EmitExplosionFire(Vector3 position, float radius, int count, float scale,
        bool isRocket)
    {
        for (int index = 0; index < count; index++)
        {
            float lifetime = isRocket
                ? Random.Range(0.28f, 0.55f)
                : Random.Range(0.14f, 0.32f);
            float size = Random.Range(isRocket ? 0.38f : 0.25f,
                isRocket ? 0.9f : 0.68f) * scale;
            float maxCenterDistance = Mathf.Max(0.1f, radius - size * 0.5f);
            float targetDistance = maxCenterDistance
                * (index == 0 ? 1f : Random.Range(isRocket ? 0.25f : 0.4f, 0.9f));
            Vector3 direction = isRocket
                ? GetRocketExplosionDirection(index, count)
                : GetGrenadeBaseDirection(index, count);
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = direction * (targetDistance / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = Color.white,
                rotation = Random.Range(0f, 360f)
            };
            m_explosionFireParticles.Emit(emit, 1);
        }
    }

    private void EmitRocketExplosionSmoke(Vector3 position, float radius, int count, float scale)
    {
        for (int index = 0; index < count; index++)
        {
            float lifetime = Random.Range(1f, 1.75f);
            float size = Random.Range(0.55f, 1.35f) * scale;
            float maxCenterDistance = Mathf.Max(0.1f, radius - size * 0.725f);
            float targetDistance = maxCenterDistance
                * (index == 0 ? 1f : Random.Range(0.3f, 0.88f));
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = GetRocketExplosionDirection(index, count) * (targetDistance / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = new Color(
                    Random.Range(0.025f, 0.09f),
                    Random.Range(0.02f, 0.075f),
                    Random.Range(0.015f, 0.055f),
                    Random.Range(0.56f, 0.78f)),
                rotation = Random.Range(0f, 360f)
            };
            m_smokeParticles.Emit(emit, 1);
        }
    }

    private void EmitGrenadeExplosionSmoke(Vector3 position, float radius, float scale)
    {
        const int baseCount = 18;
        const int jetCount = 24;
        for (int index = 0; index < baseCount + jetCount; index++)
        {
            bool isJet = index >= baseCount;
            int jetIndex = index - baseCount;
            float lifetime = isJet ? Random.Range(0.75f, 1.35f) : Random.Range(0.9f, 1.55f);
            float size = Random.Range(isJet ? 0.28f : 0.48f,
                isJet ? 0.65f : 1.05f) * scale;
            float maxCenterDistance = Mathf.Max(0.1f, radius - size * 0.725f);
            float targetDistance = maxCenterDistance * (index == 0
                ? 1f
                : isJet ? Random.Range(0.55f, 0.95f) : Random.Range(0.35f, 0.82f));
            Vector3 direction = isJet
                ? GetGrenadeJetDirection(jetIndex)
                : GetGrenadeBaseDirection(index, baseCount);
            ParticleSystem.EmitParams emit = new()
            {
                position = position,
                velocity = direction * (targetDistance / lifetime),
                startLifetime = lifetime,
                startSize = size,
                startColor = new Color(
                    Random.Range(0.04f, 0.12f),
                    Random.Range(0.035f, 0.1f),
                    Random.Range(0.025f, 0.075f),
                    Random.Range(0.52f, 0.74f)),
                rotation = Random.Range(0f, 360f)
            };
            m_smokeParticles.Emit(emit, 1);
        }
    }

    private static Vector3 GetRocketExplosionDirection(int index, int count)
    {
        float y = 1f - 2f * (index + 0.5f) / count;
        float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        float angle = index * 2.399963f + Random.Range(-0.18f, 0.18f);
        return new Vector3(horizontal * Mathf.Cos(angle), y,
            horizontal * Mathf.Sin(angle));
    }

    private static Vector3 GetGrenadeBaseDirection(int index, int count)
    {
        float angle = Mathf.PI * 2f * index / count + Random.Range(-0.12f, 0.12f);
        return new Vector3(Mathf.Cos(angle), Random.Range(0.08f, 0.32f),
            Mathf.Sin(angle)).normalized;
    }

    private static Vector3 GetGrenadeJetDirection(int index)
    {
        int column = index % 8;
        float angle = Mathf.PI * 2f * column / 8f + Random.Range(-0.08f, 0.08f);
        float horizontal = Random.Range(0.18f, 0.42f);
        return new Vector3(Mathf.Cos(angle) * horizontal, Random.Range(0.82f, 1.15f),
            Mathf.Sin(angle) * horizontal).normalized;
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
        if (m_fireMaterial == null || m_smokeMaterial == null
            || m_rocketExhaustMaterial == null || m_explosionFireMaterial == null)
        {
            return false;
        }

        m_fireParticles ??= CreateParticleSystem("SkillFireParticles", m_fireMaterial, false);
        m_smokeParticles ??= CreateParticleSystem("SkillSmokeParticles", m_smokeMaterial, true);
        m_rocketExhaustParticles ??= CreateParticleSystem(
            "SkillRocketExhaustParticles", m_rocketExhaustMaterial, false, true);
        m_explosionFireParticles ??= CreateParticleSystem(
            "SkillExplosionFireParticles", m_explosionFireMaterial, false);
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

    private ParticleSystem CreateParticleSystem(string objectName, Material material, bool isSmoke,
        bool isExhaust = false)
    {
        GameObject particleObject = new(objectName, typeof(ParticleSystem));
        particleObject.transform.SetParent(transform, false);
        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isExhaust ? 64 : 256;
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
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, isSmoke
            ? AnimationCurve.Linear(0f, 0.55f, 1f, 1.45f)
            : AnimationCurve.Linear(0f, 1f, 1f, isExhaust ? 0.05f : 0.15f));

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
        renderer.renderMode = isExhaust
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        if (isExhaust)
        {
            renderer.lengthScale = 1.35f;
            renderer.velocityScale = 0.2f;
        }
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
        Debug.Assert(m_fireMaterial != null && m_smokeMaterial != null
            && m_rocketExhaustMaterial != null && m_explosionFireMaterial != null);
        Debug.Assert(EnsureParticleSystems());
        Debug.Assert(m_fireParticles.main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_smokeParticles.main.simulationSpace == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_rocketExhaustParticles.main.simulationSpace
            == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_explosionFireParticles.main.simulationSpace
            == ParticleSystemSimulationSpace.World);
        Debug.Assert(m_fireParticles.main.maxParticles == 256
            && m_smokeParticles.main.maxParticles == 256
            && m_rocketExhaustParticles.main.maxParticles == 64
            && m_explosionFireParticles.main.maxParticles == 256);
        Debug.Assert(m_rocketLight != null && m_rocketLight.type == LightType.Point);
        if (!Application.isPlaying)
        {
            return;
        }

        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        m_rocketExhaustParticles.Clear(true);
        m_explosionFireParticles.Clear(true);
        m_impactSparkEmitter?.ClearParticles();
        EmitRocketLaunch(transform.position, transform.forward);
        Debug.Assert(m_fireParticles.particleCount == 12);
        Debug.Assert(m_smokeParticles.particleCount == 16);
        Debug.Assert(m_rocketExhaustParticles.particleCount == 1);
        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        m_rocketExhaustParticles.Clear(true);
        EmitExplosion(transform.position, 4f, true);
        Debug.Assert(m_explosionFireParticles.particleCount == 38);
        Debug.Assert(m_smokeParticles.particleCount == 32);
        Debug.Assert(m_impactSparkEmitter == null
            || m_impactSparkEmitter.ActiveParticleCount == 12);
        AssertRocketSphereSpread(m_explosionFireParticles, 28);
        AssertRocketSphereSpread(m_smokeParticles, 32);
        AssertExplosionRadius(m_explosionFireParticles, 4f, 0.5f);
        AssertExplosionRadius(m_smokeParticles, 4f, 0.725f);
        m_explosionFireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        m_impactSparkEmitter?.ClearParticles();
        EmitExplosion(transform.position, 5f, false);
        EmitExplosion(transform.position, 5f, false);
        EmitExplosion(transform.position, 5f, false);
        Debug.Assert(m_explosionFireParticles.particleCount == 78);
        Debug.Assert(m_smokeParticles.particleCount == 126);
        Debug.Assert(m_impactSparkEmitter == null
            || m_impactSparkEmitter.ActiveParticleCount == 96);
        AssertExplosionRadius(m_explosionFireParticles, 5f, 0.5f);
        AssertExplosionRadius(m_smokeParticles, 5f, 0.725f);
        m_fireParticles.Clear(true);
        m_smokeParticles.Clear(true);
        m_rocketExhaustParticles.Clear(true);
        m_explosionFireParticles.Clear(true);
        m_impactSparkEmitter?.ClearParticles();
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

    private static void AssertRocketSphereSpread(ParticleSystem particles, int expectedMovingCount)
    {
        ParticleSystem.Particle[] emitted = new ParticleSystem.Particle[particles.particleCount];
        int count = particles.GetParticles(emitted);
        int movingCount = 0;
        int upperCount = 0;
        int lowerCount = 0;
        int equatorCount = 0;
        float minimumVertical = 1f;
        float maximumVertical = -1f;
        for (int index = 0; index < count; index++)
        {
            Vector3 velocity = emitted[index].velocity;
            if (velocity.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            float vertical = velocity.normalized.y;
            movingCount++;
            upperCount += vertical > 0f ? 1 : 0;
            lowerCount += vertical < 0f ? 1 : 0;
            equatorCount += Mathf.Abs(vertical) <= 0.5f ? 1 : 0;
            minimumVertical = Mathf.Min(minimumVertical, vertical);
            maximumVertical = Mathf.Max(maximumVertical, vertical);
        }

        Debug.Assert(movingCount == expectedMovingCount);
        Debug.Assert(Mathf.Abs(upperCount - lowerCount) <= 1);
        Debug.Assert(equatorCount >= expectedMovingCount / 3);
        Debug.Assert(minimumVertical <= -0.9f);
        Debug.Assert(maximumVertical >= 0.95f);
    }
}
