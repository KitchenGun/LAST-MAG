using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerHealth), typeof(ScoreSystem))]
[RequireComponent(typeof(PlayerSkillController))]
public sealed class FirstPersonController : MonoBehaviour
{
    private const int k_WeaponSlotCount = 2;
    private const int k_ShotgunPelletCount = 8;
    private const float k_ShotgunSpreadAngle = 5f;
    private const float k_HeadshotDamageMultiplier = 2f;
    private const float k_RaycastDistance = 100f;
    private const float k_MaxPitch = 80f;
    // ponytail: fixed buffer avoids WebGL GC; raise only if one shot can cross 64 solid colliders.
    private const int k_DmrRaycastBufferSize = 64;
    private static readonly int[] s_StartingAmmo = { 15, 6, 30, 12 };
    private static readonly Comparer<RaycastHit> s_RaycastHitDistanceComparer =
        Comparer<RaycastHit>.Create(static (left, right) => left.distance.CompareTo(right.distance));

    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private GameplayHUD m_gameplayHUD;
    [SerializeField] private WeaponViewmodelController m_weaponViewmodel;
    [SerializeField] private PlayerSkillController m_skillController;
    [SerializeField] private ImpactSparkEmitter m_impactSparkEmitter;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [SerializeField] private float m_recoilKickSpeed = 18f;
    [SerializeField] private float m_recoilReturnSpeed = 10f;
    [Header("World Audio")]
    [SerializeField] private AudioClip m_wallImpactClip;
    [SerializeField] private float m_wallImpactMaxDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float m_wallImpactVolume = 0.7f;

    private readonly WeaponId[] m_loadout = new WeaponId[k_WeaponSlotCount];
    private readonly int[] m_weaponAmmo = new int[k_WeaponSlotCount];
    private readonly int[] m_maxWeaponAmmo = new int[k_WeaponSlotCount];
    private readonly RaycastHit[] m_dmrHits = new RaycastHit[k_DmrRaycastBufferSize];
    private readonly HashSet<EnemyHealth> m_dmrDamagedEnemies = new();
    private readonly HashSet<Collider> m_dmrHitStructures = new();
    private readonly Dictionary<EnemyHealth, float> m_shotgunDamageByEnemy = new();
    private readonly HashSet<EnemyHealth> m_shotgunHeadshotEnemies = new();
    private CharacterController m_characterController;
    private PlayerHealth m_playerHealth;
    private InputActionAsset m_runtimeInputActions;
    private InputActionMap m_playerMap;
    private InputAction m_moveAction;
    private InputAction m_lookAction;
    private InputAction m_attackAction;
    private InputAction m_jumpAction;
    private ScoreSystem m_scoreSystem;
    private float m_verticalVelocity;
    private float m_pitch;
    private float m_cameraRecoilPitch;
    private float m_cameraRecoilTargetPitch;
    private float m_cameraRecoilYaw;
    private float m_cameraRecoilTargetYaw;
    private float m_nextAllowedFireTime;
    private int m_activeWeaponSlot = 1;
    private bool m_isRifleFiring;

    public static FirstPersonController CurrentInstance { get; private set; }
    public int ActiveWeaponSlot => m_activeWeaponSlot;
    public PlayerClassId SelectedClass { get; private set; }
    public WeaponId CurrentWeapon => m_loadout[m_activeWeaponSlot - 1];
    public WeaponId PrimaryWeapon => m_loadout[0];

    private void Awake()
    {
        CurrentInstance = this;
        m_characterController = GetComponent<CharacterController>();
        m_playerHealth = GetComponent<PlayerHealth>();
        m_scoreSystem = GetComponent<ScoreSystem>();
        m_skillController = GetComponent<PlayerSkillController>();
        Debug.Assert(m_characterController != null && m_playerHealth != null && m_scoreSystem != null
            && m_skillController != null, "PF_Player is missing a required component.");

        SelectedClass = RunResultStore.SelectedClass;
        if (SelectedClass == PlayerClassId.Unknown)
        {
            SelectedClass = PlayerClassId.Grenadier;
            RunResultStore.SelectClass(SelectedClass);
            Debug.LogWarning("GameplayScene started without a class selection. Using Grenadier.");
        }

        ConfigureLoadout();
    }

    private void Start()
    {
        Debug.Assert(m_gameplayHUD != null && m_playerCamera != null && m_weaponViewmodel != null
            && IsAmmoConfigurationValid());
        m_gameplayHUD?.BindPlayerHealth(m_playerHealth);
        m_scoreSystem.Initialize(m_gameplayHUD);
        m_skillController.Initialize(SelectedClass, m_playerCamera, m_playerHealth, m_gameplayHUD, m_scoreSystem);
        SelectWeapon(1);
    }

