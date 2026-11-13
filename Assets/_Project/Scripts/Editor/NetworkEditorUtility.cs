using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dung chung cho cac tool dat NetworkObject san trong scene
internal static class NetworkEditorUtility
{
    private const string LOG_PREFIX = "[NetworkEditorUtility]";

    // GlobalObjectIdHash cua NetworkObject dat san trong scene chi sinh duoc khi object da co fileID (sau lan luu dau).
    // NGO tinh trong OnValidate (internal) -> luu, goi lai OnValidate, luu lan 2; thieu buoc nay hash = 0, object khong spawn (KI-27)
    internal static void SaveSceneWithFreshNetworkIds(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
        if (onValidate == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} NetworkObject.OnValidate not found - open the scene and save it once to refresh GlobalObjectIdHash.");
            return;
        }

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (NetworkObject networkObject in rootObject.GetComponentsInChildren<NetworkObject>(true))
            {
                onValidate.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
