using UnityEngine;

public static class GameplayClock
{
    private static float s_baseFixedDeltaTime = 0.02f;
    private static float s_worldScale = 1f;
    private static float s_pauseStartedAt;
    private static float s_totalPausedTime;

    public static bool IsPaused { get; private set; }
    public static float Now => ResolveNow(
        Time.unscaledTime, s_totalPausedTime, IsPaused, s_pauseStartedAt);
    public static float DeltaTime => IsPaused ? 0f : Time.unscaledDeltaTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeStatics()
    {
        IsPaused = false;
        s_worldScale = 1f;
        s_pauseStartedAt = 0f;
        s_totalPausedTime = 0f;
    }

    public static void Reset(float baseFixedDeltaTime)
    {
        s_baseFixedDeltaTime = Mathf.Max(0.001f, baseFixedDeltaTime);
        s_worldScale = 1f;
        s_pauseStartedAt = 0f;
        s_totalPausedTime = 0f;
        IsPaused = false;
        AudioListener.pause = false;
        ApplyWorldScale();
    }

    public static void SetWorldScale(float scale)
    {
        s_worldScale = Mathf.Max(0f, scale);
        ApplyWorldScale();
    }

    public static void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        s_pauseStartedAt = Time.unscaledTime;
        IsPaused = true;
        AudioListener.pause = true;
        ApplyWorldScale();
    }

    public static void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        s_totalPausedTime += Time.unscaledTime - s_pauseStartedAt;
        IsPaused = false;
        AudioListener.pause = false;
        ApplyWorldScale();
    }

    private static void ApplyWorldScale()
    {
        Time.timeScale = ResolveWorldScale(IsPaused, s_worldScale);
        Time.fixedDeltaTime = s_baseFixedDeltaTime * Mathf.Max(0.01f, s_worldScale);
    }

    internal static float ResolveNow(
        float realtime, float pausedDuration, bool paused, float pauseStartedAt)
    {
        return realtime - pausedDuration - (paused ? realtime - pauseStartedAt : 0f);
    }

    internal static float ResolveWorldScale(bool paused, float worldScale)
    {
        return paused ? 0f : Mathf.Max(0f, worldScale);
    }
}
