using UnityEngine;
using UnityEngine.SceneManagement;

// Esc lay tu InputReader cua nhan vat local (LocalPlayerBinder goi Bind khi player spawn)
public class SceneNavigator : MonoBehaviour
{
    private InputReader inputReader;

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(InputReader reader)
    {
        Unsubscribe();
        inputReader = reader;
        if (isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    public void Unbind()
    {
        Unsubscribe();
        inputReader = null;
    }

    private void Subscribe()
    {
        if (inputReader != null)
        {
            inputReader.BackPressed += OnBackPressed;
        }
    }

    private void Unsubscribe()
    {
        if (!ReferenceEquals(inputReader, null))
        {
            inputReader.BackPressed -= OnBackPressed;
        }
    }

    private void OnBackPressed()
    {
        if (NetworkSession.Instance != null)
        {
            NetworkSession.Instance.Leave();
            return;
        }

        // Chay thang Gameplay khong qua Bootstrap (khong co mang)
        SceneManager.LoadScene(Constants.SCENE_SCENARIO_SETUP);
    }
}