    private void OnEnable()
    {
        if (!InitializeInput())
        {
            return;
        }

        m_attackAction.performed += OnAttack;
        m_attackAction.canceled += OnAttackCanceled;
        m_jumpAction.performed += OnJump;
        m_playerMap.Enable();
    }

    private bool InitializeInput()
    {
        if (m_playerMap != null)
        {
            return true;
        }

        if (m_inputActions == null || m_playerCamera == null)
        {
            Debug.LogError("FirstPersonController input or camera is not assigned.");
            return false;
        }

        m_runtimeInputActions = Instantiate(m_inputActions);
        m_playerMap = m_runtimeInputActions.FindActionMap("Player", true);
        m_moveAction = m_playerMap.FindAction("Move", true);
        m_lookAction = m_playerMap.FindAction("Look", true);
        m_attackAction = m_playerMap.FindAction("Attack", true);
        m_jumpAction = m_playerMap.FindAction("Jump", true);
        return true;
    }

    private void OnDisable()
    {
        m_isRifleFiring = false;
        if (m_playerMap != null)
        {
            m_attackAction.performed -= OnAttack;
            m_attackAction.canceled -= OnAttackCanceled;
            m_jumpAction.performed -= OnJump;
            m_playerMap.Disable();
        }
        UnlockCursor();
    }

    private void OnDestroy()
    {
        if (CurrentInstance == this)
        {
            CurrentInstance = null;
        }
        if (m_runtimeInputActions != null)
        {
            Destroy(m_runtimeInputActions);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        HandleWeaponSelection();
        if (m_playerMap == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            m_skillController?.TryActivateOrArm();
        }

        HandleLook();
        HandleMovement();
        HandleAutomaticFire();
    }

    private void ConfigureLoadout()
    {
        m_loadout[0] = RunResultStore.GetPrimaryWeapon(SelectedClass);
        m_loadout[1] = WeaponId.Pistol;
        for (int index = 0; index < k_WeaponSlotCount; index++)
        {
            int ammo = GetStartingAmmo(m_loadout[index]);
            m_weaponAmmo[index] = ammo;
            m_maxWeaponAmmo[index] = ammo;
        }
    }

    private void HandleWeaponSelection()
    {
        if (Keyboard.current == null)
        {
            return;
        }
        if (m_skillController != null && m_skillController.IsWeaponInputLocked)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectWeapon(1);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectWeapon(2);
        }
    }

    private void SelectWeapon(int slot)
    {
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        m_skillController?.CancelArmedSkill();
        m_isRifleFiring = false;
        m_activeWeaponSlot = slot;
        m_weaponViewmodel?.SelectWeapon(CurrentWeapon);
        RefreshWeaponHud();
    }

    private void RefreshWeaponHud()
    {
        for (int index = 0; index < k_WeaponSlotCount; index++)
        {
            m_gameplayHUD?.RefreshWeapon(index + 1, m_loadout[index], m_weaponAmmo[index], index + 1 == m_activeWeaponSlot);
        }
    }

    public bool TryAddAmmo(WeaponId weapon, int amount)
    {
        int index = Array.IndexOf(m_loadout, weapon);
        if (index < 0 || amount <= 0)
        {
            return false;
        }

        int addedAmount = Mathf.Min(amount, m_maxWeaponAmmo[index] - m_weaponAmmo[index]);
        if (addedAmount <= 0)
        {
            return false;
        }

        m_weaponAmmo[index] += addedAmount;
        m_gameplayHUD?.RefreshWeapon(index + 1, weapon, m_weaponAmmo[index], index + 1 == m_activeWeaponSlot);
        m_gameplayHUD?.ShowAmmoPickup(weapon, addedAmount);
        return true;
    }

    private void HandleLook()
    {
        Vector2 look = m_lookAction.ReadValue<Vector2>() * m_lookSensitivity;
        m_pitch = Mathf.Clamp(m_pitch - look.y, -k_MaxPitch, k_MaxPitch);
        UpdateCameraRecoil();
        m_playerCamera.transform.localRotation = Quaternion.Euler(m_pitch - m_cameraRecoilPitch, m_cameraRecoilYaw, 0f);
        transform.Rotate(Vector3.up * look.x);
    }

