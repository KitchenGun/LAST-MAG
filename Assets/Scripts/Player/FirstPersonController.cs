using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
internal struct RecoilProfile
{
    [SerializeField] private float m_pitch;
    [SerializeField] private float m_yaw;
    [SerializeField, Range(0f, 1f)] private float m_randomRange;
    [SerializeField] private float m_softCap;
    [SerializeField] private float m_hardCap;
    [SerializeField, Range(0f, 1f)] private float m_continuousResidualRatio;
    [SerializeField] private float m_kickDuration;
    [SerializeField] private float m_recoveryDelay;
    [SerializeField] private float m_returnDuration;
    [SerializeField] private float m_fireImpulsePitch;
    [SerializeField] private float m_fireImpulseYaw;
    [SerializeField] private float m_fireImpulseRoll;
    [SerializeField] private float m_fireImpulseDuration;

    internal RecoilProfile(float pitch, float yaw, float randomRange, float softCap, float hardCap,
        float continuousResidualRatio, float kickDuration, float recoveryDelay, float returnDuration,
        float fireImpulsePitch, float fireImpulseYaw, float fireImpulseRoll, float fireImpulseDuration)
    {
        m_pitch = pitch;
        m_yaw = yaw;
        m_randomRange = randomRange;
        m_softCap = softCap;
        m_hardCap = hardCap;
        m_continuousResidualRatio = continuousResidualRatio;
        m_kickDuration = kickDuration;
        m_recoveryDelay = recoveryDelay;
        m_returnDuration = returnDuration;
        m_fireImpulsePitch = fireImpulsePitch;
        m_fireImpulseYaw = fireImpulseYaw;
        m_fireImpulseRoll = fireImpulseRoll;
        m_fireImpulseDuration = fireImpulseDuration;
    }

