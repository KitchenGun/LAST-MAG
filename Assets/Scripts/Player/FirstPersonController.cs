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
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void EnableRawPointerLock();
#endif

    private const int k_WeaponSlotCount = 2;
    private const float k_RaycastDistance = 100f;
    private const float k_MaxPitch = 80f;
    private const float k_DmrZoomFieldOfView = 45f;
    private const float k_DmrZoomTransitionDuration = 0.12f;
    private const float k_DmrZoomSensitivityMultiplier = 0.5f;
    private const float k_DeathFallDuration = 1.8f;
    private const float k_DeathFallForwardDistance = 0.35f;
    private const float k_DeathFallLateralDistance = 0.2f;
    private const float k_DeathFallFallbackDistance = 1.4f;
    private const float k_DeathFallPitch = 18f;
    private const float k_DeathFallRoll = 70f;
    private const float k_DeathCameraRadius = 0.16f;
    private const float k_DeathCameraSkin = 0.03f;
    private const float k_DeathGroundSearchDistance = 3f;
    private const int k_DeathRaycastBufferSize = 16;
    private const float k_ExplosionShakeRangeMultiplier = 2f;
    private const float k_ExplosionShakeFrequency = 32f;
    private const float k_RocketExplosionShakeAngle = 3f;
    private const float k_RocketExplosionShakeRoll = 1.5f;
    private const float k_RocketExplosionShakeDuration = 0.24f;
    private const float k_GrenadeExplosionShakeAngle = 2.2f;
    private const float k_GrenadeExplosionShakeRoll = 1f;
    private const float k_GrenadeExplosionShakeDuration = 0.2f;
    private const float k_RifleBurstResetTime = 0.25f;
    private const int k_RifleYawDirectionShots = 4;
    private const float k_PistolContinuousWindow = 0.24f;
    private const float k_RifleContinuousWindow = 0.32f;
    private const float k_DmrContinuousWindow = 0.42f;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private const float k_MouseDiagnosticsInterval = 10f;
#endif
    // ponytail: fixed buffer avoids WebGL GC; raise only if one shot can cross 64 solid colliders.
    private const int k_DmrRaycastBufferSize = 64;
    private const int k_ShotgunEnemyHitsPerPellet = 2;
    private static readonly Comparer<RaycastHit> s_RaycastHitDistanceComparer =
        Comparer<RaycastHit>.Create(static (left, right) => left.distance.CompareTo(right.distance));

    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private Light m_flashLight;
    [SerializeField] private GameplayHUD m_gameplayHUD;
    [SerializeField] private WeaponViewmodelController m_weaponViewmodel;
    [SerializeField] private PlayerSkillController m_skillController;
    [SerializeField] private ImpactSparkEmitter m_impactSparkEmitter;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [Tooltip("이 속도를 넘는 비정상 마우스 입력은 회전에 반영하지 않습니다. 정상 입력은 그대로 통과합니다.")]
    [SerializeField, Min(1f)] private float m_maxMouseAngularSpeed = 7200f;
    [Header("Damage Aim Punch")]
    [Tooltip("피격 카메라 흔들림이 목표 강도에 도달하는 속도입니다.")]
    [SerializeField, Min(0.1f)] private float m_damageAimPunchKickSpeed = 30f;
    [Tooltip("피격 카메라 흔들림이 원래 조준으로 돌아오는 속도입니다.")]
    [SerializeField, Min(0.1f)] private float m_damageAimPunchReturnSpeed = 8f;
    [Tooltip("연속 피격 시 흔들림 추가량이 줄어들기 시작하는 누적 Pitch입니다.")]
    [SerializeField, Min(0.01f)] private float m_damageAimPunchSoftCap = 3f;
    [Tooltip("피격 흔들림의 최대 상하 각도입니다.")]
    [SerializeField, Min(0f)] private float m_damageAimPunchHardCapPitch = 4f;
    [Tooltip("피격 흔들림의 최대 좌우 각도입니다.")]
    [SerializeField, Min(0f)] private float m_damageAimPunchHardCapYaw = 2f;
    [Tooltip("각 항목은 상하 Pitch, 좌우 Yaw 각도입니다.")]
    [SerializeField] private Vector2 m_suicideDamageAimPunch = new(2.8f, 1.2f);
    [SerializeField] private Vector2 m_meleeDamageAimPunch = new(1.7f, 0.75f);
    [SerializeField] private Vector2 m_rangedDamageAimPunch = new(1.6f, 0.7f);
    [Header("Weapon Ammo Capacity")]
    [Tooltip("Starting and maximum owned ammo. This game does not use magazines or reloading.")]
    [SerializeField, Min(1)] private int m_pistolAmmoCapacity = 15;
    [SerializeField, Min(1)] private int m_shotgunAmmoCapacity = 8;
    [SerializeField, Min(1)] private int m_rifleAmmoCapacity = 50;
    [SerializeField, Min(1)] private int m_dmrAmmoCapacity = 15;
    [Header("Weapon Balance")]
    [SerializeField, Min(0.01f)] private float m_pistolDamage = 30f;
    [SerializeField, Min(0.01f)] private float m_pistolShotsPerSecond = 6.75f;
    [SerializeField, Min(1f)] private float m_pistolHeadshotMultiplier = 2f;
    [SerializeField, Min(0f)] private float m_pistolSpreadAngle = 0.35f;
    [SerializeField, Min(1)] private int m_shotgunPelletCount = 8;
    [SerializeField, Min(0.01f)] private float m_shotgunPelletDamage = 12f;
    [SerializeField, Min(0.01f)] private float m_shotgunShotsPerSecond = 1.1f;
    [SerializeField, Min(1f)] private float m_shotgunHeadshotMultiplier = 2f;
    [SerializeField, Min(0f)] private float m_shotgunSpreadAngle = 5f;
    [SerializeField, Min(0.01f)] private float m_rifleDamage = 15f;
    [SerializeField, Min(0.01f)] private float m_rifleShotsPerSecond = 11f;
    [SerializeField, Min(1f)] private float m_rifleHeadshotMultiplier = 2f;
    [SerializeField, Min(0f)] private float m_rifleSpreadAngle = 0.75f;
    [SerializeField, Min(0.01f)] private float m_dmrFirstHitDamage = 60f;
    [SerializeField, Min(0.01f)] private float m_dmrSecondHitDamage = 40f;
    [SerializeField, Min(0.01f)] private float m_dmrThirdHitDamage = 20f;
    [SerializeField, Min(0.01f)] private float m_dmrShotsPerSecond = 5.25f;
    [SerializeField, Min(1f)] private float m_dmrHeadshotMultiplier = 2f;
    [Header("Weapon Recoil")]
    [SerializeField] private RecoilProfile m_pistolRecoil = new(2.2f, 0.35f, 0.1f, 3.5f, 4.5f,
        0.2f, 0.055f, 0.1f, 0.12f, 0.35f, 0.05f, 0.12f, 0.08f);
    [SerializeField] private RecoilProfile m_shotgunRecoil = new(4.6f, 0.85f, 0.15f, 4.5f, 5.5f,
        0.3f, 0.075f, 0.28f, 0.29f, 0.75f, 0.15f, 0.28f, 0.13f);
    [SerializeField] private RecoilProfile m_rifleRecoil = new(2.3f, 0.32f, 0.12f, 6f, 7f,
        0.35f, 0.045f, 0.1f, 0.12f, 0.25f, 0.05f, 0.12f, 0.06f);
    [SerializeField] private RecoilProfile m_dmrRecoil = new(3.2f, 0.45f, 0.1f, 4.8f, 6f,
        0.25f, 0.065f, 0.18f, 0.2f, 0.5f, 0.1f, 0.18f, 0.1f);
    [SerializeField] private RecoilProfile m_rocketRecoil = new(5.6f, 1.1f, 0.15f, 5.5f, 6.5f,
        0f, 0.1f, 0.28f, 0.42f, 1f, 0.2f, 0.35f, 0.16f);
    [Header("World Audio")]
    [SerializeField] private AudioClip m_wallImpactClip;
    [SerializeField] private float m_wallImpactMaxDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float m_wallImpactVolume = 0.7f;
    [Header("Humanoid Hit Marker Audio")]
    [SerializeField] private AudioSource m_humanoidHitMarkerAudioSource;
    [SerializeField] private AudioClip[] m_humanoidHeadshotHitClips;
    [SerializeField] private AudioClip[] m_humanoidBodyHitClips;
    [SerializeField] private AudioClip[] m_humanoidBodyKillClips;
    [SerializeField] private AudioClip[] m_humanoidHeadshotKillClips;
    [SerializeField, Range(0f, 1f)] private float m_humanoidHitMarkerVolume = 1f;

    private readonly WeaponId[] m_loadout = new WeaponId[k_WeaponSlotCount];
    private readonly int[] m_weaponAmmo = new int[k_WeaponSlotCount];
    private readonly int[] m_maxWeaponAmmo = new int[k_WeaponSlotCount];
    private readonly RaycastHit[] m_dmrHits = new RaycastHit[k_DmrRaycastBufferSize];
    private readonly HashSet<EnemyHealth> m_dmrDamagedEnemies = new();
    private readonly HashSet<Collider> m_dmrHitStructures = new();
    private readonly Dictionary<EnemyHealth, float> m_shotgunDamageByEnemy = new();
    private readonly HashSet<EnemyHealth> m_shotgunHeadshotEnemies = new();
    private readonly RaycastHit[] m_deathCameraHits = new RaycastHit[k_DeathRaycastBufferSize];
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
    private float m_yaw;
    private bool m_ignoreNextMouseDelta;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private float m_mouseDiagnosticsStartedAt;
    private int m_mouseDiagnosticsStartEventCount;
    private float m_maxRawMouseDelta;
    private float m_maxRawMouseAngularSpeed;
    private float m_maxAppliedMouseAngularSpeed;
    private int m_mouseSpikeDiscardCount;
    private int m_mouseTransitionDiscardCount;
