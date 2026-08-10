using UnityEngine;

public static class SpatialAudio
{
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

        AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
        if (listener == null || (listener.transform.position - position).sqrMagnitude > maxDistance * maxDistance)
        {
            return;
        }

        GameObject soundObject = new GameObject("Spatial One Shot");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.Play();

        Object.Destroy(soundObject, clip.length + 0.1f);
    }
}
