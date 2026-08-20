using UnityEngine;

public static class SpatialAudio
{
    public enum CuePriority
    {
        Ambient,
        Important,
        Gameplay
    }

    private const int k_VoiceCount = 24;
    private static AudioSource[] s_Voices;
    private static double[] s_VoiceEndTimes;
    private static CuePriority[] s_VoicePriorities;
    private static AudioListener s_Listener;
    private static Transform s_Owner;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static int DiagnosticRequestCount { get; private set; }
    internal static int DiagnosticPlayedCount { get; private set; }
    internal static int DiagnosticDroppedCount { get; private set; }
    internal static int DiagnosticReplacedCount { get; private set; }
#endif

    internal static void Initialize(Transform parent)
    {
        if (parent == null || (s_Owner == parent && s_Voices != null && s_Voices.Length == k_VoiceCount))
        {
            return;
        }

        s_Owner = parent;
        s_Voices = new AudioSource[k_VoiceCount];
        s_VoiceEndTimes = new double[k_VoiceCount];
        s_VoicePriorities = new CuePriority[k_VoiceCount];
        for (int index = 0; index < k_VoiceCount; index++)
        {
            GameObject voiceObject = new($"Spatial Voice {index + 1:00}");
            voiceObject.transform.SetParent(parent, false);
            AudioSource source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.dopplerLevel = 0f;
            s_Voices[index] = source;
        }
    }

    public static bool PlayRandomOneShot(AudioClip[] clips, Vector3 position, float maxDistance, float volume,
        CuePriority priority = CuePriority.Ambient)
    {
        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            AudioClip clip = clips[(startIndex + offset) % clips.Length];
            if (clip != null)
            {
                return PlayOneShot(clip, position, maxDistance, volume, priority);
            }
        }
        return false;
    }

    public static bool PlayOneShot(AudioClip clip, Vector3 position, float maxDistance, float volume,
        CuePriority priority = CuePriority.Ambient)
    {
        if (clip == null || maxDistance <= 0f)
        {
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticRequestCount++;
#endif
        if (!IsAudible(position, maxDistance))
        {
            return false;
        }

        int voiceIndex = FindAvailableVoice(priority, out bool replaced);
        if (voiceIndex < 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticDroppedCount++;
#endif
            return false;
        }

        AudioSource source = s_Voices[voiceIndex];
        if (replaced)
        {
            source.Stop();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticReplacedCount++;
#endif
        }
        source.spatialBlend = 1f;
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.maxDistance = maxDistance;
        source.Play();
        s_VoiceEndTimes[voiceIndex] = AudioSettings.dspTime + clip.length;
        s_VoicePriorities[voiceIndex] = priority;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticPlayedCount++;
#endif
        return true;
    }

    public static bool PlayOneShot2D(AudioClip clip, float volume,
        CuePriority priority = CuePriority.Ambient)
    {
        if (clip == null)
        {
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticRequestCount++;
#endif
        int voiceIndex = FindAvailableVoice(priority, out bool replaced);
        if (voiceIndex < 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticDroppedCount++;
#endif
            return false;
        }

        AudioSource source = s_Voices[voiceIndex];
        if (replaced)
        {
            source.Stop();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DiagnosticReplacedCount++;
#endif
        }
        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
        s_VoiceEndTimes[voiceIndex] = AudioSettings.dspTime + clip.length;
        s_VoicePriorities[voiceIndex] = priority;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticPlayedCount++;
#endif
        return true;
    }

    internal static bool IsAudible(Vector3 position, float maxDistance)
    {
        if (maxDistance <= 0f)
        {
            return false;
        }
        if (s_Listener == null)
        {
            s_Listener = Object.FindFirstObjectByType<AudioListener>();
        }
        return s_Listener != null && IsWithinDistance(s_Listener.transform.position, position, maxDistance);
    }

    internal static bool IsWithinDistance(Vector3 listenerPosition, Vector3 sourcePosition, float maxDistance)
    {
        return maxDistance > 0f
            && (listenerPosition - sourcePosition).sqrMagnitude <= maxDistance * maxDistance;
    }

    internal static bool CanReplace(CuePriority activePriority, CuePriority incomingPriority)
    {
        return activePriority < incomingPriority;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void ResetDiagnostics()
    {
        DiagnosticRequestCount = 0;
        DiagnosticPlayedCount = 0;
        DiagnosticDroppedCount = 0;
        DiagnosticReplacedCount = 0;
    }
#endif

    private static int FindAvailableVoice(CuePriority incomingPriority, out bool replaced)
    {
        replaced = false;
        if (s_Voices == null || s_VoiceEndTimes == null || s_VoicePriorities == null)
        {
            return -1;
        }

        int replacementIndex = -1;
        CuePriority replacementPriority = incomingPriority;
        double earliestEndTime = double.MaxValue;
        for (int index = 0; index < s_Voices.Length; index++)
        {
            if (!s_Voices[index].isPlaying)
            {
                return index;
            }
            CuePriority activePriority = s_VoicePriorities[index];
            if (!CanReplace(activePriority, incomingPriority))
            {
                continue;
            }
            if (replacementIndex < 0 || activePriority < replacementPriority
                || (activePriority == replacementPriority && s_VoiceEndTimes[index] < earliestEndTime))
            {
                replacementIndex = index;
                replacementPriority = activePriority;
                earliestEndTime = s_VoiceEndTimes[index];
            }
        }
        replaced = replacementIndex >= 0;
        return replacementIndex;
    }
}
