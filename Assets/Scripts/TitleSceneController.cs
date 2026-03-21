using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    [SerializeField] private string mapListSceneName = "MapList";

    public void OnStartClicked()
    {
        SceneManager.LoadScene(mapListSceneName);
    }
}
