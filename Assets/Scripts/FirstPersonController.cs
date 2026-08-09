using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    private const int k_WeaponSlotCount = 3;
    private static readonly string[] k_WeaponNames = { "PISTOL", "SHOTGUN", "RIFLE" };
    private static readonly float[] k_WeaponDamage = { 30f, 12f, 15f };

    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private GameplayHUD m_gameplayHUD;
    [SerializeField] private WeaponViewmodelController m_weaponViewmodel;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [SerializeField] private int[] m_weaponAmmo = { 15, 6, 30 };
    [SerializeField] private int[] m_maxWeaponAmmo = { 15, 6, 30 };

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
    private int m_activeWeaponSlot = 1;

    public int ActiveWeaponSlot => m_activeWeaponSlot;

    private void Awake()
    {
        m_characterController = GetComponent<CharacterController>();
        Debug.Assert(m_characterController != null);
        if (GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
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

        SelectWeapon(1);
    }

    private void OnEnable()
    {
        if (!InitializeInput())
        {
            return;
        }

        m_attackAction.performed += OnAttack;
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
        if (m_playerMap == null)
        {
            return;
        }

        m_attackAction.performed -= OnAttack;
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
        m_playerCamera.transform.localRotation = Quaternion.Euler(m_pitch, 0f, 0f);
        transform.Rotate(Vector3.up * look.x);
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

        int activeWeaponIndex = m_activeWeaponSlot - 1;
        if (m_weaponAmmo[activeWeaponIndex] == 0)
        {
            m_gameplayHUD?.ShowEmptyAmmoFeedback();
            return;
        }

        m_weaponAmmo[activeWeaponIndex]--;
        m_gameplayHUD?.RefreshWeapon(m_activeWeaponSlot, k_WeaponNames[activeWeaponIndex], m_weaponAmmo[activeWeaponIndex], true);
        m_weaponViewmodel?.PlayFireAnimation();
        Ray ray = new Ray(m_playerCamera.transform.position, m_playerCamera.transform.forward);
        float rayLength = k_RaycastDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            rayLength = hit.distance;
            MeleeEnemy enemy = hit.collider.GetComponentInParent<MeleeEnemy>();
            if (enemy != null)
            {
                enemy.ApplyDamage(k_WeaponDamage[activeWeaponIndex], m_activeWeaponSlot, enemy.IsHeadHit(hit.collider));
            }
        }

        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.red, 0.1f);
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
