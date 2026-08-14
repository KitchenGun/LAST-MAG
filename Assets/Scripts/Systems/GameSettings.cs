using UnityEngine;

public enum ZoomInputMode
{
    Toggle,
    Hold
}

public static class GameSettings
{
    private const string k_MouseSensitivityKey = "Gulag.Settings.MouseSensitivity";
    private const string k_MasterVolumeKey = "Gulag.Settings.MasterVolume";
    private const string k_ZoomInputModeKey = "Gulag.Settings.ZoomInputMode";

    private static bool s_loaded;
    private static float s_mouseSensitivity = 1f;
    private static float s_masterVolume = 1f;
    private static ZoomInputMode s_zoomInputMode = ZoomInputMode.Toggle;

    public static float MouseSensitivity
    {
        get
        {
            EnsureLoaded();
            return s_mouseSensitivity;
        }
    }

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return s_masterVolume;
        }
    }

    public static ZoomInputMode ZoomInputMode
    {
        get
        {
            EnsureLoaded();
            return s_zoomInputMode;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Load();
    }

    public static void SetMouseSensitivity(float value)
    {
        EnsureLoaded();
        s_mouseSensitivity = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(k_MouseSensitivityKey, s_mouseSensitivity);
        PlayerPrefs.Save();
    }

    public static void SetMasterVolume(float value)
    {
        EnsureLoaded();
        s_masterVolume = Mathf.Clamp01(value);
        AudioListener.volume = s_masterVolume;
        PlayerPrefs.SetFloat(k_MasterVolumeKey, s_masterVolume);
        PlayerPrefs.Save();
    }

    public static void SetZoomInputMode(ZoomInputMode mode)
    {
        EnsureLoaded();
        s_zoomInputMode = IsValidZoomInputMode(mode) ? mode : ZoomInputMode.Toggle;
        PlayerPrefs.SetInt(k_ZoomInputModeKey, (int)s_zoomInputMode);
        PlayerPrefs.Save();
    }

    internal static bool IsValidZoomInputMode(ZoomInputMode mode)
    {
        return mode is ZoomInputMode.Toggle or ZoomInputMode.Hold;
    }

    private static void EnsureLoaded()
    {
        if (!s_loaded)
        {
            Load();
        }
    }

    private static void Load()
    {
        s_mouseSensitivity = Mathf.Clamp01(PlayerPrefs.GetFloat(k_MouseSensitivityKey, 1f));
        s_masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(k_MasterVolumeKey, 1f));
        ZoomInputMode storedMode = (ZoomInputMode)PlayerPrefs.GetInt(
            k_ZoomInputModeKey, (int)ZoomInputMode.Toggle);
        s_zoomInputMode = IsValidZoomInputMode(storedMode) ? storedMode : ZoomInputMode.Toggle;
        AudioListener.volume = s_masterVolume;
        s_loaded = true;
    }
}
