using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private GameplayHUD m_gameplayHUD;
    [SerializeField] private WeaponViewmodelController m_weaponViewmodel;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [FormerlySerializedAs("m_reserveAmmo")]
    [SerializeField] private int m_ammo = 60;
    [SerializeField] private int m_maxAmmo = 120;

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
        m_gameplayHUD?.RefreshWeapon(1, "PISTOL", m_ammo, true);
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
        Debug.Assert(m_maxAmmo > 0 && m_ammo >= 0 && m_ammo <= m_maxAmmo);
        if (m_inputActions == null || m_playerCamera == null)
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

        if (m_playerMap == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        HandleLook();
        HandleMovement();
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

        if (m_ammo == 0)
        {
            m_gameplayHUD?.ShowEmptyAmmoFeedback();
            return;
        }

        m_ammo--;
        m_gameplayHUD?.RefreshWeapon(1, "PISTOL", m_ammo, true);
        m_weaponViewmodel?.PlayFireAnimation();
        Ray ray = new Ray(m_playerCamera.transform.position, m_playerCamera.transform.forward);
        float rayLength = k_RaycastDistance;
        if (Physics.Raycast(ray, out RaycastHit hit, k_RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            rayLength = hit.distance;
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
        m_maxAmmo = Mathf.Max(1, m_maxAmmo);
        m_ammo = Mathf.Clamp(m_ammo, 0, m_maxAmmo);
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_jumpHeight = Mathf.Max(0f, m_jumpHeight);
    }
}
