using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class WeaponViewmodelController : MonoBehaviour
{
    [SerializeField] private GameObject m_pistolRoot;
    [SerializeField] private GameObject m_shotgunRoot;
    [SerializeField] private GameObject m_rifleRoot;
    [SerializeField] private float m_recoilDistance = 0.06f;
    [SerializeField] private float m_recoilDuration = 0.1f;

    private readonly Vector3[] m_rootRestPositions = new Vector3[3];
    private Coroutine m_fireAnimation;
    private int m_activeSlot;

    private void Awake()
    {
        CacheRestPositions();
        SelectSlot(1);
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectSlot(1);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectSlot(2);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectSlot(3);
        }
    }

    private void OnDisable()
    {
        StopFireAnimation();

        if (Application.isPlaying)
        {
            RestoreRestPose();
        }
    }

    public void SelectSlot(int slot)
    {
        if (slot < 1 || slot > 3)
        {
            return;
        }

        StopFireAnimation();
        RestoreRestPose();
        m_activeSlot = slot;
        SetActive(m_pistolRoot, slot == 1);
        SetActive(m_shotgunRoot, slot == 2);
        SetActive(m_rifleRoot, slot == 3);
    }

    public void PlayFireAnimation()
    {
        Transform root = GetActiveRoot();
        if (root == null)
        {
            return;
        }

        StopFireAnimation();
        RestoreRestPose();
        m_fireAnimation = StartCoroutine(PlayFireAnimationRoutine(root));
    }

    [ContextMenu("Run Viewmodel Self Check")]
    private void RunViewmodelSelfCheck()
    {
        SelectSlot(0);
        Debug.Assert(m_activeSlot >= 1 && m_activeSlot <= 3);
        SelectSlot(2);
        Debug.Assert(m_activeSlot == 2);
        RestoreRestPose();
    }

    private IEnumerator PlayFireAnimationRoutine(Transform root)
    {
        Vector3 rootRest = GetRootRestPosition(m_activeSlot);
        float halfDuration = m_recoilDuration * 0.5f;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            ApplyAnimationPose(root, rootRest, Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            ApplyAnimationPose(root, rootRest, 1f - Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        root.localPosition = rootRest;
        m_fireAnimation = null;
    }

    private void ApplyAnimationPose(Transform root, Vector3 rootRest, float amount)
    {
        root.localPosition = rootRest + Vector3.back * (m_recoilDistance * amount);
    }

    private void CacheRestPositions()
    {
        m_rootRestPositions[0] = GetLocalPosition(m_pistolRoot);
        m_rootRestPositions[1] = GetLocalPosition(m_shotgunRoot);
        m_rootRestPositions[2] = GetLocalPosition(m_rifleRoot);
    }

    private void RestoreRestPose()
    {
        RestoreRootPosition(m_pistolRoot, 1);
        RestoreRootPosition(m_shotgunRoot, 2);
        RestoreRootPosition(m_rifleRoot, 3);
    }

    private void StopFireAnimation()
    {
        if (m_fireAnimation == null)
        {
            return;
        }

        StopCoroutine(m_fireAnimation);
        m_fireAnimation = null;
    }

    private Transform GetActiveRoot()
    {
        return m_activeSlot switch
        {
            1 => m_pistolRoot != null ? m_pistolRoot.transform : null,
            2 => m_shotgunRoot != null ? m_shotgunRoot.transform : null,
            3 => m_rifleRoot != null ? m_rifleRoot.transform : null,
            _ => null
        };
    }

    private Vector3 GetRootRestPosition(int slot)
    {
        return slot >= 1 && slot <= 3 ? m_rootRestPositions[slot - 1] : Vector3.zero;
    }

    private void RestoreRootPosition(GameObject root, int slot)
    {
        if (root != null)
        {
            root.transform.localPosition = GetRootRestPosition(slot);
        }
    }

    private static Vector3 GetLocalPosition(GameObject root)
    {
        return root != null ? root.transform.localPosition : Vector3.zero;
    }

    private static void SetActive(GameObject root, bool isActive)
    {
        if (root != null)
        {
            root.SetActive(isActive);
        }
    }
}