    private void UpdateCameraRecoil()
    {
        float deltaTime = Time.deltaTime;
        if (m_cameraRecoilTargetPitch > 0f)
        {
            m_cameraRecoilPitch = Mathf.Lerp(m_cameraRecoilPitch, m_cameraRecoilTargetPitch,
                Mathf.Clamp01(m_recoilKickSpeed * deltaTime));
            m_cameraRecoilYaw = Mathf.Lerp(m_cameraRecoilYaw, m_cameraRecoilTargetYaw,
                Mathf.Clamp01(m_recoilKickSpeed * deltaTime));
            if (Mathf.Abs(m_cameraRecoilPitch - m_cameraRecoilTargetPitch) < 0.01f)
            {
                if (m_isRifleFiring)
                {
                    float committedRecoil = Mathf.Min(m_cameraRecoilPitch, m_pitch + k_MaxPitch);
                    m_pitch -= committedRecoil;
                    m_cameraRecoilPitch -= committedRecoil;
                    m_cameraRecoilTargetPitch = m_cameraRecoilPitch;
                }
                else
                {
                    m_cameraRecoilTargetPitch = 0f;
                }
            }
            return;
        }

        m_cameraRecoilPitch = Mathf.Lerp(m_cameraRecoilPitch, 0f, Mathf.Clamp01(m_recoilReturnSpeed * deltaTime));
        m_cameraRecoilYaw = Mathf.Lerp(m_cameraRecoilYaw, 0f, Mathf.Clamp01(m_recoilReturnSpeed * deltaTime));
    }

    private void HandleMovement()
    {
        if (m_characterController.isGrounded && m_verticalVelocity < 0f)
        {
            m_verticalVelocity = -2f;
        }

        float deltaTime = Time.deltaTime;
        m_verticalVelocity += m_gravity * deltaTime;
        Vector2 move = m_moveAction.ReadValue<Vector2>();
        Vector3 velocity = transform.TransformDirection(new Vector3(move.x, 0f, move.y)) * m_moveSpeed;
        velocity.y = m_verticalVelocity;
        m_characterController.Move(velocity * deltaTime);
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
            return;
        }

        if (m_skillController != null && m_skillController.TryUseArmedSkill())
        {
            return;
        }
        if (m_skillController != null && m_skillController.IsWeaponInputLocked)
        {
            return;
        }

