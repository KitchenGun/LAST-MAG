using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyScannerHUD : MonoBehaviour
{
    private const int k_MaxBlips = 128;

    [SerializeField] private Camera m_playerCamera;
    [SerializeField] private GameplayObjectPool m_objectPool;
    [SerializeField] private RectTransform m_plotArea;
    [SerializeField] private Image m_pulseImage;
    [SerializeField] private Image m_blipTemplate;

    [Header("Detection")]
    [SerializeField, Min(1f)] private float m_scanRange = 40f;

    [Header("Pulse")]
    [SerializeField, Min(0.2f)] private float m_pulseInterval = 2f;
    [SerializeField, Range(0.2f, 1.5f)] private float m_pulseDuration = 0.8f;
    [SerializeField, Range(0f, 8f)] private float m_pulseThickness = 4f;
    [SerializeField, Range(0f, 1f)] private float m_pulseAlpha = 0.8f;
    [SerializeField, Min(0f)] private float m_fadeStart = 1.5f;
    [SerializeField, Min(0.01f)] private float m_fadeDuration = 0.5f;

    [Header("Blips")]
    [SerializeField, Range(4f, 32f)] private float m_blipSize = 10f;

    private readonly Vector2[] m_candidatePositions = new Vector2[k_MaxBlips];
    private readonly float[] m_candidateDistances = new float[k_MaxBlips];
    private readonly Image[] m_blips = new Image[k_MaxBlips];
    private int m_candidateCount;
    private float m_cycleStartedAt;
    private Color m_pulseBaseColor;
    private Outline m_pulseOutline;

    private void Awake()
    {
        if (m_playerCamera == null || m_objectPool == null || m_plotArea == null
            || m_pulseImage == null || m_blipTemplate == null)
        {
            Debug.LogError("EnemyScannerHUD requires scene camera, pool, plot, pulse and blip references.", this);
            enabled = false;
            return;
        }

        m_pulseOutline = m_pulseImage.GetComponent<Outline>();
        if (m_pulseOutline == null)
        {
            Debug.LogError("EnemyScannerHUD requires an Outline on the pulse image.", this);
            enabled = false;
            return;
        }

        m_pulseBaseColor = m_pulseImage.color;
        m_pulseBaseColor.a = m_pulseAlpha;
        ApplyPulseAppearance();
        m_blipTemplate.gameObject.SetActive(false);
        ApplyBlipSize(m_blipTemplate);
        for (int index = 0; index < k_MaxBlips; index++)
        {
            Image blip = Instantiate(m_blipTemplate, m_blipTemplate.transform.parent);
            blip.name = $"Blip_{index:000}";
            blip.raycastTarget = false;
            blip.gameObject.SetActive(false);
            ApplyBlipSize(blip);
            m_blips[index] = blip;
        }
    }

    private void Start()
    {
        StartPulse(GameplayClock.Now);
    }

    private void Update()
    {
        float now = GameplayClock.Now;
        if (now - m_cycleStartedAt >= m_pulseInterval)
        {
            StartPulse(now);
        }

        float elapsed = now - m_cycleStartedAt;
        float pulseProgress = Mathf.Clamp01(elapsed / m_pulseDuration);
        float easedPulse = Mathf.SmoothStep(0f, 1f, pulseProgress);
        UpdatePulse(elapsed, easedPulse);
        UpdateBlips(elapsed, easedPulse);
    }

    private void OnDisable()
    {
        if (m_pulseImage != null)
        {
            m_pulseImage.gameObject.SetActive(false);
        }
        for (int index = 0; index < m_blips.Length; index++)
        {
            if (m_blips[index] != null)
            {
                m_blips[index].gameObject.SetActive(false);
            }
        }
    }

    private void StartPulse(float now)
    {
        m_cycleStartedAt = now;
        for (int index = 0; index < m_candidateCount; index++)
        {
            m_blips[index].gameObject.SetActive(false);
        }

        m_candidateCount = 0;
        CaptureEnemies();
        m_pulseImage.gameObject.SetActive(true);
    }

    private void CaptureEnemies()
    {
        Vector3 forward = m_playerCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector2 plotSize = m_plotArea.rect.size;
        float plotRadius = Mathf.Max(0f, Mathf.Min(plotSize.x, plotSize.y) * 0.5f - m_blipSize * 0.5f);
        IReadOnlyList<EnemyHealth> enemies = m_objectPool.ActiveEnemies;

        for (int index = 0; index < enemies.Count; index++)
        {
            EnemyHealth enemy = enemies[index];
            if (enemy == null || enemy.IsDisabled || enemy.IsPooled || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 relative = enemy.transform.position - m_playerCamera.transform.position;
            relative.y = 0f;
            if (!IsWithinRange(relative, m_scanRange))
            {
                continue;
            }

            float normalizedDistance = relative.magnitude / m_scanRange;
            Vector2 position = new(
                Vector3.Dot(relative, right) / m_scanRange * plotRadius,
                Vector3.Dot(relative, forward) / m_scanRange * plotRadius);
            AddNearestCandidate(position, normalizedDistance);
        }
    }

    private void AddNearestCandidate(Vector2 position, float normalizedDistance)
    {
        if (m_candidateCount < k_MaxBlips)
        {
            m_candidatePositions[m_candidateCount] = position;
            m_candidateDistances[m_candidateCount] = normalizedDistance;
            m_candidateCount++;
            return;
        }

        // ponytail: 128 entries is the hard UI ceiling; a linear replacement scan avoids a sort/allocation.
        int farthestIndex = 0;
        for (int index = 1; index < k_MaxBlips; index++)
        {
            if (m_candidateDistances[index] > m_candidateDistances[farthestIndex])
            {
                farthestIndex = index;
            }
        }
        if (normalizedDistance >= m_candidateDistances[farthestIndex])
        {
            return;
        }

        m_candidatePositions[farthestIndex] = position;
        m_candidateDistances[farthestIndex] = normalizedDistance;
    }

    private void UpdatePulse(float elapsed, float easedPulse)
    {
        bool active = elapsed < m_pulseDuration;
        m_pulseImage.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        float scale = Mathf.Lerp(0.05f, 1f, easedPulse);
        m_pulseImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
        Color color = m_pulseBaseColor;
        color.a = m_pulseAlpha * (1f - easedPulse);
        m_pulseImage.color = color;
    }

    private void UpdateBlips(float elapsed, float easedPulse)
    {
        float alpha = elapsed <= m_fadeStart
            ? 1f
            : 1f - Mathf.Clamp01((elapsed - m_fadeStart) / m_fadeDuration);
        for (int index = 0; index < m_candidateCount; index++)
        {
            bool visible = alpha > 0f
                && (elapsed >= m_pulseDuration || easedPulse >= m_candidateDistances[index]);
            Image blip = m_blips[index];
            blip.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            blip.rectTransform.anchoredPosition = m_candidatePositions[index];
            Color color = blip.color;
            color.a = alpha;
            blip.color = color;
        }
    }

    internal static bool IsWithinRange(Vector3 relative, float range)
    {
        float sqrDistance = relative.sqrMagnitude;
        return sqrDistance > 0f && sqrDistance <= range * range;
    }

    [ContextMenu("Run Enemy Scanner Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(IsWithinRange(Vector3.forward * 40f, 40f));
        Debug.Assert(IsWithinRange(Vector3.back * 40f, 40f));
        Debug.Assert(IsWithinRange(Vector3.left * 20f, 40f));
        Debug.Assert(IsWithinRange(Vector3.right * 20f, 40f));
        Debug.Assert(!IsWithinRange(Vector3.forward * 40.01f, 40f));
        Debug.Assert(k_MaxBlips == 128);
        Debug.Assert(Mathf.Approximately(m_fadeStart + m_fadeDuration, m_pulseInterval));
        Debug.Assert(m_blipSize >= 4f && m_blipSize <= 32f);
        Debug.Assert(m_pulseThickness >= 0f && m_pulseThickness <= 8f);
    }

    private void OnValidate()
    {
        m_scanRange = Mathf.Max(1f, m_scanRange);
        m_pulseInterval = Mathf.Max(0.2f, m_pulseInterval);
        m_pulseDuration = Mathf.Clamp(m_pulseDuration, 0.2f, Mathf.Min(1.5f, m_pulseInterval));
        m_pulseThickness = Mathf.Clamp(m_pulseThickness, 0f, 8f);
        m_pulseAlpha = Mathf.Clamp01(m_pulseAlpha);
        m_fadeStart = Mathf.Clamp(m_fadeStart, 0f, m_pulseInterval - 0.01f);
        m_fadeDuration = Mathf.Clamp(m_fadeDuration, 0.01f, m_pulseInterval - m_fadeStart);
        m_blipSize = Mathf.Clamp(m_blipSize, 4f, 32f);

        if (m_pulseImage != null)
        {
            m_pulseOutline = m_pulseImage.GetComponent<Outline>();
            m_pulseBaseColor = m_pulseImage.color;
            m_pulseBaseColor.a = m_pulseAlpha;
            ApplyPulseAppearance();
        }

        ApplyBlipSize(m_blipTemplate);
        for (int index = 0; index < m_blips.Length; index++)
        {
            ApplyBlipSize(m_blips[index]);
        }
    }

    private void ApplyPulseAppearance()
    {
        if (m_pulseImage != null)
        {
            Color color = m_pulseImage.color;
            color.a = m_pulseAlpha;
            m_pulseImage.color = color;
        }
        if (m_pulseOutline != null)
        {
            m_pulseOutline.effectDistance = Vector2.one * m_pulseThickness;
            Color outlineColor = m_pulseBaseColor;
            outlineColor.a = m_pulseAlpha;
            m_pulseOutline.effectColor = outlineColor;
            m_pulseOutline.useGraphicAlpha = true;
        }
    }

    private void ApplyBlipSize(Image blip)
    {
        if (blip != null)
        {
            blip.rectTransform.sizeDelta = Vector2.one * m_blipSize;
        }
    }
}
