using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StartMenuController : MonoBehaviour
{
    public void Play()
    {
        RunResultStore.Clear();
        SceneManager.LoadScene("GameplayScene");
    }
}
