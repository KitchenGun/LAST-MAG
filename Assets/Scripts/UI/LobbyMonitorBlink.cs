using UnityEngine;

public sealed class LobbyMonitorBlink : MonoBehaviour
{
    [SerializeField] private Renderer m_logoRenderer;
    [SerializeField, Min(1f)] private float m_interval = 10f;
    [SerializeField, Min(0.1f)] private float m_flickerDuration = 1.15f;

    private float m_startedAt;

    private void OnEnable()
    {
        if (m_logoRenderer == null)
            return;

        m_startedAt = Time.unscaledTime;
        SetLogoVisible(false);
    }

    private void LateUpdate()
    {
        if (m_logoRenderer == null || m_interval <= 0f)
            return;

        float cycleTime = Mathf.Repeat(Time.unscaledTime - m_startedAt, m_interval);
        float flickerStart = Mathf.Max(0f, m_interval - m_flickerDuration);
        bool visible = IsFlickerOn(cycleTime - flickerStart);
        SetLogoVisible(visible);
    }

    private void OnDisable()
    {
        SetLogoVisible(false);
    }

    private static bool IsFlickerOn(float time)
    {
        if (time < 0f || time >= 1.05f)
            return false;

        if (time < 0.08f)
            return true;
        if (time < 0.15f)
            return false;
        if (time < 0.27f)
            return true;
        if (time < 0.35f)
            return false;
        if (time < 0.52f)
            return true;
        if (time < 0.62f)
            return false;

        return true;
    }

    private void SetLogoVisible(bool visible)
    {
        if (m_logoRenderer != null)
            m_logoRenderer.enabled = visible;
    }
}
