using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class PlayerSkillController : MonoBehaviour
{
    private const float k_BulletTimeScale = 0.35f;
    private const float k_BulletTimeDuration = 5f;
    private const float k_BulletTimeSaturation = -100f;
    private const float k_BulletTimeVisualTransition = 0.15f;
    private const float k_GrenadeFuseDuration = 4.5f;
    private const float k_GrenadeDamage = 90f;
    private const float k_GrenadeSelfDamage = 45f;
    private const float k_GrenadeRadius = 5f;
    private const float k_RocketViewmodelRecoilDistance = 0.14f;
    private const float k_RocketViewmodelRecoilLateralDistance = 0.04f;
    private const float k_RocketViewmodelRecoilPitch = 9f;
    private const float k_RocketViewmodelRecoilYaw = 2.2f;
    private const float k_RocketViewmodelRecoilRoll = 3f;
    private const float k_RocketPositionSpringFrequency = 32f;
    private const float k_RocketPositionSpringDamping = 0.7f;
    private const float k_RocketRotationSpringFrequency = 24f;
    private const float k_RocketRotationSpringDamping = 0.65f;
    private const float k_RocketSpringImpulseScale = 2f;
    private const float k_MaxRocketSpringScale = 1.5f;

    [SerializeField] private PlayerSkillProjectile m_grenadeProjectile;
    [SerializeField] private PlayerSkillProjectile m_rocketProjectile;
    [SerializeField] private GameObject m_rocketLauncherViewmodel;
    [SerializeField] private Transform m_rocketMuzzle;
    [SerializeField] private Volume m_gameplayVolume;
    [SerializeField] private Vector3 m_heldLocalPosition = new(0.32f, -0.28f, 0.72f);
    [SerializeField] private Vector3 m_heldLocalEuler = new(0f, 180f, 0f);

    private PlayerClassId m_playerClass;
    private Camera m_camera;
    private PlayerHealth m_playerHealth;
    private GameplayHUD m_hud;
    private ScoreSystem m_scoreSystem;
    private WeaponViewmodelController m_viewmodel;
    private PlayerSkillProjectile m_projectile;
    private PlayerSkillState m_state = PlayerSkillState.Ready;
    private float m_stateEndsAt;
    private float m_cooldownDuration;
    private float m_defaultFixedDeltaTime;
    private bool m_controlsTimeScale;
    private ColorAdjustments m_colorAdjustments;
    private float m_defaultSaturation;
    private float m_targetSaturation;
    private float m_saturationTransitionSpeed;
    private bool m_defaultSaturationOverride;
    private Vector3 m_rocketLauncherRestPosition;
    private Quaternion m_rocketLauncherRestRotation;
    private RecoilSample m_rocketRecoilSample;
    private float m_rocketRecoilStartedAt;
    private bool m_rocketRecoilActive;
    private Vector3 m_rocketPositionOffset;
    private Vector3 m_rocketPositionVelocity;
    private Vector3 m_rocketRotationOffset;
    private Vector3 m_rocketRotationVelocity;

    public PlayerSkillState State => m_state;
    internal bool IsWeaponInputLocked => m_rocketRecoilActive;

    private void Awake()
    {
        m_defaultFixedDeltaTime = Time.fixedDeltaTime;
        if (m_rocketLauncherViewmodel != null)
        {
            Transform launcher = m_rocketLauncherViewmodel.transform;
            m_rocketLauncherRestPosition = launcher.localPosition;
            m_rocketLauncherRestRotation = launcher.localRotation;
            m_rocketLauncherViewmodel.SetActive(false);
        }
    }

    public void Initialize(PlayerClassId playerClass, Camera playerCamera, PlayerHealth playerHealth,
        GameplayHUD hud, ScoreSystem scoreSystem)
    {
        m_playerClass = playerClass;
        m_camera = playerCamera;
        m_playerHealth = playerHealth;
        m_hud = hud;
        m_scoreSystem = scoreSystem;
        if (m_scoreSystem != null)
        {
            m_scoreSystem.SetBulletTimeActive(false);
        }
        m_viewmodel = playerCamera != null ? playerCamera.GetComponent<WeaponViewmodelController>() : null;
        m_grenadeProjectile?.Initialize(this, playerHealth, scoreSystem);
        m_rocketProjectile?.Initialize(this, playerHealth, scoreSystem);
        m_state = PlayerSkillState.Ready;
        m_stateEndsAt = 0f;
        m_cooldownDuration = 0f;
        ResetRocketLauncherPresentation();
        InitializeBulletTimeVisual();
        RefreshHud();
    }

    private void Update()
    {
        if ((m_state == PlayerSkillState.Active || m_state == PlayerSkillState.Cooldown)
            && m_stateEndsAt > 0f && Time.unscaledTime >= m_stateEndsAt)
        {
            if (m_state == PlayerSkillState.Active && m_playerClass == PlayerClassId.Sniper)
            {
                RestoreTimeScale();
                BeginCooldown(20f);
            }
            else if (m_state == PlayerSkillState.Cooldown)
            {
                m_state = PlayerSkillState.Ready;
                m_stateEndsAt = 0f;
                m_cooldownDuration = 0f;
                m_playerHealth?.PlayAbilityReadyAnnouncement();
            }
        }
        UpdateRocketRecoil();
        UpdateBulletTimeVisual();
        RefreshHud();
    }

    public bool TryActivateOrArm()
    {
        if (m_state != PlayerSkillState.Ready || m_camera == null)
        {
            return false;
        }

        if (m_playerClass == PlayerClassId.Sniper)
        {
            m_controlsTimeScale = true;
            Time.timeScale = k_BulletTimeScale;
            Time.fixedDeltaTime = m_defaultFixedDeltaTime * k_BulletTimeScale;
            SetBulletTimeVisual(true);
            m_state = PlayerSkillState.Active;
            m_stateEndsAt = Time.unscaledTime + k_BulletTimeDuration;
            if (m_scoreSystem != null)
            {
                m_scoreSystem.SetBulletTimeActive(true);
            }
            RefreshHud();
            return true;
        }

        m_projectile = m_playerClass == PlayerClassId.Engineer ? m_rocketProjectile : m_grenadeProjectile;
        if (m_projectile == null)
        {
            Debug.LogError($"Missing {GetSkillName()} projectile on PF_Player.");
            return false;
        }

        if (m_playerClass == PlayerClassId.Engineer)
        {
            if (m_rocketLauncherViewmodel == null || m_rocketMuzzle == null)
            {
                Debug.LogError("Missing RocketLauncherViewmodel or RocketMuzzle on PF_Player.");
                return false;
            }
            ResetRocketLauncherPose();
            m_rocketLauncherViewmodel.SetActive(true);
        }
        else
        {
            m_projectile.ShowArmed(m_camera.transform, m_heldLocalPosition, Quaternion.Euler(m_heldLocalEuler));
        }
        m_viewmodel?.SetSkillArmed(true);
        m_state = PlayerSkillState.Armed;
        RefreshHud();
        return true;
    }

    public bool TryUseArmedSkill()
    {
        if (m_state != PlayerSkillState.Armed || m_projectile == null)
        {
            return false;
        }

        bool isRocket = m_playerClass == PlayerClassId.Engineer;
        m_state = PlayerSkillState.Active;
        m_stateEndsAt = 0f;
        Vector3 launchPosition = isRocket && m_rocketMuzzle != null
            ? m_rocketMuzzle.position
            : m_camera.transform.position + m_camera.transform.forward * 0.8f;
        m_projectile.Launch(
            launchPosition,
            m_camera.transform.forward,
            isRocket ? 25f : 12f,
            isRocket ? 150f : k_GrenadeDamage,
            isRocket ? 75f : k_GrenadeSelfDamage,
            isRocket ? 4f : k_GrenadeRadius,
            isRocket ? 5f : k_GrenadeFuseDuration,
            !isRocket,
            FirstPersonController.CurrentInstance != null ? FirstPersonController.CurrentInstance.CurrentWeapon : WeaponId.Unknown,
            isRocket ? PlayerDeathCause.RocketSelfDamage : PlayerDeathCause.GrenadeSelfDamage);
        if (isRocket)
        {
            BeginRocketRecoil();
        }
        else
        {
            m_viewmodel?.SetSkillArmed(false);
        }
        RefreshHud();
        return true;
    }

    public void CancelArmedSkill()
    {
        if (m_rocketRecoilActive)
        {
            FinishRocketRecoil();
            return;
        }
        if (m_state != PlayerSkillState.Armed)
        {
            return;
        }
        if (m_projectile != null)
        {
            m_projectile.Hide();
        }
        ResetRocketLauncherPresentation();
        m_viewmodel?.SetSkillArmed(false);
        m_state = PlayerSkillState.Ready;
        m_stateEndsAt = 0f;
        m_cooldownDuration = 0f;
        RefreshHud();
    }

    internal void NotifyProjectileExploded()
    {
        BeginCooldown(m_playerClass == PlayerClassId.Engineer ? 14f : 10f);
    }

    private void BeginCooldown(float duration)
    {
        m_state = PlayerSkillState.Cooldown;
        m_cooldownDuration = duration;
        m_stateEndsAt = Time.unscaledTime + duration;
        RefreshHud();
    }

    private void BeginRocketRecoil()
    {
        FirstPersonController controller = FirstPersonController.CurrentInstance;
        if (controller == null)
        {
            Debug.LogError("PF_Player is missing FirstPersonController for rocket recoil.");
            FinishRocketRecoil();
            return;
        }

        m_rocketRecoilSample = controller.ApplyRocketRecoil();
        float horizontal = m_rocketRecoilSample.HorizontalScale
            * m_rocketRecoilSample.HorizontalDirection;
        Vector3 positionImpulse = new(
            k_RocketViewmodelRecoilLateralDistance * horizontal,
            0f,
            -k_RocketViewmodelRecoilDistance * m_rocketRecoilSample.VerticalScale);
        Vector3 rotationImpulse = new(
            -k_RocketViewmodelRecoilPitch * m_rocketRecoilSample.VerticalScale,
            k_RocketViewmodelRecoilYaw * horizontal,
            k_RocketViewmodelRecoilRoll * horizontal);
        m_rocketPositionOffset = Vector3.zero;
        m_rocketRotationOffset = Vector3.zero;
        m_rocketPositionVelocity = positionImpulse
            * (k_RocketPositionSpringFrequency * k_RocketSpringImpulseScale);
        m_rocketRotationVelocity = rotationImpulse
            * (k_RocketRotationSpringFrequency * k_RocketSpringImpulseScale);
        m_rocketRecoilActive = true;
        m_rocketRecoilStartedAt = Time.time;
    }

    private void UpdateRocketRecoil()
    {
        if (!m_rocketRecoilActive)
        {
            return;
        }

        float elapsed = Time.time - m_rocketRecoilStartedAt;
        if (elapsed >= m_rocketRecoilSample.RecoveryDelay + m_rocketRecoilSample.ReturnDuration)
        {
            FinishRocketRecoil();
            return;
        }

        WeaponViewmodelController.StepSpring(ref m_rocketPositionOffset, ref m_rocketPositionVelocity,
            k_RocketPositionSpringFrequency, k_RocketPositionSpringDamping, Time.deltaTime);
        WeaponViewmodelController.StepSpring(ref m_rocketRotationOffset, ref m_rocketRotationVelocity,
            k_RocketRotationSpringFrequency, k_RocketRotationSpringDamping, Time.deltaTime);
        ClampRocketSpring();

        if (m_rocketLauncherViewmodel != null)
        {
            Transform launcher = m_rocketLauncherViewmodel.transform;
            launcher.localPosition = m_rocketLauncherRestPosition + m_rocketPositionOffset;
            launcher.localRotation = m_rocketLauncherRestRotation
                * Quaternion.Euler(m_rocketRotationOffset);
        }
    }

    private void ClampRocketSpring()
    {
        WeaponViewmodelController.ClampSpringAxis(ref m_rocketPositionOffset.x,
            ref m_rocketPositionVelocity.x,
            k_RocketViewmodelRecoilLateralDistance * k_MaxRocketSpringScale);
        WeaponViewmodelController.ClampSpringAxis(ref m_rocketPositionOffset.z,
            ref m_rocketPositionVelocity.z,
            k_RocketViewmodelRecoilDistance * k_MaxRocketSpringScale);
        WeaponViewmodelController.ClampSpringAxis(ref m_rocketRotationOffset.x,
            ref m_rocketRotationVelocity.x,
            k_RocketViewmodelRecoilPitch * k_MaxRocketSpringScale);
        WeaponViewmodelController.ClampSpringAxis(ref m_rocketRotationOffset.y,
            ref m_rocketRotationVelocity.y,
            k_RocketViewmodelRecoilYaw * k_MaxRocketSpringScale);
        WeaponViewmodelController.ClampSpringAxis(ref m_rocketRotationOffset.z,
            ref m_rocketRotationVelocity.z,
            k_RocketViewmodelRecoilRoll * k_MaxRocketSpringScale);
    }

    private void FinishRocketRecoil()
    {
        m_rocketRecoilActive = false;
        m_rocketRecoilSample = default;
        ResetRocketLauncherPresentation();
        m_viewmodel?.SetSkillArmed(false);
    }

    private void ResetRocketLauncherPresentation()
    {
        ResetRocketLauncherPose();
        if (m_rocketLauncherViewmodel != null)
        {
            m_rocketLauncherViewmodel.SetActive(false);
        }
    }

    private void ResetRocketLauncherPose()
    {
        m_rocketPositionOffset = Vector3.zero;
        m_rocketPositionVelocity = Vector3.zero;
        m_rocketRotationOffset = Vector3.zero;
        m_rocketRotationVelocity = Vector3.zero;
        if (m_rocketLauncherViewmodel == null)
        {
            return;
        }
        Transform launcher = m_rocketLauncherViewmodel.transform;
        launcher.localPosition = m_rocketLauncherRestPosition;
        launcher.localRotation = m_rocketLauncherRestRotation;
    }

    private void RefreshHud()
    {
        float remaining = m_stateEndsAt > 0f ? Mathf.Max(0f, m_stateEndsAt - Time.unscaledTime) : 0f;
        float cooldownNormalized = m_state == PlayerSkillState.Cooldown && m_cooldownDuration > 0f
            ? 1f - remaining / m_cooldownDuration
            : 0f;
        m_hud?.RefreshSkill(GetSkillName(), m_state, cooldownNormalized);
    }

    private string GetSkillName()
    {
        return m_playerClass switch
        {
            PlayerClassId.Engineer => "ROCKET",
            PlayerClassId.Sniper => "BULLET TIME",
            _ => "GRENADE"
        };
    }

    private void OnDisable()
    {
        RestoreTimeScale();
        RestoreBulletTimeVisualImmediately();
        m_grenadeProjectile?.Hide();
        m_rocketProjectile?.Hide();
        m_rocketRecoilActive = false;
        m_rocketRecoilSample = default;
        ResetRocketLauncherPresentation();
        m_viewmodel?.SetSkillArmed(false);
        m_projectile = null;
        m_state = PlayerSkillState.Ready;
        m_stateEndsAt = 0f;
        m_cooldownDuration = 0f;
        RefreshHud();
    }

    private void RestoreTimeScale()
    {
        if (m_scoreSystem != null)
        {
            m_scoreSystem.SetBulletTimeActive(false);
        }
        SetBulletTimeVisual(false);
        if (!m_controlsTimeScale)
        {
            return;
        }
        m_controlsTimeScale = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_defaultFixedDeltaTime;
    }

    private void InitializeBulletTimeVisual()
    {
        m_colorAdjustments = null;
        if (m_playerClass != PlayerClassId.Sniper)
        {
            return;
        }
        if (m_gameplayVolume == null || !m_gameplayVolume.profile.TryGet(out m_colorAdjustments))
        {
            Debug.LogError("Missing Gameplay Volume ColorAdjustments for Sniper bullet time.");
            return;
        }

        m_defaultSaturation = m_colorAdjustments.saturation.value;
        m_defaultSaturationOverride = m_colorAdjustments.saturation.overrideState;
        m_targetSaturation = m_defaultSaturation;
        m_saturationTransitionSpeed = Mathf.Abs(k_BulletTimeSaturation - m_defaultSaturation)
            / k_BulletTimeVisualTransition;
    }

    private void SetBulletTimeVisual(bool active)
    {
        if (m_colorAdjustments == null)
        {
            return;
        }
        m_colorAdjustments.saturation.overrideState = true;
        m_targetSaturation = active ? k_BulletTimeSaturation : m_defaultSaturation;
    }

    private void UpdateBulletTimeVisual()
    {
        if (m_colorAdjustments == null)
        {
            return;
        }
        m_colorAdjustments.saturation.value = Mathf.MoveTowards(
            m_colorAdjustments.saturation.value,
            m_targetSaturation,
            m_saturationTransitionSpeed * Time.unscaledDeltaTime);
        if (Mathf.Approximately(m_targetSaturation, m_defaultSaturation)
            && Mathf.Approximately(m_colorAdjustments.saturation.value, m_defaultSaturation))
        {
            m_colorAdjustments.saturation.overrideState = m_defaultSaturationOverride;
        }
    }

    private void RestoreBulletTimeVisualImmediately()
    {
        if (m_colorAdjustments == null)
        {
            return;
        }
        m_targetSaturation = m_defaultSaturation;
        m_colorAdjustments.saturation.value = m_defaultSaturation;
        m_colorAdjustments.saturation.overrideState = m_defaultSaturationOverride;
    }

    [ContextMenu("Run Player Skill Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Mathf.Approximately(k_BulletTimeScale, 0.35f));
        Debug.Assert(Mathf.Approximately(k_BulletTimeDuration, 5f));
        Debug.Assert(Mathf.Approximately(k_BulletTimeSaturation, -100f));
        Debug.Assert(Mathf.Approximately(k_BulletTimeVisualTransition, 0.15f));
        Debug.Assert(Mathf.Approximately(k_GrenadeFuseDuration, 4.5f));
        Debug.Assert(Mathf.Approximately(k_GrenadeDamage, 90f));
        Debug.Assert(Mathf.Approximately(k_GrenadeSelfDamage, 45f));
        Debug.Assert(Mathf.Approximately(k_GrenadeRadius, 5f));
        Debug.Assert(Mathf.Approximately(k_RocketViewmodelRecoilDistance, 0.14f));
        Debug.Assert(Mathf.Approximately(k_RocketViewmodelRecoilLateralDistance, 0.04f));
        Debug.Assert(Mathf.Approximately(k_RocketViewmodelRecoilPitch, 9f));
        Debug.Assert(m_state is >= PlayerSkillState.Ready and <= PlayerSkillState.Cooldown);
    }
}