        m_isRifleFiring = CurrentWeapon == WeaponId.Rifle;
        TryFireCurrentWeapon();
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        m_isRifleFiring = false;
    }

    private void HandleAutomaticFire()
    {
        if (m_skillController != null && m_skillController.IsWeaponInputLocked)
        {
            m_isRifleFiring = false;
            return;
        }
        if (!m_isRifleFiring || CurrentWeapon != WeaponId.Rifle || m_attackAction == null || !m_attackAction.IsPressed())
        {
            m_isRifleFiring = false;
            return;
        }
        TryFireCurrentWeapon();
    }

    private bool TryFireCurrentWeapon()
    {
        int index = m_activeWeaponSlot - 1;
        if (m_weaponAmmo[index] == 0)
        {
            m_gameplayHUD?.ShowEmptyAmmoFeedback();
            m_gameplayHUD?.ShowEmptyAmmoPopup(CurrentWeapon);
            m_weaponViewmodel?.PlayEmptyAmmoFeedback();
            m_isRifleFiring = false;
            return false;
        }

        if (Time.unscaledTime < m_nextAllowedFireTime)
        {
            return false;
        }

        m_nextAllowedFireTime = Time.unscaledTime + GetFireInterval(CurrentWeapon);
        m_weaponAmmo[index]--;
        m_gameplayHUD?.RefreshWeapon(m_activeWeaponSlot, CurrentWeapon, m_weaponAmmo[index], true);
        m_weaponViewmodel?.PlayFireFeedback();
        ApplyCameraRecoil(GetVerticalRecoil(CurrentWeapon), GetHorizontalRecoil(CurrentWeapon));

        switch (CurrentWeapon)
        {
            case WeaponId.Shotgun:
                FireShotgun();
                break;
            case WeaponId.DMR:
                FireDmr();
                break;
            default:
                FireSingleRay(CurrentWeapon);
                break;
        }
        return true;
    }

    private void FireSingleRay(WeaponId weapon)
    {
        Ray ray = new(m_playerCamera.transform.position, CreateSingleRayDirection(weapon));
        float rayLength = k_RaycastDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            rayLength = hit.distance;
            m_impactSparkEmitter?.EmitAt(hit.point, hit.normal);
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                bool isHeadshot = enemy.IsHeadHit(ray, k_RaycastDistance);
                float damage = GetDamage(weapon) * (isHeadshot ? k_HeadshotDamageMultiplier : 1f);
                bool killed = enemy.ApplyDamage(damage, KillContext.Direct(weapon, isHeadshot));
                if (killed)
                {
                    m_scoreSystem.RegisterDirectKill(enemy.Type, weapon, isHeadshot);
                }
                m_gameplayHUD?.ShowHitMarker(isHeadshot, killed);
            }
            else
            {
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
            }
        }
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.red, 0.1f);
    }

    private void FireDmr()
    {
        Ray ray = new(m_playerCamera.transform.position, m_playerCamera.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, m_dmrHits, k_RaycastDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(m_dmrHits, 0, hitCount, s_RaycastHitDistanceComparer);
        m_dmrDamagedEnemies.Clear();
        m_dmrHitStructures.Clear();
        int collisionIndex = 0;
        bool anyHeadshot = false;
        bool anyKill = false;
        bool playedWallImpact = false;
        float rayLength = k_RaycastDistance;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = m_dmrHits[hitIndex];
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                if (!m_dmrDamagedEnemies.Add(enemy))
                {
                    continue;
                }
            }
            else if (!m_dmrHitStructures.Add(hit.collider))
            {
                continue;
            }

            float damage = collisionIndex switch { 0 => 60f, 1 => 40f, _ => 20f };
            bool isHeadshot = enemy != null && enemy.IsHeadHit(ray, hit.distance + 0.5f);
            if (enemy != null)
            {
                bool killed = enemy.ApplyDamage(damage * (isHeadshot ? k_HeadshotDamageMultiplier : 1f),
                    KillContext.Direct(WeaponId.DMR, isHeadshot));
                if (killed)
                {
                    m_scoreSystem.RegisterDirectKill(enemy.Type, WeaponId.DMR, isHeadshot);
                }
                anyHeadshot |= isHeadshot;
                anyKill |= killed;
            }
            else if (!playedWallImpact)
            {
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
                playedWallImpact = true;
            }

            m_impactSparkEmitter?.EmitAt(hit.point, hit.normal);
            rayLength = hit.distance;
            collisionIndex++;
            if (collisionIndex == 3)
            {
                break;
            }
        }

        if (m_dmrDamagedEnemies.Count > 0)
        {
            m_gameplayHUD?.ShowHitMarker(anyHeadshot, anyKill);
        }
        m_weaponViewmodel?.PlayDmrTracer(ray.GetPoint(rayLength));
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.cyan, 0.15f);
    }

    private void FireShotgun()
    {
        m_shotgunDamageByEnemy.Clear();
        m_shotgunHeadshotEnemies.Clear();
        bool playedWallImpact = false;
        for (int pellet = 0; pellet < k_ShotgunPelletCount; pellet++)
        {
            Ray ray = new(m_playerCamera.transform.position, CreateShotgunDirection());
            float rayLength = k_RaycastDistance;
            if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                rayLength = hit.distance;
                m_impactSparkEmitter?.EmitAt(hit.point, hit.normal);
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    bool isHeadshot = enemy.IsHeadHit(ray, k_RaycastDistance);
                    m_shotgunDamageByEnemy.TryGetValue(enemy, out float damage);
                    m_shotgunDamageByEnemy[enemy] = damage + 12f * (isHeadshot ? k_HeadshotDamageMultiplier : 1f);
                    if (isHeadshot)
                    {
                        m_shotgunHeadshotEnemies.Add(enemy);
                    }
                }
                else if (!playedWallImpact)
                {
                    SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
                    playedWallImpact = true;
                }
            }
            Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.yellow, 0.1f);
        }

        bool anyHeadshot = false;
        bool anyKill = false;
        foreach (KeyValuePair<EnemyHealth, float> hit in m_shotgunDamageByEnemy)
        {
            bool isHeadshot = m_shotgunHeadshotEnemies.Contains(hit.Key);
            bool killed = hit.Key.ApplyDamage(hit.Value, KillContext.Direct(WeaponId.Shotgun, isHeadshot));
            if (killed)
            {
                m_scoreSystem.RegisterDirectKill(hit.Key.Type, WeaponId.Shotgun, isHeadshot);
            }
            anyHeadshot |= isHeadshot;
            anyKill |= killed;
        }
        if (m_shotgunDamageByEnemy.Count > 0)
        {
            m_gameplayHUD?.ShowHitMarker(anyHeadshot, anyKill);
        }
    }

    private Vector3 CreateShotgunDirection()
    {
        Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(k_ShotgunSpreadAngle * Mathf.Deg2Rad);
        Transform cameraTransform = m_playerCamera.transform;
        return (cameraTransform.forward + cameraTransform.right * spread.x + cameraTransform.up * spread.y).normalized;
    }

    private Vector3 CreateSingleRayDirection(WeaponId weapon)
    {
        float spreadRange = weapon switch
        {
            WeaponId.Pistol => 0.35f,
            WeaponId.Rifle => 0.75f,
            _ => 0f
        };
        float spread = UnityEngine.Random.Range(-spreadRange, spreadRange);
        Transform cameraTransform = m_playerCamera.transform;
        return Quaternion.AngleAxis(spread, cameraTransform.up) * cameraTransform.forward;
    }

    private static float GetDamage(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => 30f,
            WeaponId.Rifle => 15f,
            WeaponId.Shotgun => 12f,
            _ => 60f
        };
    }

    private static float GetFireInterval(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => 1f / 6.75f,
            WeaponId.Shotgun => 1f / 1.1f,
            WeaponId.Rifle => 1f / 11f,
            WeaponId.DMR => 1f / 5.25f,
            _ => float.MaxValue
        };
    }

    private static float GetVerticalRecoil(WeaponId weapon)
    {
        return weapon == WeaponId.Shotgun ? 1.5f : 0.65f;
    }

    private static float GetHorizontalRecoil(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => 0.35f,
            WeaponId.Rifle => 0.15f,
            WeaponId.DMR => 0.2f,
            _ => 0f
        };
    }

    internal void ApplyCameraRecoil(float verticalRecoil, float horizontalRecoil)
    {
        m_cameraRecoilTargetPitch = Mathf.Max(m_cameraRecoilPitch, m_cameraRecoilTargetPitch)
            + Mathf.Max(0f, verticalRecoil);
        float spread = Mathf.Max(0f, horizontalRecoil);
        m_cameraRecoilTargetYaw = UnityEngine.Random.Range(-spread, spread);
    }

    private static int GetStartingAmmo(WeaponId weapon)
    {
        int index = (int)weapon - 1;
        return index >= 0 && index < s_StartingAmmo.Length ? s_StartingAmmo[index] : 0;
    }

    [ContextMenu("Run Weapon Fire Self Check")]
    private void RunWeaponFireSelfCheck()
    {
        Debug.Assert(k_WeaponSlotCount == 2 && IsAmmoConfigurationValid());
        Debug.Assert(Mathf.Approximately(60f / GetFireInterval(WeaponId.Pistol), 405f));
        Debug.Assert(Mathf.Approximately(60f / GetFireInterval(WeaponId.Shotgun), 66f));
        Debug.Assert(Mathf.Approximately(60f / GetFireInterval(WeaponId.Rifle), 660f));
        Debug.Assert(Mathf.Approximately(60f / GetFireInterval(WeaponId.DMR), 315f));
        Debug.Assert(GetStartingAmmo(WeaponId.Pistol) == 15 && GetStartingAmmo(WeaponId.Shotgun) == 6
            && GetStartingAmmo(WeaponId.Rifle) == 30 && GetStartingAmmo(WeaponId.DMR) == 12);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Grenadier) == WeaponId.Rifle
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Engineer) == WeaponId.Shotgun
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Sniper) == WeaponId.DMR);
        Debug.Assert(k_ShotgunPelletCount == 8 && Mathf.Approximately(k_ShotgunSpreadAngle, 5f));
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (Cursor.lockState == CursorLockMode.Locked && m_characterController.isGrounded)
        {
            m_verticalVelocity = Mathf.Sqrt(m_jumpHeight * -2f * m_gravity);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            UnlockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        m_isRifleFiring = false;
        m_skillController?.CancelArmedSkill();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_jumpHeight = Mathf.Max(0f, m_jumpHeight);
        m_recoilKickSpeed = Mathf.Max(0.1f, m_recoilKickSpeed);
        m_recoilReturnSpeed = Mathf.Max(0.1f, m_recoilReturnSpeed);
        m_wallImpactMaxDistance = Mathf.Max(0.1f, m_wallImpactMaxDistance);
    }

    private bool IsAmmoConfigurationValid()
    {
        for (int index = 0; index < k_WeaponSlotCount; index++)
        {
            if (m_loadout[index] == WeaponId.Unknown || m_maxWeaponAmmo[index] <= 0
                || m_weaponAmmo[index] < 0 || m_weaponAmmo[index] > m_maxWeaponAmmo[index])
            {
                return false;
            }
        }
        return m_activeWeaponSlot >= 1 && m_activeWeaponSlot <= k_WeaponSlotCount;
    }
}
