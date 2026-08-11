using UnityEngine;

public sealed class WeaponViewmodelController : MonoBehaviour
{
    private const float k_ReferenceMoveSpeed = 6f;
    private static readonly float[] s_MovementScales = { 1f, 0.65f, 0.8f, 0.75f };

    [SerializeField] private GameObject m_pistolRoot;
    [SerializeField] private GameObject m_shotgunRoot;
    [SerializeField] private GameObject m_rifleRoot;
    [SerializeField] private GameObject m_dmrRoot;
    [SerializeField] private MuzzleFlashEffect m_pistolMuzzleFlash;
    [SerializeField] private MuzzleFlashEffect m_shotgunMuzzleFlash;
    [SerializeField] private MuzzleFlashEffect m_rifleMuzzleFlash;
    [SerializeField] private MuzzleFlashEffect m_dmrMuzzleFlash;
    [SerializeField] private DmrTracerEmitter m_dmrTracer;
    [Header("Weapon Audio")]
    [SerializeField] private AudioSource m_weaponAudioSource;
    [SerializeField] private AudioClip[] m_pistolFireClips;
    [SerializeField] private AudioClip[] m_shotgunFireClips;
    [SerializeField] private AudioClip[] m_rifleFireClips;
    [SerializeField] private AudioClip[] m_dmrFireClips;
    [SerializeField] private AudioClip[] m_emptyAmmoClips;
    [SerializeField] private FootstepAudio m_playerFootsteps;
    [SerializeField] private float[] m_weaponFireVolumes = { 1f, 1f, 1f, 1f };
    [SerializeField] private float m_emptyAmmoVolume = 0.7f;
    [SerializeField] private float m_recoilDistance = 0.06f;
    [SerializeField] private float m_recoilDuration = 0.1f;
    [Header("Movement Motion")]
    [SerializeField] private float m_strafeSwayDistance = 0.025f;
    [SerializeField] private float m_forwardSwayDistance = 0.018f;
    [SerializeField] private float m_strafeSwayRoll = 2f;
    [SerializeField] private float m_forwardSwayPitch = 1f;
    [SerializeField] private float m_bobHorizontalDistance = 0.008f;
    [SerializeField] private float m_bobVerticalDistance = 0.012f;
    [SerializeField] private float m_bobFrequency = 1.6f;
    [SerializeField] private float m_movementLerpSpeed = 10f;

    private readonly Vector3[] m_rootRestPositions = new Vector3[4];
    private readonly Quaternion[] m_rootRestRotations = new Quaternion[4];
    private readonly int[] m_lastFireClipIndices = { -1, -1, -1, -1 };
    private CharacterController m_characterController;
    private Vector3 m_movementPositionOffset;
    private Vector3 m_movementRotationOffset;
    private float m_bobPhase;
    private float m_fireRecoilAmount;
    private WeaponId m_activeWeapon;
    private int m_lastEmptyAmmoClipIndex = -1;
    private bool m_waitingForLanding;
    private bool m_fireAnimationActive;
    private float m_fireAnimationStartedAt;

    private void Awake()
    {
        m_characterController = GetComponentInParent<CharacterController>();
        ConfigureAudioSource();
        Debug.Assert(IsFireAudioConfigurationValid(), "Each weapon needs 2 or 3 assigned fire clips.");
        Debug.Assert(m_characterController != null && m_dmrRoot != null && m_weaponAudioSource != null,
            "PF_Player viewmodel references are incomplete.");
        CacheRestTransforms();
        SelectWeapon(WeaponId.Pistol);
    }

    private void LateUpdate()
    {
        UpdateMovementPose();
        UpdateFireAnimation();
        ApplyActivePose();
    }

    private void OnDisable()
    {
        StopFireAnimation();
        StopAllMuzzleEffects();

        if (Application.isPlaying)
        {
            m_waitingForLanding = false;
            ResetMovementPose();
            RestoreRestPose();
        }
    }

    public void SelectWeapon(WeaponId weapon)
    {
        if (weapon < WeaponId.Pistol || weapon > WeaponId.DMR)
        {
            return;
        }

        StopFireAnimation();
        StopAllMuzzleEffects();
        ResetMovementPose();
        RestoreRestPose();
        m_activeWeapon = weapon;
        SetActive(m_pistolRoot, weapon == WeaponId.Pistol);
        SetActive(m_shotgunRoot, weapon == WeaponId.Shotgun);
        SetActive(m_rifleRoot, weapon == WeaponId.Rifle);
        SetActive(m_dmrRoot, weapon == WeaponId.DMR);
    }

