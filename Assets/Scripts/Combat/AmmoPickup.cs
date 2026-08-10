using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class AmmoPickup : MonoBehaviour
{
    private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color[] s_WeaponColors =
    {
        new Color32(234, 64, 71, 255),
        new Color32(53, 199, 89, 255),
        new Color32(44, 135, 232, 255)
    };
    private static readonly int[] s_AmmoAmounts = { 15, 6, 30 };
    private static Material s_GlowMaterial;
    private static FirstPersonController s_Player;

    [SerializeField, Range(1, 3)] private int m_weaponSlot = 1;
    [SerializeField, Min(1)] private int m_amount = 15;
    [SerializeField] private Renderer m_boxRenderer;
    [SerializeField] private SpriteRenderer m_silhouetteRenderer;
    [SerializeField] private Sprite[] m_weaponSilhouettes = new Sprite[3];
    [SerializeField] private Vector2 m_silhouetteMaxSize = new(0.7f, 0.28f);

    private Collider m_trigger;
    private Transform m_cameraTransform;

    public static AmmoPickup Create(AmmoPickup prefab, Vector3 deathPosition, int weaponSlot, int amount)
    {
        if (prefab == null)
        {
            Debug.LogError("AmmoPickup prefab is not assigned.");
            return null;
        }

        AmmoPickup pickup = Instantiate(prefab, FindGroundPosition(deathPosition), Quaternion.identity);
        pickup.Configure(weaponSlot, amount);
        return pickup;
    }

    public static AmmoPickup CreateDrop(AmmoPickup prefab, Vector3 deathPosition, int sourceWeaponSlot)
    {
        int ammoSlot = ChooseAmmoSlot(sourceWeaponSlot, Random.value);
        return Create(prefab, deathPosition, ammoSlot, s_AmmoAmounts[ammoSlot - 1]);
    }

    private static int ChooseAmmoSlot(int sourceWeaponSlot, float roll)
    {
        sourceWeaponSlot = Mathf.Clamp(sourceWeaponSlot, 1, 3);
        if (roll < 0.2f)
        {
            return sourceWeaponSlot;
        }

        int firstOtherSlot = sourceWeaponSlot == 1 ? 2 : 1;
        int secondOtherSlot = 6 - sourceWeaponSlot - firstOtherSlot;
        return roll < 0.6f ? firstOtherSlot : secondOtherSlot;
    }

    public void Configure(int weaponSlot, int amount)
    {
        m_weaponSlot = Mathf.Clamp(weaponSlot, 1, 3);
        m_amount = Mathf.Max(1, amount);

        if (m_boxRenderer == null)
        {
            m_boxRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (m_boxRenderer != null)
        {
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
            m_boxRenderer.sharedMaterial = s_GlowMaterial;
            MaterialPropertyBlock boxProperties = new();
            boxProperties.SetColor(s_BaseColor, color);
            boxProperties.SetColor(s_EmissionColor, color);
            m_boxRenderer.SetPropertyBlock(boxProperties);
        }

        ConfigureSilhouette();
    }

    private void ConfigureSilhouette()
    {
        if (m_silhouetteRenderer == null || m_weaponSilhouettes == null || m_weaponSilhouettes.Length != 3)
        {
            return;
        }

        Sprite silhouette = m_weaponSilhouettes[m_weaponSlot - 1];
        if (silhouette == null)
        {
            return;
        }

        m_silhouetteRenderer.sprite = silhouette;
        m_silhouetteRenderer.color = s_WeaponColors[m_weaponSlot - 1];

        Vector2 spriteSize = silhouette.bounds.size;
        float scale = Mathf.Min(m_silhouetteMaxSize.x / spriteSize.x, m_silhouetteMaxSize.y / spriteSize.y);
        m_silhouetteRenderer.transform.localScale = Vector3.one * scale;
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

    private void Awake()
    {
        m_trigger = GetComponent<Collider>();
    }

    private void LateUpdate()
    {
        if (m_silhouetteRenderer == null)
        {
            return;
        }

        if (m_cameraTransform == null && Camera.main != null)
        {
            m_cameraTransform = Camera.main.transform;
        }

        if (m_cameraTransform != null)
        {
            m_silhouetteRenderer.transform.rotation = m_cameraTransform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (s_Player == null)
        {
            s_Player = FindFirstObjectByType<FirstPersonController>();
        }

        CharacterController controller = s_Player != null ? s_Player.GetComponent<CharacterController>() : null;
        if (controller != null && m_trigger != null && m_trigger.bounds.Intersects(controller.bounds))
        {
            TryCollect(s_Player);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player != null)
        {
            s_Player = player;
        }
        TryCollect(player);
    }

    private void TryCollect(FirstPersonController player)
    {
        if (player != null && player.TryAddAmmo(m_weaponSlot, m_amount))
        {
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        m_weaponSlot = Mathf.Clamp(m_weaponSlot, 1, 3);
        m_amount = Mathf.Max(1, m_amount);
        m_silhouetteMaxSize.x = Mathf.Max(0.01f, m_silhouetteMaxSize.x);
        m_silhouetteMaxSize.y = Mathf.Max(0.01f, m_silhouetteMaxSize.y);
    }
}