#endif
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
    private float m_explosionShakeAngle;
    private float m_explosionShakeRoll;
    private float m_explosionShakeStartedAt;
    private float m_explosionShakeDuration;
    private float m_explosionShakeSeed;
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
    private bool m_isDeathPresentation;
    private float m_deathPresentationStartedAt;
    private Vector3 m_deathCameraStartPosition;
    private Vector3 m_deathCameraTargetPosition;
    private Quaternion m_deathCameraStartRotation;
    private Quaternion m_deathCameraTargetRotation;

    public static FirstPersonController CurrentInstance { get; private set; }
    public int ActiveWeaponSlot => m_activeWeaponSlot;
    public PlayerClassId SelectedClass { get; private set; }
    public WeaponId CurrentWeapon => m_loadout[m_activeWeaponSlot - 1];
    public WeaponId PrimaryWeapon => m_loadout[0];
    public bool IsDeathPresentation => m_isDeathPresentation;
    internal CharacterController CharacterControllerComponent => m_characterController;
    internal PlayerHealth PlayerHealthComponent => m_playerHealth;
    internal ScoreSystem ScoreSystemComponent => m_scoreSystem;

    private void Awake()
    {
        CurrentInstance = this;
        m_yaw = transform.eulerAngles.y;
        ApplyWorldYaw(0f);
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
            && m_humanoidHitMarkerAudioSource != null
            && IsAmmoConfigurationValid());
        if (m_humanoidHitMarkerAudioSource != null)
        {
            m_humanoidHitMarkerAudioSource.playOnAwake = false;
            m_humanoidHitMarkerAudioSource.loop = false;
            m_humanoidHitMarkerAudioSource.spatialBlend = 0f;
            m_humanoidHitMarkerAudioSource.priority = 32;
        }
        if (m_playerCamera != null)
        {
            m_defaultCameraFieldOfView = m_playerCamera.fieldOfView;
        }
        if (m_flashLight != null)
        {
            m_flashLight.enabled = false;
        }
        m_gameplayHUD?.BindPlayerHealth(m_playerHealth);
        m_gameplayHUD?.SetFlashlightState(false);
        m_scoreSystem.Initialize(m_gameplayHUD);
        m_skillController.Initialize(SelectedClass, m_playerCamera, m_playerHealth, m_gameplayHUD, m_scoreSystem);
        SelectWeapon(1);
#if UNITY_WEBGL && !UNITY_EDITOR
        EnableRawPointerLock();
#endif
        LockCursor();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        ResetMouseDiagnostics();
        if (InputSystem.settings.disableRedundantEventsMerging)
        {
            Debug.LogWarning("[Mouse Input] Redundant input event merging is disabled; high-polling mice may overload WebGL input processing.");
        }
