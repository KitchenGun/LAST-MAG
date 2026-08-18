using UnityEngine;

public sealed class LobbyCameraSway : MonoBehaviour
{
    [SerializeField, Min(0f)] private float m_yawAmplitude = 15f;
    [SerializeField, Min(0f)] private float m_pitchAmplitude = 1.25f;
    [SerializeField, Min(0.1f)] private float m_cycleDuration = 12f;

    private Vector3 m_baseEulerAngles;
    private float m_startTime;

    private void OnEnable()
    {
        m_baseEulerAngles = transform.localEulerAngles;
        m_startTime = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        float phase = (Time.unscaledTime - m_startTime) * Mathf.PI * 2f / m_cycleDuration;
        float yaw = Mathf.Sin(phase) * m_yawAmplitude;
        float pitch = Mathf.Sin(phase * 2f) * m_pitchAmplitude;
        transform.localRotation = Quaternion.Euler(m_baseEulerAngles + new Vector3(pitch, yaw, 0f));
    }
}
