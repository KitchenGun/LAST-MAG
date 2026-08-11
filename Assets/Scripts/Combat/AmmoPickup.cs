using System;
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
        new Color32(44, 135, 232, 255),
        new Color32(44, 135, 232, 255)
    };
    private static readonly int[] s_AmmoAmounts = { 5, 2, 10, 4 };
    private static Material s_GlowMaterial;
    private static FirstPersonController s_Player;

    [SerializeField] private WeaponId m_weapon = WeaponId.Pistol;
    [SerializeField, Min(1)] private int m_amount = 5;
    [SerializeField] private Renderer m_boxRenderer;
    [SerializeField] private SpriteRenderer m_silhouetteRenderer;
    [SerializeField] private Sprite[] m_weaponSilhouettes = new Sprite[4];
    [SerializeField] private Vector2 m_silhouetteMaxSize = new(0.7f, 0.28f);

    private Collider m_trigger;
    private Transform m_cameraTransform;
    private GameplayObjectPool m_pool;

    public bool IsPooled { get; private set; }

    public static AmmoPickup Create(AmmoPickup prefab, Vector3 deathPosition, WeaponId weapon, int amount)
    {
        if (prefab == null)
        {
            Debug.LogError("AmmoPickup prefab is not assigned.");
            return null;
        }

        AmmoPickup pickup = Instantiate(prefab, FindGroundPosition(deathPosition), Quaternion.identity);
        pickup.Configure(weapon, amount);
        return pickup;
    }

    public static AmmoPickup CreateDrop(AmmoPickup prefab, Vector3 deathPosition)
    {
        FirstPersonController player = FirstPersonController.CurrentInstance;
        WeaponId primary = player != null ? player.PrimaryWeapon : WeaponId.Rifle;
        WeaponId weapon = ChooseAmmoWeapon(primary, UnityEngine.Random.value);
        return Create(prefab, deathPosition, weapon, GetAmount(weapon));
    }

    internal static WeaponId ChooseAmmoWeapon(WeaponId primary, float roll)
    {
        return roll < 0.5f ? primary : WeaponId.Pistol;
    }

    public void Configure(WeaponId weapon, int amount)
    {
        m_weapon = weapon is >= WeaponId.Pistol and <= WeaponId.DMR ? weapon : WeaponId.Pistol;
        m_amount = Mathf.Max(1, amount);
        if (m_boxRenderer == null)
        {
            m_boxRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (m_boxRenderer != null)
        {
            if (s_GlowMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                s_GlowMaterial = new Material(shader);
                s_GlowMaterial.EnableKeyword("_EMISSION");
            }

            Color color = GetColor(m_weapon);
            m_boxRenderer.sharedMaterial = s_GlowMaterial;
            MaterialPropertyBlock properties = new();
            properties.SetColor(s_BaseColor, color);
            properties.SetColor(s_EmissionColor, color);
            m_boxRenderer.SetPropertyBlock(properties);
        }
        ConfigureSilhouette();
    }

    public void PrepareForSpawn(GameplayObjectPool pool, Vector3 position, WeaponId weapon, int amount)
    {
        m_pool = pool;
        IsPooled = false;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        Configure(weapon, amount);
    }

    internal static int GetAmount(WeaponId weapon)
    {
        int index = (int)weapon - 1;
        return index >= 0 && index < s_AmmoAmounts.Length ? s_AmmoAmounts[index] : s_AmmoAmounts[0];
    }

    internal void MarkPooled()
    {
        IsPooled = true;
    }

    private void ConfigureSilhouette()
    {
        int index = (int)m_weapon - 1;
        if (m_silhouetteRenderer == null || m_weaponSilhouettes == null
            || index < 0 || index >= m_weaponSilhouettes.Length)
        {
            return;
        }

        Sprite silhouette = m_weaponSilhouettes[index];
        m_silhouetteRenderer.sprite = silhouette;
        m_silhouetteRenderer.enabled = silhouette != null;
        if (silhouette == null)
        {
            return;
        }

        m_silhouetteRenderer.color = GetColor(m_weapon);
        Vector2 spriteSize = silhouette.bounds.size;
        float scale = Mathf.Min(m_silhouetteMaxSize.x / spriteSize.x, m_silhouetteMaxSize.y / spriteSize.y);
        m_silhouetteRenderer.transform.localScale = Vector3.one * scale;
    }

    private static Color GetColor(WeaponId weapon)
    {
        int index = Mathf.Clamp((int)weapon - 1, 0, s_WeaponColors.Length - 1);
        return s_WeaponColors[index];
    }

    [ContextMenu("Run Ammo Pickup Self Check")]
    private void RunAmmoPickupSelfCheck()
    {
        Debug.Assert(GetAmount(WeaponId.Pistol) == 5);
        Debug.Assert(GetAmount(WeaponId.Shotgun) == 2);
        Debug.Assert(GetAmount(WeaponId.Rifle) == 10);
        Debug.Assert(GetAmount(WeaponId.DMR) == 4);
        Debug.Assert(GetColor(WeaponId.DMR) == GetColor(WeaponId.Rifle));
        Debug.Assert(ChooseAmmoWeapon(WeaponId.DMR, 0.4999f) == WeaponId.DMR);
        Debug.Assert(ChooseAmmoWeapon(WeaponId.DMR, 0.5f) == WeaponId.Pistol);
    }

    internal static Vector3 FindGroundPosition(Vector3 deathPosition)
    {
        Vector3 rayOrigin = deathPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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
            s_Player = FirstPersonController.CurrentInstance;
        }
        CharacterController controller = s_Player != null ? s_Player.GetComponent<CharacterController>() : null;
        if (controller != null && m_trigger != null && m_trigger.bounds.Intersects(controller.bounds))
        {
            TryCollect(s_Player);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.GetComponentInParent<FirstPersonController>());
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollect(other.GetComponentInParent<FirstPersonController>());
    }

    private void TryCollect(FirstPersonController player)
    {
        if (player == null || !player.TryAddAmmo(m_weapon, m_amount))
        {
            return;
        }

        s_Player = player;
        if (m_pool != null)
        {
            m_pool.ReleaseAmmo(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        if (m_weapon is < WeaponId.Pistol or > WeaponId.DMR)
        {
            m_weapon = WeaponId.Pistol;
        }
        m_amount = Mathf.Max(1, m_amount);
        m_silhouetteMaxSize.x = Mathf.Max(0.01f, m_silhouetteMaxSize.x);
        m_silhouetteMaxSize.y = Mathf.Max(0.01f, m_silhouetteMaxSize.y);
        if (m_weaponSilhouettes == null)
        {
            m_weaponSilhouettes = new Sprite[4];
        }
        else if (m_weaponSilhouettes.Length != 4)
        {
            Array.Resize(ref m_weaponSilhouettes, 4);
        }
    }
}
