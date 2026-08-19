using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsPanelController : MonoBehaviour
{
    [Header("UI Skin")]
    [SerializeField] private Sprite m_toggleOnSprite;
    [SerializeField] private Sprite m_toggleOffSprite;

    private Button m_backButton;
    private Button m_toggleButton;
    private Button m_holdButton;
    private Slider m_sensitivitySlider;
    private Slider m_volumeSlider;
    private TMP_Text m_sensitivityValue;
    private TMP_Text m_volumeValue;
    private Action m_closeRequested;
    private bool m_initialized;

    public bool IsOpen => gameObject.activeSelf;

    public void Show(Action closeRequested)
    {
        EnsureInitialized();
        m_closeRequested = closeRequested;
        RefreshControls();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        m_closeRequested = null;
        gameObject.SetActive(false);
    }

    private void EnsureInitialized()
    {
        if (m_initialized)
        {
            return;
        }

        m_backButton = FindComponent<Button>("SettingsBackButton");
        m_toggleButton = FindComponent<Button>("ToggleButton");
        m_holdButton = FindComponent<Button>("HoldButton");
        m_sensitivitySlider = FindComponent<Slider>("SensitivitySlider");
        m_volumeSlider = FindComponent<Slider>("VolumeSlider");
        m_sensitivityValue = FindComponent<TMP_Text>("SensitivityValue");
        m_volumeValue = FindComponent<TMP_Text>("VolumeValue");
        if (m_backButton == null || m_toggleButton == null || m_holdButton == null
            || m_sensitivitySlider == null || m_volumeSlider == null
            || m_sensitivityValue == null || m_volumeValue == null)
        {
            Debug.LogError("[SettingsPanel] Required UI references are missing.", this);
            return;
        }

        m_backButton.onClick.RemoveAllListeners();
        m_backButton.onClick.AddListener(RequestClose);
        m_toggleButton.onClick.RemoveAllListeners();
        m_toggleButton.onClick.AddListener(() => SetZoomMode(ZoomInputMode.Toggle));
        m_holdButton.onClick.RemoveAllListeners();
        m_holdButton.onClick.AddListener(() => SetZoomMode(ZoomInputMode.Hold));
        m_sensitivitySlider.onValueChanged.RemoveAllListeners();
        m_sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        m_volumeSlider.onValueChanged.RemoveAllListeners();
        m_volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        m_initialized = true;
        UIButtonClickAudio.RefreshButtonHooks();
    }

    private void RequestClose()
    {
        Action closeRequested = m_closeRequested;
        if (closeRequested != null)
        {
            closeRequested();
        }
        else
        {
            Hide();
        }
    }

    private void RefreshControls()
    {
        if (!m_initialized)
        {
            return;
        }

        m_sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        m_volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        m_sensitivityValue.text = FormatSensitivity(GameSettings.MouseSensitivity);
        m_volumeValue.text = FormatVolume(GameSettings.MasterVolume);
        RefreshZoomSelection();
    }

    private void OnSensitivityChanged(float value)
    {
        GameSettings.SetMouseSensitivity(value);
        m_sensitivityValue.text = FormatSensitivity(value);
    }

    private void OnVolumeChanged(float value)
    {
        GameSettings.SetMasterVolume(value);
        m_volumeValue.text = FormatVolume(value);
    }

    private void SetZoomMode(ZoomInputMode mode)
    {
        GameSettings.SetZoomInputMode(mode);
        RefreshZoomSelection();
    }

    private void RefreshZoomSelection()
    {
        SetSelected(m_toggleButton, GameSettings.ZoomInputMode == ZoomInputMode.Toggle);
        SetSelected(m_holdButton, GameSettings.ZoomInputMode == ZoomInputMode.Hold);
    }

    private void SetSelected(Button button, bool selected)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            Sprite stateSprite = selected ? m_toggleOnSprite : m_toggleOffSprite;
            if (stateSprite != null)
            {
                image.sprite = stateSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = selected
                    ? new Color(0.035f, 0.26f, 0.29f, 1f)
                    : new Color(0.035f, 0.14f, 0.16f, 1f);
            }
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = selected;
        }
    }

    internal static string FormatSensitivity(float value)
    {
        return Mathf.Clamp01(value).ToString("0.00");
    }

    internal static string FormatVolume(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int index = 0; index < components.Length; index++)
        {
            if (components[index].name == objectName)
            {
                return components[index];
            }
        }

        return null;
    }
}
