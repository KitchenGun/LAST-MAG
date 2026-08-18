using UnityEngine;

public sealed class LobbyCeilingLightFlicker : MonoBehaviour
{
    [SerializeField] private Light m_roomLight;
    [SerializeField] private Renderer m_ceilingLampRenderer;
    [SerializeField, Min(0)] private int m_emissiveMaterialIndex = 1;
    [SerializeField, Min(1f)] private float m_interval = 8f;
    [SerializeField, Min(0f)] private float m_dimIntensity = 0.08f;

    private MaterialPropertyBlock m_propertyBlock;
    private Color m_emissionColor;
    private float m_brightIntensity;
    private float m_startedAt;

    private void Awake()
    {
        m_propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (m_roomLight == null || m_ceilingLampRenderer == null)
            return;

        m_brightIntensity = m_roomLight.intensity;
        Material[] materials = m_ceilingLampRenderer.sharedMaterials;
        if (m_emissiveMaterialIndex >= 0 && m_emissiveMaterialIndex < materials.Length &&
            materials[m_emissiveMaterialIndex].HasProperty("_EmissionColor"))
        {
            m_emissionColor = materials[m_emissiveMaterialIndex].GetColor("_EmissionColor");
        }

        m_startedAt = Time.unscaledTime;
        ApplyLightState(true);
    }

    private void LateUpdate()
    {
        if (m_roomLight == null || m_interval <= 0f)
            return;

        float cycleTime = Mathf.Repeat(Time.unscaledTime - m_startedAt, m_interval);
        ApplyLightState(IsLightOn(cycleTime, m_interval));
    }

    private void OnDisable()
    {
        ApplyLightState(true);
    }

    private static bool IsLightOn(float cycleTime, float interval)
    {
        float time = cycleTime - Mathf.Max(0f, interval - 0.65f);
        if (time < 0f)
            return true;
        if (time < 0.07f)
            return false;
        if (time < 0.14f)
            return true;
        if (time < 0.25f)
            return false;
        if (time < 0.33f)
            return true;
        if (time < 0.47f)
            return false;

        return true;
    }

    private void ApplyLightState(bool isOn)
    {
        if (m_roomLight != null)
            m_roomLight.intensity = isOn ? m_brightIntensity : m_dimIntensity;

        if (m_propertyBlock == null || m_ceilingLampRenderer == null || m_emissionColor == default)
            return;

        m_ceilingLampRenderer.GetPropertyBlock(m_propertyBlock, m_emissiveMaterialIndex);
        m_propertyBlock.SetColor("_EmissionColor", m_emissionColor * (isOn ? 1f : 0.03f));
        m_ceilingLampRenderer.SetPropertyBlock(m_propertyBlock, m_emissiveMaterialIndex);
    }
}
