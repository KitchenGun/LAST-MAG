using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StartMenuController : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("KKTest");
    }
}