#endif
    }

    private void OnEnable()
    {
        if (!InitializeInput())
        {
            return;
        }

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
        if (m_flashLight != null)
        {
            m_flashLight.enabled = false;
        }
        m_gameplayHUD?.SetFlashlightState(false);
        ResetCameraRecoil();
        m_damageAimPunchPitch = 0f;
        m_damageAimPunchTargetPitch = 0f;
        m_damageAimPunchYaw = 0f;
        m_damageAimPunchTargetYaw = 0f;
        ResetExplosionShake();
        if (m_playerMap != null)
        {
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
        if (m_isDeathPresentation)
        {
            UpdateDeathPresentation();
            return;
        }

        if (GameplayClock.IsPaused)
        {
            return;
        }

        m_skillController?.AdvanceTimedState(GameplayClock.Now);
        HandleWeaponSelection();
        if (m_playerMap != null && Cursor.lockState == CursorLockMode.Locked)
        {
            HandleDmrZoomInput();
        }
        UpdateDmrZoom();
        if (m_playerMap == null)
        {
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            HandleAttackInput();
            return;
        }

        HandleLook();
        HandleAttackInput();

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            m_skillController?.TryActivateOrArm();
        }

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame
            && m_flashLight != null
            && (m_skillController == null || m_skillController.State is PlayerSkillState.Ready or PlayerSkillState.Cooldown))
        {
            m_flashLight.enabled = !m_flashLight.enabled;
            m_gameplayHUD?.SetFlashlightState(m_flashLight.enabled);
        }

        HandleMovement();
    }

    internal void BeginDeathPresentation(float duration)
    {
        if (m_isDeathPresentation || m_playerCamera == null)
        {
            return;
        }

        m_isDeathPresentation = true;
        m_isRifleFiring = false;
        if (m_flashLight != null)
        {
            m_flashLight.enabled = false;
        }
        m_gameplayHUD?.SetFlashlightState(false);
        m_playerMap?.Disable();
        ResetDmrZoom(true);
        ResetCameraRecoil();
        m_damageAimPunchPitch = m_damageAimPunchTargetPitch = 0f;
        m_damageAimPunchYaw = m_damageAimPunchTargetYaw = 0f;
        ResetExplosionShake();

        Vector3 inheritedVelocity = m_characterController != null ? m_characterController.velocity : Vector3.zero;
        bool droppedSkillVisual = m_skillController != null
            && m_skillController.BeginDeathPresentation(inheritedVelocity);
        if (!droppedSkillVisual)
        {
            m_weaponViewmodel?.DropActiveWeapon(inheritedVelocity);
        }

        m_gameplayHUD?.BeginDeathPresentation(duration);
        Transform cameraTransform = m_playerCamera.transform;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        m_deathCameraStartPosition = cameraTransform.position;
        m_deathCameraTargetPosition = FindDeathCameraTarget(cameraTransform, side);
        m_deathCameraStartRotation = cameraTransform.rotation;
        m_deathCameraTargetRotation = m_deathCameraStartRotation
            * Quaternion.Euler(k_DeathFallPitch, 0f, side * k_DeathFallRoll);
        m_deathPresentationStartedAt = GameplayClock.Now;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UpdateDeathPresentation()
    {
        if (m_playerCamera == null)
        {
            return;
        }

        float progress = Mathf.Clamp01((GameplayClock.Now - m_deathPresentationStartedAt) / k_DeathFallDuration);
        Transform cameraTransform = m_playerCamera.transform;
        Vector3 desired = Vector3.Lerp(m_deathCameraStartPosition, m_deathCameraTargetPosition,
            Mathf.SmoothStep(0f, 1f, progress));
        cameraTransform.position = ResolveDeathCameraMove(cameraTransform.position, desired);
        cameraTransform.rotation = Quaternion.SlerpUnclamped(m_deathCameraStartRotation,
            m_deathCameraTargetRotation, RecoilSample.EaseOutCubic(progress));
    }

    private Vector3 FindDeathCameraTarget(Transform cameraTransform, float side)
    {
        Vector3 source = cameraTransform.position + cameraTransform.forward * k_DeathFallForwardDistance
            + cameraTransform.right * (side * k_DeathFallLateralDistance);
        int hitCount = Physics.SphereCastNonAlloc(source, k_DeathCameraRadius, Vector3.down,
            m_deathCameraHits, k_DeathGroundSearchDistance, ~((1 << 2) | (1 << 5)),
            QueryTriggerInteraction.Ignore);
        if (TryGetDeathCameraHit(hitCount, out RaycastHit hit))
        {
            return hit.point + hit.normal * (k_DeathCameraRadius + k_DeathCameraSkin);
        }

        return cameraTransform.position + cameraTransform.forward * k_DeathFallForwardDistance
            + cameraTransform.right * (side * k_DeathFallLateralDistance)
            + Vector3.down * k_DeathFallFallbackDistance;
    }

    private Vector3 ResolveDeathCameraMove(Vector3 current, Vector3 desired)
    {
        Vector3 movement = desired - current;
        float distance = movement.magnitude;
        if (distance <= 0.0001f)
        {
            return desired;
        }

        int hitCount = Physics.SphereCastNonAlloc(current, k_DeathCameraRadius, movement / distance,
            m_deathCameraHits, distance + k_DeathCameraSkin, ~((1 << 2) | (1 << 5)),
            QueryTriggerInteraction.Ignore);
        return TryGetDeathCameraHit(hitCount, out RaycastHit hit)
            ? hit.point - movement.normalized * k_DeathCameraSkin
            : desired;
    }

    private bool TryGetDeathCameraHit(int hitCount, out RaycastHit closestHit)
    {
        closestHit = default;
        float closestDistance = float.PositiveInfinity;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = m_deathCameraHits[index];
            Collider collider = hit.collider;
            if (collider == null || collider == m_characterController
                || collider.GetComponentInParent<FirstPersonController>() != null
                || collider.GetComponentInParent<EnemyHealth>() != null
                || collider.GetComponentInParent<AmmoPickup>() != null
                || collider.GetComponentInParent<PlayerSkillProjectile>() != null
                || collider.GetComponentInParent<RangedProjectile>() != null)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
            }
        }

        return closestDistance < float.PositiveInfinity;
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
        if (Mouse.current == null || CurrentWeapon != WeaponId.DMR)
        {
            return;
        }

        bool zoomed = ResolveDmrZoomState(m_isDmrZoomed, GameSettings.ZoomInputMode,
            Mouse.current.rightButton.wasPressedThisFrame, Mouse.current.rightButton.isPressed);
        if (zoomed == m_isDmrZoomed)
        {
            return;
        }
        m_isDmrZoomed = zoomed;
        m_gameplayHUD?.SetDmrAimState(true, m_isDmrZoomed);
    }

    internal static bool ResolveDmrZoomState(bool current, ZoomInputMode mode,
        bool pressedThisFrame, bool isPressed)
    {
        return mode == ZoomInputMode.Hold
            ? isPressed
            : pressedThisFrame ? !current : current;
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
            m_playerCamera.fieldOfView, targetFieldOfView, speed * GameplayClock.DeltaTime);
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
        float sensitivity = CalculateLookSensitivity(
            m_lookSensitivity, GameSettings.MouseSensitivity, m_isDmrZoomed);
        bool isGamepad = m_lookAction.activeControl?.device is Gamepad;
        Vector2 rawLook = m_lookAction.ReadValue<Vector2>();
        float deltaTime = GameplayClock.DeltaTime;
        bool discardedSpike = false;
        bool discardedTransitionDelta = false;
        Vector2 look = isGamepad
            ? rawLook * sensitivity * GetLookInputTimeScale(true, deltaTime)
            : ProcessMouseLook(rawLook, sensitivity, deltaTime,
                out discardedSpike, out discardedTransitionDelta);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (!isGamepad)
        {
            RecordMouseDiagnostics(rawLook, sensitivity, look, deltaTime,
                discardedSpike, discardedTransitionDelta);
        }
