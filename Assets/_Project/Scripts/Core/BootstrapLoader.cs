using UnityEngine;
using UnityEngine.SceneManagement;

// Scene Bootstrap chi dung de khoi tao NetworkManager mot lan roi chuyen sang man chon kich ban
public class BootstrapLoader : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadScene(Constants.SCENE_SCENARIO_SETUP);
    }

    // Virtual player MPPM bo qua playModeStartScene, vao thang scene dang mo cua Editor chinh -> khong co NetworkSession (KI-24).
    // Chot chan luc chay: scene dau tien khong phai Bootstrap thi nap Bootstrap truoc. Build da co Bootstrap o index 0 nen khong anh huong.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBootstrapLoadedFirst()
    {
        if (SceneManager.GetActiveScene().name != Constants.SCENE_BOOTSTRAP)
        {
            SceneManager.LoadScene(Constants.SCENE_BOOTSTRAP);
        }
    }
}