    public void PlayFireFeedback()
    {
        Transform root = GetActiveRoot();
        if (root == null)
        {
            return;
        }

        StopFireAnimation();
        GetActiveMuzzleFlash()?.Play();
        PlayRandomClip(GetActiveFireClips(), ref m_lastFireClipIndices[(int)m_activeWeapon - 1], GetActiveFireVolume());
        m_fireAnimationStartedAt = Time.time;
        m_fireAnimationActive = true;
    }

    public void PlayEmptyAmmoFeedback()
    {
        PlayRandomClip(m_emptyAmmoClips, ref m_lastEmptyAmmoClipIndex, m_emptyAmmoVolume);
    }

    public void PlayDmrTracer(Vector3 endPoint)
    {
        if (m_activeWeapon == WeaponId.DMR)
        {
            m_dmrTracer?.EmitTo(endPoint);
        }
    }

    public void SetSkillArmed(bool isArmed)
    {
        if (!isArmed)
        {
            SelectWeapon(m_activeWeapon);
            return;
        }

        StopFireAnimation();
        StopAllMuzzleEffects();
        SetActive(m_pistolRoot, false);
        SetActive(m_shotgunRoot, false);
        SetActive(m_rifleRoot, false);
        SetActive(m_dmrRoot, false);
    }

    [ContextMenu("Run Viewmodel Self Check")]
    private void RunViewmodelSelfCheck()
    {
        SelectWeapon(WeaponId.Unknown);
        Debug.Assert(m_activeWeapon >= WeaponId.Pistol && m_activeWeapon <= WeaponId.DMR);
        SelectWeapon(WeaponId.Shotgun);
        Debug.Assert(m_activeWeapon == WeaponId.Shotgun);
        Debug.Assert(m_weaponFireVolumes != null && m_weaponFireVolumes.Length == 4);
        Debug.Assert(IsFireAudioConfigurationValid());
        Debug.Assert(s_MovementScales[0] > s_MovementScales[2] && s_MovementScales[2] > s_MovementScales[1]);
        Debug.Assert(CrossedPhase(1f, 2f, Mathf.PI * 0.5f));
        Debug.Assert(CrossedPhase(4f, 5f, Mathf.PI * 1.5f));
        Debug.Assert(!CrossedPhase(6f, 0.2f, Mathf.PI * 0.5f));
        RestoreRestPose();
    }

    private bool IsFireAudioConfigurationValid()
    {
        return HasTwoOrThreeClips(m_pistolFireClips)
            && HasTwoOrThreeClips(m_shotgunFireClips)
            && HasTwoOrThreeClips(m_rifleFireClips)
            && HasTwoOrThreeClips(m_dmrFireClips);
    }