#endif
        m_pitch = Mathf.Clamp(m_pitch - look.y, -k_MaxPitch, k_MaxPitch);
        UpdateCameraRecoil();
        UpdateFireImpulse();
        UpdateDamageAimPunch();
        Vector3 explosionShake = SampleExplosionShake();
        m_playerCamera.transform.localRotation = Quaternion.Euler(
            m_pitch - m_cameraRecoilPitch - m_fireImpulse.x - m_damageAimPunchPitch
                + explosionShake.x,
            m_cameraRecoilYaw + m_fireImpulse.y + m_damageAimPunchYaw
                + explosionShake.y,
            m_fireImpulse.z + explosionShake.z);
        ApplyWorldYaw(look.x);
    }

    private void ApplyWorldYaw(float delta)
    {
        m_yaw = Mathf.Repeat(m_yaw + delta, 360f);
        transform.rotation = Quaternion.Euler(0f, m_yaw, 0f);
    }

    internal static float CalculateLookSensitivity(float baseSensitivity,
        float sensitivitySetting, bool zoomed)
    {
        return Mathf.Max(0f, baseSensitivity) * Mathf.Clamp01(sensitivitySetting)
            * (zoomed ? k_DmrZoomSensitivityMultiplier : 1f);
    }

    private static float GetLookInputTimeScale(bool isGamepad, float deltaTime)
    {
        return isGamepad ? Mathf.Max(0f, deltaTime) * 60f : 1f;
    }

    private Vector2 ProcessMouseLook(Vector2 rawDelta, float sensitivity, float deltaTime,
        out bool discardedSpike, out bool discardedTransitionDelta)
    {
        discardedSpike = false;
        discardedTransitionDelta = false;
        if (m_ignoreNextMouseDelta)
        {
            m_ignoreNextMouseDelta = false;
            discardedTransitionDelta = rawDelta != Vector2.zero;
            return Vector2.zero;
        }

        return FilterMouseAngularDelta(rawDelta * sensitivity, deltaTime,
            m_maxMouseAngularSpeed, out discardedSpike);
    }

    private static Vector2 FilterMouseAngularDelta(Vector2 angularDelta, float deltaTime,
        float maxAngularSpeed, out bool discardedSpike)
    {
        float maxAngularDelta = Mathf.Max(0f, maxAngularSpeed) * Mathf.Max(0f, deltaTime);
        float maxAngularDeltaSquared = maxAngularDelta * maxAngularDelta;
        if (angularDelta.sqrMagnitude <= maxAngularDeltaSquared)
        {
            discardedSpike = false;
            return angularDelta;
        }

        // A merged high-polling spike has no trustworthy direction or magnitude.
        // Clamping it still creates a maximum-speed camera snap, so reject the sample.
        discardedSpike = true;
        return Vector2.zero;
    }

    private void ResetMouseLookFilter(bool ignoreNextDelta)
    {
        m_ignoreNextMouseDelta = ignoreNextDelta;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void RecordMouseDiagnostics(Vector2 rawDelta, float sensitivity,
        Vector2 appliedAngularDelta, float deltaTime, bool discardedSpike,
        bool discardedTransitionDelta)
    {
        float safeDeltaTime = Mathf.Max(0.0001f, deltaTime);
        m_maxRawMouseDelta = Mathf.Max(m_maxRawMouseDelta, rawDelta.magnitude);
        m_maxRawMouseAngularSpeed = Mathf.Max(m_maxRawMouseAngularSpeed,
            rawDelta.magnitude * sensitivity / safeDeltaTime);
        m_maxAppliedMouseAngularSpeed = Mathf.Max(m_maxAppliedMouseAngularSpeed,
            appliedAngularDelta.magnitude / safeDeltaTime);
        m_mouseSpikeDiscardCount += discardedSpike ? 1 : 0;
        m_mouseTransitionDiscardCount += discardedTransitionDelta ? 1 : 0;

        if (Time.realtimeSinceStartup - m_mouseDiagnosticsStartedAt < k_MouseDiagnosticsInterval)
        {
            return;
        }

        int currentEventCount = InputSystem.metrics.totalEventCount;
        Debug.Log($"[Mouse Input] {k_MouseDiagnosticsInterval:0}s events={currentEventCount - m_mouseDiagnosticsStartEventCount} "
            + $"maxDelta={m_maxRawMouseDelta:0.##}px raw={m_maxRawMouseAngularSpeed:0}deg/s "
            + $"applied={m_maxAppliedMouseAngularSpeed:0}deg/s spikeDrops={m_mouseSpikeDiscardCount} "
            + $"transitionDrops={m_mouseTransitionDiscardCount}");
        ResetMouseDiagnostics();
    }

    private void ResetMouseDiagnostics()
    {
        m_mouseDiagnosticsStartedAt = Time.realtimeSinceStartup;
        m_mouseDiagnosticsStartEventCount = InputSystem.metrics.totalEventCount;
        m_maxRawMouseDelta = 0f;
        m_maxRawMouseAngularSpeed = 0f;
        m_maxAppliedMouseAngularSpeed = 0f;
        m_mouseSpikeDiscardCount = 0;
        m_mouseTransitionDiscardCount = 0;
    }
#endif

    private void UpdateCameraRecoil()
    {
        ClampCameraRecoilToPitchHeadroom();
        float now = GameplayClock.Now;
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
            ApplyWorldYaw(residual.y);
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

        float elapsed = GameplayClock.Now - m_fireImpulseStartedAt;
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
        float deltaTime = GameplayClock.DeltaTime;
        if (m_damageAimPunchTargetPitch > 0f)
        {
            float kickBlend = CalculateFrameIndependentBlend(m_damageAimPunchKickSpeed, deltaTime);
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

        float returnBlend = CalculateFrameIndependentBlend(m_damageAimPunchReturnSpeed, deltaTime);
        m_damageAimPunchPitch = Mathf.Lerp(m_damageAimPunchPitch, 0f, returnBlend);
        m_damageAimPunchYaw = Mathf.Lerp(m_damageAimPunchYaw, 0f, returnBlend);
    }

    private static float CalculateFrameIndependentBlend(float speed, float deltaTime)
    {
        float blendAt60Fps = Mathf.Clamp01(Mathf.Max(0f, speed) / 60f);
        return 1f - Mathf.Pow(1f - blendAt60Fps, Mathf.Max(0f, deltaTime) * 60f);
    }

    private void HandleMovement()
    {
        if (m_characterController.isGrounded && m_verticalVelocity < 0f)
        {
            m_verticalVelocity = -2f;
        }

        float deltaTime = GameplayClock.DeltaTime;
        float previousVerticalVelocity = m_verticalVelocity;
        m_verticalVelocity += m_gravity * deltaTime;
        Vector2 move = m_moveAction.ReadValue<Vector2>();
        Vector3 displacement = transform.TransformDirection(new Vector3(move.x, 0f, move.y))
            * m_moveSpeed * deltaTime;
        displacement.y = (previousVerticalVelocity + m_verticalVelocity) * 0.5f * deltaTime;
        m_characterController.Move(displacement);
    }

    private void HandleAttackInput()
    {
        if (m_attackAction == null)
        {
            return;
        }

        bool wasPressed = m_attackAction.WasPressedThisFrame();
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            m_isRifleFiring = false;
            if (wasPressed)
            {
                LockCursor();
            }
            return;
        }

        if (wasPressed)
        {
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
            return;
        }

        if (m_attackAction.WasReleasedThisFrame())
        {
            m_isRifleFiring = false;
            return;
        }

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
            m_playerHealth?.PlayAmmunitionZeroAnnouncement();
            m_isRifleFiring = false;
            return false;
        }

        float now = GameplayClock.Now;
        if (now < m_nextAllowedFireTime)
        {
            return false;
        }

        m_nextAllowedFireTime = CalculateNextFireTime(
            m_nextAllowedFireTime, now, GetFireInterval(CurrentWeapon));
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
                m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot, enemy.Type, ray.direction);
                float damage = GetDamage(weapon) * (isHeadshot ? GetHeadshotMultiplier(weapon) : 1f);
                bool killed = enemy.ApplyDamage(damage, KillContext.Direct(weapon, isHeadshot));
                if (killed)
                {
                    RegisterDirectKill(enemy.Type, weapon, isHeadshot);
                }
                m_gameplayHUD?.ShowHitMarker(isHeadshot, killed);
                PlayHumanoidHitMarkerFeedback(enemy.Type, isHeadshot, killed, isHeadshot && killed);
            }
            else
            {
                m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance,
                    m_wallImpactVolume, SpatialAudio.CuePriority.Gameplay);
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
        bool anyHumanoidHit = false;
        bool anyHumanoidHeadshot = false;
        bool anyHumanoidKill = false;
        bool anyHumanoidHeadshotKill = false;
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

            float damage = collisionIndex switch
            {
                0 => m_dmrFirstHitDamage,
                1 => m_dmrSecondHitDamage,
                _ => m_dmrThirdHitDamage
            };
            bool isHeadshot = enemy != null && enemy.IsHeadHit(ray, hit.distance + 0.5f);
            if (enemy != null)
            {
                m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot, enemy.Type, ray.direction);
                bool killed = enemy.ApplyDamage(damage * (isHeadshot ? m_dmrHeadshotMultiplier : 1f),
                    KillContext.Direct(WeaponId.DMR, isHeadshot));
                if (killed)
                {
                    RegisterDirectKill(enemy.Type, WeaponId.DMR, isHeadshot);
                }
                anyHeadshot |= isHeadshot;
                anyKill |= killed;
                if (enemy.Type != EnemyType.Suicide)
                {
                    anyHumanoidHit = true;
                    anyHumanoidHeadshot |= isHeadshot;
                    anyHumanoidKill |= killed;
                    anyHumanoidHeadshotKill |= isHeadshot && killed;
                }
            }
            else if (!playedWallImpact)
            {
                m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance,
                    m_wallImpactVolume, SpatialAudio.CuePriority.Gameplay);
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
            PlayHumanoidHitMarkerFeedback(anyHumanoidHit, anyHumanoidHeadshot, anyHumanoidKill,
                anyHumanoidHeadshotKill);
        }
        m_weaponViewmodel?.PlayDmrTracer(ray.GetPoint(rayLength));
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.cyan, 0.15f);
    }

    private void FireShotgun()
    {
        m_shotgunDamageByEnemy.Clear();
        m_shotgunHeadshotEnemies.Clear();
        bool playedWallImpact = false;
        Quaternion aimRotation = GetAimRotation();
        for (int pellet = 0; pellet < m_shotgunPelletCount; pellet++)
        {
            Ray ray = new(m_playerCamera.transform.position, CreateShotgunDirection(aimRotation));
            float rayLength = k_RaycastDistance;
            int hitCount = Physics.RaycastNonAlloc(ray, m_dmrHits, k_RaycastDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(m_dmrHits, 0, hitCount, s_RaycastHitDistanceComparer);
            m_dmrDamagedEnemies.Clear();
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = m_dmrHits[hitIndex];
                rayLength = hit.distance;
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    if (!m_dmrDamagedEnemies.Add(enemy))
                    {
                        continue;
                    }

                    bool isHeadshot = enemy.IsHeadHit(ray, k_RaycastDistance);
                    m_impactSparkEmitter?.EmitBiologicalAt(hit.point, hit.normal, isHeadshot, enemy.Type, ray.direction);
                    m_shotgunDamageByEnemy.TryGetValue(enemy, out float damage);
                    m_shotgunDamageByEnemy[enemy] = damage + m_shotgunPelletDamage
                        * (isHeadshot ? m_shotgunHeadshotMultiplier : 1f);
                    if (isHeadshot)
                    {
                        m_shotgunHeadshotEnemies.Add(enemy);
                    }

                    if (m_dmrDamagedEnemies.Count == k_ShotgunEnemyHitsPerPellet)
                    {
                        break;
                    }
                }
                else if (!playedWallImpact)
                {
                    m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                    SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance,
                        m_wallImpactVolume, SpatialAudio.CuePriority.Gameplay);
                    playedWallImpact = true;
                    break;
                }
                else
                {
                    m_impactSparkEmitter?.EmitSurfaceAt(hit.point, hit.normal);
                    break;
                }
            }
            Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.yellow, 0.1f);
        }

        bool anyHeadshot = false;
        bool anyKill = false;
        bool anyHumanoidHit = false;
        bool anyHumanoidHeadshot = false;
        bool anyHumanoidKill = false;
        bool anyHumanoidHeadshotKill = false;
        foreach (KeyValuePair<EnemyHealth, float> hit in m_shotgunDamageByEnemy)
        {
            bool isHeadshot = m_shotgunHeadshotEnemies.Contains(hit.Key);
            bool killed = hit.Key.ApplyDamage(hit.Value, KillContext.Direct(WeaponId.Shotgun, isHeadshot));
            if (killed)
            {
                RegisterDirectKill(hit.Key.Type, WeaponId.Shotgun, isHeadshot);
            }
            anyHeadshot |= isHeadshot;
            anyKill |= killed;
            if (hit.Key.Type != EnemyType.Suicide)
            {
                anyHumanoidHit = true;
                anyHumanoidHeadshot |= isHeadshot;
                anyHumanoidKill |= killed;
                anyHumanoidHeadshotKill |= isHeadshot && killed;
            }
        }
        if (m_shotgunDamageByEnemy.Count > 0)
        {
            m_gameplayHUD?.ShowHitMarker(anyHeadshot, anyKill);
            PlayHumanoidHitMarkerFeedback(anyHumanoidHit, anyHumanoidHeadshot, anyHumanoidKill,
                anyHumanoidHeadshotKill);
        }
    }

    private void RegisterDirectKill(EnemyType enemyType, WeaponId weapon, bool isHeadshot)
    {
        m_skillController?.AdvanceTimedState(GameplayClock.Now);
        m_scoreSystem?.RegisterDirectKill(enemyType, weapon, isHeadshot);
        if (isHeadshot)
        {
            m_skillController?.RegisterBulletTimeHeadshotKill();
        }
    }

    private void PlayHumanoidHitMarkerFeedback(EnemyType enemyType, bool isHeadshot, bool isKill,
        bool isHeadshotKill)
    {
        PlayHumanoidHitMarkerFeedback(enemyType != EnemyType.Suicide, isHeadshot, isKill, isHeadshotKill);
    }

    private void PlayHumanoidHitMarkerFeedback(bool hitHumanoid, bool anyHeadshot, bool anyKill,
        bool anyHeadshotKill)
    {
        if (!hitHumanoid)
        {
            return;
        }

        AudioClip[] clips = anyHeadshotKill ? m_humanoidHeadshotKillClips
            : anyKill ? m_humanoidBodyKillClips
            : anyHeadshot ? m_humanoidHeadshotHitClips
            : m_humanoidBodyHitClips;
        if (m_humanoidHitMarkerAudioSource == null || clips == null || clips.Length == 0)
        {
            return;
        }

        int startIndex = UnityEngine.Random.Range(0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            AudioClip clip = clips[(startIndex + offset) % clips.Length];
            if (clip != null)
            {
                m_humanoidHitMarkerAudioSource.PlayOneShot(clip, m_humanoidHitMarkerVolume);
                return;
            }
        }
    }

    private Vector3 CreateShotgunDirection(Quaternion aimRotation)
    {
        Vector2 spread = UnityEngine.Random.insideUnitCircle
            * Mathf.Tan(m_shotgunSpreadAngle * Mathf.Deg2Rad);
        return (aimRotation * Vector3.forward + aimRotation * Vector3.right * spread.x
            + aimRotation * Vector3.up * spread.y).normalized;
    }

    private Vector3 CreateSingleRayDirection(WeaponId weapon)
    {
        float spreadRange = weapon switch
        {
            WeaponId.Pistol => m_pistolSpreadAngle,
            WeaponId.Rifle => m_rifleSpreadAngle,
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

    private float GetDamage(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => m_pistolDamage,
            WeaponId.Shotgun => m_shotgunPelletDamage,
            WeaponId.Rifle => m_rifleDamage,
            WeaponId.DMR => m_dmrFirstHitDamage,
            _ => 0f
        };
    }

    private float GetFireInterval(WeaponId weapon)
    {
        float shotsPerSecond = weapon switch
        {
            WeaponId.Pistol => m_pistolShotsPerSecond,
            WeaponId.Shotgun => m_shotgunShotsPerSecond,
            WeaponId.Rifle => m_rifleShotsPerSecond,
            WeaponId.DMR => m_dmrShotsPerSecond,
            _ => 0f
        };
        return shotsPerSecond > 0f ? 1f / shotsPerSecond : float.MaxValue;
    }

    private static float CalculateNextFireTime(float previousFireTime, float now, float interval)
    {
        interval = Mathf.Max(0f, interval);
        float scheduledNext = previousFireTime + interval;
        return previousFireTime <= 0f || scheduledNext <= now
            ? now + interval
            : scheduledNext;
    }

    private static int SimulateAutomaticShots(float framesPerSecond, float duration, float shotsPerSecond)
    {
        if (framesPerSecond <= 0f || duration <= 0f || shotsPerSecond <= 0f)
        {
            return 0;
        }

        float deltaTime = 1f / framesPerSecond;
        float interval = 1f / shotsPerSecond;
        float nextFireTime = 0f;
        int shots = 0;
        for (int frame = 0; frame * deltaTime < duration; frame++)
        {
            float now = frame * deltaTime;
            if (now + 0.00001f < nextFireTime)
            {
                continue;
            }
            shots++;
            nextFireTime = CalculateNextFireTime(nextFireTime, now, interval);
        }
        return shots;
    }

    private static float SimulateMouseMotion(float framesPerSecond, float duration,
        float angularSpeed, float maxAngularSpeed)
    {
        if (framesPerSecond <= 0f || duration <= 0f)
        {
            return 0f;
        }

        float deltaTime = 1f / framesPerSecond;
        float accumulatedDelta = 0f;
        int frameCount = Mathf.RoundToInt(framesPerSecond * duration);
        for (int frame = 0; frame < frameCount; frame++)
        {
            accumulatedDelta += FilterMouseAngularDelta(
                new Vector2(angularSpeed * deltaTime, 0f), deltaTime,
                maxAngularSpeed, out _).x;
        }
        return accumulatedDelta;
    }

    private static float SimulateJumpApex(float framesPerSecond, float jumpHeight, float gravity)
    {
        if (framesPerSecond <= 0f || jumpHeight <= 0f || gravity >= 0f)
        {
            return 0f;
        }

        float deltaTime = 1f / framesPerSecond;
        float velocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        float height = 0f;
        while (velocity > 0f)
        {
            float nextVelocity = velocity + gravity * deltaTime;
            if (nextVelocity <= 0f)
            {
                float timeToApex = -velocity / gravity;
                return height + velocity * timeToApex
                    + 0.5f * gravity * timeToApex * timeToApex;
            }
            height += (velocity + nextVelocity) * 0.5f * deltaTime;
            velocity = nextVelocity;
        }
        return height;
    }

    private float GetHeadshotMultiplier(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => m_pistolHeadshotMultiplier,
            WeaponId.Shotgun => m_shotgunHeadshotMultiplier,
            WeaponId.Rifle => m_rifleHeadshotMultiplier,
            WeaponId.DMR => m_dmrHeadshotMultiplier,
            _ => 1f
        };
    }

    private static int CalculateShotsToKill(float health, float damage)
    {
        return Mathf.CeilToInt(health / damage);
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

        float now = GameplayClock.Now;
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
        float now = GameplayClock.Now;
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
        m_cameraRecoilPhaseStartedAt = GameplayClock.Now;
        m_cameraRecoilLastShotAt = GameplayClock.Now;
        m_cameraRecoilPhase = RecoilPhase.Kick;
        m_cameraRecoilReturnTarget = Vector2.zero;
        m_commitResidualOnReturn = isContinuousFire;
        m_fireImpulseStart = new Vector3(recoil.FireImpulsePitch, recoil.FireImpulseYaw, recoil.FireImpulseRoll);
        m_fireImpulse = m_fireImpulseStart;
        m_fireImpulseStartedAt = GameplayClock.Now;
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

    internal void ApplyExplosionShake(Vector3 position, float radius, bool isRocket)
    {
        if (m_playerCamera == null || radius <= 0f)
        {
            return;
        }

        if (m_explosionShakeDuration > 0f
            && GameplayClock.Now >= m_explosionShakeStartedAt + m_explosionShakeDuration)
        {
            ResetExplosionShake();
        }

        float maxAngle = isRocket
            ? k_RocketExplosionShakeAngle
            : k_GrenadeExplosionShakeAngle;
        float angle = CalculateExplosionShakeStrength(
            Vector3.Distance(m_playerCamera.transform.position, position), radius, maxAngle);
        if (angle <= 0f)
        {
            return;
        }

        float maxRoll = isRocket
            ? k_RocketExplosionShakeRoll
            : k_GrenadeExplosionShakeRoll;
        float duration = isRocket
            ? k_RocketExplosionShakeDuration
            : k_GrenadeExplosionShakeDuration;
        m_explosionShakeAngle = Mathf.Max(m_explosionShakeAngle, angle);
        m_explosionShakeRoll = Mathf.Max(m_explosionShakeRoll, angle * maxRoll / maxAngle);
        m_explosionShakeDuration = Mathf.Max(m_explosionShakeDuration, duration);
        m_explosionShakeStartedAt = GameplayClock.Now;
        m_explosionShakeSeed = UnityEngine.Random.Range(0f, 1000f);
    }

    private Vector3 SampleExplosionShake()
    {
        if (m_explosionShakeDuration <= 0f)
        {
            return Vector3.zero;
        }

        float elapsed = GameplayClock.Now - m_explosionShakeStartedAt;
        float normalizedTime = elapsed / m_explosionShakeDuration;
        if (normalizedTime >= 1f)
        {
            ResetExplosionShake();
            return Vector3.zero;
        }

        float decay = 1f - Mathf.Clamp01(normalizedTime);
        decay *= decay;
        float sampleTime = GameplayClock.Now * k_ExplosionShakeFrequency;
        Vector2 rotationalNoise = new(
            SignedPerlin(m_explosionShakeSeed, sampleTime),
            SignedPerlin(m_explosionShakeSeed + 17f, sampleTime));
        rotationalNoise = Vector2.ClampMagnitude(rotationalNoise, 1f);
        float pitch = rotationalNoise.x * m_explosionShakeAngle;
        float yaw = rotationalNoise.y * m_explosionShakeAngle;
        float roll = SignedPerlin(m_explosionShakeSeed + 31f, sampleTime) * m_explosionShakeRoll;
        return new Vector3(pitch, yaw, roll) * decay;
    }

    private static float CalculateExplosionShakeStrength(float distance, float radius,
        float maxAngle)
    {
        if (radius <= 0f || maxAngle <= 0f)
        {
            return 0f;
        }

        float falloff = 1f - Mathf.Clamp01(distance
            / (radius * k_ExplosionShakeRangeMultiplier));
        return maxAngle * falloff * falloff;
    }

    private static float SignedPerlin(float seed, float sampleTime)
    {
        return Mathf.PerlinNoise(seed, sampleTime) * 2f - 1f;
    }

    private void ResetExplosionShake()
    {
        m_explosionShakeAngle = 0f;
        m_explosionShakeRoll = 0f;
        m_explosionShakeStartedAt = 0f;
        m_explosionShakeDuration = 0f;
        m_explosionShakeSeed = 0f;
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
            Mathf.Clamp01(currentPitch / m_damageAimPunchSoftCap));
        m_damageAimPunchTargetPitch = Mathf.Min(m_damageAimPunchHardCapPitch,
            currentPitch + strength.x * addScale);
        m_damageAimPunchTargetYaw = Mathf.Clamp(m_damageAimPunchTargetYaw
            + UnityEngine.Random.Range(-strength.y, strength.y) * addScale,
            -m_damageAimPunchHardCapYaw, m_damageAimPunchHardCapYaw);
    }

    private Vector2 GetDamageAimPunchStrength(PlayerDeathCause deathCause)
    {
        return deathCause switch
        {
            PlayerDeathCause.SuicideBacteriophage => m_suicideDamageAimPunch,
            PlayerDeathCause.MeleeHumanoid => m_meleeDamageAimPunch,
            PlayerDeathCause.RangedHumanoid => m_rangedDamageAimPunch,
            _ => Vector2.zero
        };
    }

    private int GetStartingAmmo(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => m_pistolAmmoCapacity,
            WeaponId.Shotgun => m_shotgunAmmoCapacity,
            WeaponId.Rifle => m_rifleAmmoCapacity,
            WeaponId.DMR => m_dmrAmmoCapacity,
            _ => 0
        };
    }

    [ContextMenu("Run Weapon Fire Self Check")]
    private void RunWeaponFireSelfCheck()
    {
        Debug.Assert(k_WeaponSlotCount == 2);
        Debug.Assert(k_ShotgunEnemyHitsPerPellet == 2);
        Debug.Assert(Mathf.Approximately(
            CalculateExplosionShakeStrength(0f, 4f, k_RocketExplosionShakeAngle),
            k_RocketExplosionShakeAngle));
        Debug.Assert(Mathf.Approximately(
            CalculateExplosionShakeStrength(4f, 4f, k_RocketExplosionShakeAngle),
            k_RocketExplosionShakeAngle * 0.25f));
        Debug.Assert(Mathf.Approximately(
            CalculateExplosionShakeStrength(8f, 4f, k_RocketExplosionShakeAngle), 0f));
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
        Debug.Assert(Mathf.Approximately(CalculateLookSensitivity(0.1f, 0f, false), 0f)
            && Mathf.Approximately(CalculateLookSensitivity(0.1f, 0.5f, false), 0.05f)
            && Mathf.Approximately(CalculateLookSensitivity(0.1f, 1f, false), 0.1f)
            && Mathf.Approximately(CalculateLookSensitivity(0.1f, 1f, true), 0.05f));
        Debug.Assert(Mathf.Approximately(GetLookInputTimeScale(false, 1f / 30f), 1f)
            && Mathf.Approximately(GetLookInputTimeScale(false, 1f / 500f), 1f)
            && Mathf.Approximately(GetLookInputTimeScale(true, 1f / 30f), 2f)
            && Mathf.Approximately(GetLookInputTimeScale(true, 1f / 500f), 0.12f));
        Debug.Assert(Mathf.Approximately(GetLookInputTimeScale(true, 1f / 30f) * 30f, 60f)
            && Mathf.Approximately(GetLookInputTimeScale(true, 1f / 60f) * 60f, 60f)
            && Mathf.Approximately(GetLookInputTimeScale(true, 1f / 144f) * 144f, 60f)
            && Mathf.Approximately(GetLookInputTimeScale(true, 1f / 500f) * 500f, 60f));
        float mouseMotion30Fps = SimulateMouseMotion(30f, 1f, 600f, m_maxMouseAngularSpeed);
        float mouseMotion60Fps = SimulateMouseMotion(60f, 1f, 600f, m_maxMouseAngularSpeed);
        float mouseMotion144Fps = SimulateMouseMotion(144f, 1f, 600f, m_maxMouseAngularSpeed);
        float mouseMotion500Fps = SimulateMouseMotion(500f, 1f, 600f, m_maxMouseAngularSpeed);
        Debug.Assert(Mathf.Abs(mouseMotion30Fps - 600f) < 0.01f
            && Mathf.Abs(mouseMotion60Fps - mouseMotion30Fps) < 0.01f
            && Mathf.Abs(mouseMotion144Fps - mouseMotion30Fps) < 0.01f
            && Mathf.Abs(mouseMotion500Fps - mouseMotion30Fps) < 0.01f);
        Debug.Assert(Mathf.Approximately(
                SimulateMouseMotion(30f, 1f, 10000f, m_maxMouseAngularSpeed), 0f)
            && Mathf.Approximately(
                SimulateMouseMotion(500f, 1f, 10000f, m_maxMouseAngularSpeed), 0f));
        Vector2 discardedMouseSpike = FilterMouseAngularDelta(
            new Vector2(1000f, 0f), 1f / 500f, m_maxMouseAngularSpeed, out bool spikeDiscarded);
        Vector2 mouseAfterSpike = FilterMouseAngularDelta(
            new Vector2(1f, 0f), 1f / 500f, m_maxMouseAngularSpeed, out bool normalDiscarded);
        Debug.Assert(spikeDiscarded && !normalDiscarded
            && discardedMouseSpike == Vector2.zero
            && mouseAfterSpike == new Vector2(1f, 0f));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Assert(!InputSystem.settings.disableRedundantEventsMerging,
            "High-polling mouse event merging must remain enabled.");
#endif
        Debug.Assert(SimulateAutomaticShots(30f, 4f, m_rifleShotsPerSecond) == 44
            && SimulateAutomaticShots(60f, 4f, m_rifleShotsPerSecond) == 44
            && SimulateAutomaticShots(144f, 4f, m_rifleShotsPerSecond) == 44
            && SimulateAutomaticShots(500f, 4f, m_rifleShotsPerSecond) == 44);
        Debug.Assert(Mathf.Abs(SimulateJumpApex(30f, m_jumpHeight, m_gravity) - m_jumpHeight) < 0.001f
            && Mathf.Abs(SimulateJumpApex(60f, m_jumpHeight, m_gravity) - m_jumpHeight) < 0.001f
            && Mathf.Abs(SimulateJumpApex(144f, m_jumpHeight, m_gravity) - m_jumpHeight) < 0.001f
            && Mathf.Abs(SimulateJumpApex(500f, m_jumpHeight, m_gravity) - m_jumpHeight) < 0.001f);
        Debug.Assert(CalculateNextFireTime(1f, 1.25f, 1f / 11f) > 1.25f);
        float aimPunch30Fps = 0f;
        float aimPunch500Fps = 0f;
        for (int step = 0; step < 30; step++)
        {
            aimPunch30Fps = Mathf.Lerp(aimPunch30Fps, 1f,
                CalculateFrameIndependentBlend(8f, 1f / 30f));
        }
        for (int step = 0; step < 500; step++)
        {
            aimPunch500Fps = Mathf.Lerp(aimPunch500Fps, 1f,
                CalculateFrameIndependentBlend(8f, 1f / 500f));
        }
        Debug.Assert(Mathf.Abs(aimPunch30Fps - aimPunch500Fps) < 0.0001f);
        Debug.Assert(ResolveDmrZoomState(false, ZoomInputMode.Toggle, true, true)
            && !ResolveDmrZoomState(true, ZoomInputMode.Toggle, true, true)
            && ResolveDmrZoomState(false, ZoomInputMode.Hold, true, true)
            && !ResolveDmrZoomState(true, ZoomInputMode.Hold, false, false));
        Debug.Assert(GetStartingAmmo(WeaponId.Pistol) == m_pistolAmmoCapacity
            && GetStartingAmmo(WeaponId.Shotgun) == m_shotgunAmmoCapacity
            && GetStartingAmmo(WeaponId.Rifle) == m_rifleAmmoCapacity
            && GetStartingAmmo(WeaponId.DMR) == m_dmrAmmoCapacity
            && GetStartingAmmo(WeaponId.Unknown) == 0);
        Debug.Assert(RunResultStore.GetPrimaryWeapon(PlayerClassId.Grenadier) == WeaponId.Rifle
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Engineer) == WeaponId.Shotgun
            && RunResultStore.GetPrimaryWeapon(PlayerClassId.Sniper) == WeaponId.DMR);
        Debug.Assert(Mathf.Approximately(m_pistolDamage, 30f)
            && Mathf.Approximately(m_pistolHeadshotMultiplier, 2f)
            && Mathf.Approximately(m_pistolSpreadAngle, 0.35f));
        Debug.Assert(m_shotgunPelletCount == 8 && Mathf.Approximately(m_shotgunPelletDamage, 12f)
            && Mathf.Approximately(m_shotgunSpreadAngle, 5f));
        Debug.Assert(Mathf.Approximately(m_rifleDamage, 15f)
            && Mathf.Approximately(m_rifleSpreadAngle, 0.75f));
        Debug.Assert(Mathf.Approximately(m_dmrFirstHitDamage, 60f)
            && Mathf.Approximately(m_dmrSecondHitDamage, 40f)
            && Mathf.Approximately(m_dmrThirdHitDamage, 20f)
            && Mathf.Approximately(m_dmrHeadshotMultiplier, 2f));
        Debug.Assert(Mathf.Approximately(m_pistolDamage * m_pistolShotsPerSecond, 202.5f)
            && Mathf.Approximately(m_shotgunPelletDamage * m_shotgunPelletCount
                * m_shotgunShotsPerSecond, 105.6f)
            && Mathf.Approximately(m_rifleDamage * m_rifleShotsPerSecond, 165f)
            && Mathf.Approximately(m_dmrFirstHitDamage * m_dmrShotsPerSecond, 315f));
        Debug.Assert(CalculateShotsToKill(60f, m_pistolDamage) == 2
            && CalculateShotsToKill(75f, m_pistolDamage) == 3
            && CalculateShotsToKill(90f, m_pistolDamage) == 3);
        Debug.Assert(CalculateShotsToKill(60f, m_rifleDamage) == 4
            && CalculateShotsToKill(75f, m_rifleDamage) == 5
            && CalculateShotsToKill(90f, m_rifleDamage) == 6);
        Debug.Assert(CalculateShotsToKill(60f, m_dmrFirstHitDamage) == 1
            && CalculateShotsToKill(75f, m_dmrFirstHitDamage) == 2
            && CalculateShotsToKill(90f, m_dmrFirstHitDamage) == 2);
        Debug.Assert(CalculateShotsToKill(60f, m_shotgunPelletDamage) == 5
            && CalculateShotsToKill(75f, m_shotgunPelletDamage) == 7
            && CalculateShotsToKill(90f, m_shotgunPelletDamage) == 8);
        Debug.Assert(CalculateShotsToKill(60f, m_pistolDamage * m_pistolHeadshotMultiplier) == 1
            && CalculateShotsToKill(75f, m_pistolDamage * m_pistolHeadshotMultiplier) == 2
            && CalculateShotsToKill(90f, m_pistolDamage * m_pistolHeadshotMultiplier) == 2);
        Debug.Assert(CalculateShotsToKill(60f, m_dmrFirstHitDamage * m_dmrHeadshotMultiplier) == 1
            && CalculateShotsToKill(75f, m_dmrFirstHitDamage * m_dmrHeadshotMultiplier) == 1
            && CalculateShotsToKill(90f, m_dmrFirstHitDamage * m_dmrHeadshotMultiplier) == 1);
        Debug.Assert(m_pistolRecoil.IsValid() && m_shotgunRecoil.IsValid() && m_rifleRecoil.IsValid()
            && m_dmrRecoil.IsValid() && m_rocketRecoil.IsValid());
        RecoilSample pistolRecoil = m_pistolRecoil.CreateSample();
        RecoilSample rifleRecoil = m_rifleRecoil.CreateSample();
        RecoilSample dmrRecoil = m_dmrRecoil.CreateSample();
        Debug.Assert(rifleRecoil.Pitch >= 2.3f * 0.88f && rifleRecoil.Pitch <= 2.3f * 1.12f
            && Mathf.Approximately(rifleRecoil.RecoveryDelay, 0.1f)
            && Mathf.Approximately(rifleRecoil.ReturnDuration, 0.12f));
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
        if (!GameplayClock.IsPaused && Cursor.lockState == CursorLockMode.Locked
            && m_characterController.isGrounded)
        {
            m_verticalVelocity = Mathf.Sqrt(m_jumpHeight * -2f * m_gravity);
        }
    }

    public void SetPaused(bool paused)
    {
        m_isRifleFiring = false;
        if (paused)
        {
            m_playerMap?.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!m_isDeathPresentation)
        {
            m_playerMap?.Enable();
            LockCursor();
        }
    }

    private void LockCursor()
    {
        ResetMouseLookFilter(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        ResetMouseLookFilter(false);
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
        m_maxMouseAngularSpeed = Mathf.Max(1f, m_maxMouseAngularSpeed);
        m_pistolAmmoCapacity = Mathf.Max(1, m_pistolAmmoCapacity);
        m_shotgunAmmoCapacity = Mathf.Max(1, m_shotgunAmmoCapacity);
        m_rifleAmmoCapacity = Mathf.Max(1, m_rifleAmmoCapacity);
        m_dmrAmmoCapacity = Mathf.Max(1, m_dmrAmmoCapacity);
        m_pistolDamage = Mathf.Max(0.01f, m_pistolDamage);
        m_pistolShotsPerSecond = Mathf.Max(0.01f, m_pistolShotsPerSecond);
        m_pistolHeadshotMultiplier = Mathf.Max(1f, m_pistolHeadshotMultiplier);
        m_pistolSpreadAngle = Mathf.Max(0f, m_pistolSpreadAngle);
        m_shotgunPelletCount = Mathf.Max(1, m_shotgunPelletCount);
        m_shotgunPelletDamage = Mathf.Max(0.01f, m_shotgunPelletDamage);
        m_shotgunShotsPerSecond = Mathf.Max(0.01f, m_shotgunShotsPerSecond);
        m_shotgunHeadshotMultiplier = Mathf.Max(1f, m_shotgunHeadshotMultiplier);
        m_shotgunSpreadAngle = Mathf.Max(0f, m_shotgunSpreadAngle);
        m_rifleDamage = Mathf.Max(0.01f, m_rifleDamage);
        m_rifleShotsPerSecond = Mathf.Max(0.01f, m_rifleShotsPerSecond);
        m_rifleHeadshotMultiplier = Mathf.Max(1f, m_rifleHeadshotMultiplier);
        m_rifleSpreadAngle = Mathf.Max(0f, m_rifleSpreadAngle);
        m_dmrFirstHitDamage = Mathf.Max(0.01f, m_dmrFirstHitDamage);
        m_dmrSecondHitDamage = Mathf.Max(0.01f, m_dmrSecondHitDamage);
        m_dmrThirdHitDamage = Mathf.Max(0.01f, m_dmrThirdHitDamage);
        m_dmrShotsPerSecond = Mathf.Max(0.01f, m_dmrShotsPerSecond);
        m_dmrHeadshotMultiplier = Mathf.Max(1f, m_dmrHeadshotMultiplier);
        m_damageAimPunchKickSpeed = Mathf.Max(0.1f, m_damageAimPunchKickSpeed);
        m_damageAimPunchReturnSpeed = Mathf.Max(0.1f, m_damageAimPunchReturnSpeed);
        m_damageAimPunchSoftCap = Mathf.Max(0.01f, m_damageAimPunchSoftCap);
        m_damageAimPunchHardCapPitch = Mathf.Max(0f, m_damageAimPunchHardCapPitch);
        m_damageAimPunchHardCapYaw = Mathf.Max(0f, m_damageAimPunchHardCapYaw);
        m_suicideDamageAimPunch = ClampAimPunchStrength(m_suicideDamageAimPunch);
        m_meleeDamageAimPunch = ClampAimPunchStrength(m_meleeDamageAimPunch);
        m_rangedDamageAimPunch = ClampAimPunchStrength(m_rangedDamageAimPunch);
        m_wallImpactMaxDistance = Mathf.Max(0.1f, m_wallImpactMaxDistance);
    }

    private static Vector2 ClampAimPunchStrength(Vector2 value)
    {
        return new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
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
