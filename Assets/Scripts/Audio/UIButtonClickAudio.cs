using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class UIButtonClickAudio : MonoBehaviour
{
    private const string k_ClipPath = "Audio/UI/Click_01";

    private static UIButtonClickAudio s_instance;

    private AudioSource m_audioSource;
    private AudioClip m_clickClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (s_instance != null)
        {
            s_instance.HookButtons();
            return;
        }

        GameObject audioObject = new GameObject(nameof(UIButtonClickAudio));
        s_instance = audioObject.AddComponent<UIButtonClickAudio>();
        DontDestroyOnLoad(audioObject);
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        m_clickClip = Resources.Load<AudioClip>(k_ClipPath);
        m_audioSource = gameObject.AddComponent<AudioSource>();
        m_audioSource.playOnAwake = false;
        m_audioSource.loop = false;
        m_audioSource.spatialBlend = 0f;
        m_audioSource.ignoreListenerPause = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        HookButtons();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookButtons();
    }

    private void LateUpdate()
    {
        HookButtons();
    }

    internal static void RefreshButtonHooks()
    {
        s_instance?.HookButtons();
    }

    private void HookButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClick);
                button.onClick.AddListener(PlayClick);
            }
        }
    }

    private void PlayClick()
    {
        if (m_audioSource != null && m_clickClip != null)
        {
            m_audioSource.PlayOneShot(m_clickClip);
        }
    }
}
