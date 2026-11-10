using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;

    void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.BackPressed += OnBackPressed;
        }
    }

    void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.BackPressed -= OnBackPressed;
        }
    }

    private void OnBackPressed()
    {
        SceneManager.LoadScene(Constants.SCENE_SCENARIO_SETUP);
    }
}