    internal RecoilSample CreateSample(float yawDirection = 0f)
    {
        float randomRange = Mathf.Clamp01(m_randomRange);
        float basePitch = Mathf.Max(0f, m_pitch);
        float pitch = basePitch * UnityEngine.Random.Range(1f - randomRange, 1f + randomRange);
        float horizontalScale = UnityEngine.Random.Range(0.75f, 1f);
        float direction = yawDirection == 0f ? (UnityEngine.Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(yawDirection);
        float yaw = Mathf.Min(Mathf.Max(0f, m_yaw) * horizontalScale, pitch * 0.4f) * direction;
        return new RecoilSample(
            pitch,
            yaw,
            basePitch > 0f ? pitch / basePitch : 0f,
            horizontalScale,
            Mathf.Max(0.001f, m_softCap),
            Mathf.Max(m_softCap, m_hardCap),
            Mathf.Clamp01(m_continuousResidualRatio),
            Mathf.Max(0.001f, m_kickDuration),
            Mathf.Max(0f, m_recoveryDelay),
            Mathf.Max(0.001f, m_returnDuration),
            Mathf.Max(0f, m_fireImpulsePitch),
            Mathf.Max(0f, m_fireImpulseYaw) * horizontalScale * direction,
            Mathf.Max(0f, m_fireImpulseRoll) * horizontalScale * direction,
            Mathf.Max(0.001f, m_fireImpulseDuration));
    }

    internal bool IsValid()
    {
        return m_pitch > 0f && m_yaw >= 0f && m_randomRange >= 0f && m_randomRange <= 1f
            && m_softCap > 0f && m_hardCap >= m_softCap
            && m_continuousResidualRatio >= 0f && m_continuousResidualRatio <= 1f && m_kickDuration > 0f
            && m_recoveryDelay >= m_kickDuration && m_returnDuration > 0f
            && m_fireImpulsePitch >= 0f && m_fireImpulseYaw >= 0f && m_fireImpulseRoll >= 0f
            && m_fireImpulseDuration > 0f;
    }
}

internal readonly struct RecoilSample
{
    internal RecoilSample(float pitch, float yaw, float verticalScale, float horizontalScale,
        float softCap, float hardCap, float continuousResidualRatio, float kickDuration,
        float recoveryDelay, float returnDuration, float fireImpulsePitch, float fireImpulseYaw,
        float fireImpulseRoll, float fireImpulseDuration)
    {
        Pitch = pitch;
        Yaw = yaw;
        VerticalScale = verticalScale;
        HorizontalScale = horizontalScale;
        SoftCap = softCap;
        HardCap = hardCap;
        ContinuousResidualRatio = continuousResidualRatio;
        KickDuration = kickDuration;
        RecoveryDelay = recoveryDelay;
        ReturnDuration = returnDuration;
        FireImpulsePitch = fireImpulsePitch;
        FireImpulseYaw = fireImpulseYaw;
        FireImpulseRoll = fireImpulseRoll;
        FireImpulseDuration = fireImpulseDuration;
    }

    internal float Pitch { get; }
    internal float Yaw { get; }
    internal float VerticalScale { get; }
    internal float HorizontalScale { get; }
    internal float HorizontalDirection => Mathf.Sign(Yaw);
    internal float SoftCap { get; }
    internal float HardCap { get; }
    internal float ContinuousResidualRatio { get; }
    internal float KickDuration { get; }
    internal float RecoveryDelay { get; }
    internal float ReturnDuration { get; }
    internal float FireImpulsePitch { get; }
    internal float FireImpulseYaw { get; }
    internal float FireImpulseRoll { get; }
    internal float FireImpulseDuration { get; }

    internal static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    internal static float EaseOutQuad(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse;
    }
}

internal enum RecoilPhase
{
    Idle,
    Kick,
    Hold,
    Return
}

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
    private const float k_DmrZoomFieldOfView = 45f;
    private const float k_DmrZoomTransitionDuration = 0.12f;
    private const float k_DmrZoomSensitivityMultiplier = 0.5f;
    private const float k_DamageAimPunchKickSpeed = 30f;
    private const float k_DamageAimPunchReturnSpeed = 8f;
    private const float k_DamageAimPunchSoftCap = 3f;
    private const float k_DamageAimPunchHardCapPitch = 4f;
    private const float k_DamageAimPunchHardCapYaw = 2f;
    private const float k_RifleBurstResetTime = 0.25f;
    private const int k_RifleYawDirectionShots = 4;
    private const float k_PistolContinuousWindow = 0.24f;
    private const float k_RifleContinuousWindow = 0.32f;
    private const float k_DmrContinuousWindow = 0.42f;
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
    [Header("Weapon Recoil")]
    [SerializeField] private RecoilProfile m_pistolRecoil = new(2.2f, 0.35f, 0.1f, 3.5f, 4.5f,
        0.2f, 0.055f, 0.1f, 0.12f, 0.35f, 0.05f, 0.12f, 0.08f);
    [SerializeField] private RecoilProfile m_shotgunRecoil = new(4.6f, 0.85f, 0.15f, 4.5f, 5.5f,
        0.3f, 0.075f, 0.28f, 0.29f, 0.75f, 0.15f, 0.28f, 0.13f);
    [SerializeField] private RecoilProfile m_rifleRecoil = new(2f, 0.32f, 0.12f, 6f, 7f,
        0.35f, 0.045f, 0.14f, 0.15f, 0.25f, 0.05f, 0.12f, 0.06f);
    [SerializeField] private RecoilProfile m_dmrRecoil = new(3.2f, 0.45f, 0.1f, 4.8f, 6f,
        0.25f, 0.065f, 0.18f, 0.2f, 0.5f, 0.1f, 0.18f, 0.1f);
    [SerializeField] private RecoilProfile m_rocketRecoil = new(5.6f, 1.1f, 0.15f, 5.5f, 6.5f,
        0f, 0.1f, 0.28f, 0.42f, 1f, 0.2f, 0.35f, 0.16f);
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
    private float m_defaultCameraFieldOfView;
    private float m_cameraRecoilPitch;
    private float m_cameraRecoilYaw;
    private Vector2 m_cameraRecoilStart;
    private Vector2 m_cameraRecoilTarget;
    private Vector2 m_cameraRecoilReturnStart;
    private Vector2 m_cameraRecoilReturnTarget;
    private RecoilSample m_activeRecoilSample;
    private RecoilPhase m_cameraRecoilPhase;
    private float m_cameraRecoilPhaseStartedAt;
    private float m_cameraRecoilLastShotAt;
    private Vector3 m_fireImpulse;
    private Vector3 m_fireImpulseStart;
    private float m_fireImpulseStartedAt;
    private float m_fireImpulseDuration;
    private float m_damageAimPunchPitch;
    private float m_damageAimPunchTargetPitch;
    private float m_damageAimPunchYaw;
    private float m_damageAimPunchTargetYaw;
    private float m_nextAllowedFireTime;
    private float m_lastRifleShotTime = float.NegativeInfinity;
    private float m_rifleYawDirection;
    private int m_rifleBurstShots;
    private WeaponId m_burstWeapon = WeaponId.Unknown;
    private float m_lastSuccessfulShotAt = float.NegativeInfinity;
    private int m_burstShotCount;
    private bool m_commitResidualOnReturn;
    private int m_activeWeaponSlot = 1;
    private bool m_isRifleFiring;
    private bool m_isDmrZoomed;

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
        if (m_playerCamera != null)
        {
            m_defaultCameraFieldOfView = m_playerCamera.fieldOfView;
        }
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
        ResetCameraRecoil();
        m_damageAimPunchPitch = 0f;
        m_damageAimPunchTargetPitch = 0f;
        m_damageAimPunchYaw = 0f;
        m_damageAimPunchTargetYaw = 0f;
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
        if (m_playerMap != null && Cursor.lockState == CursorLockMode.Locked)
        {
            HandleDmrZoomInput();
        }
        UpdateDmrZoom();
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
        m_isDmrZoomed = false;
        m_activeWeaponSlot = slot;
        m_weaponViewmodel?.SelectWeapon(CurrentWeapon);
        m_gameplayHUD?.SetDmrAimState(CurrentWeapon == WeaponId.DMR, false);
        RefreshWeaponHud();
    }

    private void HandleDmrZoomInput()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame
            || CurrentWeapon != WeaponId.DMR)
        {
            return;
        }

        m_isDmrZoomed = !m_isDmrZoomed;
        m_gameplayHUD?.SetDmrAimState(true, m_isDmrZoomed);
    }

    private void UpdateDmrZoom()
    {
        if (m_playerCamera == null || m_defaultCameraFieldOfView <= 0f)
        {
            return;
        }

        float targetFieldOfView = m_isDmrZoomed ? k_DmrZoomFieldOfView : m_defaultCameraFieldOfView;
        float speed = Mathf.Abs(m_defaultCameraFieldOfView - k_DmrZoomFieldOfView)
            / k_DmrZoomTransitionDuration;
        m_playerCamera.fieldOfView = Mathf.MoveTowards(
            m_playerCamera.fieldOfView, targetFieldOfView, speed * Time.unscaledDeltaTime);
    }

    private void ResetDmrZoom(bool immediate)
    {
        m_isDmrZoomed = false;
        if (immediate && m_playerCamera != null && m_defaultCameraFieldOfView > 0f)
        {
            m_playerCamera.fieldOfView = m_defaultCameraFieldOfView;
        }
        m_gameplayHUD?.SetDmrAimState(CurrentWeapon == WeaponId.DMR, false);
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
        float sensitivity = m_lookSensitivity
            * (m_isDmrZoomed ? k_DmrZoomSensitivityMultiplier : 1f);
        Vector2 look = m_lookAction.ReadValue<Vector2>() * sensitivity;
        m_pitch = Mathf.Clamp(m_pitch - look.y, -k_MaxPitch, k_MaxPitch);
        UpdateCameraRecoil();
        UpdateFireImpulse();
        UpdateDamageAimPunch();
        m_playerCamera.transform.localRotation = Quaternion.Euler(
            m_pitch - m_cameraRecoilPitch - m_fireImpulse.x - m_damageAimPunchPitch,
            m_cameraRecoilYaw + m_fireImpulse.y + m_damageAimPunchYaw,
            m_fireImpulse.z);
        transform.Rotate(Vector3.up * look.x);
    }

    private void UpdateCameraRecoil()
    {
        ClampCameraRecoilToPitchHeadroom();
        float now = Time.unscaledTime;
        switch (m_cameraRecoilPhase)
        {
            case RecoilPhase.Kick:
                float kick = RecoilSample.EaseOutQuad(
                    (now - m_cameraRecoilPhaseStartedAt) / m_activeRecoilSample.KickDuration);
                SetCameraRecoil(Vector2.LerpUnclamped(m_cameraRecoilStart, m_cameraRecoilTarget, kick));
                if (kick >= 1f)
                {
                    m_cameraRecoilPhase = RecoilPhase.Hold;
                }
                break;
            case RecoilPhase.Hold:
                SetCameraRecoil(m_cameraRecoilTarget);
                break;
            case RecoilPhase.Return:
                float recovery = RecoilSample.EaseOutCubic(
                    (now - m_cameraRecoilPhaseStartedAt) / m_activeRecoilSample.ReturnDuration);
                SetCameraRecoil(Vector2.LerpUnclamped(
                    m_cameraRecoilReturnStart, m_cameraRecoilReturnTarget, recovery));
                if (recovery >= 1f)
                {
                    CompleteCameraRecoilReturn();
                }
                break;
        }

        if ((m_cameraRecoilPhase == RecoilPhase.Kick || m_cameraRecoilPhase == RecoilPhase.Hold)
            && now >= m_cameraRecoilLastShotAt + m_activeRecoilSample.RecoveryDelay
            && now >= m_cameraRecoilLastShotAt + m_activeRecoilSample.KickDuration)
        {
            m_cameraRecoilReturnStart = new Vector2(m_cameraRecoilPitch, m_cameraRecoilYaw);
            m_cameraRecoilReturnTarget = m_commitResidualOnReturn
                ? m_cameraRecoilReturnStart * m_activeRecoilSample.ContinuousResidualRatio
                : Vector2.zero;
            m_cameraRecoilPhaseStartedAt = now;
            m_cameraRecoilPhase = RecoilPhase.Return;
        }
        ClampCameraRecoilToPitchHeadroom();
    }

    private void ClampCameraRecoilToPitchHeadroom()
    {
        m_cameraRecoilPitch = ClampRecoilPitch(m_cameraRecoilPitch, m_pitch);
        m_cameraRecoilStart.x = ClampRecoilPitch(m_cameraRecoilStart.x, m_pitch);
        m_cameraRecoilTarget.x = ClampRecoilPitch(m_cameraRecoilTarget.x, m_pitch);
        m_cameraRecoilReturnStart.x = ClampRecoilPitch(m_cameraRecoilReturnStart.x, m_pitch);
        m_cameraRecoilReturnTarget.x = ClampRecoilPitch(m_cameraRecoilReturnTarget.x, m_pitch);
    }

    private static float ClampRecoilPitch(float recoilPitch, float aimPitch)
    {
        return Mathf.Clamp(recoilPitch, 0f, Mathf.Max(0f, aimPitch + k_MaxPitch));
    }

    private void SetCameraRecoil(Vector2 recoil)
    {
        m_cameraRecoilPitch = Mathf.Max(0f, recoil.x);
        m_cameraRecoilYaw = recoil.y;
    }

    private void ResetCameraRecoil()
    {
        m_cameraRecoilPitch = 0f;
        m_cameraRecoilYaw = 0f;
        m_cameraRecoilStart = Vector2.zero;
        m_cameraRecoilTarget = Vector2.zero;
        m_cameraRecoilReturnStart = Vector2.zero;
        m_cameraRecoilReturnTarget = Vector2.zero;
        m_activeRecoilSample = default;
        m_cameraRecoilPhase = RecoilPhase.Idle;
        m_cameraRecoilPhaseStartedAt = 0f;
        m_cameraRecoilLastShotAt = 0f;
        m_fireImpulse = Vector3.zero;
        m_fireImpulseStart = Vector3.zero;
        m_fireImpulseStartedAt = 0f;
        m_fireImpulseDuration = 0f;
        m_lastRifleShotTime = float.NegativeInfinity;
        m_rifleYawDirection = 0f;
        m_rifleBurstShots = 0;
        ResetBurstTracking();
        m_commitResidualOnReturn = false;
    }

    private void CompleteCameraRecoilReturn()
    {
        if (m_commitResidualOnReturn)
        {
            Vector2 residual = m_cameraRecoilReturnTarget;
            m_pitch = Mathf.Clamp(m_pitch - residual.x, -k_MaxPitch, k_MaxPitch);
            transform.Rotate(Vector3.up * residual.y);
        }
        ResetCameraRecoil();
    }

    private void ResetBurstTracking()
    {
        m_burstWeapon = WeaponId.Unknown;
        m_lastSuccessfulShotAt = float.NegativeInfinity;
        m_burstShotCount = 0;
    }

    private void UpdateFireImpulse()
    {
        if (m_fireImpulseDuration <= 0f)
        {
            return;
        }

        float elapsed = Time.unscaledTime - m_fireImpulseStartedAt;
        float amount = 1f - RecoilSample.EaseOutCubic(elapsed / m_fireImpulseDuration);
        m_fireImpulse = m_fireImpulseStart * amount;
        if (elapsed >= m_fireImpulseDuration)
        {
            m_fireImpulse = Vector3.zero;
            m_fireImpulseStart = Vector3.zero;
            m_fireImpulseDuration = 0f;
        }
    }

    private void UpdateDamageAimPunch()
    {
        float deltaTime = Time.deltaTime;
        if (m_damageAimPunchTargetPitch > 0f)
        {
            float kickBlend = Mathf.Clamp01(k_DamageAimPunchKickSpeed * deltaTime);
            m_damageAimPunchPitch = Mathf.Lerp(m_damageAimPunchPitch, m_damageAimPunchTargetPitch, kickBlend);
            m_damageAimPunchYaw = Mathf.Lerp(m_damageAimPunchYaw, m_damageAimPunchTargetYaw, kickBlend);
            if (Mathf.Abs(m_damageAimPunchPitch - m_damageAimPunchTargetPitch) < 0.01f
                && Mathf.Abs(m_damageAimPunchYaw - m_damageAimPunchTargetYaw) < 0.01f)
            {
                m_damageAimPunchTargetPitch = 0f;
                m_damageAimPunchTargetYaw = 0f;
            }
            return;
        }

        float returnBlend = Mathf.Clamp01(k_DamageAimPunchReturnSpeed * deltaTime);
        m_damageAimPunchPitch = Mathf.Lerp(m_damageAimPunchPitch, 0f, returnBlend);
        m_damageAimPunchYaw = Mathf.Lerp(m_damageAimPunchYaw, 0f, returnBlend);
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
        RecoilSample recoil = GetRecoilProfile(CurrentWeapon).CreateSample(GetRecoilYawDirection(CurrentWeapon));
        bool isContinuousFire = RegisterSuccessfulShot(CurrentWeapon);
        m_weaponViewmodel?.PlayFireFeedback(recoil);
        ApplyCameraRecoil(recoil, isContinuousFire);

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
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                bool isHeadshot = enemy.IsHeadHit(ray, k_RaycastDistance);
                m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot);
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
                m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
            }
        }
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.red, 0.1f);
    }

    private void FireDmr()
    {
        Ray ray = new(m_playerCamera.transform.position, GetAimRotation() * Vector3.forward);
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
                m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot);
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
                m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
                playedWallImpact = true;
            }
            else
            {
                m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
            }

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
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    bool isHeadshot = enemy.IsHeadHit(ray, k_RaycastDistance);
                    m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot);
                    m_shotgunDamageByEnemy.TryGetValue(enemy, out float damage);
                    m_shotgunDamageByEnemy[enemy] = damage + 12f * (isHeadshot ? k_HeadshotDamageMultiplier : 1f);
                    if (isHeadshot)
                    {
                        m_shotgunHeadshotEnemies.Add(enemy);
                    }
                }
                else if (!playedWallImpact)
                {
                    m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                    SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
                    playedWallImpact = true;
                }
                else
                {
                    m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
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
        Quaternion aimRotation = GetAimRotation();
        return (aimRotation * Vector3.forward + aimRotation * Vector3.right * spread.x
            + aimRotation * Vector3.up * spread.y).normalized;
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
        Quaternion aimRotation = GetAimRotation();
        return Quaternion.AngleAxis(spread, aimRotation * Vector3.up) * (aimRotation * Vector3.forward);
    }

    private Quaternion GetAimRotation()
    {
        return transform.rotation * Quaternion.Euler(m_pitch - m_cameraRecoilPitch, m_cameraRecoilYaw, 0f);
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

    private RecoilProfile GetRecoilProfile(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => m_pistolRecoil,
            WeaponId.Shotgun => m_shotgunRecoil,
            WeaponId.Rifle => m_rifleRecoil,
            WeaponId.DMR => m_dmrRecoil,
            _ => default
        };
    }

    private float GetRecoilYawDirection(WeaponId weapon)
    {
        if (weapon != WeaponId.Rifle)
        {
            return 0f;
        }

        float now = Time.unscaledTime;
        if (now - m_lastRifleShotTime > k_RifleBurstResetTime || m_rifleBurstShots == 0)
        {
            m_rifleBurstShots = 0;
            m_rifleYawDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }
        else if (m_rifleBurstShots % k_RifleYawDirectionShots == 0)
        {
            m_rifleYawDirection = -m_rifleYawDirection;
        }

        m_rifleBurstShots++;
        m_lastRifleShotTime = now;
        return m_rifleYawDirection;
    }

    private bool RegisterSuccessfulShot(WeaponId weapon)
    {
        float now = Time.unscaledTime;
        bool isContinuous = IsContinuousFire(
            weapon, m_burstWeapon, m_burstShotCount, now - m_lastSuccessfulShotAt);
        m_burstWeapon = weapon;
        m_burstShotCount = isContinuous ? m_burstShotCount + 1 : 1;
        m_lastSuccessfulShotAt = now;
        return isContinuous;
    }

    private static bool IsContinuousFire(WeaponId weapon, WeaponId burstWeapon, int burstShotCount,
        float elapsedSinceLastShot)
    {
        float window = weapon switch
        {
            WeaponId.Pistol => k_PistolContinuousWindow,
            WeaponId.Rifle => k_RifleContinuousWindow,
            WeaponId.DMR => k_DmrContinuousWindow,
            _ => 0f
        };
        return window > 0f && burstWeapon == weapon && burstShotCount > 0
            && elapsedSinceLastShot <= window;
    }

    private void ApplyCameraRecoil(RecoilSample recoil, bool isContinuousFire)
    {
        ClampCameraRecoilToPitchHeadroom();
        m_cameraRecoilStart = new Vector2(m_cameraRecoilPitch, m_cameraRecoilYaw);
        float capRatio = Mathf.Clamp01(m_cameraRecoilStart.x / recoil.SoftCap);
        float pitchAddScale = Mathf.Lerp(1f, 0.25f, capRatio);
        m_cameraRecoilTarget = m_cameraRecoilStart + new Vector2(recoil.Pitch * pitchAddScale, recoil.Yaw);
        m_cameraRecoilTarget.x = Mathf.Min(m_cameraRecoilTarget.x, recoil.HardCap);
        m_activeRecoilSample = recoil;
        m_cameraRecoilPhaseStartedAt = Time.unscaledTime;
        m_cameraRecoilLastShotAt = Time.unscaledTime;
        m_cameraRecoilPhase = RecoilPhase.Kick;
        m_cameraRecoilReturnTarget = Vector2.zero;
        m_commitResidualOnReturn = isContinuousFire;
        m_fireImpulseStart = new Vector3(recoil.FireImpulsePitch, recoil.FireImpulseYaw, recoil.FireImpulseRoll);
        m_fireImpulse = m_fireImpulseStart;
        m_fireImpulseStartedAt = Time.unscaledTime;
        m_fireImpulseDuration = recoil.FireImpulseDuration;
        ClampCameraRecoilToPitchHeadroom();
    }

    internal RecoilSample ApplyRocketRecoil()
    {
        RecoilSample recoil = m_rocketRecoil.CreateSample();
        ResetBurstTracking();
        ApplyCameraRecoil(recoil, false);
        return recoil;
    }

    internal void ApplyDamageAimPunch(PlayerDeathCause deathCause)
    {
        Vector2 strength = GetDamageAimPunchStrength(deathCause);
        if (strength.x <= 0f)
        {
            return;
        }

        float currentPitch = Mathf.Max(m_damageAimPunchPitch, m_damageAimPunchTargetPitch);
        float addScale = Mathf.Lerp(1f, 0.25f,
            Mathf.Clamp01(currentPitch / k_DamageAimPunchSoftCap));
        m_damageAimPunchTargetPitch = Mathf.Min(k_DamageAimPunchHardCapPitch,
            currentPitch + strength.x * addScale);
        m_damageAimPunchTargetYaw = Mathf.Clamp(m_damageAimPunchTargetYaw
            + UnityEngine.Random.Range(-strength.y, strength.y) * addScale,
            -k_DamageAimPunchHardCapYaw, k_DamageAimPunchHardCapYaw);
    }

    private static Vector2 GetDamageAimPunchStrength(PlayerDeathCause deathCause)
    {
        return deathCause switch
        {
            PlayerDeathCause.SuicideBacteriophage => new Vector2(2.8f, 1.2f),
            PlayerDeathCause.MeleeHumanoid => new Vector2(1.7f, 0.75f),
            PlayerDeathCause.RangedHumanoid => new Vector2(0.8f, 0.35f),
            _ => Vector2.zero
        };
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
        Debug.Assert(Mathf.Approximately(k_DmrZoomFieldOfView, 45f)
            && Mathf.Approximately(k_DmrZoomTransitionDuration, 0.12f)
            && Mathf.Approximately(k_DmrZoomSensitivityMultiplier, 0.5f));
        Debug.Assert(!GameplayHUD.ShouldShowCrosshair(true, false)
            && GameplayHUD.ShouldShowCrosshair(true, true)
            && GameplayHUD.ShouldShowCrosshair(false, false));
        Debug.Assert(GetStartingAmmo(WeaponId.Pistol) == 15 && GetStartingAmmo(WeaponId.Shotgun) == 6
            && GetStartingAmmo(WeaponId.Rifle) == 30 && GetStartingAmmo(WeaponId.DMR) == 12);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Grenadier) == WeaponId.Rifle
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Engineer) == WeaponId.Shotgun
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Sniper) == WeaponId.DMR);
        Debug.Assert(k_ShotgunPelletCount == 8 && Mathf.Approximately(k_ShotgunSpreadAngle, 5f));
        Debug.Assert(m_pistolRecoil.IsValid() && m_shotgunRecoil.IsValid() && m_rifleRecoil.IsValid()
            && m_dmrRecoil.IsValid() && m_rocketRecoil.IsValid());
        RecoilSample pistolRecoil = m_pistolRecoil.CreateSample();
        RecoilSample rifleRecoil = m_rifleRecoil.CreateSample();
        RecoilSample dmrRecoil = m_dmrRecoil.CreateSample();
        Debug.Assert(Mathf.Approximately(pistolRecoil.ContinuousResidualRatio, 0.2f)
            && Mathf.Approximately(rifleRecoil.ContinuousResidualRatio, 0.35f)
            && Mathf.Approximately(dmrRecoil.ContinuousResidualRatio, 0.25f));
        Debug.Assert(IsContinuousFire(WeaponId.Pistol, WeaponId.Pistol, 1, 0.24f)
            && !IsContinuousFire(WeaponId.Pistol, WeaponId.Pistol, 1, 0.241f)
            && IsContinuousFire(WeaponId.Rifle, WeaponId.Rifle, 1, 0.32f)
            && IsContinuousFire(WeaponId.DMR, WeaponId.DMR, 1, 0.42f)
            && !IsContinuousFire(WeaponId.Shotgun, WeaponId.Shotgun, 1, 0.5f));
        for (int sampleIndex = 0; sampleIndex < 64; sampleIndex++)
        {
            RecoilSample recoil = m_rocketRecoil.CreateSample();
            Debug.Assert(recoil.Pitch > 0f && Mathf.Abs(recoil.Yaw) <= recoil.Pitch * 0.4f + 0.001f);
            Debug.Assert(recoil.SoftCap > 0f && recoil.HardCap >= recoil.SoftCap
                && recoil.FireImpulseDuration > 0f);
        }
        Debug.Assert(RecoilSample.EaseOutCubic(0.25f) > 0.25f
            && RecoilSample.EaseOutCubic(0.75f) > 0.75f);
        Debug.Assert(RecoilSample.EaseOutQuad(0.25f) < RecoilSample.EaseOutCubic(0.25f));
        Debug.Assert(Mathf.Approximately(ClampRecoilPitch(12f, -75f), 5f));
        Debug.Assert(Mathf.Approximately(ClampRecoilPitch(5f, -80f), 0f));
        Vector2 suicidePunch = GetDamageAimPunchStrength(PlayerDeathCause.SuicideBacteriophage);
        Vector2 meleePunch = GetDamageAimPunchStrength(PlayerDeathCause.MeleeHumanoid);
        Vector2 rangedPunch = GetDamageAimPunchStrength(PlayerDeathCause.RangedHumanoid);
        Debug.Assert(suicidePunch.x > meleePunch.x && meleePunch.x > rangedPunch.x && rangedPunch.x > 0f);
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
        ResetDmrZoom(true);
        ResetCameraRecoil();
        m_weaponViewmodel?.ResetRecoil();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnValidate()
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_jumpHeight = Mathf.Max(0f, m_jumpHeight);
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
