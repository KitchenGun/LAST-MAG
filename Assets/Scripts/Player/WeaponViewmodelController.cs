using UnityEngine;

public sealed class WeaponViewmodelController : MonoBehaviour
{
    private const float k_ReferenceMoveSpeed = 6f;
    private const float k_MaxAccumulatedModelRecoil = 1.5f;
    private const float k_PositionSpringFrequency = 32f;
    private const float k_PositionSpringDamping = 0.7f;
    private const float k_RotationSpringFrequency = 24f;
    private const float k_RotationSpringDamping = 0.65f;
    private const float k_SpringImpulseScale = 2f;
    private const float k_MaxSpringStep = 1f / 120f;
    private const int k_MaxSpringStepsPerFrame = 8;
    private const int k_DmrVariantCount = 6;
    private static readonly float[] s_MovementScales = { 1f, 0.65f, 0.8f, 0.75f };
    private static readonly float[] s_RecoilDistances = { 0.065f, 0.18f, 0.06f, 0.12f };
    private static readonly float[] s_RecoilLateralDistances = { 0.018f, 0.04f, 0.013f, 0.027f };
    private static readonly float[] s_RecoilPitches = { 5f, 11f, 4f, 6.5f };
    private static readonly float[] s_RecoilYaws = { 0.8f, 2f, 0.9f, 1.2f };
    private static readonly float[] s_RecoilRolls = { 1.2f, 2.5f, 1.3f, 1.6f };
    private static readonly float[] s_DmrPitchScales = { 0.97f, 1f, 1.03f };
    private static readonly float[] s_DmrVolumeScales = { 0.98f, 1f, 0.97f };

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
    [SerializeField] private AudioSource[] m_weaponAudioSources;
    [SerializeField] private AudioClip[] m_pistolFireClips;
    [SerializeField] private AudioClip[] m_shotgunFireClips;
    [SerializeField] private AudioClip[] m_rifleFireClips;
    [SerializeField] private AudioClip[] m_dmrFireClips;
    [SerializeField] private AudioClip[] m_emptyAmmoClips;
    [SerializeField] private FootstepAudio m_playerFootsteps;
    [SerializeField] private float[] m_weaponFireVolumes = { 1f, 1f, 1f, 1f };
    [SerializeField] private float m_emptyAmmoVolume = 0.7f;
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
    private readonly int[] m_dmrVariantBag = new int[k_DmrVariantCount];
    private CharacterController m_characterController;
    private Vector3 m_movementPositionOffset;
    private Vector3 m_movementRotationOffset;
    private float m_bobPhase;
    private Vector3 m_firePositionOffset;
    private Vector3 m_firePositionVelocity;
    private Vector3 m_fireRotationOffset;
    private Vector3 m_fireRotationVelocity;
    private WeaponId m_activeWeapon;
    private int m_lastEmptyAmmoClipIndex = -1;
    private int m_nextWeaponAudioSourceIndex;
    private int m_dmrVariantBagIndex = k_DmrVariantCount;
    private int m_lastDmrVariant = -1;
    private int m_lastDmrClipIndex = -1;
    private int m_dmrSameClipCount;
    private bool m_waitingForLanding;

