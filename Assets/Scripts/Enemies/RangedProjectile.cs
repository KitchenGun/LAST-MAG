using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class RangedProjectile : MonoBehaviour
{
    private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static Material s_GlowMaterial;

    private float m_damage;
    private float m_destroyTime;

    public static RangedProjectile Create(Vector3 position, Vector3 direction, float speed, float damage, float radius, float lifetime)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "RangedProjectile";
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = Vector3.one * radius * 2f;
        ApplyGlow(projectileObject.GetComponent<Renderer>(), Color.red * 2f);

        SphereCollider collider = projectileObject.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        Rigidbody body = projectileObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.linearVelocity = direction.normalized * speed;

        RangedProjectile projectile = projectileObject.AddComponent<RangedProjectile>();
        projectile.m_damage = damage;
        projectile.m_destroyTime = Time.time + lifetime;
        return projectile;
    }

    public static GameObject CreateChargeVisual(Transform parent, float radius)
    {
        GameObject charge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        charge.name = "RangedAttackCharge";
        charge.transform.SetParent(parent, false);
        charge.transform.localPosition = Vector3.zero;
        charge.transform.localScale = Vector3.one * radius * 2f;
        charge.GetComponent<Collider>().enabled = false;
        ApplyGlow(charge.GetComponent<Renderer>(), Color.red * 2f);
        return charge;
    }

    private void Update()
    {
        if (Time.time >= m_destroyTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<EnemyHealth>() != null)
        {
            return;
        }

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.ApplyDamage(m_damage, PlayerDeathCause.RangedHumanoid);
        }
        Destroy(gameObject);
    }

    private static void ApplyGlow(Renderer renderer, Color color)
    {
        if (s_GlowMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            s_GlowMaterial = new Material(shader);
            s_GlowMaterial.EnableKeyword("_EMISSION");
        }

        renderer.sharedMaterial = s_GlowMaterial;
        MaterialPropertyBlock properties = new();
        properties.SetColor(s_BaseColor, color);
        properties.SetColor(s_EmissionColor, color * 2f);
        renderer.SetPropertyBlock(properties);
    }
}
