using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GameplayPauseController : MonoBehaviour
{
    [SerializeField] private SettingsPanelController m_settingsPanel;
    [SerializeField] private FirstPersonController m_player;

    private void Awake()
    {
        GameplayClock.Reset(Time.fixedDeltaTime);
        if (m_settingsPanel == null)
        {
            m_settingsPanel = GetComponentInChildren<SettingsPanelController>(true);
        }
        m_settingsPanel?.Hide();
    }

    private void Start()
    {
        if (m_player == null)
        {
            m_player = FirstPersonController.CurrentInstance;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (GameplayClock.IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    private void PauseGame()
    {
        if (m_settingsPanel == null || m_player == null || m_player.IsDeathPresentation)
        {
            return;
        }

        m_player.SetPaused(true);
        GameplayClock.Pause();
        m_settingsPanel.Show(ResumeGame);
    }

    private void ResumeGame()
    {
        m_settingsPanel?.Hide();
        GameplayClock.Resume();
        if (m_player != null && !m_player.IsDeathPresentation)
        {
            m_player.SetPaused(false);
        }
    }

    private void OnDestroy()
    {
        GameplayClock.Reset(0.02f);
    }

    [ContextMenu("Run Gameplay Pause Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Mathf.Approximately(
            GameplayClock.ResolveNow(12f, 3f, false, 0f), 9f));
        Debug.Assert(Mathf.Approximately(
            GameplayClock.ResolveNow(12f, 3f, true, 10f), 7f));
        Debug.Assert(Mathf.Approximately(
            GameplayClock.ResolveWorldScale(false, 0.35f), 0.35f));
        Debug.Assert(Mathf.Approximately(
            GameplayClock.ResolveWorldScale(true, 0.35f), 0f));
    }
}
