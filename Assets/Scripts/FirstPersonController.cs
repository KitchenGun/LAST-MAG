using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [SerializeField] private InputActionAsset m_inputActions;
    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private float m_moveSpeed = 6f;
    [SerializeField] private float m_jumpHeight = 1.2f;
    [SerializeField] private float m_gravity = -20f;
    [SerializeField] private float m_lookSensitivity = 0.1f;
    [SerializeField] private int m_magazineAmmo = 12;
    [SerializeField] private int m_reserveAmmo = 60;
    [SerializeField] private float m_reloadDuration = 1.2f;

    private const int k_MagazineCapacity = 12;
    private const float k_RaycastDistance = 100f;
    private const float k_MaxPitch = 80f;

    private CharacterController m_characterController;
    private InputActionMap m_playerMap;
    private InputAction m_moveAction;
    private InputAction m_lookAction;
    private InputAction m_attackAction;
    private InputAction m_jumpAction;
    private InputAction m_reloadAction;
    private float m_verticalVelocity;
    private float m_pitch;
    private bool m_isReloading;

    private void Awake()
    {
        m_characterController = GetComponent<CharacterController>();
        Debug.Assert(m_characterController != null);
    }

    private void OnEnable()
    {
        if (!InitializeInput())
        {
            return;
        }

        m_attackAction.performed += OnAttack;
        m_jumpAction.performed += OnJump;
        m_reloadAction.performed += OnReload;
        m_playerMap.Enable();
    }

    private bool InitializeInput()
    {
        if (m_playerMap != null)
        {
            return true;
        }

        Debug.Assert(m_inputActions != null && m_playerCamera != null);
        Debug.Assert(k_MagazineCapacity > 0 && m_magazineAmmo >= 0 && m_reserveAmmo >= 0);
        if (m_inputActions == null || m_playerCamera == null)
        {
            return false;
        }

        m_playerMap = m_inputActions.FindActionMap("Player", true);
        m_moveAction = m_playerMap.FindAction("Move", true);
        m_lookAction = m_playerMap.FindAction("Look", true);
        m_attackAction = m_playerMap.FindAction("Attack", true);
        m_jumpAction = m_playerMap.FindAction("Jump", true);
        m_reloadAction = m_playerMap.FindAction("Reload", true);
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
        m_reloadAction.performed -= OnReload;
        m_playerMap.Disable();
        UnlockCursor();
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

        if (m_isReloading || m_magazineAmmo == 0)
        {
            return;
        }

        m_magazineAmmo--;
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

    private void OnReload(InputAction.CallbackContext context)
    {
        if (Cursor.lockState == CursorLockMode.Locked && !m_isReloading && m_magazineAmmo < k_MagazineCapacity && m_reserveAmmo > 0)
        {
            StartCoroutine(Reload());
        }
    }

    private IEnumerator Reload()
    {
        m_isReloading = true;
        yield return new WaitForSeconds(m_reloadDuration);
        int reloadAmount = Mathf.Min(k_MagazineCapacity - m_magazineAmmo, m_reserveAmmo);
        m_magazineAmmo += reloadAmount;
        m_reserveAmmo -= reloadAmount;
        m_isReloading = false;
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
        m_magazineAmmo = Mathf.Clamp(m_magazineAmmo, 0, k_MagazineCapacity);
        m_reserveAmmo = Mathf.Max(0, m_reserveAmmo);
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
        m_jumpHeight = Mathf.Max(0f, m_jumpHeight);
        m_reloadDuration = Mathf.Max(0f, m_reloadDuration);
    }
}
