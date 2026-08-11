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
    private const float k_RocketRecoilKickDuration = 0.1f;
    private const float k_RocketRecoilReturnDuration = 0.2f;
    private const float k_RocketCameraRecoil = 2.3f;
    private const float k_RocketViewmodelRecoilDistance = 0.1f;
    private const float k_RocketViewmodelRecoilPitch = 4f;

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
    private float m_rocketRecoilStartedAt;
    private bool m_rocketRecoilActive;

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
            isRocket ? 150f : 90f,
            isRocket ? 75f : 45f,
            isRocket ? 4f : 5f,
            5f,
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
        m_rocketRecoilActive = true;
        m_rocketRecoilStartedAt = Time.unscaledTime;
        FirstPersonController.CurrentInstance?.ApplyCameraRecoil(k_RocketCameraRecoil, 0f);
    }

    private void UpdateRocketRecoil()
    {
        if (!m_rocketRecoilActive)
        {
            return;
        }

        float elapsed = Time.unscaledTime - m_rocketRecoilStartedAt;
        float amount = elapsed < k_RocketRecoilKickDuration
            ? Mathf.SmoothStep(0f, 1f, elapsed / k_RocketRecoilKickDuration)
            : 1f - Mathf.SmoothStep(0f, 1f,
                (elapsed - k_RocketRecoilKickDuration) / k_RocketRecoilReturnDuration);
        if (elapsed >= k_RocketRecoilKickDuration + k_RocketRecoilReturnDuration)
        {
            FinishRocketRecoil();
            return;
        }

        if (m_rocketLauncherViewmodel != null)
        {
            Transform launcher = m_rocketLauncherViewmodel.transform;
            launcher.localPosition = m_rocketLauncherRestPosition
                + Vector3.back * (k_RocketViewmodelRecoilDistance * amount);
            launcher.localRotation = m_rocketLauncherRestRotation
                * Quaternion.Euler(-k_RocketViewmodelRecoilPitch * amount, 0f, 0f);
        }
    }

    private void FinishRocketRecoil()
    {
        m_rocketRecoilActive = false;
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
        Debug.Assert(Mathf.Approximately(k_RocketCameraRecoil, 2.3f));
        Debug.Assert(Mathf.Approximately(k_RocketRecoilKickDuration + k_RocketRecoilReturnDuration, 0.3f));
        Debug.Assert(m_state is >= PlayerSkillState.Ready and <= PlayerSkillState.Cooldown);
    }
}
