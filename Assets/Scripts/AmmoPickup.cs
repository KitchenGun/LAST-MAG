using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class AmmoPickup : MonoBehaviour
{
    private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color[] s_WeaponColors =
    {
        Color.white,
        new Color32(53, 199, 89, 255),
        new Color32(168, 85, 247, 255)
    };
    private static Material s_GlowMaterial;

    [SerializeField, Range(1, 3)] private int m_weaponSlot = 1;
    [SerializeField, Min(1)] private int m_amount = 12;

    public static AmmoPickup Create(Vector3 deathPosition, int weaponSlot, int amount)
    {
        GameObject pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickupObject.name = "AmmoPickup";
        pickupObject.transform.localScale = new Vector3(0.8f, 0.15f, 0.45f);
        pickupObject.transform.position = FindGroundPosition(deathPosition);

        BoxCollider collider = pickupObject.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        Rigidbody body = pickupObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        AmmoPickup pickup = pickupObject.AddComponent<AmmoPickup>();
        pickup.Configure(weaponSlot, amount);
        return pickup;
    }

    public void Configure(int weaponSlot, int amount)
    {
        m_weaponSlot = Mathf.Clamp(weaponSlot, 1, 3);
        m_amount = Mathf.Max(1, amount);

        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        if (s_GlowMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            s_GlowMaterial = new Material(shader);
            s_GlowMaterial.EnableKeyword("_EMISSION");
        }

        Color color = s_WeaponColors[m_weaponSlot - 1];
        renderer.sharedMaterial = s_GlowMaterial;
        MaterialPropertyBlock properties = new();
        properties.SetColor(s_BaseColor, color);
        properties.SetColor(s_EmissionColor, color * 2f);
        renderer.SetPropertyBlock(properties);
    }

    private static Vector3 FindGroundPosition(Vector3 deathPosition)
    {
        Vector3 rayOrigin = deathPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.08f;
        }

        return deathPosition;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player != null && player.TryAddAmmo(m_weaponSlot, m_amount))
        {
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        m_weaponSlot = Mathf.Clamp(m_weaponSlot, 1, 3);
        m_amount = Mathf.Max(1, m_amount);
    }
}
