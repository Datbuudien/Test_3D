using UnityEditor;
using UnityEditor.SceneManagement;

// playModeStartScene la setting cua Editor, khong nam trong asset nao -> dat lai moi lan domain reload (ca virtual player cua MPPM).
// Bam Play o bat ky scene nao cung chay tu Bootstrap de NetworkManager/NetworkSession luon ton tai.
[InitializeOnLoad]
internal static class BootstrapPlayModeStarter
{
    internal const string BOOTSTRAP_SCENE_PATH = "Assets/_Project/Scenes/Bootstrap.unity";

    static BootstrapPlayModeStarter()
    {
        EditorApplication.delayCall += Apply;
    }

    internal static void Apply()
    {
        SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BOOTSTRAP_SCENE_PATH);
        if (bootstrapScene != null && EditorSceneManager.playModeStartScene != bootstrapScene)
        {
            EditorSceneManager.playModeStartScene = bootstrapScene;
        }
    }
}
