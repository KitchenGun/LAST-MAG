using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    private const int k_WeaponSlotCount = 3;
    private const int k_AmmoPerShot = 1;
    private const int k_ShotgunPelletCount = 8;
    private const float k_ShotgunSpreadAngle = 8f;
    private static readonly string[] k_WeaponNames = { "PISTOL", "SHOTGUN", "RIFLE" };
    private static readonly float[] k_WeaponDamage = { 30f, 12f, 15f };
    private static readonly float[] k_WeaponFireIntervals = { 1f / 6.75f, 1f / 1.1f, 1f / 11f };
    private static readonly float[] k_WeaponCameraRecoil = { 0.65f, 1.5f, 0.65f };
    private static readonly float[] k_WeaponCameraHorizontalRecoil = { 0.35f, 0f, 0.15f };
    private static readonly float[] k_WeaponHorizontalSpread = { 0.35f, 0f, 0.75f };

    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private GameplayHUD m_gameplayHUD;
    [SerializeField] private WeaponViewmodelController m_weaponViewmodel;
    [SerializeField] private ImpactSparkEmitter m_impactSparkEmitter;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [SerializeField] private float m_recoilKickSpeed = 18f;
    [SerializeField] private float m_recoilReturnSpeed = 10f;
    [SerializeField] private float m_maxAccumulatedRecoil = 4f;
    [SerializeField] private int[] m_weaponAmmo = { 15, 6, 30 };
    [SerializeField] private int[] m_maxWeaponAmmo = { 15, 6, 30 };
    [Header("World Audio")]
    [SerializeField] private AudioClip m_wallImpactClip;
    [SerializeField] private float m_wallImpactMaxDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float m_wallImpactVolume = 0.7f;

    private const float k_RaycastDistance = 100f;
    private const float k_MaxPitch = 80f;

    private CharacterController m_characterController;
    private InputActionAsset m_runtimeInputActions;
    private InputActionMap m_playerMap;
    private InputAction m_moveAction;
    private InputAction m_lookAction;
    private InputAction m_attackAction;
    private InputAction m_jumpAction;
    private float m_verticalVelocity;
    private float m_pitch;
    private float m_cameraRecoilPitch;
    private float m_cameraRecoilTargetPitch;
    private float m_cameraRecoilYaw;
    private float m_cameraRecoilTargetYaw;
    private float m_nextAllowedFireTime;
    private int m_activeWeaponSlot = 1;
    private bool m_isRifleFiring;
    private ScoreSystem m_scoreSystem;

    public int ActiveWeaponSlot => m_activeWeaponSlot;

    private void Awake()
    {
        m_characterController = GetComponent<CharacterController>();
        Debug.Assert(m_characterController != null);
        Debug.Assert(Mathf.Approximately(k_WeaponCameraRecoil[0], k_WeaponCameraRecoil[2])
            && k_WeaponCameraRecoil[1] > k_WeaponCameraRecoil[0]);
        if (GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
        }
        m_scoreSystem = FindFirstObjectByType<ScoreSystem>();
        if (m_scoreSystem == null)
        {
            m_scoreSystem = gameObject.AddComponent<ScoreSystem>();
        }
    }

    private void Start()
    {
        Debug.Assert(m_gameplayHUD != null);
        Debug.Assert(IsAmmoConfigurationValid());
        if (!IsAmmoConfigurationValid())
        {
            return;
        }

        m_scoreSystem.Initialize(m_gameplayHUD);
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

        Debug.Assert(m_inputActions != null && m_playerCamera != null);
        Debug.Assert(IsAmmoConfigurationValid());
        if (m_inputActions == null || m_playerCamera == null || !IsAmmoConfigurationValid())
        {
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
        if (m_playerMap == null)
        {
            return;
        }

        m_attackAction.performed -= OnAttack;
        m_attackAction.canceled -= OnAttackCanceled;
        m_jumpAction.performed -= OnJump;
        m_playerMap.Disable();
        UnlockCursor();
    }

    private void OnDestroy()
    {
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

        HandleLook();
        HandleMovement();
        HandleAutomaticFire();
    }

    private void HandleWeaponSelection()
    {
        if (Keyboard.current == null)
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
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectWeapon(3);
        }
    }

    private void SelectWeapon(int slot)
    {
        Debug.Assert(slot >= 1 && slot <= k_WeaponSlotCount);
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        m_isRifleFiring = false;
        m_activeWeaponSlot = slot;
        m_weaponViewmodel?.SelectSlot(slot);
        RefreshWeaponHud();
    }

    private void RefreshWeaponHud()
    {
        for (int index = 0; index < k_WeaponSlotCount; index++)
        {
            m_gameplayHUD?.RefreshWeapon(index + 1, k_WeaponNames[index], m_weaponAmmo[index], index + 1 == m_activeWeaponSlot);
        }
    }

    public bool TryAddAmmo(int slot, int amount)
    {
        if (slot < 1 || slot > k_WeaponSlotCount || amount <= 0 || !IsAmmoConfigurationValid())
        {
            return false;
        }

        int index = slot - 1;
        int addedAmount = Mathf.Min(amount, m_maxWeaponAmmo[index] - m_weaponAmmo[index]);
        if (addedAmount <= 0)
        {
            return false;
        }

        m_weaponAmmo[index] += addedAmount;
        m_gameplayHUD?.RefreshWeapon(slot, k_WeaponNames[index], m_weaponAmmo[index], slot == m_activeWeaponSlot);
        m_gameplayHUD?.ShowAmmoPickup(slot, addedAmount);
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
        if (m_cameraRecoilTargetPitch > 0f)
        {
            m_cameraRecoilPitch = Mathf.Lerp(
                m_cameraRecoilPitch,
                m_cameraRecoilTargetPitch,
                Mathf.Clamp01(m_recoilKickSpeed * Time.deltaTime));
            m_cameraRecoilYaw = Mathf.Lerp(
                m_cameraRecoilYaw,
                m_cameraRecoilTargetYaw,
                Mathf.Clamp01(m_recoilKickSpeed * Time.deltaTime));

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

        m_cameraRecoilPitch = Mathf.Lerp(
            m_cameraRecoilPitch,
            0f,
            Mathf.Clamp01(m_recoilReturnSpeed * Time.deltaTime));
        m_cameraRecoilYaw = Mathf.Lerp(
            m_cameraRecoilYaw,
            0f,
            Mathf.Clamp01(m_recoilReturnSpeed * Time.deltaTime));
    }

    private void HandleMovement()
    {
        if (m_characterController.isGrounded && m_verticalVelocity < 0f)
        {
            m_verticalVelocity = -2f;
        }

        m_verticalVelocity += m_gravity * Time.deltaTime;
        Vector2 move = m_moveAction.ReadValue<Vector2>();
        Vector3 velocity = transform.TransformDirection(new Vector3(move.x, 0f, move.y)) * m_moveSpeed;
        velocity.y = m_verticalVelocity;
        m_characterController.Move(velocity * Time.deltaTime);
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
            return;
        }

        m_isRifleFiring = m_activeWeaponSlot == 3;
        TryFireCurrentWeapon();
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        m_isRifleFiring = false;
    }

    private void HandleAutomaticFire()
    {
        if (!m_isRifleFiring || m_activeWeaponSlot != 3 || m_attackAction == null || !m_attackAction.IsPressed())
        {
            m_isRifleFiring = false;
            return;
        }

        TryFireCurrentWeapon();
    }

    private bool TryFireCurrentWeapon()
    {
        int activeWeaponIndex = m_activeWeaponSlot - 1;
        if (m_weaponAmmo[activeWeaponIndex] == 0)
        {
            m_gameplayHUD?.ShowEmptyAmmoFeedback();
            m_gameplayHUD?.ShowEmptyAmmoPopup(m_activeWeaponSlot);
            m_weaponViewmodel?.PlayEmptyAmmoFeedback();
            m_isRifleFiring = false;
            return false;
        }

        if (Time.time < m_nextAllowedFireTime)
        {
            return false;
        }

        m_nextAllowedFireTime = Time.time + k_WeaponFireIntervals[activeWeaponIndex];
        m_weaponAmmo[activeWeaponIndex] -= k_AmmoPerShot;
        m_gameplayHUD?.RefreshWeapon(m_activeWeaponSlot, k_WeaponNames[activeWeaponIndex], m_weaponAmmo[activeWeaponIndex], true);
        m_weaponViewmodel?.PlayFireFeedback();
        m_cameraRecoilTargetPitch = Mathf.Min(
            Mathf.Max(m_cameraRecoilPitch, m_cameraRecoilTargetPitch) + k_WeaponCameraRecoil[activeWeaponIndex],
            m_maxAccumulatedRecoil);
        float horizontalRecoil = k_WeaponCameraHorizontalRecoil[activeWeaponIndex];
        m_cameraRecoilTargetYaw = Random.Range(-horizontalRecoil, horizontalRecoil);

        if (m_activeWeaponSlot == 2)
        {
            FireShotgun();
        }
        else
        {
            FireSingleRay(activeWeaponIndex);
        }

        return true;
    }

    private void FireSingleRay(int activeWeaponIndex)
    {
        Ray ray = new Ray(m_playerCamera.transform.position, CreateSingleRayDirection(activeWeaponIndex));
        float rayLength = k_RaycastDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            rayLength = hit.distance;
            m_impactSparkEmitter?.EmitAt(hit.point, hit.normal);
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                bool isHeadshot = enemy.IsHeadHit(hit.collider);
                if (enemy.ApplyDamage(k_WeaponDamage[activeWeaponIndex], KillContext.Direct(m_activeWeaponSlot, isHeadshot)))
                {
                    m_scoreSystem.RegisterDirectKill(enemy.Type, m_activeWeaponSlot, isHeadshot);
                }
            }
            else
            {
                SpatialAudio.PlayOneShot(m_wallImpactClip, hit.point, m_wallImpactMaxDistance, m_wallImpactVolume);
            }
        }

        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.red, 0.1f);
    }

    private void FireShotgun()
    {
        Dictionary<EnemyHealth, float> damageByEnemy = new();
        HashSet<EnemyHealth> headshotEnemies = new();
        bool playedWallImpact = false;

        for (int pellet = 0; pellet < k_ShotgunPelletCount; pellet++)
        {
            Ray ray = new Ray(m_playerCamera.transform.position, CreateShotgunDirection());
            float rayLength = k_RaycastDistance;
            if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                rayLength = hit.distance;
                m_impactSparkEmitter?.EmitAt(hit.point, hit.normal);
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    damageByEnemy.TryGetValue(enemy, out float damage);
                    damageByEnemy[enemy] = damage + k_WeaponDamage[1];
                    if (enemy.IsHeadHit(hit.collider))
                    {
                        headshotEnemies.Add(enemy);
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

        foreach (KeyValuePair<EnemyHealth, float> hit in damageByEnemy)
        {
            bool isHeadshot = headshotEnemies.Contains(hit.Key);
            if (hit.Key.ApplyDamage(hit.Value, KillContext.Direct(m_activeWeaponSlot, isHeadshot)))
            {
                m_scoreSystem.RegisterDirectKill(hit.Key.Type, m_activeWeaponSlot, isHeadshot);
            }
        }
    }

    private Vector3 CreateShotgunDirection()
    {
        Vector2 spread = Random.insideUnitCircle * Mathf.Tan(k_ShotgunSpreadAngle * Mathf.Deg2Rad);
        Transform cameraTransform = m_playerCamera.transform;
        return (cameraTransform.forward + cameraTransform.right * spread.x + cameraTransform.up * spread.y).normalized;
    }

    private Vector3 CreateSingleRayDirection(int weaponIndex)
    {
        float spread = Random.Range(-k_WeaponHorizontalSpread[weaponIndex], k_WeaponHorizontalSpread[weaponIndex]);
        Transform cameraTransform = m_playerCamera.transform;
        return Quaternion.AngleAxis(spread, cameraTransform.up) * cameraTransform.forward;
    }

    [ContextMenu("Run Weapon Fire Self Check")]
    private void RunWeaponFireSelfCheck()
    {
        Debug.Assert(Application.isPlaying, "Run this check in Play Mode.");
        Debug.Assert(k_AmmoPerShot == 1);
        Debug.Assert(k_ShotgunPelletCount == 8 && Mathf.Approximately(k_ShotgunSpreadAngle, 8f));
        Debug.Assert(Mathf.Approximately(60f / k_WeaponFireIntervals[0], 405f));
        Debug.Assert(Mathf.Approximately(60f / k_WeaponFireIntervals[1], 66f));
        Debug.Assert(Mathf.Approximately(60f / k_WeaponFireIntervals[2], 660f));
        Debug.Assert(Mathf.Approximately(k_WeaponCameraHorizontalRecoil[0], 0.35f)
            && Mathf.Approximately(k_WeaponCameraHorizontalRecoil[2], 0.15f));

        Random.State randomState = Random.state;
        for (int pellet = 0; pellet < 64; pellet++)
        {
            Debug.Assert(Vector3.Angle(m_playerCamera.transform.forward, CreateShotgunDirection()) <= k_ShotgunSpreadAngle + 0.01f);
        }
        for (int weaponIndex = 0; weaponIndex < k_WeaponSlotCount; weaponIndex += 2)
        {
            for (int shot = 0; shot < 64; shot++)
            {
                Debug.Assert(Vector3.Angle(m_playerCamera.transform.forward, CreateSingleRayDirection(weaponIndex))
                    <= k_WeaponHorizontalSpread[weaponIndex] + 0.01f);
            }
        }
        Random.state = randomState;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnValidate()
    {
        if (m_weaponAmmo != null && m_maxWeaponAmmo != null && m_weaponAmmo.Length == k_WeaponSlotCount && m_maxWeaponAmmo.Length == k_WeaponSlotCount)
        {
            for (int index = 0; index < k_WeaponSlotCount; index++)
            {
                m_maxWeaponAmmo[index] = Mathf.Max(1, m_maxWeaponAmmo[index]);
                m_weaponAmmo[index] = Mathf.Clamp(m_weaponAmmo[index], 0, m_maxWeaponAmmo[index]);
            }
        }

        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_jumpHeight = Mathf.Max(0f, m_jumpHeight);
        m_recoilKickSpeed = Mathf.Max(0.1f, m_recoilKickSpeed);
        m_recoilReturnSpeed = Mathf.Max(0.1f, m_recoilReturnSpeed);
        m_maxAccumulatedRecoil = Mathf.Max(k_WeaponCameraRecoil[1], m_maxAccumulatedRecoil);
        m_wallImpactMaxDistance = Mathf.Max(0.1f, m_wallImpactMaxDistance);
    }

    private bool IsAmmoConfigurationValid()
    {
        if (m_weaponAmmo == null || m_maxWeaponAmmo == null || m_weaponAmmo.Length != k_WeaponSlotCount || m_maxWeaponAmmo.Length != k_WeaponSlotCount)
        {
            return false;
        }

        for (int index = 0; index < k_WeaponSlotCount; index++)
        {
            if (m_maxWeaponAmmo[index] <= 0 || m_weaponAmmo[index] < 0 || m_weaponAmmo[index] > m_maxWeaponAmmo[index])
            {
                return false;
            }
        }

        return m_activeWeaponSlot >= 1 && m_activeWeaponSlot <= k_WeaponSlotCount;
    }
}