    private static bool HasTwoOrThreeClips(AudioClip[] clips)
    {
        if (clips == null || clips.Length < 2 || clips.Length > 3)
        {
            return false;
        }

        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void ConfigureAudioSource()
    {
        if (m_weaponAudioSource == null)
        {
            Debug.LogError("PF_Player MainCamera is missing its weapon AudioSource.");
            return;
        }

        m_weaponAudioSource.playOnAwake = false;
        m_weaponAudioSource.loop = false;
        m_weaponAudioSource.spatialBlend = 0f;
    }

    private AudioClip[] GetActiveFireClips()
    {
        return m_activeWeapon switch
        {
            WeaponId.Pistol => m_pistolFireClips,
            WeaponId.Shotgun => m_shotgunFireClips,
            WeaponId.Rifle => m_rifleFireClips,
            WeaponId.DMR => m_dmrFireClips,
            _ => null
        };
    }

    private float GetActiveFireVolume()
    {
        return m_weaponFireVolumes != null && m_weaponFireVolumes.Length == 4
            ? m_weaponFireVolumes[(int)m_activeWeapon - 1]
            : 1f;
    }

    private void PlayRandomClip(AudioClip[] clips, ref int lastIndex, float volume)
    {
        if (m_weaponAudioSource == null || clips == null || clips.Length == 0)
        {
            return;
        }

        int playableCount = 0;
        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] != null)
            {
                playableCount++;
            }
        }

        if (playableCount == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            int candidateIndex = (selectedIndex + offset) % clips.Length;
            if (clips[candidateIndex] != null && (playableCount == 1 || candidateIndex != lastIndex))
            {
                selectedIndex = candidateIndex;
                break;
            }
        }

        lastIndex = selectedIndex;
        m_weaponAudioSource.PlayOneShot(clips[selectedIndex], Mathf.Clamp01(volume));
    }

    private void OnValidate()
    {
        if (m_weaponFireVolumes == null || m_weaponFireVolumes.Length != 4)
        {
            m_weaponFireVolumes = new[] { 1f, 1f, 1f, 1f };
        }

        for (int index = 0; index < m_weaponFireVolumes.Length; index++)
        {
            m_weaponFireVolumes[index] = Mathf.Clamp01(m_weaponFireVolumes[index]);
        }

        m_emptyAmmoVolume = Mathf.Clamp01(m_emptyAmmoVolume);
        m_recoilDistance = Mathf.Max(0f, m_recoilDistance);
        m_recoilDuration = Mathf.Max(0.01f, m_recoilDuration);
        m_strafeSwayDistance = Mathf.Max(0f, m_strafeSwayDistance);
        m_forwardSwayDistance = Mathf.Max(0f, m_forwardSwayDistance);
        m_strafeSwayRoll = Mathf.Max(0f, m_strafeSwayRoll);
        m_forwardSwayPitch = Mathf.Max(0f, m_forwardSwayPitch);
        m_bobHorizontalDistance = Mathf.Max(0f, m_bobHorizontalDistance);
        m_bobVerticalDistance = Mathf.Max(0f, m_bobVerticalDistance);
        m_bobFrequency = Mathf.Max(0f, m_bobFrequency);
        m_movementLerpSpeed = Mathf.Max(0.1f, m_movementLerpSpeed);
        if (m_weaponAudioSource != null)
        {
            m_weaponAudioSource.playOnAwake = false;
            m_weaponAudioSource.loop = false;
            m_weaponAudioSource.spatialBlend = 0f;
        }
    }

    private void UpdateFireAnimation()
    {
        if (!m_fireAnimationActive)
        {
            return;
        }

        float halfDuration = m_recoilDuration * 0.5f;
        float elapsed = Time.time - m_fireAnimationStartedAt;
        if (elapsed < halfDuration)
        {
            m_fireRecoilAmount = Mathf.Clamp01(elapsed / halfDuration);
            return;
        }
        if (elapsed < m_recoilDuration)
        {
            m_fireRecoilAmount = 1f - Mathf.Clamp01((elapsed - halfDuration) / halfDuration);
            return;
        }
        m_fireRecoilAmount = 0f;
        m_fireAnimationActive = false;
    }

    private void UpdateMovementPose()
    {
        Vector3 targetPosition = Vector3.zero;
        Vector3 targetRotation = Vector3.zero;
        Vector3 planarVelocity = m_characterController != null ? m_characterController.velocity : Vector3.zero;
        planarVelocity.y = 0f;
        float planarSpeed = planarVelocity.magnitude;
        UpdateLandingFootstep();

        if (m_characterController != null && m_characterController.isGrounded && planarSpeed > 0.05f)
        {
            float speedRatio = Mathf.Clamp01(planarSpeed / k_ReferenceMoveSpeed);
            float movementScale = GetMovementScale() * speedRatio;
            Vector3 localDirection = m_characterController.transform.InverseTransformDirection(planarVelocity.normalized);
            float previousBobPhase = m_bobPhase;
            m_bobPhase = Mathf.Repeat(
                m_bobPhase + Mathf.PI * 2f * m_bobFrequency * speedRatio * Time.deltaTime,
                Mathf.PI * 2f);
            PlayCrossedFootsteps(previousBobPhase, m_bobPhase);

            targetPosition = new Vector3(
                -localDirection.x * m_strafeSwayDistance,
                0f,
                -localDirection.z * m_forwardSwayDistance) * movementScale;
            targetPosition += new Vector3(
                Mathf.Sin(m_bobPhase * 0.5f) * m_bobHorizontalDistance,
                -Mathf.Abs(Mathf.Sin(m_bobPhase)) * m_bobVerticalDistance,
                0f) * movementScale;
            targetRotation = new Vector3(
                -localDirection.z * m_forwardSwayPitch,
                0f,
                -localDirection.x * m_strafeSwayRoll) * movementScale;
        }
        else
        {
            m_bobPhase = 0f;
        }

        float lerpAmount = Mathf.Clamp01(m_movementLerpSpeed * Time.deltaTime);
        m_movementPositionOffset = Vector3.Lerp(m_movementPositionOffset, targetPosition, lerpAmount);
        m_movementRotationOffset = Vector3.Lerp(m_movementRotationOffset, targetRotation, lerpAmount);
    }

    private void UpdateLandingFootstep()
    {
        if (m_characterController == null)
        {
            return;
        }

        if (!m_characterController.isGrounded && m_characterController.velocity.y > 0.1f)
        {
            m_waitingForLanding = true;
        }
        else if (m_characterController.isGrounded && m_waitingForLanding)
        {
            m_waitingForLanding = false;
            m_playerFootsteps?.PlayFootstep();
        }
    }

    private void PlayCrossedFootsteps(float previousPhase, float currentPhase)
    {
        if (CrossedPhase(previousPhase, currentPhase, Mathf.PI * 0.5f))
        {
            m_playerFootsteps?.PlayFootstep();
        }

        if (CrossedPhase(previousPhase, currentPhase, Mathf.PI * 1.5f))
        {
            m_playerFootsteps?.PlayFootstep();
        }
    }

    private static bool CrossedPhase(float previousPhase, float currentPhase, float threshold)
    {
        return currentPhase >= previousPhase
            ? previousPhase < threshold && currentPhase >= threshold
            : previousPhase < threshold || currentPhase >= threshold;
    }

    private void ApplyActivePose()
    {
        Transform root = GetActiveRoot();
        if (root == null)
        {
            return;
        }

        root.localPosition = GetRootRestPosition(m_activeWeapon)
            + m_movementPositionOffset
            + Vector3.back * (m_recoilDistance * m_fireRecoilAmount);
        root.localRotation = Quaternion.Euler(m_movementRotationOffset) * GetRootRestRotation(m_activeWeapon);
    }

    private void CacheRestTransforms()
    {
        m_rootRestPositions[0] = GetLocalPosition(m_pistolRoot);
        m_rootRestPositions[1] = GetLocalPosition(m_shotgunRoot);
        m_rootRestPositions[2] = GetLocalPosition(m_rifleRoot);
        m_rootRestPositions[3] = GetLocalPosition(m_dmrRoot);
        m_rootRestRotations[0] = GetLocalRotation(m_pistolRoot);
        m_rootRestRotations[1] = GetLocalRotation(m_shotgunRoot);
        m_rootRestRotations[2] = GetLocalRotation(m_rifleRoot);
        m_rootRestRotations[3] = GetLocalRotation(m_dmrRoot);
    }

    private void RestoreRestPose()
    {
        RestoreRootTransform(m_pistolRoot, 1);
        RestoreRootTransform(m_shotgunRoot, 2);
        RestoreRootTransform(m_rifleRoot, 3);
        RestoreRootTransform(m_dmrRoot, 4);
    }

    private void StopFireAnimation()
    {
        m_fireAnimationActive = false;
        m_fireRecoilAmount = 0f;
    }

    private void ResetMovementPose()
    {
        m_movementPositionOffset = Vector3.zero;
        m_movementRotationOffset = Vector3.zero;
        m_bobPhase = 0f;
    }

    private Transform GetActiveRoot()
    {
        return m_activeWeapon switch
        {
            WeaponId.Pistol => m_pistolRoot != null ? m_pistolRoot.transform : null,
            WeaponId.Shotgun => m_shotgunRoot != null ? m_shotgunRoot.transform : null,
            WeaponId.Rifle => m_rifleRoot != null ? m_rifleRoot.transform : null,
            WeaponId.DMR => m_dmrRoot != null ? m_dmrRoot.transform : null,
            _ => null
        };
    }

    private MuzzleFlashEffect GetActiveMuzzleFlash()
    {
        return m_activeWeapon switch
        {
            WeaponId.Pistol => m_pistolMuzzleFlash,
            WeaponId.Shotgun => m_shotgunMuzzleFlash,
            WeaponId.Rifle => m_rifleMuzzleFlash,
            WeaponId.DMR => m_dmrMuzzleFlash,
            _ => null
        };
    }

    private void StopAllMuzzleEffects()
    {
        m_pistolMuzzleFlash?.StopEffect();
        m_shotgunMuzzleFlash?.StopEffect();
        m_rifleMuzzleFlash?.StopEffect();
        m_dmrMuzzleFlash?.StopEffect();
        m_dmrTracer?.StopEffect();
    }

    private Vector3 GetRootRestPosition(WeaponId weapon)
    {
        int index = (int)weapon - 1;
        return index >= 0 && index < m_rootRestPositions.Length ? m_rootRestPositions[index] : Vector3.zero;
    }

    private Quaternion GetRootRestRotation(WeaponId weapon)
    {
        int index = (int)weapon - 1;
        return index >= 0 && index < m_rootRestRotations.Length ? m_rootRestRotations[index] : Quaternion.identity;
    }

    private float GetMovementScale()
    {
        int index = (int)m_activeWeapon - 1;
        return index >= 0 && index < s_MovementScales.Length ? s_MovementScales[index] : 0f;
    }

    private void RestoreRootTransform(GameObject root, int slot)
    {
        if (root != null)
        {
            WeaponId weapon = (WeaponId)slot;
            root.transform.localPosition = GetRootRestPosition(weapon);
            root.transform.localRotation = GetRootRestRotation(weapon);
        }
    }

    private static Vector3 GetLocalPosition(GameObject root)
    {
        return root != null ? root.transform.localPosition : Vector3.zero;
    }

    private static Quaternion GetLocalRotation(GameObject root)
    {
        return root != null ? root.transform.localRotation : Quaternion.identity;
    }

    private static void SetActive(GameObject root, bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }

}
