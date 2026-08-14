using UnityEngine;

public static class SpatialAudio
{
    private const int k_VoiceCount = 24;
    private static AudioSource[] s_Voices;
    private static double[] s_VoiceEndTimes;
    private static AudioListener s_Listener;
    private static Transform s_Owner;

    internal static void Initialize(Transform parent)
    {
        if (parent == null || (s_Owner == parent && s_Voices != null && s_Voices.Length == k_VoiceCount))
        {
            return;
        }

        s_Owner = parent;
        s_Voices = new AudioSource[k_VoiceCount];
        s_VoiceEndTimes = new double[k_VoiceCount];
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

    public static void PlayRandomOneShot(AudioClip[] clips, Vector3 position, float maxDistance, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        int startIndex = Random.Range(0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            AudioClip clip = clips[(startIndex + offset) % clips.Length];
            if (clip != null)
            {
                PlayOneShot(clip, position, maxDistance, volume);
                return;
            }
        }
    }

    public static void PlayOneShot(AudioClip clip, Vector3 position, float maxDistance, float volume)
    {
        if (clip == null || maxDistance <= 0f)
        {
            return;
        }

        if (s_Listener == null)
        {
            s_Listener = Object.FindFirstObjectByType<AudioListener>();
        }
        if (s_Listener == null || (s_Listener.transform.position - position).sqrMagnitude > maxDistance * maxDistance)
        {
            return;
        }

        int voiceIndex = FindAvailableVoice();
        if (voiceIndex < 0)
        {
            return;
        }

        AudioSource source = s_Voices[voiceIndex];
        source.Stop();
        source.spatialBlend = 1f;
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.maxDistance = maxDistance;
        source.Play();
        s_VoiceEndTimes[voiceIndex] = AudioSettings.dspTime + clip.length;
    }

    public static void PlayOneShot2D(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        int voiceIndex = FindAvailableVoice();
        if (voiceIndex < 0)
        {
            return;
        }

        AudioSource source = s_Voices[voiceIndex];
        source.Stop();
        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
        s_VoiceEndTimes[voiceIndex] = AudioSettings.dspTime + clip.length;
    }

    private static int FindAvailableVoice()
    {
        if (s_Voices == null || s_VoiceEndTimes == null)
        {
            return -1;
        }

        int earliestIndex = 0;
        double earliestEndTime = double.MaxValue;
        for (int index = 0; index < s_Voices.Length; index++)
        {
            if (!s_Voices[index].isPlaying)
            {
                return index;
            }
            if (s_VoiceEndTimes[index] < earliestEndTime)
            {
                earliestEndTime = s_VoiceEndTimes[index];
                earliestIndex = index;
            }
        }
        return earliestIndex;
    }
}