    private void Awake()
    {
        m_characterController = GetComponentInParent<CharacterController>();
        ConfigureAudioSource();
        Debug.Assert(IsFireAudioConfigurationValid(), "Each weapon needs 2 or 3 assigned fire clips.");
        Debug.Assert(m_characterController != null && m_dmrRoot != null && HasTwoWeaponAudioSources(),
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

    internal void PlayFireFeedback(RecoilSample recoil)
    {
        Transform root = GetActiveRoot();
        if (root == null)
        {
            return;
        }

        if (m_activeWeapon == WeaponId.DMR)
        {
            PlayDmrFireClip();
        }
        else
        {
            PlayRandomClip(GetActiveFireClips(), ref m_lastFireClipIndices[(int)m_activeWeapon - 1],
                GetActiveFireVolume());
        }
        GetActiveMuzzleFlash()?.Play();
        StartFireRecoil(recoil);
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
        SelectWeapon(WeaponId.Pistol);
        SelectWeapon(WeaponId.Unknown);
        Debug.Assert(m_activeWeapon == WeaponId.Pistol);
        SelectWeapon(WeaponId.Shotgun);
        Debug.Assert(m_activeWeapon == WeaponId.Shotgun);
        Debug.Assert(m_weaponFireVolumes != null && m_weaponFireVolumes.Length == 4);
        Debug.Assert(IsFireAudioConfigurationValid());
        Debug.Assert(s_DmrPitchScales.Length == 3 && s_DmrVolumeScales.Length == 3);
        Debug.Assert(IsDmrVariantOrderValid(new[] { 3, 0, 4, 1, 2, 5 }, 0, 0, 2));
        Debug.Assert(!IsDmrVariantOrderValid(new[] { 0, 3, 4, 1, 2, 5 }, 0, 0, 1));
        Debug.Assert(!IsDmrVariantOrderValid(new[] { 1, 2, 0, 3, 4, 5 }, 0, 0, 2));
        Debug.Assert(Mathf.Approximately(s_RecoilDistances[0], 0.065f)
            && Mathf.Approximately(s_RecoilDistances[1], 0.18f)
            && Mathf.Approximately(s_RecoilDistances[2], 0.06f)
            && Mathf.Approximately(s_RecoilDistances[3], 0.12f));
        Debug.Assert(Mathf.Approximately(s_RecoilLateralDistances[1], 0.04f));
        Debug.Assert(Mathf.Approximately(s_RecoilPitches[2], 4f)
            && Mathf.Approximately(s_RecoilYaws[2], 0.9f)
            && Mathf.Approximately(s_RecoilRolls[2], 1.3f));
        Vector3 springOffset = Vector3.zero;
        Vector3 springVelocity = Vector3.back;
        bool crossedRest = false;
        for (int step = 0; step < 240; step++)
        {
            StepSpring(ref springOffset, ref springVelocity,
                k_RotationSpringFrequency, k_RotationSpringDamping, k_MaxSpringStep);
            crossedRest |= springOffset.z > 0f;
        }
        Debug.Assert(crossedRest && springOffset.sqrMagnitude < 0.000001f);
        Debug.Assert(s_MovementScales[0] > s_MovementScales[2] && s_MovementScales[2] > s_MovementScales[1]);
        Debug.Assert(CrossedPhase(1f, 2f, Mathf.PI * 0.5f));
        Debug.Assert(CrossedPhase(4f, 5f, Mathf.PI * 1.5f));
        Debug.Assert(!CrossedPhase(6f, 0.2f, Mathf.PI * 0.5f));
        RestoreRestPose();
    }

    private bool IsFireAudioConfigurationValid()
    {
        return HasTwoWeaponAudioSources()
            && HasTwoOrThreeClips(m_pistolFireClips)
            && HasTwoOrThreeClips(m_shotgunFireClips)
            && HasTwoOrThreeClips(m_rifleFireClips)
            && HasExactlyTwoClips(m_dmrFireClips);
    }

    private static bool HasExactlyTwoClips(AudioClip[] clips)
    {
        return clips != null && clips.Length == 2 && clips[0] != null && clips[1] != null;
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
        if (!HasTwoWeaponAudioSources())
        {
            Debug.LogError("PF_Player MainCamera needs exactly two weapon AudioSources.");
            return;
        }

        foreach (AudioSource source in m_weaponAudioSources)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }
    }

    private bool HasTwoWeaponAudioSources()
    {
        return m_weaponAudioSources != null && m_weaponAudioSources.Length == 2
            && m_weaponAudioSources[0] != null && m_weaponAudioSources[1] != null;
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
        if (!HasTwoWeaponAudioSources() || clips == null || clips.Length == 0)
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
        PlayClip(clips[selectedIndex], volume, UnityEngine.Random.Range(0.98f, 1.02f));
    }

    private void PlayDmrFireClip()
    {
        if (!HasTwoWeaponAudioSources() || !HasExactlyTwoClips(m_dmrFireClips))
        {
            return;
        }

        if (m_dmrVariantBagIndex >= k_DmrVariantCount)
        {
            RefillDmrVariantBag();
        }

        int variant = m_dmrVariantBag[m_dmrVariantBagIndex++];
        int clipIndex = variant / s_DmrPitchScales.Length;
        int scaleIndex = variant % s_DmrPitchScales.Length;
        m_dmrSameClipCount = clipIndex == m_lastDmrClipIndex ? m_dmrSameClipCount + 1 : 1;
        m_lastDmrClipIndex = clipIndex;
        m_lastDmrVariant = variant;
        PlayClip(m_dmrFireClips[clipIndex],
            GetActiveFireVolume() * s_DmrVolumeScales[scaleIndex], s_DmrPitchScales[scaleIndex]);
    }

    private void RefillDmrVariantBag()
    {
        do
        {
            for (int index = 0; index < k_DmrVariantCount; index++)
            {
                m_dmrVariantBag[index] = index;
            }
            for (int index = k_DmrVariantCount - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                (m_dmrVariantBag[index], m_dmrVariantBag[swapIndex]) =
                    (m_dmrVariantBag[swapIndex], m_dmrVariantBag[index]);
            }
        }
        while (!IsDmrVariantOrderValid(m_dmrVariantBag,
            m_lastDmrVariant, m_lastDmrClipIndex, m_dmrSameClipCount));

        m_dmrVariantBagIndex = 0;
    }

    private static bool IsDmrVariantOrderValid(int[] variants, int lastVariant, int lastClip, int sameClipCount)
    {
        for (int index = 0; index < variants.Length; index++)
        {
            int variant = variants[index];
            int clipIndex = variant / s_DmrPitchScales.Length;
            if (variant == lastVariant || (clipIndex == lastClip && sameClipCount >= 2))
            {
                return false;
            }

            sameClipCount = clipIndex == lastClip ? sameClipCount + 1 : 1;
            lastVariant = variant;
            lastClip = clipIndex;
        }
        return true;
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        AudioSource source = m_weaponAudioSources[m_nextWeaponAudioSourceIndex];
        m_nextWeaponAudioSourceIndex = (m_nextWeaponAudioSourceIndex + 1) % m_weaponAudioSources.Length;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = pitch;
        source.Play();
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
        m_strafeSwayDistance = Mathf.Max(0f, m_strafeSwayDistance);
        m_forwardSwayDistance = Mathf.Max(0f, m_forwardSwayDistance);
        m_strafeSwayRoll = Mathf.Max(0f, m_strafeSwayRoll);
        m_forwardSwayPitch = Mathf.Max(0f, m_forwardSwayPitch);
        m_bobHorizontalDistance = Mathf.Max(0f, m_bobHorizontalDistance);
        m_bobVerticalDistance = Mathf.Max(0f, m_bobVerticalDistance);
        m_bobFrequency = Mathf.Max(0f, m_bobFrequency);
        m_movementLerpSpeed = Mathf.Max(0.1f, m_movementLerpSpeed);
        if (m_weaponAudioSources != null)
        {
            foreach (AudioSource source in m_weaponAudioSources)
            {
                if (source == null)
                {
                    continue;
                }
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
            }
        }
    }

    private void UpdateFireAnimation()
    {
        StepSpring(ref m_firePositionOffset, ref m_firePositionVelocity,
            k_PositionSpringFrequency, k_PositionSpringDamping, Time.deltaTime);
        StepSpring(ref m_fireRotationOffset, ref m_fireRotationVelocity,
            k_RotationSpringFrequency, k_RotationSpringDamping, Time.deltaTime);
        ClampFireSpring();

        if (m_firePositionOffset.sqrMagnitude + m_firePositionVelocity.sqrMagnitude
            + m_fireRotationOffset.sqrMagnitude + m_fireRotationVelocity.sqrMagnitude < 0.000001f)
        {
            StopFireAnimation();
        }
    }

    private void StartFireRecoil(RecoilSample recoil)
    {
        float horizontal = recoil.HorizontalScale * recoil.HorizontalDirection;
        Vector3 positionImpulse = new(
            GetWeaponValue(s_RecoilLateralDistances) * horizontal,
            0f,
            -GetWeaponValue(s_RecoilDistances) * recoil.VerticalScale);
        Vector3 rotationImpulse = new(
            -GetWeaponValue(s_RecoilPitches) * recoil.VerticalScale,
            GetWeaponValue(s_RecoilYaws) * horizontal,
            GetWeaponValue(s_RecoilRolls) * horizontal);
        m_firePositionVelocity += positionImpulse * (k_PositionSpringFrequency * k_SpringImpulseScale);
        m_fireRotationVelocity += rotationImpulse * (k_RotationSpringFrequency * k_SpringImpulseScale);
        ClampFireSpring();
    }

    internal static void StepSpring(ref Vector3 offset, ref Vector3 velocity,
        float frequency, float damping, float deltaTime)
    {
        float remaining = Mathf.Min(Mathf.Max(0f, deltaTime), k_MaxSpringStep * k_MaxSpringStepsPerFrame);
        while (remaining > 0f)
        {
            float step = Mathf.Min(k_MaxSpringStep, remaining);
            velocity += (-frequency * frequency * offset - 2f * damping * frequency * velocity) * step;
            offset += velocity * step;
            remaining -= step;
        }
    }

    private void ClampFireSpring()
    {
        ClampSpringAxis(ref m_firePositionOffset.x, ref m_firePositionVelocity.x,
            GetWeaponValue(s_RecoilLateralDistances) * k_MaxAccumulatedModelRecoil);
        ClampSpringAxis(ref m_firePositionOffset.z, ref m_firePositionVelocity.z,
            GetWeaponValue(s_RecoilDistances) * k_MaxAccumulatedModelRecoil);
        ClampSpringAxis(ref m_fireRotationOffset.x, ref m_fireRotationVelocity.x,
            GetWeaponValue(s_RecoilPitches) * k_MaxAccumulatedModelRecoil);
        ClampSpringAxis(ref m_fireRotationOffset.y, ref m_fireRotationVelocity.y,
            GetWeaponValue(s_RecoilYaws) * k_MaxAccumulatedModelRecoil);
        ClampSpringAxis(ref m_fireRotationOffset.z, ref m_fireRotationVelocity.z,
            GetWeaponValue(s_RecoilRolls) * k_MaxAccumulatedModelRecoil);
    }

    internal static void ClampSpringAxis(ref float offset, ref float velocity, float limit)
    {
        float clamped = Mathf.Clamp(offset, -limit, limit);
        if (!Mathf.Approximately(clamped, offset) && Mathf.Sign(velocity) == Mathf.Sign(offset))
        {
            velocity = 0f;
        }
        offset = clamped;
        velocity = Mathf.Clamp(velocity,
            -limit * k_PositionSpringFrequency * k_SpringImpulseScale,
            limit * k_PositionSpringFrequency * k_SpringImpulseScale);
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
            + m_firePositionOffset;
        Vector3 rotationOffset = m_movementRotationOffset + m_fireRotationOffset;
        root.localRotation = Quaternion.Euler(rotationOffset) * GetRootRestRotation(m_activeWeapon);
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
        m_firePositionOffset = Vector3.zero;
        m_firePositionVelocity = Vector3.zero;
        m_fireRotationOffset = Vector3.zero;
        m_fireRotationVelocity = Vector3.zero;
    }

    internal void ResetRecoil()
    {
        StopFireAnimation();
        RestoreRestPose();
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

    private float GetWeaponValue(float[] values)
    {
        int index = (int)m_activeWeapon - 1;
        return index >= 0 && index < values.Length ? values[index] : 0f;
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
